using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>Single source-selected warning banner.</summary>
    public sealed class DangerBannerView:MonoBehaviour,IDangerBannerView
    {
        [SerializeField] private CanvasGroup group;[SerializeField] private TMP_Text label;[SerializeField] private Image background,border;
        public CanvasGroup Group=>group;public TMP_Text Label=>label;
        public void ApplyPalette(HudPalette p){label.color=p.Text;background.color=p.DangerFill;border.color=p.Red;}
        public void Show(bool visible,char[] text,int length,float alpha){group.alpha=visible?alpha:0;if(visible)label.SetCharArray(text,0,length);}
    }
}
