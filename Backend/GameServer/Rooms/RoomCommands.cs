using System.Threading.Channels;
using JungleBackend.Rooms;

namespace GameServer.Rooms;

/// <summary>HTTP chỉ enqueue; mutations chạy tuần tự cùng UDP, tránh race vượt slot.</summary>
public sealed class RoomCommands
{
    private sealed record Work(Func<RoomManager, RoomResult> Run, TaskCompletionSource<RoomResult> Completion, CancellationToken Token);
    private readonly Channel<Work> _queue = Channel.CreateBounded<Work>(new BoundedChannelOptions(128)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    public async Task<RoomResult> Submit(Func<RoomManager, RoomResult> run, CancellationToken token)
    {
        var completion = new TaskCompletionSource<RoomResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        if (!_queue.Writer.TryWrite(new(run, completion, deadline.Token))) return new(503, new RoomError("RoomControlBusy"));
        try { return await completion.Task.WaitAsync(deadline.Token); }
        catch (OperationCanceledException) { return new(503, new RoomError("RoomControlTimeout")); }
    }
    public void Drain(RoomManager rooms)
    {
        for (var i = 0; i < 16 && _queue.Reader.TryRead(out var work); i++)
        {
            // Không tạo phòng ma khi request đã hết hạn trước khi được xử lý.
            if (work.Token.IsCancellationRequested) continue;
            try { work.Completion.TrySetResult(work.Run(rooms)); }
            catch (Exception) { work.Completion.TrySetResult(new(500, new RoomError("RoomControlFailed"))); }
        }
    }
}
