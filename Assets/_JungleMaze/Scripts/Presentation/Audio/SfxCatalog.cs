using System;
using System.Collections.Generic;
using JungleMaze.Domain.Audio;
using UnityEngine;

namespace JungleMaze.Presentation.Audio
{
    /// <summary>
    /// Data of the 26 SFX: clips plus playback rule per <see cref="SfxId"/> (spec table 2.1). Swapping or adding
    /// clip variants, or retuning numbers, is an asset edit only (ART-05); no code reads clip names.
    /// </summary>
    [CreateAssetMenu(menuName = "Jungle Maze/Audio/SFX Catalog")]
    public sealed class SfxCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SfxId id;
            public AudioClip[] clips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float volume = 0.8f;
            [Range(0f, 0.25f)] public float pitchJitter = 0.05f;
            public SfxPriority priority = SfxPriority.Normal;
            public AudioBus bus = AudioBus.Sfx;
            [Tooltip("Hearing radius in cells from the Hero; 0 = 2D.")]
            [Min(0f)] public float rangeCells;
            [Min(1)] public int maxInstances = 1;
            [Min(0f)] public float cooldownSeconds = 0.1f;
            [Min(1)] public int repeatCount = 1;
            [Min(0f)] public float repeatIntervalSeconds;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Entry[] _byId;

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>Rule table for the Application layer. Ids without an entry or without clips are reported missing there.</summary>
        public SfxRuleTable BuildRules()
        {
            var rules = new SfxRule[SfxIds.TableSize];
            for (var i = 1; i < SfxIds.TableSize; i++)
            {
                var e = Find((SfxId)i);
                if (e == null) continue;
                rules[i] = new SfxRule(e.bus, e.priority, e.rangeCells, e.maxInstances, e.cooldownSeconds,
                    CountClips(e), e.volume, e.pitchJitter, e.repeatCount, e.repeatIntervalSeconds);
            }
            return new SfxRuleTable(rules);
        }

        /// <summary>Clip variant <paramref name="index"/> of <paramref name="id"/> (null entries are skipped).</summary>
        public AudioClip GetClip(SfxId id, int index)
        {
            var e = Find(id);
            if (e?.clips == null) return null;
            var k = 0;
            foreach (var clip in e.clips)
            {
                if (clip == null) continue;
                if (k++ == index) return clip;
            }
            return null;
        }

        public Entry Find(SfxId id)
        {
            if (_byId == null) BuildIndex();
            return SfxIds.IsValid(id) ? _byId[(int)id] : null;
        }

        private static int CountClips(Entry e)
        {
            var n = 0;
            if (e.clips != null) foreach (var clip in e.clips) if (clip != null) n++;
            return n;
        }

        private void BuildIndex()
        {
            _byId = new Entry[SfxIds.TableSize];
            foreach (var e in entries)
                if (e != null && SfxIds.IsValid(e.id) && _byId[(int)e.id] == null) _byId[(int)e.id] = e;
        }

        private void OnEnable() => _byId = null;

        private void OnValidate()
        {
            _byId = null;
            var missing = new List<SfxId>();
            BuildRules().CollectMissing(missing);
            if (missing.Count > 0) Debug.LogWarning($"[Audio] {name}: no clip for {string.Join(", ", missing)}.", this);
        }
    }
}
