#if UNITY_EDITOR
using System;
using JungleMaze.App.Flow;
using JungleMaze.App.MatchUi;
using JungleMaze.App.MatchUi.Mocks;
using JungleMaze.Core.Events;
using JungleMaze.Domain.MatchUi;
using JungleMaze.Presentation.UI.Hud;
using JungleMaze.Presentation.UI.MatchOverlay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode.MatchUi
{
    /// <summary>Phase panels on the real prefab, driven by mocks and a manual clock (no frames needed).</summary>
    public sealed class MatchOverlayPhaseViewTests
    {
        private const string PrefabPath = "Assets/_JungleMaze/Prefabs/UI/MatchOverlay/MatchOverlay.prefab";
        private static readonly Guid Match = Guid.NewGuid();
        private EventBus _bus;
        private MatchUiPlayFakes.Clock _clock;
        private MockEliminationSource _elimination;
        private MockSpectatorSource _spectator;
        private MockKillcamReplaySource _replay;
        private MatchMenuController _menu;
        private MatchOverlayFlow _flow;
        private MatchOverlayRoot _root;
        private MatchOverlayPresenter _presenter;

        private void Build(bool killcam, int heroMaxHp = 100)
        {
            _bus = new EventBus();
            _clock = new MatchUiPlayFakes.Clock { Now = 10 };
            _elimination = new MockEliminationSource();
            _spectator = new MockSpectatorSource();
            _spectator.Populate(9);
            _replay = new MockKillcamReplaySource(_clock.Read);
            var session = new KillcamSession(_clock.Read, new KillcamOptions(killcam, 5f, 1f), _replay);
            _menu = new MatchMenuController(_bus, _clock.Read, () => Match, AppScreen.InMatch);
            _flow = new MatchOverlayFlow(_bus, _clock.Read, _elimination, _spectator, AppScreen.InMatch, null, session);
            _root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).GetComponent<MatchOverlayRoot>();
            _presenter = new MatchOverlayPresenter(_root, _menu, _flow, null, null, null, heroMaxHp);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter?.Dispose();
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            _flow?.Dispose();
            _menu?.Dispose();
        }

        private void Eliminate(EliminationCause cause) => _elimination.Eliminate(MockEliminationSource.Sample(cause, Match));

        [Test]
        public void Eliminated_ShowsLines_HidesMissing()
        {
            Build(false);
            Eliminate(EliminationCause.Player);
            Assert.IsTrue(_presenter.EliminatedFade.Visible, "shown in the same call (AC18)");
            Assert.AreEqual("#4 / 10", _root.Eliminated.Rank.text);
            Assert.AreEqual("Bị Hào hạ bằng Nỏ · cách 6 ô", _root.Eliminated.Cause.text);
            Assert.IsTrue(_root.Eliminated.Kills.gameObject.activeSelf);
            Assert.IsTrue(_root.Eliminated.Spectate.interactable);
            Assert.IsFalse(_root.Eliminated.BlockedReason.gameObject.activeSelf);

            _elimination.Update(new EliminationInfo(Match, 1, 5, 10, EliminationCause.Unknown, EliminationInfo.NoNetId, null, null, float.NaN, 75f, -1));
            Assert.IsFalse(_root.Eliminated.Kills.gameObject.activeSelf, "missing kills line hidden (AC20)");
            Assert.AreEqual("Sống sót: 01:15", _root.Eliminated.Survival.text);
            Assert.IsTrue(_root.Eliminated.Home.interactable, "buttons still usable");
        }

        // AC21: spectate disabled with a reason when nobody is alive; "Về menu" publishes once without confirm.
        [Test]
        public void SpectateDisabled_HomePublishesOnce()
        {
            Build(false);
            _spectator.Populate(0);
            var leaves = new MatchUiPlayFakes.Recorder<LeaveMatchRequested>();
            _bus.Subscribe(leaves.Handler);
            Eliminate(EliminationCause.Zone);
            Assert.IsFalse(_root.Eliminated.Spectate.interactable);
            Assert.IsTrue(_root.Eliminated.BlockedReason.gameObject.activeSelf);
            _root.Eliminated.Home.onClick.Invoke();
            _root.Eliminated.Home.onClick.Invoke();
            Assert.AreEqual(1, leaves.Count);
            Assert.AreEqual(LeaveMatchSource.EliminatedScreen, leaves.Items[0].Source);
            Assert.IsFalse(_menu.IsConfirmOpen, "no confirm box after elimination (Q7)");
        }

        // AC16/G-7 after elimination (review lát 7–11, Gợi ý 1): "Về menu" and "Về sảnh" lock for 5 s with the ESC
        // menu's buttons, then unlock with "Chưa thoát được, thử lại" on the notice bar.
        [Test]
        public void HomeButtons_LockedFiveSeconds_ThenRetryOnNotice()
        {
            Build(false);
            var leaves = new MatchUiPlayFakes.Recorder<LeaveMatchRequested>();
            _bus.Subscribe(leaves.Handler);
            Eliminate(EliminationCause.Player);
            Assert.IsTrue(_root.Eliminated.Home.interactable);
            _root.Eliminated.Home.onClick.Invoke();
            Assert.AreEqual(1, leaves.Count);
            Assert.IsFalse(_root.Eliminated.Home.interactable, "locked in the same call");
            Assert.IsFalse(_root.Spectator.Home.interactable);
            Assert.IsFalse(_presenter.NoticeFade.Visible);

            _clock.Now += 4.9;
            _menu.Tick(0f);
            Assert.IsFalse(_root.Eliminated.Home.interactable, "still locked at 4.9 s");
            _clock.Now += 0.2;
            _menu.Tick(0f);
            Assert.IsTrue(_root.Eliminated.Home.interactable, "unlocked at 5 s");
            Assert.IsTrue(_presenter.NoticeFade.Visible);
            Assert.AreEqual(MatchOverlayPresenter.RetryText, _root.Notice.Text.text);

            _flow.Spectate();
            Assert.IsTrue(_root.Spectator.Home.interactable);
            _root.Spectator.Home.onClick.Invoke();
            Assert.AreEqual(2, leaves.Count, "the retry goes through");
            Assert.AreEqual(LeaveMatchSource.Spectator, leaves.Items[1].Source);
            Assert.IsFalse(_root.Spectator.Home.interactable);
            Assert.IsFalse(_presenter.NoticeFade.Visible, "retry line gone while the new request is pending");
        }

        // AC23: nine rows, no self, the watched row marked, keys and clicks change target in the same call.
        [Test]
        public void Spectator_NineRows_ClickAndKeys_SameCall()
        {
            Build(false);
            Eliminate(EliminationCause.Player);
            _root.Eliminated.Spectate.onClick.Invoke();
            Assert.AreEqual(MatchOverlayPhase.Spectating, _flow.Phase);
            Assert.IsTrue(_presenter.SpectatorFade.Visible);
            Assert.IsFalse(_presenter.EliminatedFade.Visible);
            var rows = _root.Spectator.Rows;
            var shown = 0;
            foreach (var row in rows)
                if (row.gameObject.activeSelf) { shown++; Assert.AreNotEqual(_spectator.LocalNetId, row.NetId); }
            Assert.AreEqual(9, shown);
            Assert.IsTrue(rows[0].Selected);
            Assert.AreEqual("Vinh", _root.Spectator.WatchingName.text);
            Assert.AreEqual("Còn sống 9/10", _root.Spectator.AliveCount.text);

            rows[4].Button.onClick.Invoke();
            Assert.AreEqual(rows[4].NetId, _flow.Targets.Current);
            Assert.AreEqual("Minh", _root.Spectator.WatchingName.text, "label follows in the same call");
            Assert.IsTrue(rows[4].Selected);
            Assert.IsFalse(rows[0].Selected);

            var keys = new System.Collections.Generic.HashSet<KeyCode>();
            var input = new MatchOverlayInput(_menu, _flow, k => keys.Contains(k));
            keys.Add(KeyCode.D);
            input.Poll();
            Assert.AreEqual(6, _flow.Targets.Current);
            keys.Clear(); keys.Add(KeyCode.LeftArrow);
            input.Poll();
            input.Poll();
            Assert.AreEqual(4, _flow.Targets.Current);
            keys.Clear(); keys.Add(KeyCode.E);
            input.Poll();
            Assert.AreEqual(MockEliminationSource.KillerNetId, _flow.Targets.Current, "E jumps to the killer");
            Assert.AreEqual("Hào", _root.Spectator.WatchingName.text);
        }

        // AC26 and AC29 (card): killer out → next player, notice, killer button off; card shows only given fields.
        [Test]
        public void Spectator_KillerOut_Notice_CardFields()
        {
            Build(false);
            Eliminate(EliminationCause.Player);
            _flow.Spectate();
            Assert.IsTrue(_root.Spectator.Killer.interactable);
            _flow.Targets.JumpToKiller();
            Assert.IsTrue(_root.Spectator.CardHp.activeSelf, "HP shown when the state has it");
            Assert.IsTrue(_root.Spectator.CardWeapon.gameObject.activeSelf, "weapon shown when the state has it");
            _spectator.Eliminate(MockEliminationSource.KillerNetId);
            Assert.AreEqual(3, _flow.Targets.Current);
            Assert.IsTrue(_presenter.NoticeFade.Visible);
            Assert.AreEqual("Hào đã bị loại", _root.Notice.Text.text);
            Assert.IsFalse(_root.Spectator.Killer.interactable);
            Assert.IsFalse(_root.Spectator.CardWeapon.gameObject.activeSelf, "player 3 has no weapon in the state");
            Assert.AreEqual("Còn sống 8/10", _root.Spectator.AliveCount.text);
        }

        // AC32/AC34 view side: the board is centred on the killer, dots only for players in the clip.
        [Test]
        public void Killcam_DrawsOnlyClipPlayers_CentredOnKiller()
        {
            Build(true);
            Eliminate(EliminationCause.Player);
            Assert.AreEqual(MatchOverlayPhase.Killcam, _flow.Phase);
            Assert.IsTrue(_presenter.KillcamFade.Visible);
            _clock.Now += 1.0;
            _flow.Tick(0f);
            _presenter.Tick(0f);
            var dots = _root.Killcam.Dots;
            var killer = MockEliminationSource.KillerNetId;
            var visible = 0;
            for (var i = 0; i < dots.Length; i++) if (dots[i].gameObject.activeSelf) visible++;
            Assert.AreEqual(2, visible, "killer and victim only");
            Assert.AreEqual(Vector2.zero, dots[killer].anchoredPosition, "board centred on the killer");
            Assert.AreEqual("Killcam 4,0 s", _root.Killcam.Countdown.text);
            StringAssert.Contains("Hào", _root.Killcam.Title.text);
            _root.Killcam.Skip.onClick.Invoke();
            Assert.AreEqual(MatchOverlayPhase.Eliminated, _flow.Phase);
            Assert.IsFalse(_presenter.KillcamFade.Visible);
        }

        // Review lát 7–11, Gợi ý 2: HP bars use the balance max HP and stay inside 0–1.
        [Test]
        public void Killcam_HpBars_UseBalanceMaxHp()
        {
            Build(true, heroMaxHp: 160);
            Eliminate(EliminationCause.Player);
            _clock.Now += 1.0;
            _flow.Tick(0f);
            _presenter.Tick(0f);
            var fills = _root.Killcam.HpFills;
            Assert.AreEqual(80f / 160f, fills[MockEliminationSource.KillerNetId].anchorMax.x, 1e-4f, "killer HP 80 of 160");
            Assert.AreEqual(1f, KillcamView.HpFraction(150, 100), "over max is clamped");
            Assert.AreEqual(0f, KillcamView.HpFraction(-5, 100));
            Assert.AreEqual(0f, KillcamView.HpFraction(50, 0), "no max HP draws an empty bar");
            Assert.AreEqual(0.5f, KillcamView.HpFraction(50, 100), 1e-6f);
        }

        // AC29 as changed by UI-02 round 2 (UX review): the whole HUD canvas is off during killcam and spectating, on
        // while eliminated, and back on afterwards.
        [Test]
        public void HudMask_TurnsHudCanvasOff_InKillcamAndSpectator()
        {
            Build(true);
            var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_JungleMaze/Prefabs/UI/Hud.prefab")).GetComponent<HudRoot>();
            var canvas = hud.GetComponentInParent<Canvas>(true);
            try
            {
                var mask = new HudSpectatorMask(_flow, () => hud);
                Eliminate(EliminationCause.Player);
                Assert.AreEqual(MatchOverlayPhase.Killcam, _flow.Phase);
                Assert.IsFalse(canvas.enabled, "off under the killcam");
                _root.Killcam.Skip.onClick.Invoke();
                Assert.IsTrue(canvas.enabled, "on while eliminated");
                _flow.Spectate();
                Assert.IsFalse(canvas.enabled, "off while spectating");
                Assert.IsTrue(hud.Vitals.gameObject.activeSelf, "nothing inside the HUD is touched");
                _bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.Results, null));
                Assert.IsTrue(canvas.enabled);
                mask.Dispose();
            }
            finally { Object.DestroyImmediate(hud.gameObject); }
        }

        // AC24 (driver): writes the camera only while spectating, inside the map, on the target when the view fits.
        [Test]
        public void CameraDriver_FollowsTarget_OnlyWhileSpectating()
        {
            Build(false);
            var camGo = new GameObject("cam", typeof(Camera));
            var host = new GameObject("driver");
            try
            {
                var cam = camGo.GetComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 6f;
                cam.aspect = 16f / 9f;
                cam.transform.position = new Vector3(1, 1, -10);
                var driver = host.AddComponent<MatchOverlayCameraDriver>();
                driver.Construct(_flow, _clock.Read, 50f, cam);
                driver.Drive();
                Assert.IsFalse(driver.Wrote, "not spectating: camera untouched");
                Eliminate(EliminationCause.Player);
                _flow.Spectate();
                _spectator.SetPosition(_flow.Targets.Current, 20f, 22f);
                _clock.Now += 0.3;
                driver.Drive();
                Assert.IsTrue(driver.Wrote);
                Assert.AreEqual(20f, cam.transform.position.x, 0.1f);
                Assert.AreEqual(22f, cam.transform.position.y, 0.1f);
                Assert.AreEqual(-10f, cam.transform.position.z);
                _spectator.SetPosition(_flow.Targets.Current, 1f, 49f);
                driver.Drive();
                Assert.AreEqual(6f * 16f / 9f, cam.transform.position.x, 0.01f, "clamped inside the 50×50 map");
                Assert.AreEqual(44f, cam.transform.position.y, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
#endif
