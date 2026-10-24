using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>Only renders the use action; all clocks live in the model.</summary>
    public sealed class UseActionView:MonoBehaviour,IUseActionView
    {
        [SerializeField] private CanvasGroup group;[SerializeField] private TMP_Text label,seconds,hint;[SerializeField] private RectTransform fill;[SerializeField] private Image background;
        private HudPalette _palette;
        public CanvasGroup Group=>group;public TMP_Text Label=>label;public TMP_Text Seconds=>seconds;public TMP_Text Hint=>hint;public RectTransform Fill=>fill;
        public void ApplyPalette(HudPalette palette){_palette=palette;if(background!=null)background.color=palette.Surface;fill.GetComponent<Image>().color=palette.Amber;seconds.color=palette.Amber;hint.color=palette.Muted;}
        public void Show(bool visible,char[] text,int length,char[] time,int timeLength,float ratio,bool showHint,bool cancelled)
        {
            group.alpha=visible?1:0;if(!visible)return;label.SetCharArray(text,0,length);label.color=cancelled?_palette.Muted:_palette.Text;
            seconds.SetCharArray(time,0,timeLength);seconds.enabled=!cancelled;hint.enabled=showHint;fill.anchorMax=new Vector2(ratio,1);
        }
    }
}
