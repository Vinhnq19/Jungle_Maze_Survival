using System;
using JungleMaze.World;
using UnityEngine;
namespace JungleMaze.Presentation.World.Phase2
{
    [CreateAssetMenu(menuName="Jungle Maze/World visual set")]
    public sealed class WorldVisualSet : ScriptableObject
    {
        [Serializable] public struct Entry { public WorldObjectKind Kind; public Sprite Sprite; }
        public Entry[] Entries=Array.Empty<Entry>();
        public Sprite Find(byte kind){foreach(var e in Entries)if((byte)e.Kind==kind)return e.Sprite;return null;}
    }
}
