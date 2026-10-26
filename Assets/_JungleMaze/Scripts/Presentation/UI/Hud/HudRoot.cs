using JungleMaze.App.Hud;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.DI;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>
    /// Root of Hud.prefab. Receives only the three read ports (plus icons) through [Inject], applies the palette
    /// and drives <see cref="HudPresenter"/> from LateUpdate (after TickRunner's Update has advanced effect timers).
    /// Without Bootstrap (dependencies missing) it logs a warning and disables itself instead of throwing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudRoot : MonoBehaviour
    {
        [SerializeField] private HudTuning tuning = HudTuning.Default;
        [SerializeField] private HudPalette palette = HudPalette.Default;
        [SerializeField] private VitalsView vitals;
        [SerializeField] private QuickbarView quickbar;
        [SerializeField] private MinimapView minimap;
        [SerializeField] private MatchStatusView matchStatus;
        [Tooltip("Direction A edge/bevel overlays of the block panels (slots own theirs).")]
        [SerializeField] private HudArtFrameView[] artFrames = new HudArtFrameView[0];

        private IHudState _hud;
        private IInventoryState _inventory;
        private IEffectState _effects;
        private IItemIconProvider _icons;

        public HudPresenter Presenter { get; private set; }
        public HudTuning Tuning => tuning;
        public HudPalette Palette => palette;
        public VitalsView Vitals => vitals;
        public QuickbarView Quickbar => quickbar;
        public MinimapView Minimap => minimap;
        public MatchStatusView MatchStatus => matchStatus;
        public HudArtFrameView[] ArtFrames => artFrames;

        [Inject]
        public void Construct(IHudState hud, IInventoryState inventory, IEffectState effects, IItemIconProvider icons)
        {
            _hud = hud;
            _inventory = inventory;
            _effects = effects;
            _icons = icons;
        }

        private void Start()
        {
            if (_hud == null || _inventory == null || _effects == null)
            {
                Debug.LogWarning("[HUD] HudRoot has no HUD state (no Bootstrap?); HUD disabled.");
                enabled = false;
                return;
            }
            if (vitals == null || quickbar == null || minimap == null || matchStatus == null)
            {
                Debug.LogWarning("[HUD] HudRoot is missing a view reference; HUD disabled.");
                enabled = false;
                return;
            }
            ApplyPalette();
            quickbar.SetIconProvider(_icons ?? new NullItemIconProvider());
            Presenter = new HudPresenter(_hud, _inventory, _effects, tuning, vitals, quickbar, minimap, matchStatus);
        }

        private void LateUpdate() => Presenter?.Tick(Time.deltaTime);

        private void OnDestroy()
        {
            Presenter?.Dispose();
            Presenter = null;
        }

        /// <summary>Pushes <see cref="palette"/> to every view (also usable from the inspector after an art change).</summary>
        [ContextMenu("Apply palette")]
        public void ApplyPalette()
        {
            if (vitals != null) vitals.ApplyPalette(palette);
            if (quickbar != null) quickbar.ApplyPalette(palette);
            if (minimap != null) minimap.ApplyPalette(palette);
            if (matchStatus != null) matchStatus.ApplyPalette(palette);
            if (artFrames != null)
                foreach (var frame in artFrames)
                    if (frame != null) frame.ApplyPalette(palette);
        }
    }
}
