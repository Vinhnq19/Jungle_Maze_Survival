using System;
using System.Collections.Generic;
using JungleMaze.App.Ports;

namespace JungleMaze.App.Player
{
    /// <summary>
    /// MOV-02: bản sao phía client của <c>GameServer.Simulation.PhysicsSimulation</c> (circle-vs-AABB sweep + slide,
    /// clamp biên map, đẩy khỏi Hard Cover). Prediction chỉ đúng khi client và server cùng công thức, nên mọi thay đổi
    /// ở server phải sửa cả file này (test <c>MovementSimulationTests</c> giữ các số mẫu).
    /// Kinematics (chuẩn hóa đường chéo, speed × dt) dùng lại <see cref="HeroMotion"/> của MOV-01;
    /// vật cản là Hard Cover ENV-01 lấy từ runtime data đã kiểm hash với server.
    /// Không phụ thuộc UnityEngine và không cấp phát khi Move: chạy lại hàng chục input mỗi snapshot vẫn rẻ.
    /// </summary>
    public sealed class MovementSimulation
    {
        /// <summary>Thời gian một tick server (30Hz). Mỗi input ứng với đúng một bước mô phỏng.</summary>
        public const float TickSeconds = 1f / JungleBackend.Protocol.Gameplay.GameplayProtocol.TickRate;
        private const float Epsilon = 1e-6f;

        private readonly RuntimeCover[] _covers;
        private readonly float _minX, _maxX, _minY, _maxY;

        public MovementSimulation(RuntimeMapBounds bounds, IReadOnlyList<RuntimeCover> covers, float radius, float speed)
        {
            if (!IsFinite(radius) || !IsFinite(speed) || radius <= 0 || speed <= 0)
                throw new ArgumentException("Invalid movement config.");
            if (!(bounds.MinX < bounds.MaxX) || !(bounds.MinY < bounds.MaxY))
                throw new ArgumentException("Invalid map bounds.");
            Radius = radius;
            Speed = speed;
            _minX = bounds.MinX; _maxX = bounds.MaxX; _minY = bounds.MinY; _maxY = bounds.MaxY;
            _covers = new RuntimeCover[covers?.Count ?? 0];
            for (var i = 0; i < _covers.Length; i++) _covers[i] = covers[i];
        }

        public float Radius { get; }
        public float Speed { get; }

        /// <summary>Server lượng tử hướng đi thành sbyte rồi chia 127; client phải dùng đúng giá trị đó để khớp.</summary>
        public static float Dequantize(sbyte axis) => axis / 127f;

        /// <summary>Một bước di chuyển giống server: sweep tối đa 2 lần (đi + trượt), clamp biên, đẩy khỏi vật cản.</summary>
        public (float X, float Y) Move(float x, float y, float moveX, float moveY, float deltaTime)
        {
            if (deltaTime <= 0f) return (x, y);
            var (dispX, dispY) = HeroMotion.ComputeMoveDelta(moveX, moveY, Speed, deltaTime);
            if (Math.Abs(dispX) < Epsilon && Math.Abs(dispY) < Epsilon) return ResolvePenetration(x, y);

            for (var iteration = 0; iteration < 2; iteration++)
            {
                if (Math.Abs(dispX) < Epsilon && Math.Abs(dispY) < Epsilon) break;
                if (!Sweep(x, y, dispX, dispY, out var t, out var nx, out var ny))
                {
                    x += dispX;
                    y += dispY;
                    break;
                }
                // Dừng sát điểm chạm (lùi epsilon để không lún lại), phần còn lại chiếu lên mặt tường để trượt.
                var safeT = Math.Max(0f, t - 1e-4f);
                x += dispX * safeT;
                y += dispY * safeT;
                var remX = dispX * (1f - t);
                var remY = dispY * (1f - t);
                var dot = remX * nx + remY * ny;
                dispX = remX - dot * nx;
                dispY = remY - dot * ny;
            }

            x = Clamp(x, _minX + Radius, _maxX - Radius);
            y = Clamp(y, _minY + Radius, _maxY - Radius);
            return ResolvePenetration(x, y);
        }

        /// <summary>Đẩy hình tròn ra khỏi mọi AABB nó đang lấn vào, rồi clamp vào map.</summary>
        public (float X, float Y) ResolvePenetration(float x, float y)
        {
            var r = Radius;
            for (var i = 0; i < _covers.Length; i++)
            {
                var c = _covers[i];
                var diffX = x - Clamp(x, c.MinX, c.MaxX);
                var diffY = y - Clamp(y, c.MinY, c.MaxY);
                var distSq = diffX * diffX + diffY * diffY;
                if (distSq >= r * r) continue;
                var dist = (float)Math.Sqrt(distSq);
                if (dist > 1e-5f)
                {
                    var penetration = r - dist;
                    x += diffX / dist * penetration;
                    y += diffY / dist * penetration;
                    continue;
                }
                // Tâm nằm hẳn trong hộp: đẩy theo trục lún ít nhất.
                var left = x - c.MinX; var right = c.MaxX - x;
                var bottom = y - c.MinY; var top = c.MaxY - y;
                var min = Math.Min(Math.Min(left, right), Math.Min(bottom, top));
                if (min == left) x = c.MinX - r;
                else if (min == right) x = c.MaxX + r;
                else if (min == bottom) y = c.MinY - r;
                else y = c.MaxY + r;
            }
            return (Clamp(x, _minX + r, _maxX - r), Clamp(y, _minY + r, _maxY - r));
        }

