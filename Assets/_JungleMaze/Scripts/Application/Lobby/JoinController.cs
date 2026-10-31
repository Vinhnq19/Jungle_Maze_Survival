using System;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Lobby
{
    /// <summary>
    /// Guest join form (AC5–AC10): checks the fields on the spot, sends one join per press, gives up after the Auto
    /// budget (20 s) and translates the reject reason. It never needs the API (AC6) and never retries by itself (AC7).
    /// </summary>
    public sealed class JoinController
    {
        public const string BadCodeText = "Mã chỉ gồm chữ A–Z (trừ I, O) và số 2–9";
        public const string CodeLengthText = "Mã phòng gồm 6 ký tự";
        public const string NameText = "Tên cần 1–32 ký tự";

        private readonly ILobbyCommands _commands;
        private readonly Func<double> _clock;
        private readonly LobbyOptions _options;
        private readonly Action<string> _rememberName;
        private double _startedAt;

        public JoinController(ILobbyCommands commands, Func<double> clock, LobbyOptions options = null,
            string displayName = "", string roomCode = "", Action<string> rememberName = null)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _options = options ?? LobbyOptions.Default;
            _rememberName = rememberName;
            SetName(displayName);
            SetCode(roomCode);
            Message = "";
        }

        public string DisplayName { get; private set; } = "";
        public string RoomCode { get; private set; } = "";
        public JoinFieldError NameError { get; private set; }
        public JoinFieldError CodeError { get; private set; }
        public string Message { get; private set; } = "";
        public bool Busy { get; private set; }
        public bool TimedOut { get; private set; }
        /// <summary>Set after a wrong-code reject: the view puts the caret in the code field (AC7).</summary>
        public bool FocusCode { get; private set; }
        public int Attempts { get; private set; }

        public bool CanSubmit => !Busy && NameError == JoinFieldError.None && CodeError == JoinFieldError.None;

        public string NameErrorText => NameError == JoinFieldError.None ? "" : NameText;

        public string CodeErrorText =>
            CodeError == JoinFieldError.BadChar ? BadCodeText :
            CodeError == JoinFieldError.TooShort || CodeError == JoinFieldError.TooLong ? CodeLengthText : "";

        public void SetName(string raw)
        {
            DisplayName = (raw ?? "").Trim();
            NameError = RoomCodeRules.ValidateName(DisplayName);
            ClearNotice();
        }

        /// <summary>Upper-cases as the player types (AC5); an empty field shows no error until submitted.</summary>
        public void SetCode(string raw)
        {
            RoomCode = RoomCodeRules.Normalize(raw);
            CodeError = RoomCodeRules.Validate(RoomCode);
            ClearNotice();
        }

        /// <summary>One connect per press; repeated presses while busy do nothing (AC9).</summary>
        public bool Submit()
        {
            if (!CanSubmit) return false;
            Busy = true;
            TimedOut = false;
            FocusCode = false;
            Message = "";
            _startedAt = _clock();
            Attempts++;
            _rememberName?.Invoke(DisplayName);
            _commands.Join(RoomCode, DisplayName);
            return true;
        }

        /// <summary>"Hủy" while connecting: disconnect and return to the form (AC9).</summary>
        public void Cancel()
        {
            if (!Busy) return;
            Busy = false;
            _commands.Cancel();
        }

        public void Joined()
        {
            Busy = false;
            TimedOut = false;
        }

        public void Rejected(DisconnectReason reason)
        {
            Busy = false;
            Message = JoinRejectText.For(reason);
            FocusCode = reason == DisconnectReason.InvalidRoomCode;
        }

        /// <summary>Gives up after the Auto budget; returns true on the frame it gives up (AC10).</summary>
        public bool Tick()
        {
            if (!Busy || _clock() - _startedAt < _options.ConnectBudgetSeconds) return false;
            Busy = false;
            TimedOut = true;
            Message = JoinRejectText.Unreachable;
            _commands.Cancel();
            return true;
        }

        private void ClearNotice()
        {
            if (Busy) return;
            Message = ""; // G-6: editing the form hides the old notice
            TimedOut = false;
        }
    }
}
