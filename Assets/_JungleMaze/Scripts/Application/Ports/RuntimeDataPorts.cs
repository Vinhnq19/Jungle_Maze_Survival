using System.Collections.Generic;

namespace JungleMaze.App.Ports
{
    public struct RuntimeCover
    {
        public string Name;
        public float MinX, MinY, MaxX, MaxY;
    }
    public struct RuntimeMapBounds { public float MinX, MinY, MaxX, MaxY; }
    public interface IClientRuntimeData
    {
        IReadOnlyList<RuntimeCover> Covers { get; }
        RuntimeMapBounds Bounds { get; }
        string Verify(string expectedVersion, string expectedHash);
    }
}
