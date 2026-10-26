using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Player;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using JungleMaze.Domain.Player;
using JungleMaze.Tests.App;
using NUnit.Framework;

namespace JungleMaze.Tests.Movement
{
    /// <summary>MOV-03: buffer theo tick, nội suy có độ trễ, jitter/loss/trùng, spawn/despawn, không tích state cũ.</summary>
    public sealed class RemoteInterpolationTests
    {
        private const float FrameDt = 1f / 60f;
        private const float TickDt = 1f / 30f;
        private const float Speed = 4f;

        private static PlayerSnapshot At(uint tick, float x, float y = 10f, float rotation = 0f, bool dead = false) =>
            new PlayerSnapshot { Tick = tick, X = x, Y = y, RotationDegrees = rotation, Hp = 100, IsDead = dead };

        /// <summary>Vị trí thật của người chơi chạy +X với 4 cell/s, dùng làm đáp án so sánh.</summary>
        private static float TruthX(double tick) => 5f + (float)tick * Speed * TickDt;

        // ---------- SnapshotBuffer ----------

        [Test]
        public void Buffer_KeepsTickOrder_RejectsDuplicateAndTooOld()
        {
            var buffer = new SnapshotBuffer();
            Assert.AreEqual(SnapshotInsert.Added, buffer.Insert(At(10, 1)));
            Assert.AreEqual(SnapshotInsert.Added, buffer.Insert(At(12, 3)));
            Assert.AreEqual(SnapshotInsert.AddedOutOfOrder, buffer.Insert(At(11, 2)));
            Assert.AreEqual(SnapshotInsert.Duplicate, buffer.Insert(At(11, 2)));
            Assert.AreEqual(SnapshotInsert.TooOld, buffer.Insert(At(9, 0)));
            Assert.AreEqual(new uint[] { 10, 11, 12 }, new[] { buffer[0].Tick, buffer[1].Tick, buffer[2].Tick });
        }

        [Test]
        public void Buffer_IsBounded_DropsOldest()
        {
            var buffer = new SnapshotBuffer();
            for (uint t = 1; t <= SnapshotBuffer.Capacity + 10; t++) buffer.Insert(At(t, t));
            Assert.AreEqual(SnapshotBuffer.Capacity, buffer.Count);
            Assert.AreEqual(11u, buffer.Oldest.Tick);
            Assert.AreEqual((uint)SnapshotBuffer.Capacity + 10, buffer.Newest.Tick);
        }

        [Test]
        public void Buffer_InterpolatesPositionAndShortestRotation()
        {
            var buffer = new SnapshotBuffer();
            buffer.Insert(At(10, 0f, rotation: 350f));
            buffer.Insert(At(12, 2f, rotation: 10f));
            Assert.IsTrue(buffer.TrySample(11, 3, 3, out var pose, out var extrapolated));
            Assert.IsFalse(extrapolated);
            Assert.AreEqual(1f, pose.X, 1e-5f);
            Assert.AreEqual(0f, HeroMotion.NormalizeDegrees(pose.RotationDegrees), 1e-3f, "Quay qua 0°, không quay ngược 340°");
        }

        [Test]
        public void Buffer_ExtrapolationIsLimited()
        {
            var buffer = new SnapshotBuffer();
            buffer.Insert(At(10, 0f));
            buffer.Insert(At(11, 1f));
            Assert.IsTrue(buffer.TrySample(12, 3, 3, out var near, out var e1));
            Assert.IsTrue(e1);
            Assert.AreEqual(2f, near.X, 1e-5f);
            Assert.IsTrue(buffer.TrySample(30, 3, 3, out var far, out _));
            Assert.AreEqual(4f, far.X, 1e-5f, "Chỉ ngoại suy tối đa 3 tick rồi đứng yên");
        }

        [Test]
        public void Buffer_TrimPastNewest_KeepsTwoSamplesForExtrapolation()
        {
            // Hồi quy: cắt còn một snapshot làm mất vận tốc → hình nhảy lùi về snapshot cuối khi đang thiếu gói.
            var buffer = new SnapshotBuffer();
            for (uint t = 10; t <= 14; t++) buffer.Insert(At(t, t));
            buffer.TrimBefore(15.5);
            Assert.AreEqual(2, buffer.Count);
            Assert.IsTrue(buffer.TrySample(16.5, 3, 3, out var pose, out var extrapolated));
            Assert.IsTrue(extrapolated);
            Assert.AreEqual(16.5f, pose.X, 1e-4f);
        }

        [Test]
        public void Buffer_Teleport_IsNotSmearedAcrossMap()
        {
            var buffer = new SnapshotBuffer();
            buffer.Insert(At(10, 5f));
            buffer.Insert(At(11, 40f));
            Assert.IsTrue(buffer.TrySample(10.5, 3, 3, out var pose, out _));
            Assert.AreEqual(5f, pose.X, 1e-5f);
        }

        // ---------- RemotePlayerInterpolator ----------

