using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Flow;
using JungleMaze.App.Hud.State;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JungleMaze.App.Hud.Mocks;
#endif
using JungleMaze.App.Match;
using JungleMaze.App.MatchUi;
using JungleMaze.App.Network;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Core.Logging;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Player;
using JungleMaze.Infrastructure.Auth;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Infrastructure.Logging;
using JungleMaze.Infrastructure.Network;
using JungleMaze.Infrastructure.Pooling;
using JungleMaze.Infrastructure.Scenes;
using JungleMaze.Infrastructure.Ticking;
using JungleMaze.Presentation.UI;
using JungleMaze.Presentation.UI.Hud;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JungleMaze.Composition
{
    /// <summary>
    /// Composition root duy nhất: tạo và nối mọi service theo thứ tự đọc từ trên xuống, rồi inject MonoBehaviour
    /// trong mọi scene được load. Không có service locator tĩnh: code khác nhận dependency qua constructor/[Inject].
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed partial class Bootstrap : MonoBehaviour
    {
        [SerializeField] private string menuScene = "Boot";
        [SerializeField] private string gameScene = "Game";
        [Tooltip("UI art (item icons + menu art). Empty: loaded once from Resources/" + UiArtSet.ResourcePath + " at composition.")]
        [SerializeField] private UiArtSet uiArt;

        private ServiceRegistry _registry;
        private Injector _injector;
        private ILog _log;

        public bool IsComposed => _registry != null;

        private void Awake()
        {
            // Quay lại scene Boot sẽ tạo thêm một Bootstrap: giữ bản đang sống, hủy bản mới.
            foreach (var other in FindObjectsByType<Bootstrap>(FindObjectsSortMode.None))
                if (other != this && other.IsComposed) { Destroy(gameObject); return; }

            DontDestroyOnLoad(gameObject);
            Compose();
            for (var i = 0; i < SceneManager.sceneCount; i++) InjectScene(SceneManager.GetSceneAt(i));
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Compose()
        {
            _log = new UnityLog();
            _registry = new ServiceRegistry(_log);
            _injector = new Injector(_registry);

            // Core
            var bus = new EventBus(_log);
            _registry.Register<ILog>(_log);
            _registry.Register<IEventBus>(bus);
            _registry.Register(_injector);

            // Infrastructure (implement port của Application)
            var config = new FileConfigProvider(Application.streamingAssetsPath, Application.persistentDataPath,
                Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable, _log);
            var packets = new PacketRegistry();
            var network = new NetworkClient(bus, packets, _log);
            var tickRunner = gameObject.AddComponent<TickRunner>();
            tickRunner.SetLog(_log);
            var pool = new PoolService(_injector, _log, transform);
            var balance = new BalanceConfigProvider(Application.streamingAssetsPath, _log);
            _registry.Register<IConfigProvider>(config);
            _registry.Register<IBalanceConfigProvider>(balance);
            _registry.Register(packets);
            _registry.Register(network);
            _registry.Register<INetworkClient>(network);
            _registry.Register<IMatchConnection>(network);
            _registry.Register<IClientRuntimeData>(new ClientRuntimeData(Application.streamingAssetsPath));
            var history = new GuestHistoryClient(new GuestCredentialStore(Application.persistentDataPath));
            _registry.Register<IPlayerHistory>(history);
            _registry.Register<IPacketSender>(network);
            _registry.Register<IPacketReceiver>(network);
            _registry.Register<IAuthApi>(new AuthApiClient(_log));
            _registry.Register<ISceneLoader>(new SceneLoader(_log));
            _registry.Register<ITickScheduler>(tickRunner);
            _registry.Register<IPoolService>(pool);

            // HUD state (UI-01): HUD reads the three ports, data owners write through the writer ports.
            var hudState = new HudStateStore(bus, balance, _log);
            var inventoryState = new InventoryStateStore(bus, balance, _log);
            var effectState = new EffectStateStore(bus);
            _registry.Register<IHudState>(hudState);
            _registry.Register<IHudStateWriter>(hudState);
            _registry.Register<IInventoryState>(inventoryState);
            _registry.Register<IInventoryStateWriter>(inventoryState);
            _registry.Register<IEffectState>(effectState);
            _registry.Register<IEffectStateWriter>(effectState);
            // ADHOC-UI-ASSETS-A: real icon catalog. A Bootstrap auto-created in Game/HudPreview has no serialized
            // reference, so it loads the same asset once here (never per frame).
            if (uiArt == null) uiArt = Resources.Load<UiArtSet>(UiArtSet.ResourcePath);
            if (uiArt != null && uiArt.ItemIcons != null) _registry.Register<IItemIconProvider>(uiArt.ItemIcons);
            else
            {
                _log.Warn("[Bootstrap] UiArtSet/item icon catalog missing; HUD keeps placeholder icons.");
                _registry.Register<IItemIconProvider>(new NullItemIconProvider());
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var hudMock = new MockHudState(hudState, balance);
            var inventoryMock = new MockInventoryState(inventoryState, balance);
            var effectMock = new MockEffectState(effectState);
            _registry.Register(hudMock);
            _registry.Register(inventoryMock);
            _registry.Register(effectMock);
            _registry.Register(new HudMockScenarios(hudMock, inventoryMock, effectMock));
            var feedbackScenarios = new HudFeedbackScenarios(hudMock, inventoryMock, effectMock, inventoryState);
            _registry.Register(feedbackScenarios);
#endif

            // Domain + Application
            var players = new PlayerRegistry();
            var input = new PlayerInputService(network);
            var startLocalPreview = false;
#if UNITY_EDITOR
            // shortcut: direct Game play tests local movement only; use Boot for an authoritative match.
            startLocalPreview = SceneManager.GetActiveScene().path == "Assets/_JungleMaze/Scenes/Game.unity";
#endif
            var flow = new GameFlow(bus, network, network, _registry.Resolve<ISceneLoader>(), input,
                new GameFlowSettings { MenuScene = menuScene, GameScene = gameScene, StartLocalPreview = startLocalPreview });
            var connect = new ConnectToServerUseCase(config, network, _registry.Resolve<IAuthApi>(), bus);
            _registry.Register(players);
            _registry.Register(input);
            _registry.Register(new WorldSyncService(network, bus, players));
            _registry.Register(connect);
            var prediction = new LocalPrediction();                              // MOV-02
            var match = new MatchSession(network, network, network, bus, players, prediction);
            match.PhaseUpdated += flow.ApplyAuthoritativePhase;
            _registry.Register(prediction);
            _registry.Register(match);
            var hudBinding = new MatchHudBinding(match, hudState);
            _registry.Register(hudBinding);
            var remote = new RemotePlayerInterpolator(bus, players);              // MOV-03
            _registry.Register(remote);
            _registry.Register<IPlayerPoseSource>(new PlayerPoseSource(match, remote));
            var movementLog = new MovementTelemetry(match, network, remote, _log);
            _registry.Register(new GuestHistoryCoordinator(config, network, history, match, bus));

            // Presentation (presenter đăng ký trước GameFlow để nhận AppScreenChanged ngay từ Boot)
            _registry.Register(new ConnectionMenuPresenter(config, connect, network, flow, bus, match.Leave));
            _registry.Register(flow);

            tickRunner.Add(pool);
            tickRunner.Add(match); // Match-scoped input replaces the local foundation sender in network demo.
            tickRunner.Add(remote); // Sau match: đồng hồ render tiến sau khi snapshot của frame đã vào buffer.
            tickRunner.Add(movementLog);
            tickRunner.Add(flow);
            tickRunner.Add(hudState);
            tickRunner.Add(hudBinding);
            tickRunner.Add(inventoryState);
            tickRunner.Add(effectState);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            tickRunner.Add(feedbackScenarios);
#endif
            ComposeAudio(bus, tickRunner, hudState, inventoryState, balance, match, network, flow); // AUD-01, Bootstrap.Audio.cs
            ComposeMatchOverlay(bus, tickRunner, match, network, flow, balance); // UI-03, Bootstrap.MatchOverlay.cs
            ComposeTelemetry(tickRunner, match, network, remote); // TEL-01, Bootstrap.Telemetry.cs (only with -telemetry)
            _registry.InitializeAll();

            // View/vỏ MonoBehaviour sống cùng Bootstrap (DontDestroyOnLoad).
            Attach<NetworkManager>();
            if (!startLocalPreview)
            {
                var menuView = Attach<ConnectionMenuView>();
                menuView.ConfigureArt(uiArt != null ? uiArt.MenuArt : null, menuScene);
            }
            Attach<NetworkStatsOverlay>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Attach<HudDebugPanel>();
            Attach<HudFeedbackDebugPanel>();
#endif
            var demoView = Attach<MatchDemoView>();
            _registry.TryResolve<IMatchMenuState>(out var menuState);
            _registry.TryResolve<MatchOverlayFlow>(out var overlayFlow);
            if (menuState != null || overlayFlow != null) demoView.BindOverlay(menuState, overlayFlow);

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-onlineTest") >= 0)
            {
                JungleMaze.App.Testing.OnlineTestExtensions.Reset();
                ConfigureOnlineChecks();
                Attach<OnlineScenarioRunner>();
            }
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-phase2Smoke") >= 0) Attach<Phase2DemoProbe>();
            _log.Info("[Bootstrap] Đã nối service.");
        }

        private T Attach<T>() where T : MonoBehaviour
        {
            var component = gameObject.AddComponent<T>();
            _injector.Inject(component);
            return component;
        }

        // Feature tests can implement this partial hook in a separate Bootstrap.*Tests.cs file.
        // It is called only for the opt-in runner; ordinary play does not register test drivers.
        partial void ConfigureOnlineChecks();

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InjectScene(scene);

        /// <summary>
        /// Inject mọi MonoBehaviour có [Inject] trong scene. Chạy sau Awake/OnEnable, trước Start:
        /// component dùng dependency trong Start (hoặc trong chính method Construct).
        /// </summary>
        private void InjectScene(Scene scene)
        {
            if (!scene.isLoaded || _injector == null) return;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue; // missing script trong scene cũ
                try { _injector.Inject(behaviour); }
                catch (Exception e) { _log.Error($"[Bootstrap] Inject {behaviour.GetType().Name} lỗi: {e.Message}"); }
            }
        }

        private void OnDestroy()
        {
            if (!IsComposed) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _registry.Dispose(); // ngược thứ tự đăng ký: GameFlow trước, NetworkClient (gửi disconnect) sau
            _registry = null;
        }
    }

    /// <summary>Bấm Play ở scene của dự án (không phải Boot) vẫn có Bootstrap, không cần mở Boot trước.</summary>
    internal static class BootstrapAutoCreate
    {
        private const string ProjectScenes = "Assets/_JungleMaze/";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            // Không chen vào scene JSurviv/SampleScene: chỉ áp dụng cho scene trong thư mục dự án mới.
            if (!SceneManager.GetActiveScene().path.StartsWith(ProjectScenes, StringComparison.Ordinal)) return;
            if (UnityEngine.Object.FindAnyObjectByType<Bootstrap>() != null) return;
            new GameObject("[Bootstrap]").AddComponent<Bootstrap>();
        }
    }
}
