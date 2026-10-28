using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Ports;

namespace JungleMaze.App.Match
{
    public sealed class ZoneInterval
    {
        public int Index { get; set; }
        public double StartSecond { get; set; }
        public double EndSecond { get; set; }
        public float StartRadius { get; set; }
        public float EndRadius { get; set; }
    }

    public sealed class ZoneSchedule
    {
        public string Version { get; set; } = "1.0.0";
        public (float X, float Y) InitialCenter { get; set; } = (25f, 25f);
        public float InitialRadius { get; set; } = 25f;
        public double WarningDurationSeconds { get; set; } = 10.0;
        public float ShrinkSpeedCellsPerSecond { get; set; } = 1.0f;
        public int DamagePerSecond { get; set; } = 5;
        public List<ZoneInterval> Intervals { get; } = new List<ZoneInterval>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Version) || !float.IsFinite(InitialCenter.X) || !float.IsFinite(InitialCenter.Y) ||
                !float.IsFinite(InitialRadius) || InitialRadius <= 0 || !double.IsFinite(WarningDurationSeconds) ||
                WarningDurationSeconds < 0 || DamagePerSecond < 1 || DamagePerSecond > 255 || Intervals.Count < 1 || Intervals.Count > 16)
                throw new ArgumentException("Invalid initial zone, warning or damage configuration.");

            double previousEnd = 0;
            float previousRadius = InitialRadius;
            for (var i = 0; i < Intervals.Count; i++)
            {
                var p = Intervals[i];
                if (p.Index != i + 1 || !double.IsFinite(p.StartSecond) || !double.IsFinite(p.EndSecond) ||
                    !float.IsFinite(p.StartRadius) || !float.IsFinite(p.EndRadius) ||
                    p.StartSecond < previousEnd || p.EndSecond <= p.StartSecond || p.EndRadius < 0 ||
                    p.StartRadius <= p.EndRadius || Math.Abs(p.StartRadius - previousRadius) > .001f)
                    throw new ArgumentException("Zone intervals must be finite, ordered, contiguous in radius, and shrinking.");
                if (Math.Abs((p.StartRadius - p.EndRadius) / (p.EndSecond - p.StartSecond) - ShrinkSpeedCellsPerSecond) > .001)
                    throw new ArgumentException("Zone shrink duration must agree with configured speed.");
                previousEnd = p.EndSecond;
                previousRadius = p.EndRadius;
            }
            if (previousRadius != 0) throw new ArgumentException("Final safe zone must shrink to zero.");
        }

        public static ZoneSchedule Default()
        {
            var s = new ZoneSchedule();
            s.Intervals.Add(new ZoneInterval { Index = 1, StartSecond = 90.0, EndSecond = 97.0, StartRadius = 25.0f, EndRadius = 18.0f });
            s.Intervals.Add(new ZoneInterval { Index = 2, StartSecond = 182.0, EndSecond = 188.0, StartRadius = 18.0f, EndRadius = 12.0f });
            s.Intervals.Add(new ZoneInterval { Index = 3, StartSecond = 268.0, EndSecond = 273.0, StartRadius = 12.0f, EndRadius = 7.0f });
            s.Intervals.Add(new ZoneInterval { Index = 4, StartSecond = 343.0, EndSecond = 347.0, StartRadius = 7.0f, EndRadius = 3.0f });
            s.Intervals.Add(new ZoneInterval { Index = 5, StartSecond = 387.0, EndSecond = 390.0, StartRadius = 3.0f, EndRadius = 0.0f });
            s.Validate();
            return s;
        }
    }

    /// <summary>
    /// Pure deterministic simulation of the safe zone bo, identical to server's ZoneSimulation math.
    /// Drives zone radius, center movement, warning state, and out-of-zone damage checks.
    /// </summary>
    public sealed class ZoneSimulation
    {
        private readonly ZoneSchedule _schedule;
        private readonly (float X, float Y)[] _targetCenters;

        public (float X, float Y) CurrentCenter { get; private set; }
        public float CurrentRadius { get; private set; }
        public (float X, float Y) NextCenter { get; private set; }
        public float NextRadius { get; private set; }
        public bool InWarning { get; private set; }
        public byte PhaseIndex { get; private set; }
        public uint ShrinkStartTick { get; private set; }
        public uint ShrinkEndTick { get; private set; }

        public ZoneSchedule Schedule => _schedule;

        public ZoneSimulation(ZoneSchedule schedule, int seed = 42, float margin = 2.0f, RuntimeMapBounds? bounds = null)
        {
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _schedule.Validate();
            _targetCenters = new (float X, float Y)[_schedule.Intervals.Count];

            var maxX = bounds?.MaxX ?? 50f;
            var maxY = bounds?.MaxY ?? 50f;

            var currentCenter = _schedule.InitialCenter;
            var currentRadius = _schedule.InitialRadius;

            for (var i = 0; i < _schedule.Intervals.Count; i++)
            {
                var interval = _schedule.Intervals[i];
                var rOld = currentRadius;
                var rNew = interval.EndRadius;
                var maxOffset = Math.Max(0f, rOld - Math.Max(rNew, margin));

                var chosenCenter = currentCenter;
                if (maxOffset > 0f)
                {
                    var rand = new Random(seed ^ ((i + 1) * 7919));
                    for (var attempt = 0; attempt < 32; attempt++)
                    {
                        var angle = rand.NextDouble() * Math.PI * 2.0;
                        var dist = rand.NextDouble() * maxOffset;
                        var cx = (float)(currentCenter.X + dist * Math.Cos(angle));
                        var cy = (float)(currentCenter.Y + dist * Math.Sin(angle));

                        if (cx >= margin && cx <= maxX - margin &&
                            cy >= margin && cy <= maxY - margin)
                        {
                            var d = (float)Math.Sqrt(Math.Pow(cx - currentCenter.X, 2) + Math.Pow(cy - currentCenter.Y, 2));
                            if (d + rNew <= rOld + 1e-4f)
                            {
                                chosenCenter = (cx, cy);
                                break;
                            }
                        }
                    }
                }

                _targetCenters[i] = chosenCenter;
                currentCenter = chosenCenter;
                currentRadius = rNew;
            }

            CurrentCenter = _schedule.InitialCenter;
            CurrentRadius = _schedule.InitialRadius;
            NextCenter = _targetCenters.Length > 0 ? _targetCenters[0] : _schedule.InitialCenter;
            NextRadius = _targetCenters.Length > 0 ? _schedule.Intervals[0].EndRadius : _schedule.InitialRadius;
        }

        public void Update(double elapsedSeconds, uint currentTick, int tickRate = 30)
        {
            var intervals = _schedule.Intervals;
            if (intervals.Count == 0) return;

            // Before first interval
            if (elapsedSeconds < intervals[0].StartSecond)
            {
                CurrentCenter = _schedule.InitialCenter;
                CurrentRadius = _schedule.InitialRadius;
                NextCenter = _targetCenters[0];
                NextRadius = intervals[0].EndRadius;
                InWarning = elapsedSeconds >= (intervals[0].StartSecond - _schedule.WarningDurationSeconds);
                PhaseIndex = 0;
                ShrinkStartTick = (uint)(intervals[0].StartSecond * tickRate);
                ShrinkEndTick = (uint)(intervals[0].EndSecond * tickRate);
                return;
            }

            // After all intervals (r0 stage)
            var last = intervals[intervals.Count - 1];
            if (elapsedSeconds >= last.EndSecond)
            {
                CurrentCenter = _targetCenters[intervals.Count - 1];
                CurrentRadius = 0f;
                NextCenter = CurrentCenter;
                NextRadius = 0f;
                InWarning = false;
                PhaseIndex = (byte)intervals.Count;
                ShrinkStartTick = (uint)(last.StartSecond * tickRate);
                ShrinkEndTick = (uint)(last.EndSecond * tickRate);
                return;
            }

            // Find active interval or wait period between intervals
            for (var i = 0; i < intervals.Count; i++)
            {
                var interval = intervals[i];
                var oldCenter = i == 0 ? _schedule.InitialCenter : _targetCenters[i - 1];

                if (elapsedSeconds >= interval.StartSecond && elapsedSeconds < interval.EndSecond)
                {
                    // Shrinking
                    var progress = (float)((elapsedSeconds - interval.StartSecond) / (interval.EndSecond - interval.StartSecond));
                    if (progress < 0f) progress = 0f;
                    else if (progress > 1f) progress = 1f;

                    CurrentCenter = (
                        oldCenter.X + (_targetCenters[i].X - oldCenter.X) * progress,
                        oldCenter.Y + (_targetCenters[i].Y - oldCenter.Y) * progress
                    );
                    CurrentRadius = interval.StartRadius + (interval.EndRadius - interval.StartRadius) * progress;
                    NextCenter = _targetCenters[i];
                    NextRadius = interval.EndRadius;
                    InWarning = false;
                    PhaseIndex = (byte)(i + 1);
                    ShrinkStartTick = (uint)(interval.StartSecond * tickRate);
                    ShrinkEndTick = (uint)(interval.EndSecond * tickRate);
                    return;
                }

                if (i + 1 < intervals.Count)
                {
                    var nextInterval = intervals[i + 1];
                    if (elapsedSeconds >= interval.EndSecond && elapsedSeconds < nextInterval.StartSecond)
                    {
                        // Waiting between shrinks
                        CurrentCenter = _targetCenters[i];
                        CurrentRadius = interval.EndRadius;
                        NextCenter = _targetCenters[i + 1];
                        NextRadius = nextInterval.EndRadius;
                        InWarning = elapsedSeconds >= (nextInterval.StartSecond - _schedule.WarningDurationSeconds);
                        PhaseIndex = (byte)(i + 1);
                        ShrinkStartTick = (uint)(nextInterval.StartSecond * tickRate);
                        ShrinkEndTick = (uint)(nextInterval.EndSecond * tickRate);
                        return;
                    }
                }
            }
        }

        /// <summary>Checks if point (x, y) is outside the safe zone circle. At r=0, all points are outside.</summary>
        public bool IsOutside(float x, float y)
        {
            if (CurrentRadius <= 0.0001f) return true;
            var distSq = (x - CurrentCenter.X) * (x - CurrentCenter.X) + (y - CurrentCenter.Y) * (y - CurrentCenter.Y);
            return distSq > CurrentRadius * CurrentRadius;
        }

        public ZoneStatePacket GetStatePacket(uint tick) => new ZoneStatePacket
        {
            Tick = tick,
            PhaseIndex = PhaseIndex,
            InWarning = InWarning,
            CurrentCenterX = CurrentCenter.X,
            CurrentCenterY = CurrentCenter.Y,
            CurrentRadius = CurrentRadius,
            TargetCenterX = NextCenter.X,
            TargetCenterY = NextCenter.Y,
            TargetRadius = NextRadius,
            ShrinkStartTick = ShrinkStartTick,
            ShrinkEndTick = ShrinkEndTick
        };
    }
}
