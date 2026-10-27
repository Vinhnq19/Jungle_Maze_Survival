#if UNITY_EDITOR
using System;
using JungleMaze.App.Flow;
using JungleMaze.App.MatchUi;
using JungleMaze.App.MatchUi.Mocks;
using JungleMaze.Core.Events;
using JungleMaze.Domain.MatchUi;
using JungleMaze.Presentation.UI.MatchOverlay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode.MatchUi
{
    /// <summary>
    /// The whole UI-03 overlay on real frames: prefab, presenter, input and the logic ticked every Update on the real
    /// unscaled clock, data from the mocks. What Bootstrap.MatchOverlay composes, minus network and Bootstrap.
    /// </summary>
    internal sealed class OverlayRig : IDisposable
    {
        public const string PrefabPath = "Assets/_JungleMaze/Prefabs/UI/MatchOverlay/MatchOverlay.prefab";
        public static readonly Guid Match = Guid.NewGuid();

        public readonly EventBus Bus = new EventBus();
        public readonly Func<double> Clock = () => Time.unscaledTimeAsDouble;
        public readonly MockEliminationSource Elimination = new MockEliminationSource();
        public readonly MockSpectatorSource Spectator = new MockSpectatorSource();
        public readonly MockKillcamReplaySource Replay;
        public readonly KillcamSession Killcam;
        public readonly MatchUiPlayFakes.VolumeAudio Audio = new MatchUiPlayFakes.VolumeAudio();
        public readonly PlayerSettingsService Settings;
        public readonly MatchMenuController Menu;
        public readonly MatchOverlayFlow Flow;
        public readonly MatchOverlayRoot Root;
        public readonly MatchOverlayPresenter Presenter;
        public readonly Ticker Driver;

        public OverlayRig(bool killcam = false, int alive = 9)
        {
            Spectator.Populate(alive);
            Replay = new MockKillcamReplaySource(Clock);
            Killcam = new KillcamSession(Clock, new KillcamOptions(killcam, 5f, 1f), Replay);
            Settings = new PlayerSettingsService(new MatchUiPlayFakes.Store(), Bus, Audio);
            Menu = new MatchMenuController(Bus, Clock, () => Match, AppScreen.InMatch, Settings);
            Flow = new MatchOverlayFlow(Bus, Clock, Elimination, Spectator, AppScreen.InMatch, null, Killcam);
            Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).GetComponent<MatchOverlayRoot>();
            Root.EnsureEventSystem();
            Presenter = new MatchOverlayPresenter(Root, Menu, Flow, Settings, Audio);
            Root.Bind(Presenter, new MatchOverlayInput(Menu, Flow));
            Driver = new GameObject("UI-03 ticker").AddComponent<Ticker>();
            Driver.Rig = this;
        }

        public void Eliminate(EliminationCause cause = EliminationCause.Player) =>
            Elimination.Eliminate(MockEliminationSource.Sample(cause, Match));

        public void Dispose()
        {
            if (Driver != null) Object.Destroy(Driver.gameObject);
            if (Root != null) Object.Destroy(Root.gameObject);
            Flow.Dispose();
            Menu.Dispose();
        }

        /// <summary>Stands in for TickRunner: ticks the UI-03 services once per frame.</summary>
        internal sealed class Ticker : MonoBehaviour
        {
            public OverlayRig Rig;
            public int Frames;

            private void Update()
            {
                if (Rig == null) return;
                Frames++;
                var dt = Time.unscaledDeltaTime;
                Rig.Settings.Tick(dt);
                Rig.Menu.Tick(dt);
                Rig.Flow.Tick(dt);
            }
        }
    }
}
#endif
