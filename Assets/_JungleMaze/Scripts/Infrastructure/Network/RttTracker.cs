using System;

namespace JungleMaze.Infrastructure.Network
{
    /// <summary>
    /// Đo RTT theo Ping/Pong sequence như SmokeSession: chỉ tính pong của sequence đã gửi và chưa quá hạn,
    /// bỏ pong trùng/đến muộn. Toàn bộ là mảng cố định, không cấp phát sau khi khởi tạo.
    /// </summary>
    public sealed class RttTracker
    {
        private readonly int[] _pendingSequence;
        private readonly long[] _pendingSentAt;
        private readonly double[] _samples;
        private readonly double[] _sortScratch;
        private readonly double _timeoutTicks;
        private readonly double _ticksPerMs;
        private int _sampleCount, _sampleNext;

        public RttTracker(int maxPending = 16, int sampleCapacity = 256, int pongTimeoutMs = 5000,
            long timestampFrequency = TimeSpan.TicksPerSecond)
        {
            _pendingSequence = new int[maxPending];
            _pendingSentAt = new long[maxPending];
            _samples = new double[sampleCapacity];
            _sortScratch = new double[sampleCapacity];
            _ticksPerMs = timestampFrequency / 1000.0;
            _timeoutTicks = pongTimeoutMs * _ticksPerMs;
        }

        public int Sent { get; private set; }
        public int Received { get; private set; }
        public int Lost { get; private set; }
        public double LastMs { get; private set; }
        public int Outstanding
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _pendingSequence.Length; i++) if (_pendingSequence[i] != 0) count++;
                return count;
            }
        }

        public void Reset()
        {
            Array.Clear(_pendingSequence, 0, _pendingSequence.Length);
            _sampleCount = _sampleNext = 0;
            Sent = Received = Lost = 0;
            LastMs = 0;
        }

        /// <summary>Ghi nhận ping đã gửi. Hết chỗ chờ thì ping cũ nhất bị tính là mất.</summary>
        public void OnPingSent(int sequence, long timestamp)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            var slot = -1;
            var oldest = 0;
            for (var i = 0; i < _pendingSequence.Length; i++)
            {
                if (_pendingSequence[i] == 0) { slot = i; break; }
                if (_pendingSentAt[i] < _pendingSentAt[oldest]) oldest = i;
            }
            if (slot < 0) { slot = oldest; Lost++; }
            _pendingSequence[slot] = sequence;
            _pendingSentAt[slot] = timestamp;
            Sent++;
        }

        /// <summary>Trả true nếu pong khớp một ping đang chờ; RTT được ghi vào mẫu.</summary>
        public bool OnPongReceived(int sequence, long timestamp)
        {
            for (var i = 0; i < _pendingSequence.Length; i++)
            {
                if (_pendingSequence[i] != sequence || sequence == 0) continue;
                _pendingSequence[i] = 0;
                LastMs = (timestamp - _pendingSentAt[i]) / _ticksPerMs;
                _samples[_sampleNext] = LastMs;
                _sampleNext = (_sampleNext + 1) % _samples.Length;
                if (_sampleCount < _samples.Length) _sampleCount++;
                Received++;
                return true;
            }
            return false;
        }

        /// <summary>Ping chờ quá pongTimeoutMs bị tính là mất (giống PongTimeout của SmokeSession).</summary>
        public void ExpirePending(long now)
        {
            for (var i = 0; i < _pendingSequence.Length; i++)
            {
                if (_pendingSequence[i] == 0 || now - _pendingSentAt[i] < _timeoutTicks) continue;
                _pendingSequence[i] = 0;
                Lost++;
            }
        }

        public double AverageMs
        {
            get
            {
                if (_sampleCount == 0) return 0;
                double sum = 0;
                for (var i = 0; i < _sampleCount; i++) sum += _samples[i];
                return sum / _sampleCount;
            }
        }

        /// <summary>Percentile kiểu nearest-rank, cùng công thức với SmokeSession để so sánh số đo.</summary>
        public double Percentile(double p)
        {
            if (_sampleCount == 0) return 0;
            Array.Copy(_samples, _sortScratch, _sampleCount);
            Array.Sort(_sortScratch, 0, _sampleCount);
            var rank = (int)Math.Ceiling(_sampleCount * p) - 1;
            if (rank < 0) rank = 0;
            return _sortScratch[rank];
        }
    }
}
