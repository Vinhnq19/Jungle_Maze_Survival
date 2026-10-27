using System;

namespace JungleMaze.Domain.Telemetry
{
    /// <summary>Frame-time summary of one perf_client window (Docs/Telemetry/Schema.vi.md section 8).</summary>
    public struct FrameSummary
    {
        public int Frames;
        public double WindowSeconds;
        public double FpsAvg;
        public double Fps1Low;
        public double FrameMsP95;
        public double FrameMsMax;
    }

    /// <summary>
    /// Frame times of one telemetry window in preallocated arrays: 0 allocations per frame and per summary.
    /// <list type="bullet">
    /// <item><c>fpsAvg</c> = frames / total frame time, over every frame added.</item>
    /// <item><c>fps1Low</c> = k / (sum of the k slowest frame times), k = max(1, ceil(0.01 · n)) (G-7).</item>
    /// <item><c>frameMsP95</c> = nearest rank ceil(0.95 · n) of the sorted frame times, like ServerMetrics.Percentile.</item>
    /// </list>
    /// Beyond <see cref="Capacity"/> frames in one window the oldest stored times are overwritten: averages still count
    /// every frame, the percentiles use the newest <see cref="Capacity"/>.
    /// </summary>
    public sealed class FrameStats
    {
        public const int DefaultCapacity = 2048;
        private readonly float[] _times;
        private readonly float[] _sorted;
        private int _next, _stored, _frames;
        private double _total;

        public FrameStats(int capacity = DefaultCapacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _times = new float[capacity];
            _sorted = new float[capacity];
        }

        public int Capacity => _times.Length;
        public int Frames => _frames;
        public double TotalSeconds => _total;

        /// <summary>Adds one frame time in seconds; zero, negative or non-finite values are ignored.</summary>
        public void Add(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds)) return;
            _times[_next] = deltaSeconds;
            _next = (_next + 1) % _times.Length;
            if (_stored < _times.Length) _stored++;
            _frames++;
            _total += deltaSeconds;
        }

        public void Reset()
        {
            _next = _stored = _frames = 0;
            _total = 0;
        }

        /// <summary>False when no frame was added since the last reset.</summary>
        public bool TrySummarize(out FrameSummary summary)
        {
            summary = default;
            if (_frames == 0 || _total <= 0) return false;
            Array.Copy(_times, _sorted, _stored);
            Sort(_sorted, 0, _stored - 1); // Array.Sort allocates under Mono
            var k = Math.Max(1, (int)Math.Ceiling(0.01 * _stored));
            double slowest = 0;
            for (var i = _stored - k; i < _stored; i++) slowest += _sorted[i];
            var p95 = Math.Max(1, (int)Math.Ceiling(0.95 * _stored));
            summary.Frames = _frames;
            summary.WindowSeconds = _total;
            summary.FpsAvg = _frames / _total;
            summary.Fps1Low = k / slowest;
            summary.FrameMsP95 = _sorted[p95 - 1] * 1000.0;
            summary.FrameMsMax = _sorted[_stored - 1] * 1000.0;
            return true;
        }

        /// <summary>In-place quicksort (median of three, insertion sort below 16), recursing into the smaller half.</summary>
        private static void Sort(float[] a, int lo, int hi)
        {
            while (hi - lo > 16)
            {
                var mid = lo + (hi - lo) / 2;
                if (a[mid] < a[lo]) Swap(a, mid, lo);
                if (a[hi] < a[lo]) Swap(a, hi, lo);
                if (a[hi] < a[mid]) Swap(a, hi, mid);
                var pivot = a[mid];
                int i = lo, j = hi;
                while (i <= j)
                {
                    while (a[i] < pivot) i++;
                    while (a[j] > pivot) j--;
                    if (i <= j) Swap(a, i++, j--);
                }
                if (j - lo < hi - i) { Sort(a, lo, j); lo = i; }
                else { Sort(a, i, hi); hi = j; }
            }
            for (var i = lo + 1; i <= hi; i++)
            {
                var v = a[i];
                var j = i - 1;
                while (j >= lo && a[j] > v) { a[j + 1] = a[j]; j--; }
                a[j + 1] = v;
            }
        }

        private static void Swap(float[] a, int i, int j)
        {
            var t = a[i];
            a[i] = a[j];
            a[j] = t;
        }
    }
}
