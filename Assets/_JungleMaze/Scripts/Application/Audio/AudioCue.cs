using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio
{
    /// <summary>
    /// One request to play a sound, as raised by an <see cref="IAudioEventSource"/>.
    /// <see cref="DedupeKey"/> identifies a server event so repeats of the same event play once (0 = no key).
    /// </summary>
    public readonly struct AudioCue
    {
        public readonly SfxId Id;
        public readonly bool HasPosition;
        public readonly float X, Y;
        /// <summary>Who made the sound (net id, danger id...); cooldown is counted per source. 0 = global.</summary>
        public readonly int SourceKey;
        public readonly ulong DedupeKey;

        public AudioCue(SfxId id, bool hasPosition, float x, float y, int sourceKey, ulong dedupeKey)
        {
            Id = id;
            HasPosition = hasPosition;
            X = x;
            Y = y;
            SourceKey = sourceKey;
            DedupeKey = dedupeKey;
        }

        public static AudioCue Global(SfxId id, int sourceKey = 0, ulong dedupeKey = 0) =>
            new AudioCue(id, false, 0f, 0f, sourceKey, dedupeKey);

        public static AudioCue At(SfxId id, float x, float y, int sourceKey = 0, ulong dedupeKey = 0) =>
            new AudioCue(id, true, x, y, sourceKey, dedupeKey);
    }
}
