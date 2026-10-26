namespace JungleMaze.App.Hud.Feedback
{
    /// <summary>Whether a world coordinate is inside the active camera viewport.</summary>
    public interface IWorldViewport { bool Contains(float worldX,float worldY); }
}
