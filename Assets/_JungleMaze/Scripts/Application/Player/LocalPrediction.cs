using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.Domain.Player;

namespace JungleMaze.App.Player
{
    /// <summary>Số đo prediction cho overlay/log/test. Khoảng cách tính bằng cell (1 world unit).</summary>
    public struct PredictionStats
    {
        /// <summary>Số input đã dự đoán và số input chưa được server xác nhận (ack).</summary>
        public int Predicted, Pending;
        /// <summary>Snapshot của mình đã dùng để reconcile; trùng/cũ bị bỏ qua.</summary>
        public int Reconciles, IgnoredSnapshots;
        /// <summary>Correction nhỏ được làm mượt; Snap là lệch quá xa nên dịch thẳng (respawn/resume).</summary>
        public int Corrections, Snaps;
        /// <summary>Input bị bỏ vì buffer đầy (server không ack quá lâu).</summary>
        public int Overflows;
        public float LastErrorMeters, MaxErrorMeters, AverageErrorMeters;
        /// <summary>Thời gian từ lúc gửi input tới lúc snapshot ack nó (gồm RTT + chờ tick server).</summary>
        public float LastAckRttMs, AverageAckRttMs;
    }

    /// <summary>
    /// MOV-02 — client-side prediction + server reconciliation cho nhân vật của chính mình.
    /// <list type="number">
    /// <item>Mỗi input gửi đi (30Hz) được mô phỏng NGAY bằng <see cref="MovementSimulation"/> và giữ trong buffer.</item>
    /// <item>Snapshot server mang <c>LastProcessedInputSeq</c> (ack): bỏ các input đã ack, lấy vị trí server làm gốc,
    /// chạy lại các input chưa ack → vị trí dự đoán mới.</item>
    /// <item>Chênh lệch giữa dự đoán cũ và mới được cộng vào offset hiển thị rồi giảm dần (không giật hình);
    /// lệch quá <see cref="SnapDistance"/> (respawn, resume) thì dịch thẳng.</item>
    /// </list>
    /// Server vẫn quyết định vị trí: client không bao giờ gửi tọa độ, chỉ gửi input.
    /// </summary>
    public sealed class LocalPrediction
    {
        public const int Capacity = 128;
        public const float SnapDistance = 2f;
        public const float SmoothingSeconds = 0.1f;
        public const float CorrectionEpsilon = 0.01f;

        private struct PendingInput
        {
            public ushort Seq;
            public float MoveX, MoveY;
            public float MovementMultiplier;
            public double SentAt;
        }

        private readonly PendingInput[] _pending = new PendingInput[Capacity];
        private MovementSimulation _world;
        private int _head, _count;
        private float _x, _y, _prevX, _prevY, _offsetX, _offsetY, _aim, _sinceInput;
        private double _now, _errorSum;
        private uint _lastAuthorityTick;
        private bool _hasAuthority, _dead;
        private PredictionStats _stats;

        public bool HasWorld => _world != null;
        /// <summary>Đang dự đoán: có map, đã có snapshot gốc từ server và nhân vật còn sống.</summary>
        public bool IsActive => _world != null && _hasAuthority && !_dead;
        public MovementSimulation World => _world;
        /// <summary>Vị trí logic sau input mới nhất (chưa cộng offset làm mượt).</summary>
        public float PredictedX => _x;
        public float PredictedY => _y;
        /// <summary>Authoritative status multiplier applied to new inputs and retained separately in replay history.</summary>
        public float MovementMultiplier { get; set; } = 1f;

        public PredictionStats Stats
        {
            get
            {
                var s = _stats;
                s.Pending = _count;
                s.AverageErrorMeters = s.Reconciles > 0 ? (float)(_errorSum / s.Reconciles) : 0f;
                return s;
            }
        }

        /// <summary>Gắn map đã kiểm hash. Giữ qua các trận cùng map; đổi map thì gọi lại.</summary>
        public void SetWorld(MovementSimulation world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Reset();
        }

        /// <summary>Bỏ mọi input/gốc server (mất kết nối, đổi trận). Thống kê giữ lại để log cuối phiên.</summary>
        public void Reset()
        {
            _head = _count = 0;
            _hasAuthority = _dead = false;
            _offsetX = _offsetY = 0f;
            _sinceInput = 0f;
            MovementMultiplier=1f;
        }

        public void ResetStats()
        {
            _stats = default;
            _errorSum = 0;
        }

