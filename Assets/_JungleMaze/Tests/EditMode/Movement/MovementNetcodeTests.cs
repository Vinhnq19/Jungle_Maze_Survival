using System;
using System.Collections.Generic;
using System.Globalization;
using JungleMaze.App.Player;
using JungleMaze.Domain.Player;
using NUnit.Framework;

namespace JungleMaze.Tests.Movement
{
    /// <summary>
    /// MOV-02 + MOV-03 end-to-end với fake authoritative server: client A di chuyển (đâm vào Hard Cover, trượt, zig-zag),
    /// client B quan sát A. Cùng một kịch bản chạy qua nhiều điều kiện mạng; mỗi lượt in một dòng [Mov] (RTT/correction/jitter).
    /// </summary>
    public sealed class MovementNetcodeTests
    {
        private const float FrameDt = 1f / 60f;
        private const double Seconds = 9.0;

        public static IEnumerable<LinkProfile> Profiles()
        {
            yield return new LinkProfile { Name = "udp-rtt0" };
            yield return new LinkProfile { Name = "udp-rtt50", RttMs = 50 };
            yield return new LinkProfile { Name = "udp-rtt100", RttMs = 100 };
            yield return new LinkProfile { Name = "udp-rtt100-jitter20", RttMs = 100, JitterMs = 20 };
            yield return new LinkProfile { Name = "udp-rtt50-jitter20-loss33", RttMs = 50, JitterMs = 20, Loss = 1f / 3f };
            yield return new LinkProfile { Name = "udp-rtt100-jitter20-loss33", RttMs = 100, JitterMs = 20, Loss = 1f / 3f };
            yield return new LinkProfile { Name = "udp-rtt100-jitter10-dup20", RttMs = 100, JitterMs = 10, Duplicate = 0.2f };
            yield return new LinkProfile { Name = "wss-rtt100-jitter20", RttMs = 100, JitterMs = 20, Reliable = true };
        }

        /// <summary>Kịch bản input của A theo thời gian (giây): đâm vào BoulderRock, trượt, sang trái chạm CityWall, zig-zag rồi dừng.</summary>
        private static (float X, float Y, float Aim) Script(double t)
        {
            if (t < 1.5) return (1, 0, 0);
            if (t < 2.5) return (1, 1, 45);
            if (t < 3.5) return (-1, 0, 180);
            if (t < 4.5) return (0, -1, 270);
            if (t < 6.5) return ((int)(t / 0.2) % 2 == 0 ? (-1f, 1f, 135f) : (1f, 0.5f, 30f));
            return (0, 0, 60);
        }

        private sealed class Result
        {
            public bool CoverViolation;
            public string CoverDetail = "";
            public float RemoteMaxError, RemoteAvgError, FinalPredictionError, FinalPoseError, FinalRemoteRotationError;
            public int RemoteSamples;
            public PredictionStats Prediction;
            public InterpolationStats Interpolation;
            public string Line = "";
        }

        private static Result Run(LinkProfile profile)
        {
            var world = MovementTestMap.Create();
            var server = new FakeAuthoritativeServer(world);
            using var a = new TestMovementClient(world, profile, 0);
            using var b = new TestMovementClient(world, profile, 1);
            server.AddPlayer(0, 26f, 25f, a.Up, a.Down);
            server.AddPlayer(1, 10f, 10f, b.Up, b.Down);
            a.Join(); b.Join();
            server.Start(0);

            var result = new Result();
            double errorSum = 0;
            for (var now = 0.0; now < Seconds; now += FrameDt)
            {
                var (mx, my, aim) = Script(now);
                a.Match.SetIntent(new PlayerIntent { MoveX = mx, MoveY = my, AimDegrees = aim });
                server.Update(now);
                a.Frame(now, FrameDt);
                b.Frame(now, FrameDt);

                var prediction = a.Match.Prediction;
                if (prediction.IsActive && !result.CoverViolation)
                {
                    if (world.Overlaps(prediction.PredictedX, prediction.PredictedY))
                    { result.CoverViolation = true; result.CoverDetail = $"logic ({prediction.PredictedX}, {prediction.PredictedY}) t={now:F2}"; }
                    else if (a.Poses.TryGetPose(0, out var own) && world.Overlaps(own.X, own.Y))
                    { result.CoverViolation = true; result.CoverDetail = $"pose ({own.X}, {own.Y}) t={now:F2}"; }
                }
                if (now > 1.0 && b.Poses.TryGetPose(0, out var seen) && server.TryGetTruth(0, b.Remote.RenderTick, out var tx, out var ty))
                {
                    var error = (float)Math.Sqrt((seen.X - tx) * (seen.X - tx) + (seen.Y - ty) * (seen.Y - ty));
                    result.RemoteMaxError = Math.Max(result.RemoteMaxError, error);
                    errorSum += error;
                    result.RemoteSamples++;
                }
            }

            var serverA = server.Peers[0];
            result.FinalPredictionError = Distance(a.Match.Prediction.PredictedX, a.Match.Prediction.PredictedY, serverA.X, serverA.Y);
            if (a.Poses.TryGetPose(0, out var finalPose)) result.FinalPoseError = Distance(finalPose.X, finalPose.Y, serverA.X, serverA.Y);
            else result.FinalPoseError = float.MaxValue;
            if (b.Poses.TryGetPose(0, out var remotePose))
                result.FinalRemoteRotationError = Math.Abs(HeroMotion.NormalizeDegrees(remotePose.RotationDegrees - 60f));
            else result.FinalRemoteRotationError = float.MaxValue;
            result.RemoteAvgError = result.RemoteSamples > 0 ? (float)(errorSum / result.RemoteSamples) : float.MaxValue;
            result.Prediction = a.Match.Prediction.Stats;
            result.Interpolation = b.Remote.Stats;
            result.Line = MovementTelemetry.Format(profile.Name, profile.RttMs, profile.RttMs + profile.JitterMs, result.Prediction, result.Interpolation)
                + string.Format(CultureInfo.InvariantCulture, " | remoteErrAvg={0:F3} remoteErrMax={1:F3} final={2:F4} lossUp={3} lossDown={4} dupDown={5}",
                    result.RemoteAvgError, result.RemoteMaxError, result.FinalPredictionError, a.Up.Dropped, b.Down.Dropped, b.Down.Duplicated);
            return result;
        }

