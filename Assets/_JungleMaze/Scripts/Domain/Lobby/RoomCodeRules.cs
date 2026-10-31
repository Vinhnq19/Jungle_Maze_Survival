namespace JungleMaze.Domain.Lobby
{
    /// <summary>Which part of a join field is wrong; the form shows one message per value (AC5).</summary>
    public enum JoinFieldError : byte { None, Empty, TooShort, TooLong, BadChar }

    /// <summary>
    /// Room code and display name rules, copied from <c>RoomLimits</c>/<c>NetworkProtocol.MaxNameChars</c> because
    /// Backend/Shared is not compiled into the client (T-3). <c>LobbyLimitsDriftTests</c> keeps the copies in sync.
    /// </summary>
    public static class RoomCodeRules
    {
        public const int Length = 6;
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>Trim and upper-case (invariant); null becomes "".</summary>
        public static string Normalize(string raw) => (raw ?? "").Trim().ToUpperInvariant();

        public static JoinFieldError Validate(string code)
        {
            code = Normalize(code);
            if (code.Length == 0) return JoinFieldError.Empty;
            foreach (var c in code)
                if (Alphabet.IndexOf(c) < 0) return JoinFieldError.BadChar;
            if (code.Length < Length) return JoinFieldError.TooShort;
            return code.Length > Length ? JoinFieldError.TooLong : JoinFieldError.None;
        }

        /// <summary>Display name after trimming: 1–32 UTF-16 chars.</summary>
        public static JoinFieldError ValidateName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return JoinFieldError.Empty;
            return name.Length > LobbyLimits.MaxNameChars ? JoinFieldError.TooLong : JoinFieldError.None;
        }
    }

    /// <summary>Limits from <c>RoomLimits</c>, <c>AuthDtos</c> and <c>CreateRoomRequest</c> (T-3).</summary>
    public static class LobbyLimits
    {
        public const int MaxNameChars = 32, PlayersPerRoom = 10, RoomsPerUser = 2, Rooms = 5;
        public const int UserMin = 3, UserMax = 32, PassMin = 8, PassMax = 128, RoomNameMax = 48;
    }
}
