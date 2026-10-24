namespace GameServer.Simulation;

public sealed class PhysicsSimulation
{
    private readonly MapData _map;
    public const float PlayerRadius = 0.4f;
    public const float PlayerSpeed = 4.0f;

    private readonly float _radius, _speed;
    public PhysicsSimulation(MapData map, float radius = PlayerRadius, float speed = PlayerSpeed)
    {
        if(!float.IsFinite(radius)||!float.IsFinite(speed)||radius<=0||speed<=0)throw new ArgumentException("Invalid physics config.");
        _map = map; _radius=radius; _speed=speed;
    }

    /// <summary>
    /// Updates position of entity using continuous sweep circle-vs-AABB + slide and map bounds.
    /// Diagonal movement is normalized so speed never exceeds PlayerSpeed (4.0).
    /// </summary>
    public (float X, float Y) Move(float currentX, float currentY, float moveXNorm, float moveYNorm, double deltaTime)
    {
        var dt = (float)deltaTime;
        if (dt <= 0f) return (currentX, currentY);

        // Normalize diagonal input
        var inputLen = MathF.Sqrt(moveXNorm * moveXNorm + moveYNorm * moveYNorm);
        if (inputLen > 1.0f)
        {
            moveXNorm /= inputLen;
            moveYNorm /= inputLen;
        }

        var vx = moveXNorm * _speed;
        var vy = moveYNorm * _speed;
        var dispX = vx * dt;
        var dispY = vy * dt;

        if (MathF.Abs(dispX) < 1e-6f && MathF.Abs(dispY) < 1e-6f)
        {
            // Still resolve any penetration if standing
            return ResolvePenetration(currentX, currentY);
        }

        var posX = currentX;
        var posY = currentY;

        // Perform up to 2 continuous sweep iterations (initial movement + 1 slide)
        for (var iter = 0; iter < 2; iter++)
        {
            if (MathF.Abs(dispX) < 1e-6f && MathF.Abs(dispY) < 1e-6f) break;

            var (hit, t, nx, ny) = Sweep(posX, posY, dispX, dispY);
            if (!hit)
            {
                posX += dispX;
                posY += dispY;
                break;
            }

            // Move up to the hit point (with a tiny epsilon buffer to avoid floating re-penetration)
            var safeT = MathF.Max(0f, t - 1e-4f);
            posX += dispX * safeT;
            posY += dispY * safeT;

            // Slide response: project remaining displacement onto plane orthogonal to normal
            var remX = dispX * (1f - t);
            var remY = dispY * (1f - t);
            var dot = remX * nx + remY * ny;

            dispX = remX - dot * nx;
            dispY = remY - dot * ny;
        }

        // Clamp to map boundaries
        var minBoundX = _map.MinX + _radius;
        var maxBoundX = _map.MaxX - _radius;
        var minBoundY = _map.MinY + _radius;
        var maxBoundY = _map.MaxY - _radius;

        if (posX < minBoundX) posX = minBoundX;
        if (posX > maxBoundX) posX = maxBoundX;
        if (posY < minBoundY) posY = minBoundY;
        if (posY > maxBoundY) posY = maxBoundY;

        return ResolvePenetration(posX, posY);
    }