        private sealed class Arrival { public double At; public PlayerSnapshot Snapshot; }

        /// <summary>
        /// Phát snapshot 30Hz (player chạy +X), gói đến theo lịch arrival (jitter/loss/trùng tùy hàm),
        /// chạy client 60 FPS; trả sai số lớn nhất so với vị trí thật tại render tick và số lần đi lùi.
        /// </summary>
        private static (float maxError, float avgError, int backwards, InterpolationStats stats) Run(
            Func<uint, Random, IEnumerable<double>> arrivals, double seconds = 6, int seed = 7)
        {
            var bus = new EventBus();
            var remote = new RemotePlayerInterpolator(bus, new PlayerRegistry());
            var rng = new Random(seed);
            var queue = new List<Arrival>();
            for (uint tick = 1; tick <= seconds * 30; tick++)
                foreach (var at in arrivals(tick, rng))
                    queue.Add(new Arrival { At = at, Snapshot = At(tick, TruthX(tick)) });
            queue.Sort((a, b) => a.At.CompareTo(b.At));

            var next = 0;
            float maxError = 0, sumError = 0, lastX = float.MinValue;
            var samples = 0; var backwards = 0;
            for (var now = 0.0; now < seconds; now += FrameDt)
            {
                while (next < queue.Count && queue[next].At <= now) remote.Push(3, queue[next++].Snapshot);
                remote.Tick(FrameDt);
                if (now < 1.0 || !remote.TryGetPose(3, out var pose)) continue; // bỏ 1s khởi động đồng hồ
                var error = Math.Abs(pose.X - TruthX(remote.RenderTick));
                maxError = Math.Max(maxError, error); sumError += error; samples++;
                if (pose.X < lastX - 1e-4f) backwards++;
                lastX = pose.X;
            }
            return (maxError, samples > 0 ? sumError / samples : 0, backwards, remote.Stats);
        }

        private static double Sent(uint tick) => tick * TickDt;

        [Test]
        public void SteadyStream_RendersExactlyDelayed_NoBackwardMotion()
        {
            var r = Run((tick, _) => new[] { Sent(tick) + 0.05 });
            Assert.Less(r.maxError, 0.01f);
            Assert.AreEqual(0, r.backwards);
            Assert.AreEqual(0, r.stats.ClampedSamples);
            TestContext.Progress.WriteLine($"[MOV-03] steady maxErr={r.maxError:F4} jitterMs={r.stats.JitterMs:F1}");
        }

        [Test]
        public void Jitter20ms_StaysSmooth_AndMeasuresJitter()
        {
            var r = Run((tick, rng) => new[] { Sent(tick) + 0.05 + (rng.NextDouble() * 2 - 1) * 0.02 });
            Assert.Less(r.maxError, 0.05f);
            Assert.AreEqual(0, r.backwards, "Không giật lùi khi gói đến lệch nhịp");
            Assert.Greater(r.stats.JitterMs, 1f, "Jitter được đo");
            TestContext.Progress.WriteLine($"[MOV-03] jitter20 maxErr={r.maxError:F4} avgErr={r.avgError:F4} jitterMs={r.stats.JitterMs:F1} reordered={r.stats.OutOfOrder}");
        }

        [Test]
        public void LossOneThird_WithJitter_StaysBounded()
        {
            var r = Run((tick, rng) =>
            {
                var lost = rng.NextDouble() < 1.0 / 3.0;
                return lost ? Array.Empty<double>() : new[] { Sent(tick) + 0.05 + (rng.NextDouble() * 2 - 1) * 0.02 };
            }, seconds: 10);
            // Đường thẳng: nội suy qua lỗ hổng vẫn đúng; sai số chỉ đến từ ngoại suy bị chặn.
            Assert.Less(r.avgError, 0.02f);
            Assert.Less(r.maxError, 0.3f);
            Assert.AreEqual(0, r.backwards);
            Assert.Less(r.stats.ClampedSamples, r.stats.Samples / 20 + 1);
            TestContext.Progress.WriteLine($"[MOV-03] loss33 maxErr={r.maxError:F4} avgErr={r.avgError:F4} extrapolated={r.stats.ExtrapolatedSamples}/{r.stats.Samples} clamped={r.stats.ClampedSamples}");
        }

        [Test]
        public void DuplicatePackets_AreIgnored()
        {
            var r = Run((tick, _) => new[] { Sent(tick) + 0.05, Sent(tick) + 0.06 });
            Assert.Greater(r.stats.Duplicates, 100);
            Assert.Less(r.maxError, 0.01f);
            Assert.AreEqual(0, r.backwards);
        }

