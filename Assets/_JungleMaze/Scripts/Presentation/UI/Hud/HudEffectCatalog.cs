using System;
using JungleMaze.Domain.Hud;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>Art-owned effect labels and optional icon sprites.</summary>
    [CreateAssetMenu(menuName="Jungle Maze/HUD Effect Catalog")]
    public sealed class HudEffectCatalog:ScriptableObject
    {
        [Serializable] public struct Entry {public EffectKind Kind;public string Label;public Sprite Icon;}
        [SerializeField] private Entry[] entries=Array.Empty<Entry>();
        private readonly string[] _fallback=new string[256];
        public Entry[] Entries=>entries;
        public string Label(EffectKind kind)
        {
            for(var i=0;i<entries.Length;i++)if(entries[i].Kind==kind&&!string.IsNullOrEmpty(entries[i].Label))return entries[i].Label;
            var k=(int)kind;return _fallback[k]??(_fallback[k]=kind.ToString());
        }
        public Sprite Icon(EffectKind kind){for(var i=0;i<entries.Length;i++)if(entries[i].Kind==kind)return entries[i].Icon;return null;}
    }
}
