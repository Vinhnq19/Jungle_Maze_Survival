namespace JungleMaze.App.Audio
{
    /// <summary>
    /// A producer of sound cues: the match/network adapter, the dev mock, and later WPN-01, DRG-01 or a footstep
    /// estimator. The router listens to every source the same way, so new sources need no router change.
    /// </summary>
    public interface IAudioEventSource
    {
        event System.Action<AudioCue> CueRaised;
    }
}
