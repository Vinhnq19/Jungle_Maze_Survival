using System;
using System.Collections.Generic;

namespace JungleMaze.Domain.Audio
{
    /// <summary>Playback rule of one <see cref="SfxId"/>, built from the SFX catalog asset. A rule without clips counts as missing.</summary>
    public readonly struct SfxRule
    {
        public readonly AudioBus Bus;
        public readonly SfxPriority Priority;
        /// <summary>Hearing radius in cells from the Hero; 0 = 2D (local player or global, no distance falloff).</summary>
        public readonly float RangeCells;
        /// <summary>Simultaneous voices of this id across the whole pool.</summary>
        public readonly int MaxInstances;
        /// <summary>Minimum time between two plays of this id from the same source (global when there is no source).</summary>
        public readonly float CooldownSeconds;
        public readonly int ClipCount;
        public readonly float Volume;
        /// <summary>Random pitch spread, e.g. 0.05 = ±5%.</summary>
        public readonly float PitchJitter;
        /// <summary>Times one request plays (zone bell = 3); 1 = once.</summary>
        public readonly int RepeatCount;
        public readonly float RepeatInterval;

        public SfxRule(AudioBus bus, SfxPriority priority, float rangeCells, int maxInstances, float cooldownSeconds,
            int clipCount, float volume, float pitchJitter, int repeatCount, float repeatInterval)
        {
            Bus = bus;
            Priority = priority;
            RangeCells = Math.Max(0f, rangeCells);
            MaxInstances = Math.Max(1, maxInstances);
            CooldownSeconds = Math.Max(0f, cooldownSeconds);
            ClipCount = Math.Max(0, clipCount);
            Volume = Math.Max(0f, volume);
            PitchJitter = Math.Max(0f, pitchJitter);
            RepeatCount = Math.Max(1, repeatCount);
            RepeatInterval = Math.Max(0f, repeatInterval);
        }

        public bool Is2D => RangeCells <= 0f;
        public bool HasClips => ClipCount > 0;
    }

    /// <summary>Rules indexed by <c>(int)SfxId</c>. Lookups never allocate; ids without clips are reported as missing.</summary>
    public sealed class SfxRuleTable
    {
        private readonly SfxRule[] _rules = new SfxRule[SfxIds.TableSize];

        /// <param name="byId">Array indexed by <c>(int)SfxId</c>; shorter arrays leave the remaining ids missing.</param>
        public SfxRuleTable(SfxRule[] byId)
        {
            if (byId == null) throw new ArgumentNullException(nameof(byId));
            Array.Copy(byId, _rules, Math.Min(byId.Length, _rules.Length));
            _rules[0] = default;
        }

        public bool TryGet(SfxId id, out SfxRule rule)
        {
            if (SfxIds.IsValid(id) && _rules[(int)id].HasClips) { rule = _rules[(int)id]; return true; }
            rule = default;
            return false;
        }

        /// <summary>Appends every id without a usable rule to <paramref name="missing"/>; returns how many.</summary>
        public int CollectMissing(List<SfxId> missing)
        {
            var count = 0;
            for (var i = 1; i < SfxIds.TableSize; i++)
            {
                if (_rules[i].HasClips) continue;
                missing?.Add((SfxId)i);
                count++;
            }
            return count;
        }
    }
}
