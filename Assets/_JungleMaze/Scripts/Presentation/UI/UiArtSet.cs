using JungleMaze.Presentation.UI.Hud;
using UnityEngine;

namespace JungleMaze.Presentation.UI
{
    /// <summary>
    /// The one UI art entry point Bootstrap wires: item icon catalog plus menu art. Lives at
    /// <c>Assets/_JungleMaze/Resources/UiArtSet.asset</c> so a Bootstrap created by <c>BootstrapAutoCreate</c>
    /// (Play pressed in Game or HudPreview) can load it once at composition; Boot references it directly.
    /// </summary>
    [CreateAssetMenu(menuName = "Jungle Maze/UI Art Set")]
    public sealed class UiArtSet : ScriptableObject
    {
        /// <summary>Resources path (no extension) used by the one-time fallback load in Bootstrap.</summary>
        public const string ResourcePath = "UiArtSet";

        [SerializeField] private HudItemIconCatalog itemIcons;
        [SerializeField] private UiMenuArtCatalog menuArt;

        public HudItemIconCatalog ItemIcons => itemIcons;
        public UiMenuArtCatalog MenuArt => menuArt;
    }
}