        /// <summary>Gọi ngay sau khi input đã được gửi: dự đoán một tick với đúng giá trị đã lượng tử hóa.</summary>
        public void RecordInput(in PlayerInputPacket input)
        {
            if (!IsActive) return;
            if (_count == Capacity)
            {
                // Server im lặng quá lâu: bỏ input cũ nhất, lần reconcile tới sẽ sửa phần lệch.
                _head = (_head + 1) % Capacity;
                _count--;
                _stats.Overflows++;
            }
            var mx = MovementSimulation.Dequantize(input.MoveX);
            var my = MovementSimulation.Dequantize(input.MoveY);
            _pending[(_head + _count) % Capacity] = new PendingInput { Seq = input.InputSeq, MoveX = mx, MoveY = my, SentAt = _now, MovementMultiplier=MovementMultiplier };
            _count++;
            _prevX = _x; _prevY = _y;
            (_x, _y) = _world.Move(_x, _y, mx, my, MovementSimulation.TickSeconds, MovementMultiplier);
            _aim = input.AimDegrees;
            _sinceInput = 0f;
            _stats.Predicted++;
        }

        /// <summary>Áp snapshot authoritative của chính mình (tick tăng dần; trùng/cũ bị bỏ).</summary>
        public void Reconcile(in PlayerSnapshot server)
        {
            if (_hasAuthority && unchecked((int)(server.Tick - _lastAuthorityTick)) <= 0) { _stats.IgnoredSnapshots++; return; }
            _lastAuthorityTick = server.Tick;
            _dead = server.IsDead;
            if (!_hasAuthority || _world == null || server.IsDead)
            {
                _hasAuthority = true;
                _head = _count = 0;
                _x = _prevX = server.X; _y = _prevY = server.Y;
                _offsetX = _offsetY = 0f;
                _aim = server.RotationDegrees;
                return;
            }
            _stats.Reconciles++;
            DropAcknowledged(server.LastProcessedInputSeq);

            // Gốc server + chạy lại input chưa ack.
            float x = server.X, y = server.Y, prevX = x, prevY = y;
            for (var i = 0; i < _count; i++)
            {
                var input = _pending[(_head + i) % Capacity];
                prevX = x; prevY = y;
                (x, y) = _world.Move(x, y, input.MoveX, input.MoveY, MovementSimulation.TickSeconds, input.MovementMultiplier);
            }

            var errX = _x - x; var errY = _y - y;
            var error = (float)Math.Sqrt(errX * errX + errY * errY);
            _stats.LastErrorMeters = error;
            _errorSum += error;
            if (error > _stats.MaxErrorMeters) _stats.MaxErrorMeters = error;

            if (error > SnapDistance)
            {
                _stats.Snaps++;
                _offsetX = _offsetY = 0f;
                _x = x; _y = y; _prevX = prevX; _prevY = prevY;
                return;
            }
            if (error > CorrectionEpsilon) _stats.Corrections++;
            // Giữ nguyên hình đang vẽ: dời cả prev/cur về quỹ đạo mới và bù đúng phần đó vào offset.
            _offsetX += errX; _offsetY += errY;
            _prevX -= errX; _prevY -= errY;
            _x = x; _y = y;
            if (_offsetX * _offsetX + _offsetY * _offsetY > SnapDistance * SnapDistance) _offsetX = _offsetY = 0f;
        }

        /// <summary>Gọi mỗi frame: tiến đồng hồ, giảm offset correction theo hàm mũ.</summary>
        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            _now += deltaTime;
            _sinceInput += deltaTime;
            var decay = (float)Math.Exp(-deltaTime / SmoothingSeconds);
            _offsetX *= decay; _offsetY *= decay;
            if (_offsetX * _offsetX + _offsetY * _offsetY < 1e-8f) _offsetX = _offsetY = 0f;
        }

        /// <summary>Pose để vẽ: nội suy giữa hai bước tick (mượt ở FPS cao) + offset correction, không lấn Hard Cover.</summary>
        public bool TryGetPose(out PlayerPose pose)
        {
            pose = default;
            if (!IsActive) return false;
            var alpha = Math.Min(1f, _sinceInput / MovementSimulation.TickSeconds);
            var x = _prevX + (_x - _prevX) * alpha + _offsetX;
            var y = _prevY + (_y - _prevY) * alpha + _offsetY;
            (x, y) = _world.ResolvePenetration(x, y);
            pose = new PlayerPose { X = x, Y = y, RotationDegrees = _aim, IsDead = false };
            return true;
        }

        private void DropAcknowledged(ushort lastProcessed)
        {
            // Seq 0 nghĩa là server chưa xử lý input nào (trận mới/resume); MatchSession không bao giờ gửi seq 0.
            if (lastProcessed == 0) return;
            while (_count > 0)
            {
                var oldest = _pending[_head];
                if (unchecked((short)(oldest.Seq - lastProcessed)) > 0) break;
                if (oldest.Seq == lastProcessed)
                {
                    var rtt = (float)((_now - oldest.SentAt) * 1000.0);
                    _stats.LastAckRttMs = rtt;
                    _stats.AverageAckRttMs = _stats.AverageAckRttMs <= 0f ? rtt : _stats.AverageAckRttMs + (rtt - _stats.AverageAckRttMs) * 0.1f;
                }
                _head = (_head + 1) % Capacity;
                _count--;
            }
        }
    }
}
