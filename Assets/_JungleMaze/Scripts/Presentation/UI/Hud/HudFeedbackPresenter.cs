using System;
using JungleMaze.App.Hud.Feedback;
using JungleMaze.App.Hud.State;
using JungleMaze.Domain.Hud;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>Read-port-only feedback binding; owns no gameplay data and allocates nothing while ticking.</summary>
    public sealed class HudFeedbackPresenter:IDisposable
    {
        private readonly IHudState _hud;private readonly IInventoryState _inventory;private readonly IEffectState _effects;private readonly IWorldViewport _viewport;
        private readonly IQuickbarFeedbackHooks _quickbar;private readonly IUseActionView _useView;private readonly IEffectRowView _effectsView;private readonly IPickupFeedView _pickupView;
        private readonly IHitMarkerView _hitView;private readonly IDamageArcView _arcView;private readonly IDangerBannerView _bannerView;private readonly IDangerArrowView _arrowView;
        private readonly Action<ItemPickup> _onPickup;private readonly Action<HitDealt> _onHit;private readonly Action<DamageTaken> _onDamage;private readonly Action<DangerChange> _onDanger;private readonly Action<ItemUseStatus> _onUse;
        private readonly char[] _text=new char[HudFeedbackFormat.BufferSize],_seconds=new char[24];
        private readonly EffectChipOutput[] _lastChips=new EffectChipOutput[6];private readonly PickupOutput[] _lastPickups=new PickupOutput[3];private readonly DamageArcOutput[] _lastArcs=new DamageArcOutput[4];private readonly DangerArrowOutput[] _lastArrows=new DangerArrowOutput[3];
        private readonly bool[] _chipVisible=new bool[6],_pickupVisible=new bool[3],_arcVisible=new bool[4],_arrowVisible=new bool[3];
        private bool _first=true,_disposed,_lastUseVisible;private int _lastSlot=-1,_lastOverflow;private float _lastOverflowAlpha,_lastHitAlpha,_lastAimX,_lastAimY,_lastUseFill;private int _lastTenths;private ItemUseStatus _lastUse;private DangerBannerOutput _lastBanner;
        public UseActionFeedback Use {get;}public EffectChipFeedback Effects {get;}public PickupFeed Pickups {get;}public HitMarkerFeedback Hit {get;}public DamageArcFeedback Arcs {get;}public DangerFeedback Dangers {get;}
        public HudFeedbackPresenter(IHudState hud,IInventoryState inventory,IEffectState effects,HudFeedbackTuning tuning,IWorldViewport viewport,IQuickbarFeedbackHooks quickbar,IUseActionView use,IEffectRowView effectRow,IPickupFeedView pickup,IHitMarkerView hit,IDamageArcView arcs,IDangerBannerView banner,IDangerArrowView arrows)
        {
            _hud=hud??throw new ArgumentNullException(nameof(hud));_inventory=inventory??throw new ArgumentNullException(nameof(inventory));_effects=effects??throw new ArgumentNullException(nameof(effects));_viewport=viewport;
            _quickbar=quickbar;_useView=use;_effectsView=effectRow;_pickupView=pickup;_hitView=hit;_arcView=arcs;_bannerView=banner;_arrowView=arrows;
            Use=new UseActionFeedback(tuning);Effects=new EffectChipFeedback(tuning);Pickups=new PickupFeed(tuning);Hit=new HitMarkerFeedback(tuning);Arcs=new DamageArcFeedback(tuning);Dangers=new DangerFeedback(tuning);
            _onPickup=OnPickup;_onHit=OnHit;_onDamage=OnDamage;_onDanger=OnDanger;_onUse=OnUse;
            hud.ItemPicked+=_onPickup;hud.HitDealt+=_onHit;hud.DamageTaken+=_onDamage;hud.DangerChanged+=_onDanger;inventory.UseChanged+=_onUse;
            Use.Accept(inventory.Use);for(var i=0;i<hud.DangerCount;i++)Dangers.Upsert(hud.GetDangerAt(i));
        }
        public void Tick(float dt,float aimX,float aimY)
        {
            if(_disposed)return;
            Use.Tick(dt,_inventory.Use);Effects.BeginSync();for(var i=0;i<_effects.Count;i++)Effects.Sync(_effects.GetAt(i));Effects.Tick(dt);Pickups.Tick(dt);Hit.Tick(dt);Arcs.Tick(dt);
            for(var i=0;i<_hud.DangerCount;i++)Dangers.SyncSnapshot(_hud.GetDangerAt(i));Dangers.Tick(dt,_hud.HasPose,_hud.Pose,_viewport);
            RenderUse();RenderEffects();RenderPickups();RenderArcs();RenderDanger();
            var alpha=Hit.Alpha;if(_first||alpha!=_lastHitAlpha||(alpha>0&&(aimX!=_lastAimX||aimY!=_lastAimY))){_hitView?.Show(alpha,aimX,aimY);_lastHitAlpha=alpha;_lastAimX=aimX;_lastAimY=aimY;}
            _first=false;
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;_hud.ItemPicked-=_onPickup;_hud.HitDealt-=_onHit;_hud.DamageTaken-=_onDamage;_hud.DangerChanged-=_onDanger;_inventory.UseChanged-=_onUse;
            if(_lastSlot>=0)_quickbar?.SetSlotInUse(_lastSlot,false);_quickbar?.SetWeaponStripVisible(true);
        }
        private void OnPickup(ItemPickup item)=>Pickups.Add(item);
        private void OnHit(HitDealt hit)=>Hit.Add(hit.Target);
        private void OnDamage(DamageTaken damage)=>Arcs.Add(damage);
        private void OnUse(ItemUseStatus status)=>Use.Accept(status);
        private void OnDanger(DangerChange change)
        {
            if(change.Type==DangerChangeType.Removed||change.Type==DangerChangeType.Expired){Dangers.Remove(change.Id,change.Type==DangerChangeType.Expired);return;}
            for(var i=0;i<_hud.DangerCount;i++){var w=_hud.GetDangerAt(i);if(w.Id==change.Id){Dangers.Upsert(w);return;}}
        }
        private void RenderUse()
        {
            var s=Use.Status;var tenths=(int)Math.Ceiling(Math.Max(0,s.RemainingSeconds)*10-.00001f);
            if(_first||Use.Visible!=_lastUseVisible||Use.Fill!=_lastUseFill||s.Phase!=_lastUse.Phase||s.DisplayName!=_lastUse.DisplayName||s.Cancellable!=_lastUse.Cancellable||tenths!=_lastTenths){
                _useView?.Show(Use.Visible,_text,HudFeedbackFormat.UseLabel(_text,s),_seconds,HudFeedbackFormat.Tenths(_seconds,s.RemainingSeconds),Use.Fill,Use.Hint,s.Phase==ItemUsePhase.Cancelled);
                if(_first||Use.Visible!=_lastUseVisible)_quickbar?.SetWeaponStripVisible(!Use.Visible);
                _lastUse=s;_lastUseVisible=Use.Visible;_lastUseFill=Use.Fill;_lastTenths=tenths;
            }
            var slot=Use.SlotInUse;if(_first||slot!=_lastSlot){if(_lastSlot>=0)_quickbar?.SetSlotInUse(_lastSlot,false);if(slot>=0)_quickbar?.SetSlotInUse(slot,true);_lastSlot=slot;}
        }
        private void RenderEffects()
        {
            for(var i=0;i<6;i++){var visible=i<Effects.Count;var v=visible?Effects.GetAt(i):default;var old=_lastChips[i];
                if(_first||visible!=_chipVisible[i]||v.Kind!=old.Kind||v.Polarity!=old.Polarity||v.Timed!=old.Timed||v.Fill!=old.Fill||v.Seconds!=old.Seconds||v.Overflow!=old.Overflow||v.Alpha!=old.Alpha){_effectsView?.SetChip(i,visible,v.Kind,v.Polarity,v.Timed,v.Fill,v.Seconds,v.Overflow,v.Alpha);_lastChips[i]=v;_chipVisible[i]=visible;}}
        }
        private void RenderPickups()
        {
            for(var i=0;i<3;i++){var visible=i<Pickups.Count;var v=visible?Pickups.GetAt(i):default;var old=_lastPickups[i];
                if(_first||visible!=_pickupVisible[i]||v.ItemId!=old.ItemId||v.Count!=old.Count||v.Name!=old.Name||v.Alpha!=old.Alpha){_pickupView?.SetRow(i,visible,v.ItemId,v.Name,v.Count,v.Alpha);_lastPickups[i]=v;_pickupVisible[i]=visible;}}
            if(_first||Pickups.Overflow!=_lastOverflow||Pickups.OverflowAlpha!=_lastOverflowAlpha){_pickupView?.SetOverflow(Pickups.Overflow,Pickups.OverflowAlpha);_lastOverflow=Pickups.Overflow;_lastOverflowAlpha=Pickups.OverflowAlpha;}
        }
        private void RenderArcs()
        {
            for(var i=0;i<4;i++){var visible=i<Arcs.Count;var v=visible?Arcs.GetAt(i):default;var old=_lastArcs[i];if(_first||visible!=_arcVisible[i]||v.X!=old.X||v.Y!=old.Y||v.Rotation!=old.Rotation||v.Alpha!=old.Alpha){_arcView?.SetArc(i,visible,v.X,v.Y,v.Rotation,v.Alpha);_lastArcs[i]=v;_arcVisible[i]=visible;}}
        }
        private void RenderDanger()
        {
            var b=Dangers.Banner;var o=_lastBanner;
            if(_first||b.Visible!=o.Visible||b.Alpha!=o.Alpha||b.Warning.IsTimed!=o.Warning.IsTimed||Math.Ceiling(b.Warning.RemainingSeconds)!=Math.Ceiling(o.Warning.RemainingSeconds)||b.OtherCount!=o.OtherCount||b.HasDirection!=o.HasDirection||b.Compass!=o.Compass||b.Distance!=o.Distance){_bannerView?.Show(b.Visible,_text,HudFeedbackFormat.Banner(_text,b),b.Alpha);_lastBanner=b;}
            for(var i=0;i<3;i++){var visible=i<Dangers.ArrowCount;var v=visible?Dangers.GetArrow(i):default;var old=_lastArrows[i];if(_first||visible!=_arrowVisible[i]||v.X!=old.X||v.Y!=old.Y||v.Degrees!=old.Degrees||v.Timed!=old.Timed||v.Seconds!=old.Seconds||v.Alpha!=old.Alpha){_arrowView?.SetArrow(i,visible,v.X,v.Y,v.Degrees,v.Timed,v.Seconds,v.Alpha);_lastArrows[i]=v;_arrowVisible[i]=visible;}}
        }
    }
}
