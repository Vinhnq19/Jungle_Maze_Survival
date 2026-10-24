using JungleMaze.App.Hud.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>One pickup toast with an art-swappable placeholder icon.</summary>
    public sealed class PickupToastView:MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;[SerializeField] private TMP_Text label,count,plus;[SerializeField] private Image background,icon;
        private readonly char[] _buffer=new char[24];
        public CanvasGroup Group=>group;public TMP_Text Label=>label;public Image Icon=>icon;
        public void ApplyPalette(HudPalette p){background.color=p.Surface;label.color=p.Text;count.color=p.Muted;plus.color=p.Green;icon.color=p.IconPlaceholder;}
        public void Show(bool visible,string name,int quantity,float alpha){group.alpha=visible?alpha:0;if(!visible)return;if(label.text!=name)label.text=name;count.SetCharArray(_buffer,0,HudFeedbackFormat.Count(_buffer,quantity));}
    }
}
