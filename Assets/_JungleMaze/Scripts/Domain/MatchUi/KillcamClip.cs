using System;

namespace JungleMaze.Domain.MatchUi
{
    /// <summary>Why the killcam was skipped (AC35). <see cref="None"/> = playable.</summary>
    public enum KillcamUnavailableReason : byte
    {
        None = 0,
        Disabled = 1,
        UnknownKiller = 2,
        Timeout = 3,
        NoData = 4,
        WrongMatch = 5,
        WrongPlayer = 6,
        TooShort = 7,
        MatchEnded = 8
    }

    /// <summary>One player in one 30 Hz replay frame: position, HP and state flags only (Q16, no shots or damage).</summary>
    public struct KillcamSample
    {
        public byte NetId;
        public byte Hp;
        public byte Flags;
        public bool Present;
        public float X;
        public float Y;
    }

    /// <summary>
    /// Replay window for the killcam (N9), allocated once and reused (G-14). Frame f, player slot s lives at
    /// <c>Samples[f * MaxPlayers + s]</c>; the slot is the NetId (always &lt; 10). <see cref="Clear"/> drops the data
    /// but keeps the arrays, so nothing outside the 5 s window is kept after a match (AC41, AC42).
    /// </summary>
    public sealed class KillcamClip
    {
        public const int MaxPlayers = 10;
        public const int FramesPerSecond = 30;

        public readonly KillcamSample[] Samples;
        public readonly float[] ZoneX;
        public readonly float[] ZoneY;
        public readonly float[] ZoneRadius;

        public Guid MatchId;
        public byte VictimNetId = EliminationInfo.NoNetId;
        public byte KillerNetId = EliminationInfo.NoNetId;
        public uint EndTick;
        public int FrameCount;

        public KillcamClip(int frameCapacity)
        {
            if (frameCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(frameCapacity));
            FrameCapacity = frameCapacity;
            Samples = new KillcamSample[frameCapacity * MaxPlayers];
            ZoneX = new float[frameCapacity];
            ZoneY = new float[frameCapacity];
            ZoneRadius = new float[frameCapacity];
        }

        public int FrameCapacity { get; }

        public float DurationSeconds => FrameCount / (float)FramesPerSecond;

        public ref KillcamSample At(int frame, int slot) => ref Samples[frame * MaxPlayers + slot];

        /// <summary>Writes one player into one frame; ignores out-of-range input instead of throwing (mock data).</summary>
        public void Set(int frame, byte netId, float x, float y, byte hp, byte flags = 0)
        {
            if (frame < 0 || frame >= FrameCapacity || netId >= MaxPlayers) return;
            ref var s = ref At(frame, netId);
            s.NetId = netId;
            s.X = x;
            s.Y = y;
            s.Hp = hp;
            s.Flags = flags;
            s.Present = true;
        }

        public void Clear()
        {
            Array.Clear(Samples, 0, Samples.Length);
            Array.Clear(ZoneX, 0, ZoneX.Length);
            Array.Clear(ZoneY, 0, ZoneY.Length);
            Array.Clear(ZoneRadius, 0, ZoneRadius.Length);
            MatchId = Guid.Empty;
            VictimNetId = EliminationInfo.NoNetId;
            KillerNetId = EliminationInfo.NoNetId;
            EndTick = 0;
            FrameCount = 0;
        }

        /// <summary>
        /// Position of <paramref name="netId"/> at fractional frame <paramref name="frame"/>, linearly between the two
        /// neighbouring frames. Missing on one side uses the other; missing on both returns false.
        /// </summary>
        public bool TryGetPosition(byte netId, float frame, out float x, out float y)
        {
            x = y = 0f;
            if (netId >= MaxPlayers || FrameCount <= 0 || float.IsNaN(frame)) return false;
            if (frame < 0f) frame = 0f;
            var last = FrameCount - 1;
            if (frame > last) frame = last;
            var a = (int)frame;
            var b = a < last ? a + 1 : a;
            var t = frame - a;
            ref var sa = ref At(a, netId);
            ref var sb = ref At(b, netId);
            if (!sa.Present && !sb.Present) return false;
            if (!sa.Present) { x = sb.X; y = sb.Y; return true; }
            if (!sb.Present) { x = sa.X; y = sa.Y; return true; }
            x = sa.X + (sb.X - sa.X) * t;
            y = sa.Y + (sb.Y - sa.Y) * t;
            return true;
        }
    }
}
