using JungleMaze.App.Hud.Feedback;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.DI;
using JungleMaze.Presentation.UI.Hud.Feedback;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>Composition of feedback views; only three read ports are injected.</summary>
    [DisallowMultipleComponent]
    public sealed class HudFeedbackRoot:MonoBehaviour
    {
        [SerializeField] private HudFeedbackTuning tuning=HudFeedbackTuning.Default;
        [SerializeField] private HudEffectCatalog catalog;
        [SerializeField] private UseActionView useAction;
        [SerializeField] private EffectRowView effectRow;
        [SerializeField] private PickupFeedView pickupFeed;
        [SerializeField] private HitMarkerView hitMarker;
        [SerializeField] private DamageArcView[] damageArcs=new DamageArcView[4];
        [SerializeField] private DangerBannerView dangerBanner;
        [SerializeField] private DangerArrowView[] dangerArrows=new DangerArrowView[3];
        private IHudState _hud;private IInventoryState _inventory;private IEffectState _effects;private IItemIconProvider _icons;
        public HudFeedbackPresenter Presenter {get;private set;}
        public HudFeedbackTuning Tuning=>tuning;public HudEffectCatalog Catalog=>catalog;public UseActionView UseAction=>useAction;public EffectRowView EffectRow=>effectRow;
        public PickupFeedView PickupFeed=>pickupFeed;public HitMarkerView HitMarker=>hitMarker;public DamageArcView[] DamageArcs=>damageArcs;public DangerBannerView DangerBanner=>dangerBanner;public DangerArrowView[] DangerArrows=>dangerArrows;
        [Inject] public void Construct(IHudState hud,IInventoryState inventory,IEffectState effects){_hud=hud;_inventory=inventory;_effects=effects;}
        /// <summary>Item icon provider for the pickup feed; separate from <see cref="Construct"/> so three-port helpers keep working (null keeps placeholders).</summary>
        [Inject] public void ConstructIcons(IItemIconProvider icons){_icons=icons;if(pickupFeed!=null)pickupFeed.ConfigureIcons(icons);}
        private void Start()
        {
            if(_hud==null||_inventory==null||_effects==null){Debug.LogWarning("[HUD] HudFeedbackRoot has no HUD state (no Bootstrap?); feedback disabled.");enabled=false;return;}
            var core=GetComponent<HudRoot>();
            if(core==null||useAction==null||effectRow==null||pickupFeed==null||hitMarker==null||dangerBanner==null){Debug.LogWarning("[HUD] HudFeedbackRoot is missing a view reference; feedback disabled.");enabled=false;return;}
            var palette=core.Palette;useAction.ApplyPalette(palette);effectRow.Configure(palette,catalog);pickupFeed.ApplyPalette(palette);pickupFeed.ConfigureIcons(_icons);hitMarker.ApplyPalette(palette);dangerBanner.ApplyPalette(palette);
            foreach(var arc in damageArcs)if(arc!=null)arc.ApplyPalette(palette);foreach(var arrow in dangerArrows)if(arrow!=null)arrow.ApplyPalette(palette);
            Presenter=new HudFeedbackPresenter(_hud,_inventory,_effects,tuning,new CameraWorldViewport(),core.Quickbar,useAction,effectRow,pickupFeed,hitMarker,new DamageArcViewGroup(damageArcs),dangerBanner,new DangerArrowViewGroup(dangerArrows));
        }
        private void LateUpdate(){if(Presenter==null)return;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,Input.mousePosition,null,out var p);Presenter.Tick(Time.deltaTime,p.x,p.y);}
        private void OnDestroy(){Presenter?.Dispose();Presenter=null;}
    }
}
