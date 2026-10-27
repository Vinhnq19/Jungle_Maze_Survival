using System;
using System.Collections.Generic;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>
    /// Art-owned item ID → icon sprite table (ART-04 / ADHOC-UI-ASSETS-A). Implements <see cref="IItemIconProvider"/>:
    /// the lookup dictionary (ordinal, case-sensitive IDs as in the balance JSON) is built once when the asset is
    /// enabled or edited, so <see cref="GetIcon"/> never allocates. Null, empty and unknown IDs return null and the
    /// view keeps its placeholder.
    /// </summary>
    [CreateAssetMenu(menuName = "Jungle Maze/HUD Item Icon Catalog")]
    public sealed class HudItemIconCatalog : ScriptableObject, IItemIconProvider
    {
        /// <summary>One item ID and its 64×64 icon sprite.</summary>
        [Serializable]
        public struct Entry
        {
            public string Id;
            public Sprite Icon;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<string, Sprite> _lookup;

        public Entry[] Entries => entries;
        public int Count => entries != null ? entries.Length : 0;

        /// <summary>Returns the icon for <paramref name="itemId"/>, or null when the ID is null, empty or unknown.</summary>
        public Sprite GetIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_lookup == null) Rebuild();
            return _lookup.TryGetValue(itemId, out var sprite) ? sprite : null;
        }

        /// <summary>Replaces the table (editor builder and tests) and rebuilds the lookup.</summary>
        public void SetEntries(Entry[] value)
        {
            entries = value ?? Array.Empty<Entry>();
            Rebuild();
        }

        /// <summary>
        /// Appends every problem (empty ID, duplicate ID, missing sprite) to <paramref name="problems"/> and returns
        /// true when there is none. Used by the editor validator and tests; runtime lookup never throws.
        /// </summary>
        public bool Validate(List<string> problems)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var before = problems.Count;
            for (var i = 0; i < Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrEmpty(entry.Id)) { problems.Add($"entry {i}: empty ID"); continue; }
                if (!seen.Add(entry.Id)) problems.Add($"entry {i}: duplicate ID {entry.Id}");
                if (entry.Icon == null) problems.Add($"entry {i}: {entry.Id} has no sprite");
            }
            return problems.Count == before;
        }

        private void OnEnable() => Rebuild();

        private void OnValidate() => Rebuild();

        private void Rebuild()
        {
            var lookup = new Dictionary<string, Sprite>(Count, StringComparer.Ordinal);
            for (var i = 0; i < Count; i++)
            {
                var entry = entries[i];
                // First entry wins on duplicates; the validator reports them.
                if (!string.IsNullOrEmpty(entry.Id) && entry.Icon != null && !lookup.ContainsKey(entry.Id))
                    lookup.Add(entry.Id, entry.Icon);
            }
            _lookup = lookup;
        }
    }
}
