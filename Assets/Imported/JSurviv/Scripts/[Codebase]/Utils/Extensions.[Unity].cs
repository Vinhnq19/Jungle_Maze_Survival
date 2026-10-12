using _Codebase_.Entity.Player;
using UnityEngine;

namespace _Codebase_.Utils
{
    public static class UnityExtensions
    {
        public static T SetTransformValues<T>(this T component, PartialTransform values)
            where T : Component
        {
            values.ApplyToTransform(component.transform);
            return component;
        }

        public static GameObject SetTransformValues(this GameObject gameObject, PartialTransform values)
        {
            return gameObject.transform.SetTransformValues(values).gameObject;
        }

        public static Vector2 ToVector2(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector2.up,
                Direction.Down => Vector2.down,
                Direction.Left => Vector2.left,
                Direction.Right => Vector2.right,
                _ => Vector2.zero,
            };
        }
    }
}