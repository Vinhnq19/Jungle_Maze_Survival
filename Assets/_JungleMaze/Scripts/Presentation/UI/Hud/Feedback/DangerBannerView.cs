using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>Single source-selected warning banner.</summary>
    public sealed class DangerBannerView:MonoBehaviour,IDangerBannerView
    {
        [SerializeField] private CanvasGroup group;[SerializeField] private TMP_Text label,timer,suffix;[SerializeField] private Image background,border;
        public CanvasGroup Group=>group;public TMP_Text Label=>label;
        public void ApplyPalette(HudPalette p){label.color=p.Text;timer.color=p.Red;suffix.color=p.Muted;background.color=p.DangerFill;border.color=p.Red;}
        public void Show(bool visible,char[] text,int length,float alpha)
        {
            group.alpha=visible?alpha:0;if(!visible)return;int start=-1,end=-1;
            for(var i=0;i<length;i++)if(text[i]>='0'&&text[i]<='9'){var j=i;while(j<length&&text[j]>='0'&&text[j]<='9')j++;if(j<length&&text[j]=='s'){start=i;end=j+1;break;}}
            timer.enabled=start>=0;suffix.enabled=start>=0;
            if(start<0){label.rectTransform.sizeDelta=new Vector2(277,24);label.SetCharArray(text,0,length);}
            else{label.rectTransform.sizeDelta=new Vector2(110,24);label.SetCharArray(text,0,start);timer.SetCharArray(text,start,end-start);suffix.SetCharArray(text,end,length-end);}
        }
    }
}
