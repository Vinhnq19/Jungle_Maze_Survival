using UnityEngine;

namespace _Codebase_.Utils
{
    public struct PartialTransform
    {
        public Vector3?    Position;
        public Quaternion? Rotation;
        public Vector3?    Scale;

        public Transform Parent;

        public void ApplyToTransform(Transform transform)
        {
            if (this.Parent)
            {
                transform.SetParent(this.Parent, false);
            }

            if (this.Position.HasValue)
            {
                transform.position = this.Position.Value;
            }

            if (this.Rotation.HasValue)
            {
                transform.rotation = this.Rotation.Value;
            }

            if (this.Scale.HasValue)
            {
                transform.localScale = this.Scale.Value;
            }
        }
    }
}