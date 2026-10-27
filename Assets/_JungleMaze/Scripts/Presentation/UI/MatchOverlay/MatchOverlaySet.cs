using JungleMaze.App.MatchUi;
using UnityEngine;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// UI-03 entry asset at <c>Art/UI/MatchOverlay/Resources/MatchOverlaySet.asset</c>, loaded once by Bootstrap like
    /// <see cref="UiArtSet"/>. Holds the overlay prefab, the client killcam switch (N10, off by default, Q1/T-3) and a
    /// designer copy of the G-1…G-8 timings.
    /// </summary>
    [CreateAssetMenu(menuName = "Jungle Maze/Match Overlay Set")]
    public sealed class MatchOverlaySet : ScriptableObject
    {
        public const string ResourcePath = "MatchOverlaySet";

        [SerializeField] private MatchOverlayRoot overlayPrefab;
        [Tooltip("N10: play the killer's 5 s replay before the Eliminated screen. Off by default (Q1).")]
        [SerializeField] private bool killcamEnabled;
        [SerializeField] private float menuFadeSeconds = 0.12f;
        [SerializeField] private float killcamWaitSeconds = 1f;
        [Tooltip("AC41: ticks the replay's end may differ from the elimination tick (30 = 1 s).")]
        [SerializeField] private int killcamEndTickTolerance = KillcamOptions.DefaultEndTickTolerance;
        [SerializeField] private float killcamNoticeSeconds = 3f;
        [SerializeField] private float eliminatedNoticeSeconds = 2f;
        [SerializeField] private float cameraSwitchSeconds = 0.25f;
        [SerializeField] private float leaveRetrySeconds = 5f;

        public MatchOverlayRoot OverlayPrefab => overlayPrefab;
        public bool KillcamEnabled => killcamEnabled;

        public MatchOverlayTuning ToTuning() => new MatchOverlayTuning
        {
            MenuFadeSeconds = menuFadeSeconds,
            KillcamWaitSeconds = killcamWaitSeconds,
            KillcamEndTickTolerance = killcamEndTickTolerance,
            KillcamNoticeSeconds = killcamNoticeSeconds,
            EliminatedNoticeSeconds = eliminatedNoticeSeconds,
            CameraSwitchSeconds = cameraSwitchSeconds,
            LeaveRetrySeconds = leaveRetrySeconds
        };
    }
}
