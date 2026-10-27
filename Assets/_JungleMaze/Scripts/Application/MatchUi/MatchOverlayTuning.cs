namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// UI-03 timings (G-1…G-8) in one place. Plain C# so EditMode tests use the same numbers as the game; the
    /// overlay asset keeps a copy the designer can tweak. Every "≤" budget of the spec is a ceiling, not a delay.
    /// </summary>
    public sealed class MatchOverlayTuning
    {
        /// <summary>G-1: menu fade in/out; open must finish ≤ 0.2 s, close ≤ 0.15 s.</summary>
        public float MenuFadeSeconds = 0.12f;

        /// <summary>G-3: longest wait for replay data after the elimination.</summary>
        public float KillcamWaitSeconds = 1f;

        /// <summary>AC35: "Killcam unavailable" notice lifetime.</summary>
        public float KillcamNoticeSeconds = 3f;

        /// <summary>G-4: auto-switch when the watched player is eliminated (the flow switches on the next tick).</summary>
        public float AutoAdvanceSeconds = 1f;

        /// <summary>AC26: "&lt;name&gt; was eliminated" notice lifetime.</summary>
        public float EliminatedNoticeSeconds = 2f;

        /// <summary>G-5: camera ease-out between two spectator targets.</summary>
        public float CameraSwitchSeconds = 0.25f;

        /// <summary>G-7: buttons unlock with a retry message if the screen has not changed after a leave request.</summary>
        public float LeaveRetrySeconds = 5f;

        public static MatchOverlayTuning Default => new MatchOverlayTuning();
    }
}
