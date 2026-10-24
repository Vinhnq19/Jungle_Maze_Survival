using System.Text.Json;
using JungleBackend.Protocol.Gameplay;

namespace GameServer.Simulation;

public sealed class ZoneInterval
{
    public int Index { get; set; }
    public double StartSecond { get; set; }
    public double EndSecond { get; set; }
    public float StartRadius { get; set; }
    public float EndRadius { get; set; }
    public (float X, float Y) TargetCenter { get; set; }
}

public sealed class ZoneSchedule
{
    public string Version { get; set; } = "1.0.0";
    public (float X, float Y) InitialCenter { get; set; } = (25f, 25f);
    public float InitialRadius { get; set; } = 25f;
    public double WarningDurationSeconds { get; set; } = 10.0;
    public float ShrinkSpeedCellsPerSecond { get; set; } = 1.0f;
    public int DamagePerSecond { get; set; } = 5;
    public List<ZoneInterval> Intervals { get; } = new();

    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Version)||!float.IsFinite(InitialCenter.X)||!float.IsFinite(InitialCenter.Y)||!float.IsFinite(InitialRadius)||InitialRadius<=0||!double.IsFinite(WarningDurationSeconds)||WarningDurationSeconds<0||DamagePerSecond<1||DamagePerSecond>255||Intervals.Count<1||Intervals.Count>16)
            throw new ArgumentException("Invalid initial zone, warning or damage configuration.");
        double previousEnd=0;float previousRadius=InitialRadius;
        for(var i=0;i<Intervals.Count;i++)
        {
            var p=Intervals[i];
            if(p.Index!=i+1||!double.IsFinite(p.StartSecond)||!double.IsFinite(p.EndSecond)||!float.IsFinite(p.StartRadius)||!float.IsFinite(p.EndRadius)||
                p.StartSecond<previousEnd||p.EndSecond<=p.StartSecond||p.EndRadius<0||p.StartRadius<=p.EndRadius||MathF.Abs(p.StartRadius-previousRadius)>.001f)
                throw new ArgumentException("Zone intervals must be finite, ordered, contiguous in radius, and shrinking.");
            if(Math.Abs((p.StartRadius-p.EndRadius)/(p.EndSecond-p.StartSecond)-ShrinkSpeedCellsPerSecond)>.001)
                throw new ArgumentException("Zone shrink duration must agree with configured speed.");
            previousEnd=p.EndSecond;previousRadius=p.EndRadius;
        }
        if(previousRadius!=0)throw new ArgumentException("Final safe zone must shrink to zero.");
    }

    public static ZoneSchedule Load(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var schedule = new ZoneSchedule { Version = root.GetProperty("version").GetString() ?? throw new ArgumentException("Missing zone version.") };
        if(root.TryGetProperty("shrinkSpeedCellsPerSecond",out var speed))schedule.ShrinkSpeedCellsPerSecond=speed.GetSingle();

        if (root.TryGetProperty("initialCenter", out var center))
        {
            schedule.InitialCenter = (
                (float)center.GetProperty("x").GetDouble(),
                (float)center.GetProperty("y").GetDouble()
            );
        }

        if (root.TryGetProperty("initialRadius", out var r))
            schedule.InitialRadius = (float)r.GetDouble();

        if (root.TryGetProperty("warningDurationSeconds", out var w))
            schedule.WarningDurationSeconds = w.GetDouble();

        if (root.TryGetProperty("damagePerSecond", out var d))
            schedule.DamagePerSecond = d.GetInt32();

        if (root.TryGetProperty("intervals", out var intervals))
        {
            foreach (var elem in intervals.EnumerateArray())
            {
                var interval = new ZoneInterval
                {
                    Index = elem.GetProperty("index").GetInt32(),
                    StartSecond = elem.GetProperty("startSecond").GetDouble(),
                    EndSecond = elem.GetProperty("endSecond").GetDouble(),
                    StartRadius = (float)elem.GetProperty("startRadius").GetDouble(),
                    EndRadius = (float)elem.GetProperty("endRadius").GetDouble()
                };
                schedule.Intervals.Add(interval);
            }
        }

        schedule.Validate();
        return schedule;
    }
}

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

    public ZoneSimulation(ZoneSchedule schedule, int seed, float margin = 2.0f, MapData? map = null)
    {
        _schedule = schedule;
        _schedule.Validate();
        _targetCenters = new (float X, float Y)[schedule.Intervals.Count];

        var reachable=map?.ReachableCells();
        bool Valid(float x,float y) => map is null || map.IsWalkable(x,y) && reachable!.Contains(((int)MathF.Floor(x),(int)MathF.Floor(y)));
        // Seed deterministic target centers ensuring distance(new, old) + rNew <= rOld
        var currentCenter = schedule.InitialCenter;
        var currentRadius = schedule.InitialRadius;

        for (var i = 0; i < schedule.Intervals.Count; i++)
        {
            var interval = schedule.Intervals[i];
            var rOld = currentRadius;
            var rNew = interval.EndRadius;
            var maxOffset = MathF.Max(0,rOld-MathF.Max(rNew,margin));

            var chosenCenter = currentCenter;
            if (maxOffset > 0f)
            {
                var rand = new Random(seed ^ ((i + 1) * 7919));
                var found = false;
                for (var attempt = 0; attempt < 32; attempt++)
                {
                    var angle = rand.NextDouble() * Math.PI * 2.0;
                    var dist = rand.NextDouble() * maxOffset;
                    var cx = (float)(currentCenter.X + dist * Math.Cos(angle));
                    var cy = (float)(currentCenter.Y + dist * Math.Sin(angle));

                    // Respect center margin (e.g. margin=2 in [0, 50])
                    if (cx >= margin && cx <= (map?.MaxX??50f) - margin &&
                        cy >= margin && cy <= (map?.MaxY??50f) - margin && Valid(cx,cy))
                    {
                        var d = MathF.Sqrt(MathF.Pow(cx - currentCenter.X, 2) + MathF.Pow(cy - currentCenter.Y, 2));
                        if (d + rNew <= rOld + 1e-4f)
                        {
                            chosenCenter = (cx, cy);
                            found = true;
                            break;
                        }
                    }
                }

                // If no valid random center found, fall back to currentCenter
                if (!found)
                {
                    chosenCenter = currentCenter;
                }
            }

            _targetCenters[i] = chosenCenter;
            if(!Valid(chosenCenter.X,chosenCenter.Y))throw new ArgumentException("No reachable center for zone; fix map/config before starting a match.");
            currentCenter = chosenCenter;
            currentRadius = rNew;
        }

        CurrentCenter = schedule.InitialCenter;
        CurrentRadius = schedule.InitialRadius;
        NextCenter = _targetCenters.Length > 0 ? _targetCenters[0] : schedule.InitialCenter;
        NextRadius = _targetCenters.Length > 0 ? schedule.Intervals[0].EndRadius : schedule.InitialRadius;
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

        // Check if past all intervals (r0 stage)
        var last = intervals[^1];
        if (elapsedSeconds >= last.EndSecond)
        {
            CurrentCenter = _targetCenters[^1];
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

    /// <summary>
    /// Checks if point (x, y) is outside the active zone.
    /// r0 means whole map damages INCLUDING exact center.
    /// </summary>
    public bool IsOutside(float x, float y)
    {
        if (CurrentRadius <= 0.0001f)
            return true; // r0 damages whole map including exact center

        var dist = MathF.Sqrt(MathF.Pow(x - CurrentCenter.X, 2) + MathF.Pow(y - CurrentCenter.Y, 2));
        return dist > CurrentRadius;
    }

    public ZoneStatePacket GetStatePacket(uint tick) => new()
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
