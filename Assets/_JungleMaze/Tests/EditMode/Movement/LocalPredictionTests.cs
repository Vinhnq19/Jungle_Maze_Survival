using JungleMaze.App.Player;
using JungleMaze.Domain.Player;
using NUnit.Framework;

namespace JungleMaze.Tests.Movement
{
    /// <summary>MOV-02: prediction tức thì, reconcile theo ack, làm mượt correction, không xuyên Hard Cover.</summary>
    public sealed class LocalPredictionTests
    {
        private const float Dt = MovementSimulation.TickSeconds;
        private const float Step = MovementTestMap.Speed * Dt;

        private static LocalPrediction Create(float x = 10f, float y = 10f, uint tick = 1)
        {
            var prediction = new LocalPrediction();
            prediction.SetWorld(MovementTestMap.Create());
            prediction.Reconcile(Server(tick, x, y, 0));
            return prediction;
        }

        private static PlayerSnapshot Server(uint tick, float x, float y, ushort ack, bool dead = false) =>
            new PlayerSnapshot { Tick = tick, X = x, Y = y, Hp = dead ? (byte)0 : (byte)100, IsDead = dead, LastProcessedInputSeq = ack };

        private static PlayerPose Pose(LocalPrediction prediction)
        {
            Assert.IsTrue(prediction.TryGetPose(out var pose));
            return pose;
        }

        [Test]
        public void WithoutWorldOrAuthority_DoesNotPredict()
        {
            var prediction = new LocalPrediction();
            prediction.RecordInput(MovementTestMap.Input(1, 1, 0));
            Assert.IsFalse(prediction.IsActive);
            Assert.IsFalse(prediction.TryGetPose(out _));
            prediction.SetWorld(MovementTestMap.Create());
            Assert.IsFalse(prediction.IsActive, "Chưa có snapshot gốc của server");
        }

        [Test]
        public void Input_MovesHeroImmediately_BeforeServerAck()
        {
            var prediction = Create();
            prediction.RecordInput(MovementTestMap.Input(1, 1, 0, aim: 90));
            Assert.AreEqual(10f + Step, prediction.PredictedX, 1e-4f);
            prediction.Advance(Dt);
            var pose = Pose(prediction);
            Assert.AreEqual(10f + Step, pose.X, 1e-4f);
            Assert.AreEqual(90f, pose.RotationDegrees, 0.01f, "Hướng ngắm cũng phản hồi ngay");
            Assert.AreEqual(1, prediction.Stats.Pending);
        }

        [Test]
        public void ServerAgrees_NoCorrections_AckedInputsDropped()
        {
            var prediction = Create();
            var server = MovementTestMap.Create();
            float sx = 10f, sy = 10f;
            for (ushort seq = 1; seq <= 60; seq++)
            {
                var input = MovementTestMap.Input(seq, seq < 30 ? 1f : 0.3f, seq % 7 == 0 ? -1f : 0.5f);
                prediction.RecordInput(input);
                prediction.Advance(Dt);
                // Server trễ 3 input so với client.
                if (seq > 3)
                {
                    var acked = MovementTestMap.Input((ushort)(seq - 3), (seq - 3) < 30 ? 1f : 0.3f, (seq - 3) % 7 == 0 ? -1f : 0.5f);
                    (sx, sy) = server.Move(sx, sy, MovementSimulation.Dequantize(acked.MoveX), MovementSimulation.Dequantize(acked.MoveY), Dt);
                    prediction.Reconcile(Server((uint)(seq + 1), sx, sy, acked.InputSeq));
                }
            }
            var stats = prediction.Stats;
            Assert.AreEqual(0, stats.Corrections);
            Assert.AreEqual(0, stats.Snaps);
            Assert.Less(stats.MaxErrorMeters, 1e-4f);
            Assert.AreEqual(3, stats.Pending);
        }

        [Test]
        public void ServerDisagrees_CorrectionIsSmoothed_ThenConverges()
        {
            var prediction = Create();
            for (ushort seq = 1; seq <= 5; seq++) { prediction.RecordInput(MovementTestMap.Input(seq, 1, 0)); prediction.Advance(Dt); }
            var before = Pose(prediction);
            // Server chỉ đi 3 input (ví dụ bị chặn) dù đã ack tới seq 5: lệch 2 bước.
            prediction.Reconcile(Server(10, 10f + 3 * Step, 10f, 5));
            var stats = prediction.Stats;
            Assert.AreEqual(1, stats.Corrections);
            Assert.AreEqual(2 * Step, stats.LastErrorMeters, 1e-3f);
            var after = Pose(prediction);
            Assert.AreEqual(before.X, after.X, 1e-4f, "Không giật hình ngay khi correction");
            for (var i = 0; i < 60; i++) prediction.Advance(1f / 60f);
            Assert.AreEqual(10f + 3 * Step, Pose(prediction).X, 1e-3f, "Offset làm mượt giảm về 0");
        }

