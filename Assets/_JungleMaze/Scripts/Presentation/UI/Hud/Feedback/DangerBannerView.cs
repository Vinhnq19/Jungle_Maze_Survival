using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>Single source-selected warning banner.</summary>
    public sealed class DangerBannerView:MonoBehaviour,IDangerBannerView
    {
        [SerializeField] private CanvasGroup group;[SerializeField] private TMP_Text label,timer,suffix;[SerializeField] private Image background,border;
        public CanvasGroup Group=>group;public TMP_Text Label=>label;public TMP_Text Timer=>timer;public TMP_Text Suffix=>suffix;
        public void ApplyPalette(HudPalette p){label.color=p.Text;timer.color=p.Red;suffix.color=p.Muted;background.color=p.DangerFill;border.color=p.Red;}
        public void Show(bool visible,char[] text,int length,float alpha)
        {
            group.alpha=visible?alpha:0;if(!visible)return;int start=-1,end=-1;
            for(var i=0;i<length;i++)if(text[i]>='0'&&text[i]<='9'){var j=i;while(j<length&&text[j]>='0'&&text[j]<='9')j++;if(j<length&&text[j]=='s'){start=i;end=j+1;break;}}
            timer.enabled=start>=0;suffix.enabled=start>=0;
            label.rectTransform.anchoredPosition=new Vector2(6,-8);
            if(start<0){label.rectTransform.sizeDelta=new Vector2(289,24);label.SetCharArray(text,0,length);}
            else{
                label.SetCharArray(text,0,start);timer.SetCharArray(text,start,end-start);suffix.SetCharArray(text,end,length-end);
                var prefixWidth=Mathf.Ceil(label.preferredWidth);var timerWidth=Mathf.Ceil(timer.preferredWidth);
                label.rectTransform.sizeDelta=new Vector2(prefixWidth,24);timer.rectTransform.anchoredPosition=new Vector2(6+prefixWidth,-8);timer.rectTransform.sizeDelta=new Vector2(timerWidth,24);
                suffix.rectTransform.anchoredPosition=new Vector2(6+prefixWidth+timerWidth,-8);suffix.rectTransform.sizeDelta=new Vector2(Mathf.Max(1,289-prefixWidth-timerWidth),24);
            }
        }
    }
}
