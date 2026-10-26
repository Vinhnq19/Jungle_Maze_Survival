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
        /// <summary>Tốc độ/bán kính server dùng cho movement; chỉ hợp lệ sau khi Verify trả null.</summary>
        float PlayerSpeed { get; }
        float PlayerRadius { get; }
        string Verify(string expectedVersion, string expectedHash);
    }
}
