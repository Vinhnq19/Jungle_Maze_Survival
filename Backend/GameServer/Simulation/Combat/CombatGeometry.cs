using System.Numerics;

namespace GameServer.Simulation.Combat;

/// <summary>Pure .NET geometry for hit tests. Does not reference Unity Collider2D or CoverQuery.</summary>
public static class CombatGeometry
{
    /// <summary>Converts an aim angle in 0..65535 (same scale as PlayerInputPacket.Aim) to a unit vector.</summary>
    public static Vector2 AimDirection(ushort aim)
    {
        var radians = aim / (65536f / 360f) * MathF.PI / 180f;
        return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
    }

    /// <summary>Returns whether segment a→b intersects the AABB (slab method). Touching an edge counts as a hit.</summary>
    public static bool SegmentIntersectsAabb(Vector2 a, Vector2 b, AABB box, out float entryT)
    {
        entryT = 0f;
        var tMin = 0f;
        var tMax = 1f;
        var delta = b - a;

        if (!ClipAxis(a.X, delta.X, box.MinX, box.MaxX, ref tMin, ref tMax)) return false;
        if (!ClipAxis(a.Y, delta.Y, box.MinY, box.MaxY, ref tMin, ref tMax)) return false;

        entryT = tMin;
        return true;
    }

    /// <summary>Casts a ray from origin along dir up to maxDistance; returns the distance to the box entry point, or false if missed.</summary>
    public static bool RayHitsAabb(Vector2 origin, Vector2 dir, float maxDistance, AABB box, out float distance)
    {
        var end = origin + dir * maxDistance;
        if (!SegmentIntersectsAabb(origin, end, box, out var t))
        {
            distance = 0f;
            return false;
        }

        distance = t * maxDistance;
        return true;
    }

    /// <summary>Casts a ray against a circle; returns the nearest entry distance within [0, maxDistance].</summary>
    public static bool RayHitsCircle(Vector2 origin, Vector2 dir, float maxDistance, Vector2 center, float radius, out float distance)
    {
        distance = 0f;
        var toCenter = center - origin;
        var along = Vector2.Dot(toCenter, dir);
        var perpSq = Vector2.Dot(toCenter, toCenter) - along * along;
        var radiusSq = radius * radius;
        if (perpSq > radiusSq) return false;

        var halfChord = MathF.Sqrt(radiusSq - perpSq);
        var entry = along - halfChord;
        if (entry < 0f)
        {
            // The origin lies inside the circle, so the hit is at distance zero.
            if (along + halfChord < 0f) return false;
            entry = 0f;
        }

        if (entry > maxDistance) return false;
        distance = entry;
        return true;
    }

    /// <summary>Melee arc test: the target is within range + radius and its angle from aim is at most arc/2.</summary>
    public static bool InMeleeArc(Vector2 origin, Vector2 aimDir, float range, float arcDegrees, Vector2 target, float targetRadius)
    {
        var toTarget = target - origin;
        var dist = toTarget.Length();
        if (dist > range + targetRadius) return false;
        if (dist <= targetRadius) return true;

        var cos = Vector2.Dot(toTarget / dist, aimDir);
        var halfArcCos = MathF.Cos(arcDegrees * 0.5f * MathF.PI / 180f);
        return cos >= halfArcCos;
    }

    private static bool ClipAxis(float origin, float delta, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(delta) < 1e-6f)
            return origin >= min && origin <= max;

        var t1 = (min - origin) / delta;
        var t2 = (max - origin) / delta;
        if (t1 > t2) (t1, t2) = (t2, t1);

        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        return tMin <= tMax;
    }
}
