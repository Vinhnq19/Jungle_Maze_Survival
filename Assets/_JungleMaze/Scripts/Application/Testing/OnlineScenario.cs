using System;
using System.Collections.Generic;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;

namespace JungleMaze.App.Testing
{
    /// <summary>Kịch bản có version, tách điều kiện nghiệm thu khỏi gameplay và transport.</summary>
    [Serializable]
    public sealed class OnlineScenario
    {
        public int schemaVersion = 1;
        public string id = "connection";
        public float maxDurationSeconds = 120;
        public float minConnectedSeconds = 5;
        public int expectedPlayers = 2;
        public bool requireMovement = true, requireZone = true, requireHpDamage;
        public bool requireResult, requireReplay, requireHistory;
        public float minInputHz = 28.5f;
        public float minSnapshotHz;
        public float maxPongTimeoutRatio = .01f;
        public string driver = "movement";
        public string[] extraAssertions = Array.Empty<string>();

        public string Validate()
        {
            if (schemaVersion != 1) return "Unsupported scenario schemaVersion";
            if (string.IsNullOrWhiteSpace(id) || id.Length > 80) return "Scenario id required (max80)";
            if (expectedPlayers < 1 || expectedPlayers > 10) return "expectedPlayers must be1..10";
            if (float.IsNaN(maxDurationSeconds) || float.IsInfinity(maxDurationSeconds) || maxDurationSeconds < 5 || maxDurationSeconds > 900)
                return "maxDurationSeconds must be5..900";
            if (float.IsNaN(minConnectedSeconds) || float.IsInfinity(minConnectedSeconds) || minConnectedSeconds < 0 || minConnectedSeconds >= maxDurationSeconds)
                return "minConnectedSeconds must be below deadline";
            if (float.IsNaN(minInputHz) || float.IsInfinity(minInputHz) || minInputHz < 0 || minInputHz > 30) return "minInputHz must be0..30";
            if (float.IsNaN(minSnapshotHz) || float.IsInfinity(minSnapshotHz) || minSnapshotHz < 0 || minSnapshotHz > 30) return "minSnapshotHz must be0..30";
            if (float.IsNaN(maxPongTimeoutRatio) || maxPongTimeoutRatio < 0 || maxPongTimeoutRatio > 1) return "maxPongTimeoutRatio must be0..1";
            if (requireReplay && !requireResult) return "Replay requires a result";
            if (string.IsNullOrWhiteSpace(driver)) return "Driver required";
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in extraAssertions ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(name) || !unique.Add(name)) return "Empty/duplicate extension assertion";
            return null;
        }
    }

    /// <summary>Read model cho test feature mới; không có quyền tự đánh dấu cả lượt là PASS.</summary>
    public readonly struct OnlineTestContext
    {
        public readonly MatchSession Match;
        public readonly INetworkClient Network;
        public readonly IPlayerHistory History;
        public readonly IPacketSender Sender;
        public readonly float ElapsedSeconds;
        public OnlineTestContext(MatchSession match, INetworkClient network, IPlayerHistory history, IPacketSender sender, float elapsed)
        { Match = match; Network = network; History = history; Sender = sender; ElapsedSeconds = elapsed; }
    }

    public readonly struct OnlineCheckResult
    {
        public readonly string Status, Detail;
        public OnlineCheckResult(string status, string detail = "")
        {
            if (status != "WAIT" && status != "PASS" && status != "FAIL") throw new ArgumentException("Check status must be WAIT/PASS/FAIL");
            Status = status; Detail = detail ?? "";
        }
    }
    public interface IOnlineTestDriver { void Apply(in OnlineTestContext context); }

    /// <summary>
    /// Dev combat/inventory đăng ký check/driver trong Bootstrap trước khi runner khởi tạo.
    /// Tên không có implementation phải FAIL cấu hình; không âm thầm bỏ qua test.
    /// </summary>
    public static class OnlineTestExtensions
    {
        private static readonly Dictionary<string, Func<OnlineTestContext, OnlineCheckResult>> Checks = new Dictionary<string, Func<OnlineTestContext, OnlineCheckResult>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Func<IOnlineTestDriver>> Drivers = new Dictionary<string, Func<IOnlineTestDriver>>(StringComparer.Ordinal);
        public static void RegisterCheck(string id, Func<OnlineTestContext, OnlineCheckResult> check)
        { if (string.IsNullOrWhiteSpace(id) || check == null || Checks.ContainsKey(id)) throw new ArgumentException("Duplicate/invalid assertion"); Checks.Add(id, check); }
        public static void RegisterDriver(string id, Func<IOnlineTestDriver> factory)
        { if (string.IsNullOrWhiteSpace(id) || id == "movement" || factory == null || Drivers.ContainsKey(id)) throw new ArgumentException("Duplicate/invalid driver"); Drivers.Add(id, factory); }
        public static bool TryGetCheck(string id, out Func<OnlineTestContext, OnlineCheckResult> check) => Checks.TryGetValue(id, out check);
        public static bool TryCreateDriver(string id, out IOnlineTestDriver driver)
        { driver = Drivers.TryGetValue(id, out var factory) ? factory() : null; return driver != null; }
        // Gọi khi khởi tạo một composition root mới (cũng đúng khi Editor tắt domain reload).
        public static void Reset() { Checks.Clear(); Drivers.Clear(); }
    }
}
