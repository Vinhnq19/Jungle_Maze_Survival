using UnityEngine;

namespace JungleMaze.Presentation.UI
{
    /// <summary>
    /// Direction A menu art for the connection screen (P07): background, key art, logo and the three empty button
    /// faces. Pure data: <see cref="ConnectionMenuView"/> builds its cached IMGUI styles from it and never changes flow.
    /// </summary>
    [CreateAssetMenu(menuName = "Jungle Maze/UI Menu Art Catalog")]
    public sealed class UiMenuArtCatalog : ScriptableObject
    {
        [SerializeField] private Texture2D background;
        [SerializeField] private Texture2D keyArt;
        [SerializeField] private Texture2D logo;
        [SerializeField] private Texture2D buttonPrimary;
        [SerializeField] private Texture2D buttonSecondary;
        [SerializeField] private Texture2D buttonGhost;
        [Tooltip("IMGUI 9-slice border in texture pixels. The faces have a 7 px frame; the sprite border (32) is for uGUI.")]
        [SerializeField] private int imguiBorder = 10;
        [SerializeField] private Color text = new Color(0xED / 255f, 0xF2 / 255f, 0xEE / 255f);
        [SerializeField] private Color muted = new Color(0xA6 / 255f, 0xBB / 255f, 0xB7 / 255f);

        public Texture2D Background => background;
        public Texture2D KeyArt => keyArt;
        public Texture2D Logo => logo;
        public Texture2D ButtonPrimary => buttonPrimary;
        public Texture2D ButtonSecondary => buttonSecondary;
        public Texture2D ButtonGhost => buttonGhost;
        public int ImguiBorder => imguiBorder;
        public Color Text => text;
        public Color Muted => muted;

        /// <summary>Number of art slots that are assigned (6 when complete).</summary>
        public int AssignedCount =>
            (background != null ? 1 : 0) + (keyArt != null ? 1 : 0) + (logo != null ? 1 : 0) +
            (buttonPrimary != null ? 1 : 0) + (buttonSecondary != null ? 1 : 0) + (buttonGhost != null ? 1 : 0);
    }
}