        private static float Distance(float ax, float ay, float bx, float by) =>
            (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        [TestCaseSource(nameof(Profiles))]
        public void PredictionAndInterpolation_StayStable(LinkProfile profile)
        {
            var r = Run(profile);
            TestContext.Progress.WriteLine(r.Line);
            UnityEngine.Debug.Log(r.Line);

            // MOV-02
            Assert.IsFalse(r.CoverViolation, "Xuyên Hard Cover: " + r.CoverDetail);
            Assert.Greater(r.Prediction.Reconciles, 100, "Phải reconcile liên tục theo snapshot");
            Assert.AreEqual(0, r.Prediction.Snaps, "Không được phải dịch thẳng khi mạng chỉ trễ/mất gói");
            Assert.AreEqual(0, r.Prediction.Overflows);
            Assert.Less(r.Prediction.MaxErrorMeters, 0.75f, "Correction lớn nhất");
            Assert.Less(r.Prediction.AverageErrorMeters, 0.1f, "Correction trung bình");
            Assert.Less(r.FinalPredictionError, 0.01f, "Đứng yên thì dự đoán phải trùng server");
            Assert.Less(r.FinalPoseError, 0.01f, "Offset làm mượt đã hết khi đứng yên");

            // MOV-03
            Assert.Greater(r.RemoteSamples, 300);
            Assert.Less(r.RemoteAvgError, profile.Loss > 0 ? 0.12f : 0.06f, "B nhìn A lệch trung bình so với sự thật của server");
            Assert.Less(r.RemoteMaxError, 0.8f);
            Assert.Less(r.FinalRemoteRotationError, 1f, "B thấy A xoay đúng hướng ngắm");
            Assert.Less(r.Interpolation.BufferedMs, 250f, "Không tích state cũ");
            Assert.LessOrEqual(r.Interpolation.MaxBufferDepth, 16);
        }

        [Test]
        public void LongSession_PendingInputsStayBounded()
        {
            // Mạng xấu kéo dài: số input chờ ack luôn ~ RTT × 30Hz, không phình theo thời gian.
            var profile = new LinkProfile { Name = "udp-rtt100-jitter20-loss33-long", RttMs = 100, JitterMs = 20, Loss = 1f / 3f, Seed = 3 };
            var world = MovementTestMap.Create();
            var server = new FakeAuthoritativeServer(world);
            using var a = new TestMovementClient(world, profile, 0);
            server.AddPlayer(0, 10f, 10f, a.Up, a.Down);
            a.Join();
            server.Start(0);
            var maxPending = 0;
            for (var now = 0.0; now < 30; now += FrameDt)
            {
                a.Match.SetIntent(new PlayerIntent { MoveX = (int)now % 2 == 0 ? 1 : -1, MoveY = 0.2f });
                server.Update(now);
                a.Frame(now, FrameDt);
                maxPending = Math.Max(maxPending, a.Match.Prediction.Stats.Pending);
            }
            TestContext.Progress.WriteLine($"[MOV-02] long maxPending={maxPending} " + MovementTelemetry.Format(profile.Name, 100, 120, a.Match.Prediction.Stats, a.Remote.Stats));
            Assert.Less(maxPending, 20);
            Assert.AreEqual(0, a.Match.Prediction.Stats.Overflows);
        }
    }
}
