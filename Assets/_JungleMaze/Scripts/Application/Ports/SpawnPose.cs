using UnityEngine;

namespace JungleMaze.App.Ports
{
    /// <summary>Tham số chung cho factory prefab: vị trí, hướng, cha.</summary>
    public readonly struct SpawnPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Transform Parent;

        public SpawnPose(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            Position = position;
            Rotation = rotation;
            Parent = parent;
        }

        public static SpawnPose At(Vector3 position) => new SpawnPose(position, Quaternion.identity);
    }
}
