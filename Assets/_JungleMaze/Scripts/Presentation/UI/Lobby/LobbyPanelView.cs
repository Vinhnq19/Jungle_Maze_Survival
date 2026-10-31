using UnityEngine;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>
    /// Base of every UI-02 panel: shown and hidden by its CanvasGroup only, never SetActive or Instantiate (G-1).
    /// </summary>
    public abstract class LobbyPanelView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;

        public CanvasGroup Group => group;
        public bool Visible => group != null && group.alpha > 0.5f;

        public void Show(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
