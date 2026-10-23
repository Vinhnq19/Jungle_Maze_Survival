using UnityEngine;

namespace JungleMaze.Presentation.Player
{
    /// <summary>Screen ray -> Hero's XY plane, independent of camera offset, zoom and window size.</summary>
    public static class HeroAim
    {
        public static bool TryGetTargetDegrees(Camera camera, Vector3 hero, Vector2 mouseScreen, out float degrees)
        {
            degrees = 0f;
            if (camera == null) return false;
            var plane = new Plane(Vector3.forward, hero);
            var ray = camera.ScreenPointToRay(mouseScreen);
            if (!plane.Raycast(ray, out var distance)) return false;
            var direction = ray.GetPoint(distance) - hero;
            // Cursor exactly on Hero: keep the previous aim instead of snapping to zero degrees.
            if (direction.sqrMagnitude < 0.000001f) return false;
            degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return true;
        }
    }
}
