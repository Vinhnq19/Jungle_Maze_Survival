using System.IO;
using System.Linq;
using NUnit.Framework;

namespace JungleMaze.Tests.MatchUi
{
    /// <summary>
    /// 4.4: EditMode cannot compile a Release build, so this reads the source instead: every UI-03 mock, the scenario
    /// switchboard and the F7 panel must open with the dev-only guard and close it at the end of the file.
    /// </summary>
    public sealed class MatchOverlayMockGuardTests
    {
        private const string Guard = "#if UNITY_EDITOR || DEVELOPMENT_BUILD";

        private static string[] DevFiles()
        {
            var mocks = Directory.GetFiles("Assets/_JungleMaze/Scripts/Application/MatchUi/Mocks", "*.cs");
            return mocks.Concat(new[] { "Assets/_JungleMaze/Scripts/Presentation/UI/MatchOverlay/MatchOverlayDebugPanel.cs" }).ToArray();
        }

        [Test]
        public void MocksAndPanel_AreDevOnly()
        {
            var files = DevFiles();
            Assert.GreaterOrEqual(files.Length, 5, "3 mocks + scenarios + F7 panel");
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
                Assert.AreEqual(Guard, lines.First(), file);
                Assert.AreEqual("#endif", lines.Last(), file);
            }
        }

        [Test]
        public void Composition_RegistersMocksOnlyInDevBuilds()
        {
            var text = File.ReadAllText("Assets/_JungleMaze/Scripts/Bootstrap/MatchOverlay/Bootstrap.MatchOverlay.cs");
            var guard = text.LastIndexOf(Guard);
            Assert.Greater(guard, 0);
            foreach (var devOnly in new[] { "new MatchOverlayScenarios", "Attach<MatchOverlayDebugPanel>" })
                Assert.Greater(text.IndexOf(devOnly), guard, devOnly + " must sit inside the dev-only block");
        }
    }

    /// <summary>The F7 switchboard drives the real flow through the mocks and gives the live sources back.</summary>
    public sealed class MatchOverlayScenariosTests
    {
        [Test]
        public void Preview_Eliminate_Killcam_ThenLiveAgain()
        {
            var bus = new JungleMaze.Core.Events.EventBus();
            var clock = new FakeClock { Now = 5 };
            var liveElimination = new JungleMaze.App.MatchUi.Mocks.MockEliminationSource();
            var liveSpectator = new JungleMaze.App.MatchUi.Mocks.MockSpectatorSource();
            var liveReplay = new JungleMaze.App.MatchUi.UnavailableKillcamReplaySource();
            var killcam = new JungleMaze.App.MatchUi.KillcamSession(clock.Read, new JungleMaze.App.MatchUi.KillcamOptions(false, 5f, 1f), liveReplay);
            var menu = new JungleMaze.App.MatchUi.MatchMenuController(bus, clock.Read, () => System.Guid.Empty, JungleMaze.App.Flow.AppScreen.MainMenu);
            var flow = new JungleMaze.App.MatchUi.MatchOverlayFlow(bus, clock.Read, liveElimination, liveSpectator,
                JungleMaze.App.Flow.AppScreen.MainMenu, null, killcam);
            var scenarios = new JungleMaze.App.MatchUi.Mocks.MatchOverlayScenarios(flow, menu, killcam, liveElimination, liveSpectator, liveReplay, clock.Read);

            Assert.AreEqual(JungleMaze.Domain.MatchUi.MatchOverlayPhase.Inactive, flow.Phase);
            scenarios.PreviewMatch();
            Assert.AreEqual(JungleMaze.Domain.MatchUi.MatchOverlayPhase.Playing, flow.Phase);
            Assert.IsTrue(menu.PressEscape(), "ESC works in the preview");
            menu.PressEscape();
            Assert.IsFalse(scenarios.KillcamEnabled);
            scenarios.SetKillcam(true);
            Assert.IsTrue(scenarios.KillcamEnabled, "the F7 box reads the session");
            scenarios.Eliminate(JungleMaze.Domain.MatchUi.EliminationCause.Player);
            Assert.AreEqual(JungleMaze.Domain.MatchUi.MatchOverlayPhase.Killcam, flow.Phase);
            Assert.AreEqual(1, scenarios.Replay.RequestCount);

            scenarios.UseMocks(false);
            Assert.AreSame(liveSpectator, flow.Spectator);
            Assert.AreSame(liveElimination, flow.Elimination);
            scenarios.EndPreview();
            Assert.AreEqual(JungleMaze.Domain.MatchUi.MatchOverlayPhase.Inactive, flow.Phase);
            flow.Dispose();
            menu.Dispose();
        }

        // Gợi ý 6 (review lát 7–11): started with `-killcam`, the F7 box shows "on" before anyone touches it.
        [Test]
        public void KillcamSwitch_ShowsTheSessionState()
        {
            var bus = new JungleMaze.Core.Events.EventBus();
            var clock = new FakeClock();
            var liveElimination = new JungleMaze.App.MatchUi.Mocks.MockEliminationSource();
            var liveSpectator = new JungleMaze.App.MatchUi.Mocks.MockSpectatorSource();
            var liveReplay = new JungleMaze.App.MatchUi.UnavailableKillcamReplaySource();
            var killcam = new JungleMaze.App.MatchUi.KillcamSession(clock.Read, new JungleMaze.App.MatchUi.KillcamOptions(true, 5f, 1f), liveReplay);
            var flow = new JungleMaze.App.MatchUi.MatchOverlayFlow(bus, clock.Read, liveElimination, liveSpectator,
                JungleMaze.App.Flow.AppScreen.MainMenu, null, killcam);
            var scenarios = new JungleMaze.App.MatchUi.Mocks.MatchOverlayScenarios(flow, null, killcam, liveElimination, liveSpectator, liveReplay, clock.Read);
            Assert.IsTrue(scenarios.KillcamEnabled);
            scenarios.SetKillcam(false);
            Assert.IsFalse(killcam.Options.Enabled);
            Assert.IsFalse(scenarios.KillcamEnabled);
            Assert.IsFalse(new JungleMaze.App.MatchUi.Mocks.MatchOverlayScenarios(flow, null, null, liveElimination, liveSpectator, liveReplay, clock.Read).KillcamEnabled);
            flow.Dispose();
        }
    }
}