        [Test]
        public void HugeError_SnapsInsteadOfSmoothing()
        {
            var prediction = Create();
            prediction.RecordInput(MovementTestMap.Input(1, 1, 0));
            prediction.Reconcile(Server(5, 40f, 40f, 1));
            Assert.AreEqual(1, prediction.Stats.Snaps);
            var pose = Pose(prediction);
            Assert.AreEqual(40f, pose.X, 1e-4f);
            Assert.AreEqual(40f, pose.Y, 1e-4f);
        }

        [Test]
        public void DuplicateAndOldSnapshots_AreIgnored()
        {
            var prediction = Create(tick: 10);
            prediction.RecordInput(MovementTestMap.Input(1, 1, 0));
            prediction.Reconcile(Server(10, 0f, 0f, 1));
            prediction.Reconcile(Server(9, 0f, 0f, 1));
            Assert.AreEqual(2, prediction.Stats.IgnoredSnapshots);
            Assert.AreEqual(10f + Step, prediction.PredictedX, 1e-4f);
        }

        [Test]
        public void ServerHasNotProcessedInput_KeepsAllPending()
        {
            var prediction = Create();
            for (ushort seq = 1; seq <= 4; seq++) prediction.RecordInput(MovementTestMap.Input(seq, 1, 0));
            prediction.Reconcile(Server(3, 10f, 10f, 0));
            Assert.AreEqual(4, prediction.Stats.Pending);
            Assert.AreEqual(10f + 4 * Step, prediction.PredictedX, 1e-3f);
            Assert.AreEqual(0, prediction.Stats.Corrections);
        }

        [Test]
        public void SequenceWrap_AcksAcrossOverflow()
        {
            var prediction = Create();
            foreach (var seq in new ushort[] { 65534, 65535, 1, 2 }) prediction.RecordInput(MovementTestMap.Input(seq, 1, 0));
            prediction.Reconcile(Server(3, 10f + 2 * Step, 10f, 65535));
            Assert.AreEqual(2, prediction.Stats.Pending);
            Assert.AreEqual(0, prediction.Stats.Corrections);
        }

        [Test]
        public void Dead_StopsPredicting_UntilServerSaysAlive()
        {
            var prediction = Create();
            prediction.Reconcile(Server(2, 10f, 10f, 0, dead: true));
            Assert.IsFalse(prediction.IsActive);
            prediction.RecordInput(MovementTestMap.Input(1, 1, 0));
            Assert.AreEqual(0, prediction.Stats.Predicted);
            Assert.IsFalse(prediction.TryGetPose(out _));
        }

        [Test]
        public void PushingIntoCover_WithCorrections_NeverDrawsInsideCover()
        {
            var world = MovementTestMap.Create();
            var prediction = Create(26f, 25f);
            for (ushort seq = 1; seq <= 60; seq++)
            {
                prediction.RecordInput(MovementTestMap.Input(seq, 1, 0));
                prediction.Advance(Dt);
                // Server giả lệch ra xa cover để buộc offset correction đẩy hình về phía cover.
                if (seq % 10 == 0) prediction.Reconcile(Server((uint)(seq + 1), 27.0f, 25f, seq));
                Assert.IsFalse(world.Overlaps(prediction.PredictedX, prediction.PredictedY), "Vị trí logic lấn cover");
                var pose = Pose(prediction);
                Assert.IsFalse(world.Overlaps(pose.X, pose.Y), $"Hình vẽ lấn cover tại seq {seq}: {pose.X}");
            }
        }

        [Test]
        public void AckRtt_IsMeasuredFromSendToAck()
        {
            var prediction = Create();
            prediction.RecordInput(MovementTestMap.Input(1, 0, 0));
            prediction.Advance(0.05f);
            prediction.Advance(0.05f);
            prediction.Reconcile(Server(5, 10f, 10f, 1));
            Assert.AreEqual(100f, prediction.Stats.LastAckRttMs, 0.5f);
        }

        [Test]
        public void BufferOverflow_DropsOldestAndCounts()
        {
            var prediction = Create();
            for (var i = 1; i <= LocalPrediction.Capacity + 5; i++) prediction.RecordInput(MovementTestMap.Input((ushort)i, 0, 0));
            Assert.AreEqual(5, prediction.Stats.Overflows);
            Assert.AreEqual(LocalPrediction.Capacity, prediction.Stats.Pending);
        }
    }
}
