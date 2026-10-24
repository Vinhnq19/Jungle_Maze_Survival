using JungleMaze.Domain.Hud;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>Six pooled-in-place chip views, never instantiated during a match.</summary>
    public sealed class EffectRowView:MonoBehaviour,IEffectRowView
    {
        [SerializeField] private EffectChipView[] chips=new EffectChipView[6];
        public EffectChipView[] Chips=>chips;
        public void Configure(HudPalette palette,HudEffectCatalog catalog){foreach(var chip in chips)if(chip!=null)chip.Configure(palette,catalog);}
        public void SetChip(int index,bool visible,EffectKind kind,EffectPolarity polarity,bool timed,float fill,int seconds,int overflow,float alpha){if(index>=0&&index<chips.Length&&chips[index]!=null)chips[index].Show(visible,kind,polarity,timed,fill,seconds,overflow,alpha);}
    }
}
