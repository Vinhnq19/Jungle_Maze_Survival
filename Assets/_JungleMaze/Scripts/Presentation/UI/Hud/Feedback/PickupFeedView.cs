using JungleMaze.App.Hud.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>Right-aligned toast rows with the newest visible row anchored at the bottom.</summary>
    public sealed class PickupFeedView:MonoBehaviour,IPickupFeedView
    {
        [SerializeField] private PickupToastView[] rows=new PickupToastView[3];[SerializeField] private CanvasGroup overflowGroup;[SerializeField] private TMP_Text overflowLabel;[SerializeField] private Image overflowBackground;
        private readonly bool[] _visible=new bool[3];private readonly char[] _buffer=new char[32];
        public PickupToastView[] Rows=>rows;public CanvasGroup OverflowGroup=>overflowGroup;
        public void ApplyPalette(HudPalette p){foreach(var row in rows)row.ApplyPalette(p);overflowLabel.color=p.Muted;overflowBackground.color=p.Surface;}
        public void SetRow(int index,bool visible,string id,string name,int count,float alpha)
        {
            if(index<0||index>=rows.Length)return;_visible[index]=visible;rows[index].Show(visible,name,count,alpha);
            var position=0;for(var i=rows.Length-1;i>=0;i--)if(_visible[i]){((RectTransform)rows[i].transform).anchoredPosition=new Vector2(0,position*37);position++;}
        }
        public void SetOverflow(int count,float alpha){overflowGroup.alpha=count>0?alpha:0;if(count>0)overflowLabel.SetCharArray(_buffer,0,HudFeedbackFormat.Overflow(_buffer,count,true));}
    }
}
