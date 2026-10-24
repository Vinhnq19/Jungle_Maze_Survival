using JungleMaze.App.Hud.Feedback;
using JungleMaze.Domain.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>One prebuilt effect chip with configurable label and icon.</summary>
    public sealed class EffectChipView:MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;[SerializeField] private Image border,icon;[SerializeField] private TMP_Text label,seconds;[SerializeField] private RectTransform fill;[SerializeField] private Image background;
        private readonly char[] _buffer=new char[24];private HudPalette _palette;private HudEffectCatalog _catalog;
        public CanvasGroup Group=>group;public TMP_Text Label=>label;public TMP_Text Seconds=>seconds;public RectTransform Fill=>fill;public Image Border=>border;
        public void Configure(HudPalette palette,HudEffectCatalog catalog){_palette=palette;_catalog=catalog;if(background!=null)background.color=palette.Surface;}
        public void Show(bool visible,EffectKind kind,EffectPolarity polarity,bool timed,float ratio,int left,int overflow,float alpha)
        {
            group.alpha=visible?alpha:0;if(!visible)return;var color=polarity==EffectPolarity.Harmful?_palette.Red:_palette.Green;
            border.color=color;label.color=color;fill.GetComponent<Image>().color=color;
            if(overflow>0){label.SetCharArray(_buffer,0,HudFeedbackFormat.Overflow(_buffer,overflow,false));icon.enabled=false;}
            else {var title=_catalog!=null?_catalog.Label(kind):"?";if(label.text!=title)label.text=title;var sprite=_catalog!=null?_catalog.Icon(kind):null;icon.sprite=sprite;icon.enabled=sprite!=null;}
            seconds.enabled=timed&&overflow==0;fill.gameObject.SetActive(timed&&overflow==0);if(seconds.enabled)seconds.SetCharArray(_buffer,0,HudFeedbackFormat.Seconds(_buffer,left));fill.anchorMax=new Vector2(ratio,1);seconds.color=_palette.Text;
        }
    }
}