    /// <summary>
    /// Sweeps circle of _radius from (x, y) along (dx, dy). Returns earliest hit t in [0, 1] and normal (nx, ny).
    /// </summary>
    private (bool Hit, float T, float Nx, float Ny) Sweep(float x, float y, float dx, float dy)
    {
        var earliestT = 1.0f;
        var hit = false;
        var bestNx = 0f;
        var bestNy = 0f;

        // 1. Check map boundaries
        var minBoundX = _map.MinX + _radius;
        var maxBoundX = _map.MaxX - _radius;
        var minBoundY = _map.MinY + _radius;
        var maxBoundY = _map.MaxY - _radius;

        if (dx < 0f && x > minBoundX)
        {
            var t = (minBoundX - x) / dx;
            if (t >= 0f && t < earliestT) { earliestT = t; bestNx = 1f; bestNy = 0f; hit = true; }
        }
        else if (dx > 0f && x < maxBoundX)
        {
            var t = (maxBoundX - x) / dx;
            if (t >= 0f && t < earliestT) { earliestT = t; bestNx = -1f; bestNy = 0f; hit = true; }
        }

        if (dy < 0f && y > minBoundY)
        {
            var t = (minBoundY - y) / dy;
            if (t >= 0f && t < earliestT) { earliestT = t; bestNx = 0f; bestNy = 1f; hit = true; }
        }
        else if (dy > 0f && y < maxBoundY)
        {
            var t = (maxBoundY - y) / dy;
            if (t >= 0f && t < earliestT) { earliestT = t; bestNx = 0f; bestNy = -1f; hit = true; }
        }

        // 2. Check each obstacle AABB
        foreach (var obs in _map.Obstacles)
        {
            var r = _radius;
            var boxMinX = obs.MinX - r;
            var boxMaxX = obs.MaxX + r;
            var boxMinY = obs.MinY - r;
            var boxMaxY = obs.MaxY + r;

            // Swept ray vs expanded AABB
            // X slab
            var invDx = MathF.Abs(dx) > 1e-6f ? 1f / dx : float.PositiveInfinity;
            var tx1 = (boxMinX - x) * invDx;
            var tx2 = (boxMaxX - x) * invDx;
            var tNearX = MathF.Min(tx1, tx2);
            var tFarX = MathF.Max(tx1, tx2);

            // Y slab
            var invDy = MathF.Abs(dy) > 1e-6f ? 1f / dy : float.PositiveInfinity;
            var ty1 = (boxMinY - y) * invDy;
            var ty2 = (boxMaxY - y) * invDy;
            var tNearY = MathF.Min(ty1, ty2);
            var tFarY = MathF.Max(ty1, ty2);

            var tEnter = MathF.Max(tNearX, tNearY);
            var tExit = MathF.Min(tFarX, tFarY);

            if (tExit >= tEnter && tExit >= 0f && tEnter < earliestT)
            {
                var tCandidate = MathF.Max(0f, tEnter);
                if (tCandidate < earliestT)
                {
                    // Determine normal at entry
                    float nx, ny;
                    if (tNearX > tNearY)
                    {
                        nx = dx > 0f ? -1f : 1f;
                        ny = 0f;
                    }
                    else
                    {
                        nx = 0f;
                        ny = dy > 0f ? -1f : 1f;
                    }

                    // Check if entry point is on flat edge or rounded corner
                    var hitX = x + dx * tCandidate;
                    var hitY = y + dy * tCandidate;

                    // If within flat regions:
                    if ((nx != 0f && hitY >= obs.MinY && hitY <= obs.MaxY) ||
                        (ny != 0f && hitX >= obs.MinX && hitX <= obs.MaxX))
                    {
                        earliestT = tCandidate;
                        bestNx = nx;
                        bestNy = ny;
                        hit = true;
                    }
                    else
                    {
                        // Check rounded corners of the obstacle
                        var corners = new[]
                        {
                            (obs.MinX, obs.MinY),
                            (obs.MaxX, obs.MinY),
                            (obs.MinX, obs.MaxY),
                            (obs.MaxX, obs.MaxY)
                        };

                        foreach (var (cx, cy) in corners)
                        {
                            // Ray vs circle centered at (cx, cy) with radius r
                            var ox = x - cx;
                            var oy = y - cy;
                            var a = dx * dx + dy * dy;
                            var b = 2f * (ox * dx + oy * dy);
                            var c = ox * ox + oy * oy - r * r;
                            var disc = b * b - 4f * a * c;
                            if (disc >= 0f && a > 1e-6f)
                            {
                                var sqrtDisc = MathF.Sqrt(disc);
                                var tc = (-b - sqrtDisc) / (2f * a);
                                if (tc >= 0f && tc < earliestT)
                                {
                                    earliestT = tc;
                                    var cornerHitX = x + dx * tc;
                                    var cornerHitY = y + dy * tc;
                                    var cornerLen = MathF.Sqrt(MathF.Pow(cornerHitX - cx, 2) + MathF.Pow(cornerHitY - cy, 2));
                                    if (cornerLen > 1e-5f)
                                    {
                                        bestNx = (cornerHitX - cx) / cornerLen;
                                        bestNy = (cornerHitY - cy) / cornerLen;
                                    }
                                    else
                                    {
                                        bestNx = nx;
                                        bestNy = ny;
                                    }
                                    hit = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        return (hit, earliestT, bestNx, bestNy);
    }

    /// <summary>
    /// Pushes circle out if it penetrates any obstacle AABB.
    /// </summary>
    public (float X, float Y) ResolvePenetration(float x, float y)
    {
        var r = _radius;
        foreach (var obs in _map.Obstacles)
        {
            // Nearest point on AABB to circle center
            var closestX = Math.Clamp(x, obs.MinX, obs.MaxX);
            var closestY = Math.Clamp(y, obs.MinY, obs.MaxY);

            var diffX = x - closestX;
            var diffY = y - closestY;
            var distSq = diffX * diffX + diffY * diffY;

            if (distSq < r * r)
            {
                var dist = MathF.Sqrt(distSq);
                if (dist > 1e-5f)
                {
                    var pen = r - dist;
                    x += (diffX / dist) * pen;
                    y += (diffY / dist) * pen;
                }
                else
                {
                    // Center is strictly inside AABB: push out along minimal penetration axis
                    var left = x - obs.MinX;
                    var right = obs.MaxX - x;
                    var bottom = y - obs.MinY;
                    var top = obs.MaxY - y;
                    var minPen = MathF.Min(MathF.Min(left, right), MathF.Min(bottom, top));

                    if (minPen == left) x = obs.MinX - r;
                    else if (minPen == right) x = obs.MaxX + r;
                    else if (minPen == bottom) y = obs.MinY - r;
                    else y = obs.MaxY + r;
                }
            }
        }

        // Clamp to map bounds
        x = Math.Clamp(x, _map.MinX + r, _map.MaxX - r);
        y = Math.Clamp(y, _map.MinY + r, _map.MaxY - r);

        return (x, y);
    }
}
