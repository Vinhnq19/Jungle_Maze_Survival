using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>One lobby slot: name and the 'Bạn'/ready tag; empty slots read 'Chỗ trống' (AC20). References only.</summary>
    public sealed class LobbySlotView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private TMP_Text badge;
        [Tooltip("Placeholder tints (AC50: changed on the prefab only).")]
        [SerializeField] private Color selfColor = new Color(0.20f, 0.42f, 0.33f);
        [SerializeField] private Color filledColor = new Color(0.12f, 0.17f, 0.16f);
        [SerializeField] private Color emptyColor = new Color(0.09f, 0.12f, 0.11f);

        public Image Background => background;
        public TMP_Text PlayerName => playerName;
        public TMP_Text Badge => badge;

        public void Tint(bool filled, bool self) => background.color = self ? selfColor : filled ? filledColor : emptyColor;
    }
}
