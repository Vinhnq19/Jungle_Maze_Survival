using UnityEngine;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// Root of MatchOverlay.prefab (canvas above the HUD). Instantiated once at composition (T-4), never per press.
    /// Holds view references only; Bootstrap binds a presenter and the key reader, which this component drives:
    /// keys in Update, drawing and fades in LateUpdate on unscaled time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchOverlayRoot : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private MatchMenuView menu;
        [SerializeField] private LeaveConfirmView confirm;
        [SerializeField] private EliminatedView eliminated;
        [SerializeField] private SpectatorView spectator;
        [SerializeField] private KillcamView killcam;
        [SerializeField] private OverlayNoticeView notice;
        [Tooltip("Inactive child; turned on at composition only when the scene has no EventSystem.")]
        [SerializeField] private GameObject eventSystem;

        public Canvas Canvas => canvas;
        public GameObject EventSystemObject => eventSystem;
        public MatchMenuView Menu => menu;
        public LeaveConfirmView Confirm => confirm;
        public EliminatedView Eliminated => eliminated;
        public SpectatorView Spectator => spectator;
        public KillcamView Killcam => killcam;
        public OverlayNoticeView Notice => notice;
        public MatchOverlayPresenter Presenter { get; private set; }
        public MatchOverlayInput Input { get; private set; }

        public void Bind(MatchOverlayPresenter presenter, MatchOverlayInput input)
        {
            Presenter?.Dispose();
            Presenter = presenter;
            Input = input;
        }

        /// <summary>uGUI needs one EventSystem; no scene has one yet, so the prefab's own is used only then.</summary>
        public void EnsureEventSystem()
        {
            if (eventSystem == null || eventSystem.activeSelf) return;
            if (UnityEngine.EventSystems.EventSystem.current != null
                || FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            eventSystem.SetActive(true);
        }

        private void Update() => Input?.Poll();

        private void LateUpdate() => Presenter?.Tick(Time.unscaledDeltaTime);

        private void OnDestroy()
        {
            Presenter?.Dispose();
            Presenter = null;
            Input = null;
        }
    }
}
