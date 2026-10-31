using System;

namespace JungleMaze.Domain.Lobby
{
    /// <summary>UI-02 screens. Private to UI-02 inside <c>AppScreen.MainMenu</c>; never an <c>AppScreen</c> value (G-2).</summary>
    public enum LobbyScreen : byte
    {
        MainMenu, JoinForm, Connecting, Login, RoomManager, History, Leaderboard, Settings,
        Lobby, Loading, LoadError, InGame, Reconnecting, Disconnected, Results
    }

    public enum ResultHeadline : byte { Victory, Ranked, Tie, Cancelled }

    /// <summary>One lobby row, keyed by NetId/session (two "Huy" stay two rows, AC11).</summary>
    public readonly struct LobbySlot
    {
        public readonly byte NetId;
        public readonly string SessionId;
        public readonly string Name;
        public readonly bool IsSelf, Ready, HasReady;

        public LobbySlot(byte netId, string sessionId, string name, bool isSelf, bool hasReady = false, bool ready = false)
        {
            NetId = netId;
            SessionId = sessionId ?? "";
            Name = name ?? "";
            IsSelf = isSelf;
            HasReady = hasReady;
            Ready = ready;
        }
    }

    /// <summary>One results row as the server sent it; the rank is never recomputed (AC30).</summary>
    public readonly struct ResultRow
    {
        public readonly byte NetId, Rank;
        public readonly int Kills;
        public readonly float SurvivalSeconds;
        public readonly string Name;
        public readonly bool IsSelf;

        public ResultRow(byte netId, byte rank, int kills, float survivalSeconds, string name, bool isSelf)
        {
            NetId = netId;
            Rank = rank;
            Kills = kills;
            SurvivalSeconds = survivalSeconds;
            Name = name ?? "";
            IsSelf = isSelf;
        }

        /// <summary>"mm:ss", whole seconds rounded down.</summary>
        public static string Clock(float seconds)
        {
            var s = Math.Max(0, (int)seconds);
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }
    }
}
