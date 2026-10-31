using System;
using System.Collections.Generic;

namespace JungleMaze.App.Lobby
{
    /// <summary>
    /// N9 switches plus the G-1…G-8 numbers. Designer copy lives on <c>LobbyUiSet</c>; grace comes from
    /// <c>MatchConfigData.DisconnectGraceSeconds</c> (T-9). Unapproved rules are only switched here (DoD 3).
    /// </summary>
    public sealed class LobbyOptions
    {
        public bool ReadyEnabled;          // Q1: off
        public bool StartEnabled;          // Q1: off; on shows a locked button (T-6)
        public bool ReconnectEnabled = true; // Q2
        public bool HistoryEnabled = true;
        public bool ShowKills;             // T-4: off until PlayerDied ships
        public float GraceSeconds = 15f;
        public float ConnectBudgetSeconds = 20f;   // AC10: 5 s UDP + 15 s WS
        public float LoadingStuckSeconds = 35f;    // G-5: 30 s + 5 s
        public string ControlsHint = "WASD di chuyển · Chuột chọn hướng"; // G-8
        public string[] Tips = { "Bo thu hẹp theo pha: để ý vòng tiếp theo trên bản đồ." };

        public static LobbyOptions Default => new LobbyOptions();
    }

    /// <summary>
    /// One send per (match, command) (AC9, AC15, AC22, AC32, AC42). A new match id opens the gate again; join and
    /// API writes use their own busy flag instead because they have no match yet.
    /// </summary>
    public sealed class OnceGate
    {
        private readonly HashSet<(Guid, string)> _passed = new HashSet<(Guid, string)>();

        public bool TryPass(Guid matchId, string command) => _passed.Add((matchId, command));

        public void Reset() => _passed.Clear();
    }
}
