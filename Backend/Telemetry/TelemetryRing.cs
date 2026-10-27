#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace JungleMaze.Telemetry.Server;

/// <summary>
/// Hàng đợi MPSC có giới hạn kiểu Vyukov: mảng cấp sẵn lũy thừa 2, chỉ Interlocked/Volatile, không lock.
/// Một ring cho mỗi phòng giữ đúng thứ tự tín hiệu. Bản ghi bulk (snapshot) chỉ được vào khi ring đang dùng
/// ít hơn <see cref="BulkLimit"/> ô, nên luôn chừa chỗ cho bản ghi critical (vòng đời, damage, chết).
/// </summary>
public sealed class TelemetryRing
{
    private struct Cell
    {
        public long Sequence;
        public TelemetryEvent Value;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct PaddedLong { [FieldOffset(64)] public long Value; }

    private readonly Cell[] _cells;
    private readonly int _mask;
    private PaddedLong _enqueuePos;
    private PaddedLong _dequeuePos;
    private long _droppedCritical;
    private long _droppedBulk;

    public const int DefaultCapacity = 16384;
    public const int DefaultBulkLimit = 8192;

    public int Capacity => _cells.Length;
    public int BulkLimit { get; }
    public long DroppedCritical => Interlocked.Read(ref _droppedCritical);
    public long DroppedBulk => Interlocked.Read(ref _droppedBulk);
    public long Dropped => DroppedCritical + DroppedBulk;
    public int Count => (int)Math.Max(0, Volatile.Read(ref _enqueuePos.Value) - Volatile.Read(ref _dequeuePos.Value));

    public TelemetryRing(int capacity = DefaultCapacity, int bulkLimit = DefaultBulkLimit)
    {
        if (capacity < 2 || (capacity & (capacity - 1)) != 0) throw new ArgumentException("Ring capacity must be a power of two.");
        if (bulkLimit < 0 || bulkLimit > capacity) throw new ArgumentException("Bulk limit must be within capacity.");
        _cells = new Cell[capacity];
        _mask = capacity - 1;
        BulkLimit = bulkLimit;
        for (var i = 0; i < capacity; i++) _cells[i].Sequence = i;
    }

    /// <summary>An toàn cho nhiều luồng ghi. Trả false và tăng bộ đếm bỏ khi đầy.</summary>
    public bool TryWrite(in TelemetryEvent e, bool bulk = false)
    {
        if (bulk && Volatile.Read(ref _enqueuePos.Value) - Volatile.Read(ref _dequeuePos.Value) >= BulkLimit)
        {
            Interlocked.Increment(ref _droppedBulk);
            return false;
        }

        var pos = Volatile.Read(ref _enqueuePos.Value);
        while (true)
        {
            ref var cell = ref _cells[pos & _mask];
            var diff = Volatile.Read(ref cell.Sequence) - pos;
            if (diff == 0)
            {
                if (Interlocked.CompareExchange(ref _enqueuePos.Value, pos + 1, pos) == pos)
                {
                    cell.Value = e;
                    Volatile.Write(ref cell.Sequence, pos + 1);
                    return true;
                }
                pos = Volatile.Read(ref _enqueuePos.Value);
            }
            else if (diff < 0)
            {
                if (bulk) Interlocked.Increment(ref _droppedBulk);
                else Interlocked.Increment(ref _droppedCritical);
                return false;
            }
            else
            {
                pos = Volatile.Read(ref _enqueuePos.Value);
            }
        }
    }

    /// <summary>Chỉ một luồng đọc (luồng nền của writer). Xóa ô sau khi đọc để GC thu byte[] frame.</summary>
    public bool TryRead(out TelemetryEvent e)
    {
        var pos = _dequeuePos.Value;
        ref var cell = ref _cells[pos & _mask];
        if (Volatile.Read(ref cell.Sequence) - (pos + 1) == 0)
        {
            e = cell.Value;
            cell.Value = default;
            Volatile.Write(ref cell.Sequence, pos + _cells.Length);
            Volatile.Write(ref _dequeuePos.Value, pos + 1);
            return true;
        }
        e = default;
        return false;
    }
}
