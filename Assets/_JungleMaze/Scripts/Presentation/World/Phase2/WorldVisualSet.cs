using System;
using JungleMaze.World;
using UnityEngine;
namespace JungleMaze.Presentation.World.Phase2
{
    [CreateAssetMenu(menuName="Jungle Maze/World visual set")]
    public sealed class WorldVisualSet : ScriptableObject
    {
        /// <summary>Sprite-only clip data; legacy animation events and gameplay components are never imported.</summary>
        [Serializable] public sealed class Frames
        {
            public Sprite[] Sprites = Array.Empty<Sprite>();
            public float[] Times = Array.Empty<float>();
            public float Duration;
            public bool Loop;

            public Sprite Sample(float elapsed)
            {
                if (Sprites == null || Sprites.Length == 0) return null;
                var time = Loop && Duration > 0 ? Mathf.Repeat(elapsed, Duration) : Mathf.Max(0, elapsed);
                var index = 0;
                if (Times != null)
                    for (var i = 1; i < Sprites.Length && i < Times.Length; i++)
                    { if (Times[i] > time) break; index = i; }
                return Sprites[index];
            }
        }

        /// <summary>Verified source visuals for one world kind. Missing art uses an explicit fallback.</summary>
        [Serializable] public struct Entry
        {
            public WorldObjectKind Kind;
            public Sprite Sprite, BrokenSprite;
            public Frames Idle, Open, Close;
            public string Source;
        }
        public Entry[] Entries=Array.Empty<Entry>();
        public Sprite Find(byte kind){foreach(var e in Entries)if((byte)e.Kind==kind)return e.Sprite;return null;}
        /// <summary>Returns the matching visual without allocating or loading a source prefab.</summary>
        public bool TryFind(byte kind, out Entry entry)
        {
            foreach (var candidate in Entries) if ((byte)candidate.Kind == kind) { entry = candidate; return true; }
            entry = default; return false;
        }
    }
}