        [Test]
        public void WssStallBurst_HoldsThenResumesAtPresent_DoesNotReplayOldState()
        {
            // TCP/WSS: gói không mất nhưng đứng 2 giây rồi xả dồn một lúc.
            var bus = new EventBus();
            var remote = new RemotePlayerInterpolator(bus, new PlayerRegistry());
            uint sentTick = 0;
            var held = false;
            float afterBurstMaxError = 0;
            for (var now = 0.0; now < 7; now += FrameDt)
            {
                var stalled = now >= 3 && now < 5;
                while ((sentTick + 1) * TickDt + 0.05 <= now && !stalled)
                {
                    sentTick++;
                    remote.Push(1, At(sentTick, TruthX(sentTick)));
                }
                remote.Tick(FrameDt);
                if (!remote.TryGetPose(1, out var pose)) continue;
                if (now > 3.5 && now < 5) held |= remote.Stats.ClampedSamples > 0;
                if (now > 5.1) afterBurstMaxError = Math.Max(afterBurstMaxError, Math.Abs(pose.X - TruthX(remote.RenderTick)));
            }
            var stats = remote.Stats;
            Assert.IsTrue(held, "Trong lúc nghẽn: ngoại suy tối đa rồi đứng chờ, không trôi");
            Assert.Less(afterBurstMaxError, 0.01f, "Sau khi xả dồn: vẽ đúng thời điểm hiện tại, không phát lại 2 giây cũ");
            Assert.LessOrEqual(stats.MaxBufferDepth, SnapshotBuffer.Capacity);
            Assert.Less(stats.BufferedMs, 250f, "Không giữ hàng giây state cũ trong buffer");
        }

        [Test]
        public void LatencyDrop_ResyncsClockForward()
        {
            // 3 giây đầu gói đến trễ 1s (đường WSS nghẽn), sau đó về bình thường: đồng hồ phải nhảy tới.
            var r = Run((tick, _) => new[] { Sent(tick) + (Sent(tick) < 3 ? 1.0 : 0.05) }, seconds: 6);
            Assert.GreaterOrEqual(r.stats.ClockResets, 2);
            Assert.AreEqual(0, r.backwards, "Resync chỉ tiến, không tua lùi");
            Assert.Less(r.stats.BufferedMs, 250f);
        }

        [Test]
        public void Despawn_ThenRespawnFarAway_NoGhostInterpolation()
        {
            var remote = new RemotePlayerInterpolator(new EventBus(), new PlayerRegistry());
            for (uint t = 1; t <= 10; t++) { remote.Push(2, At(t, 5f)); remote.Push(4, At(t, 1f)); remote.Tick(TickDt); }
            remote.Remove(2);
            Assert.AreEqual(0, remote.BufferedCount(2));
            Assert.IsFalse(remote.TryGetPose(2, out _), "Đã despawn thì không còn vẽ");
            for (uint t = 11; t <= 20; t++) { remote.Push(2, At(t, 40f, 40f)); remote.Push(4, At(t, 1f)); remote.Tick(TickDt); }
            Assert.IsTrue(remote.TryGetPose(2, out var pose));
            Assert.AreEqual(40f, pose.X, 1e-4f, "Spawn lại không trượt từ vị trí cũ");
            Assert.AreEqual(1, remote.Stats.Despawns);
        }

        [Test]
        public void MatchSession_MissingPlayerAndNewMatch_RemoveBuffers()
        {
            var bus = new EventBus(); var network = new FakeNetwork(bus); var players = new PlayerRegistry();
            using var match = new MatchSession(network, network, network, bus, players);
            using var remote = new RemotePlayerInterpolator(bus, players);
            match.Initialize(); remote.Initialize();
            network.SetState(NetworkState.Joined, DisconnectReason.None);
            var first = Guid.NewGuid();
            network.Push(new MatchPhasePacket { MatchId = first, Phase = MatchPhase.InGame });
            network.Push(Snapshot(first, 1, 0, 1));
            Assert.AreEqual(1, remote.BufferedCount(1));
            // Mất gói despawn reliable: snapshot không còn NetId 1 vẫn phải xóa entity.
            network.Push(Snapshot(first, 2, 0));
            Assert.AreEqual(0, remote.BufferedCount(1));
            Assert.AreEqual(2, remote.BufferedCount(0), "NetId 0 vẫn còn, giữ cả hai snapshot");
            // Đổi trận: mọi entity cũ bị xóa, không có ghost.
            network.Push(new MatchPhasePacket { MatchId = Guid.NewGuid(), Phase = MatchPhase.InGame });
            Assert.AreEqual(0, remote.BufferedCount(0));
            // Mất kết nối cũng xóa.
            network.SetState(NetworkState.Failed, DisconnectReason.ConnectionLost);
            for (byte id = 0; id < RemotePlayerInterpolator.MaxPlayers; id++) Assert.AreEqual(0, remote.BufferedCount(id));
        }

        private static WorldSnapshotPacket Snapshot(Guid match, uint tick, params byte[] ids)
        {
            var packet = new WorldSnapshotPacket { MatchId = match, ServerTick = tick, PlayerCount = (byte)ids.Length };
            for (var i = 0; i < ids.Length; i++)
                packet.SetPlayer(i, new PlayerStatePacket { Tick = tick, NetId = ids[i], X = 10 + ids[i], Y = 10, Hp = 100 });
            return packet;
        }
    }
}
