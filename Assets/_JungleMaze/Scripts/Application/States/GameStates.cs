using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Flow;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.States;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.States
{
    /// <summary>State cấp ứng dụng; override phần event mình quan tâm, phần còn lại bỏ qua.</summary>
    public abstract class GameState : IState
    {
        protected readonly GameFlow Flow;
        protected GameState(GameFlow flow) => Flow = flow;

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }

        /// <summary>Mặc định: mất kết nối/thất bại ở bất kỳ đâu thì về menu kèm lý do.</summary>
        public virtual void OnNetworkStateChanged(in NetworkStateChanged change)
        {
            if (change.Current == NetworkState.Failed)
                Flow.Go(Flow.MainMenu.WithMessage(DisconnectReasons.Describe(change.Reason)));
            else if (change.Current == NetworkState.Disconnected)
                Flow.Go(Flow.MainMenu.WithMessage(DisconnectReasons.Describe(change.Reason)));
        }

        public virtual void OnGameEvent(in EventPacket evt) { }
    }

    public sealed class BootState : GameState
    {
        public BootState(GameFlow flow) : base(flow) { }

        public override void Enter()
        {
            Flow.Show(AppScreen.Boot);
            // Không load scene ở đây: dev bấm Play ở scene bất kỳ vẫn ở lại scene đó.
            Flow.Go(Flow.MainMenu.WithMessage(""));
        }
    }

    public sealed class MainMenuState : GameState
    {
        private readonly ISceneLoader _scenes;
        private string _message = "";

        public MainMenuState(GameFlow flow, ISceneLoader scenes) : base(flow) => _scenes = scenes;

        public MainMenuState WithMessage(string message)
        {
            _message = message ?? "";
            return this;
        }

        public override void Enter()
        {
            // Chỉ quay về scene menu khi đang ở scene gameplay; lúc boot giữ nguyên scene hiện tại.
            if (_scenes.ActiveScene == Flow.Settings.GameScene) _scenes.Load(Flow.Settings.MenuScene);
            Flow.Show(AppScreen.MainMenu, _message);
        }

        public override void OnNetworkStateChanged(in NetworkStateChanged change)
        {
            if (change.Current == NetworkState.Connecting) Flow.Go(Flow.Connecting);
            else if (change.Current == NetworkState.Failed) { _message = DisconnectReasons.Describe(change.Reason); Flow.Show(AppScreen.MainMenu, _message); }
        }
    }

    public sealed class ConnectingState : GameState
    {
        private readonly INetworkClient _network;

        public ConnectingState(GameFlow flow, INetworkClient network) : base(flow) => _network = network;

        public override void Enter() => Flow.Show(AppScreen.Connecting, "Đang kết nối " + _network.Endpoint);

        public override void OnNetworkStateChanged(in NetworkStateChanged change)
        {
            if (change.Current == NetworkState.Authenticating) Flow.Show(AppScreen.Connecting, "Đang xác thực...");
            else if (change.Current == NetworkState.Joined) Flow.Go(Flow.InRoom);
            else base.OnNetworkStateChanged(change);
        }
    }

    public sealed class InRoomState : GameState
    {
        private readonly ISceneLoader _scenes;

        public InRoomState(GameFlow flow, ISceneLoader scenes) : base(flow) => _scenes = scenes;

        public override void Enter()
        {
            _scenes.Load(Flow.Settings.GameScene);
            Flow.Show(AppScreen.InRoom, "Đã vào phòng, chờ trận bắt đầu.");
        }

        public override void OnGameEvent(in EventPacket evt)
        {
            if (evt.EventType == GameEventType.MatchStarted) Flow.Go(Flow.InMatch);
        }
    }

    public sealed class InMatchState : GameState
    {
        private readonly PlayerInputService _input;

        public InMatchState(GameFlow flow, PlayerInputService input) : base(flow) => _input = input;

        public override void Enter()
        {
            _input.Enabled = true; // chỉ gửi input khi đang trong trận
            Flow.Show(AppScreen.InMatch);
        }

        public override void Exit() => _input.Enabled = false;

        public override void OnGameEvent(in EventPacket evt)
        {
            if (evt.EventType == GameEventType.MatchEnded) Flow.Go(Flow.Results);
        }
    }

    public sealed class ResultsState : GameState
    {
        private float _remaining;

        public ResultsState(GameFlow flow) : base(flow) { }

        public override void Enter()
        {
            _remaining = Flow.Settings.ResultsSeconds;
            Flow.Show(AppScreen.Results, "Trận đã kết thúc.");
        }

        public override void Tick(float deltaTime)
        {
            if (Flow.ServerDriven) return; // Ending/replay duration belongs to the authoritative server.
            _remaining -= deltaTime;
            if (_remaining <= 0f) Flow.Go(Flow.InRoom);
        }
    }
}
