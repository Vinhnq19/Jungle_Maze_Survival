using System;
using JungleMaze.App.Audio;
using JungleMaze.App.Flow;
using JungleMaze.App.Match;
using JungleMaze.App.MatchUi;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JungleMaze.App.MatchUi.Mocks;
#endif
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;
using JungleMaze.Infrastructure.Settings;
using JungleMaze.Presentation.UI;
using JungleMaze.Presentation.UI.MatchOverlay;
using UnityEngine;

namespace JungleMaze.Composition
{
    /// <summary>
    /// UI-03 composition: settings (N2–N4), ESC menu (N5, N6), elimination/spectator sources (N7, N8), killcam (N9, N10),
    /// the overlay prefab instantiated once (T-4) and its camera driver, plus two temporary hooks that each go away with
    /// one line (T-12): the N5 input guard and the leave bridge. Dev builds add the mocks and the F7 panel.
    /// Kept in its own file so <c>Bootstrap.cs</c> only carries the one call.
    /// </summary>
    public sealed partial class Bootstrap
    {
        private void ComposeMatchOverlay(IEventBus bus, ITickScheduler ticks, MatchSession match, IPacketReceiver receiver,
            GameFlow flow, IBalanceConfigProvider balance)
        {
            // Loaded once here, like UiArtSet and AudioRuntimeSet: an auto-created Bootstrap has no serialized reference.
            var set = Resources.Load<MatchOverlaySet>(MatchOverlaySet.ResourcePath);
            if (set == null || set.OverlayPrefab == null)
            {
                _log.Warn("[Bootstrap] MatchOverlaySet missing or incomplete; in-match settings, spectator and killcam are off.");
                return;
            }

            var tuning = set.ToTuning();
            Func<double> clock = () => Time.unscaledTimeAsDouble;
            var audio = _registry.Resolve<IAudioService>();
            var poses = _registry.Resolve<IPlayerPoseSource>();
            var settings = new PlayerSettingsService(new PlayerPrefsPlayerSettingsStore(), bus, audio);
            var menu = new MatchMenuController(bus, clock, () => match.MatchId, flow.Screen, settings, tuning);
            var elimination = new MatchSessionEliminationSource(match, receiver);
            var spectator = new MatchSessionSpectatorSource(match, receiver, poses, balance.Match.HeroMaxHp);
            var replay = new UnavailableKillcamReplaySource();
            var killcamOn = set.KillcamEnabled;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            killcamOn |= Array.IndexOf(Environment.GetCommandLineArgs(), "-killcam") >= 0;
#endif
            var killcam = new KillcamSession(clock,
                new KillcamOptions(killcamOn, balance.Match.KillcamDurationSeconds, tuning.KillcamWaitSeconds,
                    tuning.KillcamEndTickTolerance), replay);
            var overlayFlow = new MatchOverlayFlow(bus, clock, elimination, spectator, flow.Screen, tuning, killcam);

            _registry.Register<IPlayerSettings>(settings);
            _registry.Register<IPlayerSettingsWriter>(settings);
            _registry.Register<IMatchMenuState>(menu);
            _registry.Register(menu);
            _registry.Register<IEliminationSource>(elimination);
            _registry.Register<ISpectatorSource>(spectator);
            _registry.Register<IKillcamReplaySource>(replay);
            _registry.Register(killcam);
            _registry.Register(overlayFlow);
            ticks.Add(settings);
            ticks.Add(menu);
            ticks.Add(elimination);
            ticks.Add(spectator);
            ticks.Add(overlayFlow);

            var root = Instantiate(set.OverlayPrefab, transform);
            root.name = "MatchOverlay";
            root.EnsureEventSystem();
            root.Bind(new MatchOverlayPresenter(root, menu, overlayFlow, settings, audio, tuning, balance.Match.HeroMaxHp),
                new MatchOverlayInput(menu, overlayFlow));
            root.BindSettingsFlush(settings.Flush);
            gameObject.AddComponent<MatchOverlayCameraDriver>().Construct(overlayFlow, clock, balance.Match.MapSizeCells);
            _registry.Register(new HudSpectatorMask(overlayFlow));

            // Temporary hook (T-12): delete the line when MOV-02 reads IMatchMenuState. UI-02 now handles LeaveMatchRequested.
            _registry.Register(new MatchMenuInputGuard(menu, intent => match.SetIntent(intent), () => match.OwnNetId, poses,
                _registry.Resolve<PlayerInputService>(), GetComponent<MatchDemoView>, () => menu.InMatch));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _registry.Register(new MatchOverlayScenarios(overlayFlow, menu, killcam, elimination, spectator, replay, clock));
            Attach<MatchOverlayDebugPanel>();
#endif
        }
    }
}
