using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>One solid incoming damage arc.</summary>
    public sealed class DamageArcView:MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;[SerializeField] private Image image;
        public CanvasGroup Group=>group;public Image Image=>image;
        public void ApplyPalette(HudPalette p){image.color=p.Red;}
        public void Show(bool visible,float x,float y,float rotation,float alpha){group.alpha=visible?alpha:0;var r=(RectTransform)transform;r.anchoredPosition=new Vector2(x,y);r.localEulerAngles=new Vector3(0,0,rotation);}
    }
    /// <summary>Fixed-size arc collection adapter.</summary>
    public sealed class DamageArcViewGroup:IDamageArcView
    {
        private readonly DamageArcView[] _views;public DamageArcViewGroup(DamageArcView[] views){_views=views;}
        public void SetArc(int index,bool visible,float x,float y,float rotation,float alpha){if(index>=0&&index<_views.Length&&_views[index]!=null)_views[index].Show(visible,x,y,rotation,alpha);}
    }
}
