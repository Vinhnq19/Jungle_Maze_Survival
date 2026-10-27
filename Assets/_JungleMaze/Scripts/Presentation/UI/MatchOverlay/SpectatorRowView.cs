using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>One row of the spectator list (SpectatorRow.prefab, 160×32, G-12). Colors are prefab data (AC47).</summary>
    public sealed class SpectatorRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;
        [Tooltip("Outline shown on the selected row only.")]
        [SerializeField] private Image selectedOutline;
        [SerializeField] private Color normal = new Color(0.125f, 0.165f, 0.176f, 0.92f);
        [SerializeField] private Color selected = new Color(0.776f, 0.682f, 0.471f, 0.35f);

        private byte _netId = 255;
        private string _name;

        public Button Button => button;
        public TMP_Text Label => label;
        public byte NetId => _netId;
        public bool Selected { get; private set; }

        public void Set(byte netId, string name, bool isSelected)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (_netId != netId || !ReferenceEquals(_name, name))
            {
                _netId = netId;
                _name = name;
                label.text = name ?? "Người chơi " + (netId + 1);
            }
            Selected = isSelected;
            background.color = isSelected ? selected : normal;
            if (selectedOutline != null && selectedOutline.enabled != isSelected) selectedOutline.enabled = isSelected;
        }

        public void Hide()
        {
            _netId = 255;
            Selected = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
