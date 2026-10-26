using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>One aim-anchored marker made of four art-swappable Images.</summary>
    public sealed class HitMarkerView:MonoBehaviour,IHitMarkerView
    {
        [SerializeField] private CanvasGroup group;[SerializeField] private Image[] ticks;
        public CanvasGroup Group=>group;public Image[] Ticks=>ticks;
        public void ApplyPalette(HudPalette p){foreach(var tick in ticks)tick.color=p.HitMarker;}
        public void Show(float alpha,float x,float y){group.alpha=alpha;((RectTransform)transform).anchoredPosition=new Vector2(x,y);}
    }
}
