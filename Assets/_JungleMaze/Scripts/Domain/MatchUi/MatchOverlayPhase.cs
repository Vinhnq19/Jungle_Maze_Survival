namespace JungleMaze.Domain.MatchUi
{
    /// <summary>
    /// Sub-states of <c>AppScreen.InMatch</c> owned by UI-03 (G-13); no new AppScreen. <see cref="HandedOff"/> means
    /// the overlay let go (results are UI-02's) and waits for GameFlow to change the screen.
    /// </summary>
    public enum MatchOverlayPhase : byte
    {
        Inactive = 0,
        Playing = 1,
        Killcam = 2,
        Eliminated = 3,
        Spectating = 4,
        HandedOff = 5
    }
}
