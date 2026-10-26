using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;

namespace JungleMaze.Tests.Movement
{
    /// <summary>Bản sao bố cục Hard Cover của map_demo.json (ENV-01) để test không phụ thuộc file StreamingAssets.</summary>
    public static class MovementTestMap
    {
        public const float Speed = 4f, Radius = 0.4f;

        public static readonly RuntimeCover Pillar = Cover("AncientPillar", 22.5f, 23.5f, 24f, 26f);
        public static readonly RuntimeCover Statue = Cover("AncientStatue", 23.5f, 26.5f, 18.5f, 21.5f);
        public static readonly RuntimeCover Boulder = Cover("BoulderRock", 28f, 32f, 23f, 27f);
        public static readonly RuntimeCover Wall = Cover("CityWall", 24.5f, 25.5f, 26f, 30f);

        public static MovementSimulation Create() =>
            new MovementSimulation(new RuntimeMapBounds { MinX = 0, MinY = 0, MaxX = 50, MaxY = 50 },
                new[] { Pillar, Statue, Boulder, Wall }, Radius, Speed);

        public static PlayerInputPacket Input(ushort seq, float moveX, float moveY, float aim = 0f)
        {
            var input = new PlayerInputPacket { InputSeq = seq };
            input.SetMove(moveX, moveY);
            input.SetAimDegrees(aim);
            return input;
        }

        private static RuntimeCover Cover(string name, float minX, float maxX, float minY, float maxY) =>
            new RuntimeCover { Name = name, MinX = minX, MaxX = maxX, MinY = minY, MaxY = maxY };
    }
}
