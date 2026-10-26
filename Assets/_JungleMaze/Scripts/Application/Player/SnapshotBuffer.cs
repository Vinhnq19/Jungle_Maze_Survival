using System;
using JungleMaze.Domain.Player;

namespace JungleMaze.App.Player
{
    /// <summary>Kết quả thêm một snapshot vào buffer.</summary>
    public enum SnapshotInsert : byte { Added, AddedOutOfOrder, Duplicate, TooOld }

    /// <summary>
    /// MOV-03 — buffer vòng cố định các snapshot của MỘT người chơi, luôn sắp theo tick tăng dần.
    /// Không cấp phát sau khi tạo; đầy thì bỏ snapshot cũ nhất nên không bao giờ tích state cũ.
    /// </summary>
    public sealed class SnapshotBuffer
    {
        public const int Capacity = 32;
        private readonly PlayerSnapshot[] _items = new PlayerSnapshot[Capacity];
        private int _start, _count;

        public int Count => _count;
        public bool IsEmpty => _count == 0;
        public PlayerSnapshot Oldest => _items[_start];
        public PlayerSnapshot Newest => _items[Index(_count - 1)];
        public PlayerSnapshot this[int i] => _items[Index(i)];

        public void Clear() => _start = _count = 0;

        public SnapshotInsert Insert(in PlayerSnapshot snapshot)
        {
            if (_count == 0)
            {
                _items[_start] = snapshot;
                _count = 1;
                return SnapshotInsert.Added;
            }
            var newest = Newest.Tick;
            var diff = unchecked((int)(snapshot.Tick - newest));
            if (diff > 0)
            {
                if (_count == Capacity) { _start = (_start + 1) % Capacity; _count--; }
                _items[Index(_count)] = snapshot;
                _count++;
                return SnapshotInsert.Added;
            }
            // Đến trễ (UDP đảo thứ tự): chèn đúng chỗ nếu còn mới hơn snapshot cũ nhất.
            var position = _count;
            while (position > 0)
            {
                var tick = this[position - 1].Tick;
                var d = unchecked((int)(snapshot.Tick - tick));
                if (d == 0) return SnapshotInsert.Duplicate;
                if (d > 0) break;
                position--;
            }
            if (position == 0) return SnapshotInsert.TooOld;
            if (_count == Capacity) { _start = (_start + 1) % Capacity; _count--; position--; }
            for (var i = _count; i > position; i--) _items[Index(i)] = _items[Index(i - 1)];
            _items[Index(position)] = snapshot;
            _count++;
            return SnapshotInsert.AddedOutOfOrder;
        }

        /// <summary>
        /// Bỏ các snapshot không còn cần: chỉ giữ MỘT snapshot có tick ≤ renderTick (làm mốc nội suy),
        /// nhưng luôn giữ hai snapshot cuối để còn vận tốc khi phải ngoại suy (mất gói liên tiếp).
        /// </summary>
        public void TrimBefore(double renderTick)
        {
            while (_count > 2 && TickAsDouble(this[1].Tick, renderTick) <= renderTick)
            {
                _start = (_start + 1) % Capacity;
                _count--;
            }
        }

        /// <summary>
        /// Pose tại renderTick. Nằm giữa hai snapshot → nội suy tuyến tính (góc theo cung ngắn).
        /// Vượt snapshot mới nhất → ngoại suy theo vận tốc cuối, tối đa maxExtrapolationTicks rồi đứng yên.
        /// </summary>
        public bool TrySample(double renderTick, float maxExtrapolationTicks, float teleportDistance, out PlayerPose pose, out bool extrapolated)
        {
            pose = default;
            extrapolated = false;
            if (_count == 0) return false;
            var first = this[0];
            if (_count == 1 || TickAsDouble(first.Tick, renderTick) >= renderTick)
            {
                pose = ToPose(first);
                return true;
            }
            for (var i = 0; i < _count - 1; i++)
            {
                var a = this[i];
                var b = this[i + 1];
                var tb = TickAsDouble(b.Tick, renderTick);
                if (tb < renderTick) continue;
                var ta = TickAsDouble(a.Tick, renderTick);
                var t = (float)((renderTick - ta) / (tb - ta));
                pose = Lerp(a, b, t, teleportDistance);
                return true;
            }

            // renderTick vượt snapshot mới nhất: ngoại suy có giới hạn.
            var last = this[_count - 1];
            var before = this[_count - 2];
            var lastTick = TickAsDouble(last.Tick, renderTick);
            var span = (float)(lastTick - TickAsDouble(before.Tick, renderTick));
            pose = ToPose(last);
            if (last.IsDead || span <= 0f || IsTeleport(before, last, teleportDistance)) return true;
            var ahead = (float)Math.Min(renderTick - lastTick, maxExtrapolationTicks);
            if (ahead <= 0f) return true;
            extrapolated = true;
            pose.X += (last.X - before.X) / span * ahead;
            pose.Y += (last.Y - before.Y) / span * ahead;
            return true;
        }

        private static PlayerPose Lerp(in PlayerSnapshot a, in PlayerSnapshot b, float t, float teleportDistance)
        {
            // Dịch chuyển tức thời (respawn/teleport): không kéo lê nhân vật xuyên map, đổi hẳn khi tới tick sau.
            if (IsTeleport(a, b, teleportDistance)) return ToPose(t >= 1f ? b : a);
            var delta = HeroMotion.NormalizeDegrees(b.RotationDegrees - a.RotationDegrees);
            return new PlayerPose
            {
                X = a.X + (b.X - a.X) * t,
                Y = a.Y + (b.Y - a.Y) * t,
                RotationDegrees = HeroMotion.NormalizeDegrees(a.RotationDegrees + delta * t),
                // Trạng thái rời rạc lấy theo snapshot đã "xảy ra" (≤ renderTick).
                IsDead = t >= 1f ? b.IsDead : a.IsDead
            };
        }

        private static bool IsTeleport(in PlayerSnapshot a, in PlayerSnapshot b, float teleportDistance)
        {
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            return dx * dx + dy * dy > teleportDistance * teleportDistance;
        }

        private static PlayerPose ToPose(in PlayerSnapshot s) =>
            new PlayerPose { X = s.X, Y = s.Y, RotationDegrees = s.RotationDegrees, IsDead = s.IsDead };

        /// <summary>Đổi tick uint sang double gần renderTick (an toàn khi uint quay vòng).</summary>
        private static double TickAsDouble(uint tick, double near)
        {
            var reference = (uint)(long)Math.Floor(near);
            return Math.Floor(near) + unchecked((int)(tick - reference));
        }

        private int Index(int i) => (_start + i) % Capacity;
    }
}
