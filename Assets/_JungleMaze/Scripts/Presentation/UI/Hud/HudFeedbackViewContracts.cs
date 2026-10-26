using JungleMaze.Domain.Hud;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>Minimal bridge to the two approved quick-bar extension points.</summary>
    public interface IQuickbarFeedbackHooks {void SetWeaponStripVisible(bool visible);void SetSlotInUse(int index,bool inUse);}
    /// <summary>Use strip rendering with caller-owned text buffers.</summary>
    public interface IUseActionView {void Show(bool visible,char[] label,int labelLength,char[] seconds,int secondsLength,float fill,bool hint,bool cancelled);}
    /// <summary>Bounded effect row rendering.</summary>
    public interface IEffectRowView {void SetChip(int index,bool visible,EffectKind kind,EffectPolarity polarity,bool timed,float fill,int seconds,int overflow,float alpha);}
    /// <summary>Bounded pickup feed rendering.</summary>
    public interface IPickupFeedView {void SetRow(int index,bool visible,string id,string name,int count,float alpha);void SetOverflow(int count,float alpha);}
    /// <summary>One hit marker at the aim point.</summary>
    public interface IHitMarkerView {void Show(float alpha,float x,float y);}
    /// <summary>Bounded incoming damage arc rendering.</summary>
    public interface IDamageArcView {void SetArc(int index,bool visible,float x,float y,float rotation,float alpha);}
    /// <summary>One warning banner with already formatted text.</summary>
    public interface IDangerBannerView {void Show(bool visible,char[] text,int length,float alpha);}
    /// <summary>Bounded offscreen warning arrow rendering.</summary>
    public interface IDangerArrowView {void SetArrow(int index,bool visible,float x,float y,float degrees,bool timed,int seconds,float alpha);}
}
