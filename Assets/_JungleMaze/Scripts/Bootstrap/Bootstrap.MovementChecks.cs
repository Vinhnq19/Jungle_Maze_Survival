using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Player;
using JungleMaze.App.Testing;

namespace JungleMaze.Composition
{
    /// <summary>
    /// Check MOV-02/MOV-03 cho OnlineScenarioRunner (chỉ đăng ký khi chạy với -onlineTest).
    /// Scenario bật bằng extraAssertions: "mov02.prediction", "mov03.interpolation".
    /// Detail của check chứa dòng [Mov] để evidence JSON giữ RTT/correction/jitter của lượt chạy.
    /// Đã đủ mẫu thì giữ PASS (trận có thể kết thúc khi client kia rời), nhưng lỗi xuyên cover/ghost vẫn FAIL bất cứ lúc nào.
    /// </summary>
    public sealed partial class Bootstrap
    {
        private const int MovementMinSnapshots = 150; // ~5 giây ở 30Hz

        partial void ConfigureOnlineChecks()
        {
            var remote = _registry.Resolve<RemotePlayerInterpolator>();
            var predictionPassed = false;
            var interpolationPassed = false;
            OnlineTestExtensions.RegisterCheck("mov02.prediction", context =>
            {
                var match = context.Match;
                var prediction = match.Prediction;
                var stats = prediction.Stats;
                var line = MovementTelemetry.Format(context.Network.Stats.Transport, context.Network.Stats.P50RttMs,
                    context.Network.Stats.P95RttMs, stats, remote.Stats);
                if (prediction.IsActive && prediction.World.Overlaps(prediction.PredictedX, prediction.PredictedY))
                    return new OnlineCheckResult("FAIL", "Predicted Hero overlaps Hard Cover/map edge. " + line);
                if (stats.Overflows > 0) return new OnlineCheckResult("FAIL", "Input buffer overflow: server stopped acking. " + line);
                if (stats.Snaps > 0 || stats.MaxErrorMeters > 0.5f || (stats.Reconciles >= MovementMinSnapshots && stats.AverageErrorMeters > 0.05f))
                    return new OnlineCheckResult("FAIL", "Prediction drifted from server. " + line);
                predictionPassed |= match.Phase == MatchPhase.InGame && stats.Reconciles >= MovementMinSnapshots;
                return new OnlineCheckResult(predictionPassed ? "PASS" : "WAIT", line);
            });
            OnlineTestExtensions.RegisterCheck("mov03.interpolation", context =>
            {
                var match = context.Match;
                var stats = remote.Stats;
                var line = MovementTelemetry.Format(context.Network.Stats.Transport, context.Network.Stats.P50RttMs,
                    context.Network.Stats.P95RttMs, match.Prediction.Stats, stats);
                // Ghost: còn buffer cho NetId mà server đã despawn.
                for (byte id = 0; id < RemotePlayerInterpolator.MaxPlayers; id++)
                    if (remote.BufferedCount(id) > 0 && !match.Players.TryGet(id, out _))
                        return new OnlineCheckResult("FAIL", "Ghost entity NetId " + id + ". " + line);
                if (stats.Samples >= MovementMinSnapshots && stats.ClampedSamples > stats.Samples / 20)
                    return new OnlineCheckResult("FAIL", "Too many frames past extrapolation limit (snapshots starving). " + line);
                var otherVisible = false;
                foreach (var id in match.Players.ActiveIds)
                    if (id != match.OwnNetId && remote.ClockStarted && remote.BufferedCount(id) > 0) { otherVisible = true; break; }
                interpolationPassed |= match.Phase == MatchPhase.InGame && otherVisible && stats.Samples >= MovementMinSnapshots;
                return new OnlineCheckResult(interpolationPassed ? "PASS" : "WAIT", line);
            });
        }
    }
}