        /// <summary>True nếu hình tròn bán kính Radius tại (x, y) lấn vào Hard Cover hoặc ra ngoài map (dùng cho test/QA).</summary>
        public bool Overlaps(float x, float y, float tolerance = 1e-3f)
        {
            var r = Radius - tolerance;
            if (x < _minX + r || x > _maxX - r || y < _minY + r || y > _maxY - r) return true;
            for (var i = 0; i < _covers.Length; i++)
            {
                var c = _covers[i];
                var dx = x - Clamp(x, c.MinX, c.MaxX);
                var dy = y - Clamp(y, c.MinY, c.MaxY);
                if (dx * dx + dy * dy < r * r) return true;
            }
            return false;
        }

        private bool Sweep(float x, float y, float dx, float dy, out float earliestT, out float bestNx, out float bestNy)
        {
            earliestT = 1f; bestNx = 0f; bestNy = 0f;
            var hit = false;
            var r = Radius;

            // 1. Biên map.
            var minBoundX = _minX + r; var maxBoundX = _maxX - r;
            var minBoundY = _minY + r; var maxBoundY = _maxY - r;
            if (dx < 0f && x > minBoundX) TryHit((minBoundX - x) / dx, 1f, 0f, ref earliestT, ref bestNx, ref bestNy, ref hit);
            else if (dx > 0f && x < maxBoundX) TryHit((maxBoundX - x) / dx, -1f, 0f, ref earliestT, ref bestNx, ref bestNy, ref hit);
            if (dy < 0f && y > minBoundY) TryHit((minBoundY - y) / dy, 0f, 1f, ref earliestT, ref bestNx, ref bestNy, ref hit);
            else if (dy > 0f && y < maxBoundY) TryHit((maxBoundY - y) / dy, 0f, -1f, ref earliestT, ref bestNx, ref bestNy, ref hit);

            // 2. Từng Hard Cover: tia vs AABB nở thêm bán kính, góc hộp xử lý như hình tròn.
            for (var i = 0; i < _covers.Length; i++)
            {
                var c = _covers[i];
                var invDx = Math.Abs(dx) > Epsilon ? 1f / dx : float.PositiveInfinity;
                var tx1 = (c.MinX - r - x) * invDx;
                var tx2 = (c.MaxX + r - x) * invDx;
                var tNearX = Math.Min(tx1, tx2); var tFarX = Math.Max(tx1, tx2);
                var invDy = Math.Abs(dy) > Epsilon ? 1f / dy : float.PositiveInfinity;
                var ty1 = (c.MinY - r - y) * invDy;
                var ty2 = (c.MaxY + r - y) * invDy;
                var tNearY = Math.Min(ty1, ty2); var tFarY = Math.Max(ty1, ty2);
                var tEnter = Math.Max(tNearX, tNearY);
                var tExit = Math.Min(tFarX, tFarY);
                if (!(tExit >= tEnter && tExit >= 0f && tEnter < earliestT)) continue;
                var candidate = Math.Max(0f, tEnter);
                if (candidate >= earliestT) continue;

                float nx, ny;
                if (tNearX > tNearY) { nx = dx > 0f ? -1f : 1f; ny = 0f; }
                else { nx = 0f; ny = dy > 0f ? -1f : 1f; }
                var hitX = x + dx * candidate;
                var hitY = y + dy * candidate;
                if ((nx != 0f && hitY >= c.MinY && hitY <= c.MaxY) || (ny != 0f && hitX >= c.MinX && hitX <= c.MaxX))
                {
                    earliestT = candidate; bestNx = nx; bestNy = ny; hit = true;
                    continue;
                }
                // Cùng thứ tự góc với server để kết quả trùng khi hai góc chạm cùng t.
                Corner(c.MinX, c.MinY, x, y, dx, dy, r, nx, ny, ref earliestT, ref bestNx, ref bestNy, ref hit);
                Corner(c.MaxX, c.MinY, x, y, dx, dy, r, nx, ny, ref earliestT, ref bestNx, ref bestNy, ref hit);
                Corner(c.MinX, c.MaxY, x, y, dx, dy, r, nx, ny, ref earliestT, ref bestNx, ref bestNy, ref hit);
                Corner(c.MaxX, c.MaxY, x, y, dx, dy, r, nx, ny, ref earliestT, ref bestNx, ref bestNy, ref hit);
            }
            return hit;
        }

        private static void TryHit(float t, float nx, float ny, ref float earliestT, ref float bestNx, ref float bestNy, ref bool hit)
        {
            if (t < 0f || t >= earliestT) return;
            earliestT = t; bestNx = nx; bestNy = ny; hit = true;
        }

        private static void Corner(float cx, float cy, float x, float y, float dx, float dy, float r, float fallbackNx, float fallbackNy,
            ref float earliestT, ref float bestNx, ref float bestNy, ref bool hit)
        {
            var ox = x - cx; var oy = y - cy;
            var a = dx * dx + dy * dy;
            var b = 2f * (ox * dx + oy * dy);
            var c = ox * ox + oy * oy - r * r;
            var disc = b * b - 4f * a * c;
            if (disc < 0f || a <= Epsilon) return;
            var t = (-b - (float)Math.Sqrt(disc)) / (2f * a);
            if (t < 0f || t >= earliestT) return;
            earliestT = t;
            var hx = x + dx * t - cx; var hy = y + dy * t - cy;
            var length = (float)Math.Sqrt(hx * hx + hy * hy);
            if (length > 1e-5f) { bestNx = hx / length; bestNy = hy / length; }
            else { bestNx = fallbackNx; bestNy = fallbackNy; }
            hit = true;
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
