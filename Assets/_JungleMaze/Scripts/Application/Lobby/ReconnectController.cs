using System;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Lobby
{
    public enum ReconnectState : byte { Idle, Trying, Succeeded, Failed }

    /// <summary>
    /// Reconnect overlay (AC38, AC39): asks the connection to resume once, shows the grace left (counted from when the
    /// client saw the loss, G-11), and fails on timeout or on a reject after the attempt. How resume is attempted is
    /// MOV-02's; this only sends the request and reads the state.
    /// </summary>
    public sealed class ReconnectController
    {
        public const string FailedText = "Không kết nối lại được: bạn đã bị loại khỏi trận";
        public const string LostText = "Mất kết nối";

        private readonly ILobbyCommands _commands;
        private readonly Func<double> _clock;
        private readonly LobbyOptions _options;
        private double _startedAt;
        private bool _sawAttempt;

        public ReconnectController(ILobbyCommands commands, Func<double> clock, LobbyOptions options)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _options = options ?? LobbyOptions.Default;
        }

        public ReconnectState State { get; private set; }
        public int ResumeRequests { get; private set; }

        /// <summary>Whole seconds left, rounded up ("khoảng", G-11).</summary>
        public int SecondsLeft => State != ReconnectState.Trying ? 0
            : Math.Max(0, (int)Math.Ceiling(_options.GraceSeconds - (_clock() - _startedAt)));

        /// <summary>False when reconnect is off or the server gave no resume proof: show "Mất kết nối" at once.</summary>
        public bool Begin(bool canResume)
        {
            if (!_options.ReconnectEnabled || !canResume) { State = ReconnectState.Failed; return false; }
            State = ReconnectState.Trying;
            _startedAt = _clock();
            _sawAttempt = false;
            ResumeRequests++;
            _commands.Resume();
            return true;
        }

        public ReconnectState Tick(NetworkState network)
        {
            if (State != ReconnectState.Trying) return State;
            if (network == NetworkState.Joined) State = ReconnectState.Succeeded;
            else if (network == NetworkState.Connecting || network == NetworkState.Authenticating) _sawAttempt = true;
            else if (_sawAttempt) State = ReconnectState.Failed; // rejected after trying
            if (State == ReconnectState.Trying && _clock() - _startedAt >= _options.GraceSeconds)
            {
                State = ReconnectState.Failed;
                _commands.Cancel();
            }
            return State;
        }

        public void Reset() => State = ReconnectState.Idle;
    }
}
