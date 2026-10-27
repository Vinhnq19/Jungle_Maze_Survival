using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Flow;
using JungleMaze.App.MatchUi;
using JungleMaze.App.Player;
using JungleMaze.Domain.Player;
using JungleMaze.Presentation.UI;
using JungleMaze.Presentation.UI.MatchOverlay;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.MatchUi
{
    /// <summary>
    /// AC2 (data side): with the menu open and the N5 guard engaged, a 30 Hz snapshot feed for 10 s is fully received
    /// (≥ 90 % of 300), the zone timer keeps moving and the match keeps running; the frame side is a PlayMode test.
    /// </summary>
    public sealed class MatchMenuNoPauseTests
    {
        [Test]
        public void TenSecondsMenuOpen_AllSnapshotsReceived_ZoneMoves()
        {
            using (var h = new LiveMatchHarness())
            {
                h.Start(ownNetId: 0, count: 3);
                var menu = new MatchMenuController(h.Bus, () => 0d, () => h.MatchId, AppScreen.InMatch);
                var host = new GameObject("demo");
                var demo = host.AddComponent<MatchDemoView>();
                var guard = new MatchMenuInputGuard(menu, i => h.Match.SetIntent(i), () => h.Match.OwnNetId, null,
                    new PlayerInputService(h.Network), () => demo, () => menu.InMatch);
                try
                {
                    menu.Open();
                    var before = h.Match.Snapshots;
                    h.Network.Push(new ZoneStatePacket { MatchId = h.MatchId, Tick = h.Tick, PhaseIndex = 1 });
                    var zoneTick = h.Match.Zone.Tick;
                    for (var i = 0; i < 300; i++)
                    {
                        h.Snapshot(new byte[] { 0, 1, 2 });
                        h.Match.Tick(1f / 30f);
                    }
                    h.Network.Push(new ZoneStatePacket { MatchId = h.MatchId, Tick = h.Tick, PhaseIndex = 1 });
                    Assert.IsTrue(menu.IsOpen);
                    Assert.GreaterOrEqual(h.Match.Snapshots - before, 270, "≥ 90 % of 300 snapshots");
                    Assert.Greater(h.Match.Zone.Tick, zoneTick, "zone state keeps updating");
                    Assert.Greater(h.Match.InputsSent, 0, "the match keeps sending (neutral) input: it is not paused");
                }
                finally
                {
                    guard.Dispose();
                    menu.Dispose();
                    Object.DestroyImmediate(host);
                }
            }
        }
    }
}
