using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Player;

namespace JungleMaze.App.Player
{
    /// <summary>Số đo interpolation cho overlay/log/test.</summary>
    public struct InterpolationStats
    {
        public int Received, Duplicates, TooOld, OutOfOrder;
        /// <summary>Số lần sample phải ngoại suy (snapshot tới trễ/mất) và số lần đã chạm giới hạn ngoại suy.</summary>
        public int ExtrapolatedSamples, ClampedSamples, Samples;
        /// <summary>Số lần đồng hồ render nhảy (bắt đầu trận, sau khi WSS/UDP đứng lâu).</summary>
        public int ClockResets;
        public int Spawns, Despawns;
        public float JitterMs, RenderDelayMs, BufferedMs;
        public int MaxBufferDepth;
    }

    /// <summary>
    /// MOV-03 — hiển thị chuyển động của người chơi khác bằng snapshot interpolation.
    /// <list type="bullet">
    /// <item>Mỗi NetId (0..9, server cấp) có một <see cref="SnapshotBuffer"/> riêng; spawn/despawn theo event của MatchSession.</item>
    /// <item>Đồng hồ render = ước lượng tick server − <see cref="DelaySeconds"/> (mặc định 100ms), nên luôn có hai snapshot
    /// bao quanh thời điểm vẽ dù gói đến lệch nhịp (jitter) hoặc mất lẻ tẻ.</item>
    /// <item>Thiếu snapshot mới: ngoại suy tối đa <see cref="MaxExtrapolationSeconds"/> rồi đứng yên, không trôi mãi.</item>
    /// <item>Đồng hồ lệch quá <see cref="ResetThresholdTicks"/> (WSS dồn gói sau khi nghẽn) thì nhảy tới hiện tại thay vì phát lại state cũ.</item>
    /// </list>
    /// Chỉ đọc state server; không sửa PlayerModel, không tự cấp NetId và không dự đoán nhân vật của mình (việc của MOV-02).
    /// </summary>
    public sealed class RemotePlayerInterpolator : IInitializable, ITickable, IDisposable
    {
        public const int MaxPlayers = MatchProtocol.MaxPlayers;
        public const float TickRate = GameplayProtocol.TickRate;
        public const float ResetThresholdTicks = 15f;
        public const float TeleportDistance = 3f;
        private const float OffsetSmoothing = 0.05f;

        private readonly IEventBus _bus;
        private readonly PlayerRegistry _players;
        private readonly SnapshotBuffer[] _buffers = new SnapshotBuffer[MaxPlayers];
        private readonly Action<PlayerStateUpdated> _onUpdated;
        private readonly Action<PlayerRemoved> _onRemoved;
        private double _localTicks, _offset, _renderTick;
        private double _newestTick;
        private float _jitterTicks;
        private bool _clockStarted;
        private InterpolationStats _stats;

        public RemotePlayerInterpolator(IEventBus bus, PlayerRegistry players)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _onUpdated = OnUpdated;
            _onRemoved = e => Remove(e.NetId);
            for (var i = 0; i < _buffers.Length; i++) _buffers[i] = new SnapshotBuffer();
        }

        public float DelaySeconds { get; set; } = 0.1f;
        public float MaxExtrapolationSeconds { get; set; } = 0.1f;
        public double RenderTick => _renderTick;
        public bool ClockStarted => _clockStarted;

        public InterpolationStats Stats
        {
            get
            {
                var s = _stats;
                s.JitterMs = _jitterTicks * 1000f / TickRate;
                s.RenderDelayMs = DelaySeconds * 1000f;
                // Không còn ai (hết trận/rời phòng) thì không có gì đang đệm; âm nghĩa là đang thiếu snapshot.
                s.BufferedMs = _clockStarted && !AllEmpty() ? (float)((_newestTick - _renderTick) * 1000.0 / TickRate) : 0f;
                return s;
            }
        }

        public void Initialize()
        {
            _bus.Subscribe(_onUpdated);
            _bus.Subscribe(_onRemoved);
        }

        public void Dispose()
        {
            _bus.Unsubscribe(_onUpdated);
            _bus.Unsubscribe(_onRemoved);
        }

        public void ResetStats() => _stats = default;

        /// <summary>Thêm snapshot server của một người chơi (MatchSession đã lọc MatchId và tick cũ của PlayerModel).</summary>
        public void Push(byte netId, in PlayerSnapshot snapshot)
        {
            if (netId >= MaxPlayers) return;
            var buffer = _buffers[netId];
            if (buffer.IsEmpty)
            {
                _stats.Spawns++;
                // Không còn ai trong buffer (trận mới, vừa reconnect): tick server có thể đã đổi mốc, đo lại đồng hồ.
                if (AllEmpty()) _clockStarted = false;
            }
            switch (buffer.Insert(snapshot))
            {
                case SnapshotInsert.Duplicate: _stats.Duplicates++; return;
                case SnapshotInsert.TooOld: _stats.TooOld++; return;
                case SnapshotInsert.AddedOutOfOrder: _stats.OutOfOrder++; break;
            }
            _stats.Received++;
            if (buffer.Count > _stats.MaxBufferDepth) _stats.MaxBufferDepth = buffer.Count;
            ObserveTick(snapshot.Tick);
        }

