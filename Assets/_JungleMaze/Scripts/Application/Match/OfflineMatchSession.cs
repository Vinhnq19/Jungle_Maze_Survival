using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Flow;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;



namespace JungleMaze.App.Match
{
    /// <summary>
    /// Lightweight offline match engine running a solo Battle Royale round against the shrinking bo.
    /// Operates completely locally without network sockets, HTTP, backend server, Docker, or bots.
    /// Drives zone shrinkage according to canonical zone_schedule.json and feeds existing HUD and UI ports.
    /// </summary>
    public sealed class OfflineMatchSession : ITickable, IDisposable
    {
        private readonly IBalanceConfigProvider _balance;
        private readonly IConfigProvider _config;
        private readonly IClientRuntimeData _data;
        private readonly IHudStateWriter _hudWriter;
        private readonly IHudState _hudState;
        private readonly IEventBus _bus;
        private readonly GameFlow _flow;

        private float _tickFraction;
        private uint _lastDamageTick;

        private float? _overrideX;
        private float? _overrideY;

        public OfflineMatchSession(IBalanceConfigProvider balance, IConfigProvider config, IClientRuntimeData data,
            IHudStateWriter hudWriter, IEventBus bus, GameFlow flow, IHudState hudState = null)
        {
            _balance = balance;
            _config = config;
            _data = data;
            _hudWriter = hudWriter ?? throw new ArgumentNullException(nameof(hudWriter));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _hudState = hudState;
        }

        public bool IsActive { get; private set; }
        public Guid MatchId { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public uint CurrentTick { get; private set; }
        public byte PlayerHp { get; private set; } = 100;
        public bool IsDead { get; private set; }
        public bool HasResult { get; private set; }
        public float SurvivalSeconds { get; private set; }
        public string DisplayName { get; private set; } = "Player";
        public ZoneSimulation Zone { get; private set; }
        public MatchResultPacket Result { get; private set; }

        public void StartRound()
        {
            var error = _data?.LoadLocal();
            if (error != null) throw new InvalidOperationException(error);
            _overrideX = null; _overrideY = null;
            _hudWriter.Reset();
            IsActive = true;
            MatchId = Guid.NewGuid();
            ElapsedSeconds = 0f;
            CurrentTick = 0;
            _tickFraction = 0f;
            _lastDamageTick = 0;
            PlayerHp = (byte)Math.Min(255, Math.Max(1, _hudState?.MaxHp ?? 100));
            IsDead = false;
            HasResult = false;
            SurvivalSeconds = 0f;
            DisplayName = _config?.CommandLineDisplayName ?? "Player";

            var schedule = _data?.ZoneSchedule ?? ZoneSchedule.Default();
            var bounds = _data?.Bounds;
            Zone = new ZoneSimulation(schedule, seed: MatchId.GetHashCode(), margin: 2.0f, bounds: bounds);

            _hudWriter.SetHp(PlayerHp);
            _hudWriter.SetAliveCount(1, 1);
            UpdateZoneCountdown();


        }

        public void SetPlayerPosition(float x, float y)
        {
            _overrideX = x;
            _overrideY = y;
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive || IsDead || !(deltaTime > 0) || float.IsInfinity(deltaTime)) return;

            ElapsedSeconds += deltaTime;
            _tickFraction += deltaTime * 30f;
            var ticks = (uint)_tickFraction;
            CurrentTick += ticks;
            _tickFraction -= ticks;

            Zone.Update(ElapsedSeconds, CurrentTick, 30);
            UpdateZoneCountdown();

            var (px, py) = GetPlayerPosition();
            if (Zone.IsOutside(px, py))
            {
                if (_lastDamageTick == 0 || unchecked((int)(CurrentTick - _lastDamageTick)) >= 30)
                {
                    _lastDamageTick = CurrentTick;
                    ApplyDamage((byte)Zone.Schedule.DamagePerSecond);
                }
            }
        }

        public void ApplyDamage(byte damage)
        {
            if (damage == 0 || IsDead) return;
            PlayerHp = (byte)Math.Max(0, PlayerHp - damage);
            _hudWriter.SetHp(PlayerHp);
            _hudWriter.NotifyDamageTaken(false, 0f);

            if (PlayerHp == 0)
            {
                IsDead = true;
                HasResult = true;
                SurvivalSeconds = ElapsedSeconds;
                _hudWriter.SetAliveCount(0, 1);

                Result = new MatchResultPacket
                {
                    MatchId = MatchId,
                    Reason = MatchEndReason.Cancelled,
                    WinnerNetId = 255,
                    ParticipantCount = 1,
                    Participants = new[]
                    {
                        new MatchParticipantState
                        {
                            NetId = 0,
                            DisplayName = DisplayName,
                            Rank = 1,
                            SurvivalTicks = CurrentTick,
                            Kills = 0
                        }
                    }
                };

                _flow.Go(_flow.Results);
            }
        }

        public void Replay()
        {
            if (!IsActive) return;
            StartRound();
            _flow.Go(_flow.InMatch);
        }

        public void Leave()
        {
            IsActive = false;
            HasResult = false;
            IsDead = false;
            MatchId = Guid.Empty;
            _overrideX = null;
            _overrideY = null;
            _hudWriter.Reset();

        }

        public void Dispose() => Leave();

        private void UpdateZoneCountdown()
        {
            if (Zone == null) return;
            float secondsRemaining;
            if (unchecked((int)(Zone.ShrinkStartTick - CurrentTick)) > 0)
                secondsRemaining = unchecked((int)(Zone.ShrinkStartTick - CurrentTick)) / 30f;
            else if (unchecked((int)(Zone.ShrinkEndTick - CurrentTick)) > 0)
                secondsRemaining = unchecked((int)(Zone.ShrinkEndTick - CurrentTick)) / 30f;
            else
                secondsRemaining = 0f;

            _hudWriter.SetZoneCountdown(Math.Max(0f, secondsRemaining));
        }

        private (float X, float Y) GetPlayerPosition()
        {
            if (_overrideX.HasValue && _overrideY.HasValue)
                return (_overrideX.Value, _overrideY.Value);

            if (_hudState != null && _hudState.HasPose)
                return (_hudState.Pose.X, _hudState.Pose.Y);

            return (25f, 25f);
        }
    }
}