        /// <summary>Despawn: xóa buffer để lần spawn sau (rejoin, trận mới) không nội suy từ vị trí cũ → không có ghost.</summary>
        public void Remove(byte netId)
        {
            if (netId >= MaxPlayers || _buffers[netId].IsEmpty) return;
            _buffers[netId].Clear();
            _stats.Despawns++;
        }

        public void Clear()
        {
            for (var i = 0; i < _buffers.Length; i++) _buffers[i].Clear();
            _clockStarted = false;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            _localTicks += deltaTime * TickRate;
            if (!_clockStarted) return;
            var target = _localTicks + _offset - DelaySeconds * TickRate;
            // Không lùi đồng hồ (hình giật ngược); offset giảm thì chỉ chậm lại cho tới khi khớp.
            if (target > _renderTick) _renderTick = target;
            for (var i = 0; i < _buffers.Length; i++) _buffers[i].TrimBefore(_renderTick - 1);
        }

        public bool TryGetPose(byte netId, out PlayerPose pose)
        {
            pose = default;
            if (netId >= MaxPlayers || !_clockStarted) return false;
            var buffer = _buffers[netId];
            if (buffer.IsEmpty) return false;
            var maxTicks = MaxExtrapolationSeconds * TickRate;
            if (!buffer.TrySample(_renderTick, maxTicks, TeleportDistance, out pose, out var extrapolated)) return false;
            _stats.Samples++;
            if (extrapolated)
            {
                _stats.ExtrapolatedSamples++;
                if (_renderTick - SnapshotTick(buffer.Newest.Tick) >= maxTicks) _stats.ClampedSamples++;
            }
            return true;
        }

        public int BufferedCount(byte netId) => netId < MaxPlayers ? _buffers[netId].Count : 0;

        private bool AllEmpty()
        {
            for (var i = 0; i < _buffers.Length; i++) if (!_buffers[i].IsEmpty) return false;
            return true;
        }

        private void OnUpdated(PlayerStateUpdated e)
        {
            if (_players.TryGet(e.NetId, out var model) && model.HasState) Push(e.NetId, model.Current);
        }

        /// <summary>
        /// Ước lượng tick server từ thời điểm nhận: offset = tick − đồng hồ local, lọc trung bình mũ.
        /// Độ lệch tuyệt đối trung bình của mẫu so với offset là jitter (giống RFC 3550).
        /// </summary>
        private void ObserveTick(uint tick)
        {
            var asDouble = SnapshotTick(tick);
            // Tick lùi xa hơn mức gói trễ thông thường: server đã sang mốc tick khác, coi như timeline mới.
            if (_clockStarted && asDouble < _newestTick - 2 * ResetThresholdTicks)
            {
                _clockStarted = false;
                asDouble = tick;
            }
            if (!_clockStarted)
            {
                _clockStarted = true;
                _newestTick = asDouble;
                _offset = asDouble - _localTicks;
                _renderTick = asDouble - DelaySeconds * TickRate;
                _jitterTicks = 0f;
                _stats.ClockResets++;
                return;
            }
            if (asDouble <= _newestTick) return; // gói trễ: không dùng để chỉnh đồng hồ
            _newestTick = asDouble;
            var sample = asDouble - _localTicks;
            var deviation = sample - _offset;
            if (Math.Abs(deviation) > ResetThresholdTicks)
            {
                // Độ trễ đổi hẳn (sau nghẽn, đổi đường mạng): đo lại offset. Chỉ nhảy TỚI, không tua lùi để
                // phát lại state cũ; nếu gói giờ đến chậm hơn thì đồng hồ tự đứng chờ trong Tick.
                _offset = sample;
                _renderTick = Math.Max(_renderTick, asDouble - DelaySeconds * TickRate);
                for (var i = 0; i < _buffers.Length; i++) _buffers[i].TrimBefore(_renderTick - 1);
                _stats.ClockResets++;
                return;
            }
            _offset += deviation * OffsetSmoothing;
            _jitterTicks += ((float)Math.Abs(deviation) - _jitterTicks) / 16f;
        }

        /// <summary>Tick uint → double liên tục quanh tick mới nhất (chịu được uint quay vòng).</summary>
        private double SnapshotTick(uint tick)
        {
            if (!_clockStarted) return tick;
            var reference = (uint)(long)Math.Floor(_newestTick);
            return Math.Floor(_newestTick) + unchecked((int)(tick - reference));
        }
    }
}
