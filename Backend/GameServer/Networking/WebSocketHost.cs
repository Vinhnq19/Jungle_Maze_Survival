using System.Net;
using System.Net.WebSockets;
using System.Threading.Channels;
using JungleBackend.Protocol;
using LiteNetLib.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking;

/// <summary>Async I/O posts bounded work to the same application thread as UDP.</summary>
public sealed class WebSocketHost : IAsyncDisposable
{
    private sealed class Peer(WebSocket socket) : IGamePeer
    {
        private readonly Channel<byte[]> _send = Channel.CreateBounded<byte[]>(64);
        private volatile bool _open = true;
        public readonly CancellationTokenSource Stop = new();
        public string Transport => "Wss";
        public bool IsOpen => _open;
        public bool TrySend(byte[] packet, bool reliable) => _open && _send.Writer.TryWrite(packet);
        public void Close(string reason)
        {
            if (!_open) return;
            _send.Writer.TryWrite(NetworkProtocol.StringMessage(NetworkProtocol.JoinRejected, reason).CopyData());
            _open = false; _send.Writer.TryComplete();
        }
        public void Abort() { _open = false; Stop.Cancel(); _send.Writer.TryComplete(); socket.Abort(); }
        public async Task SendLoop()
        {
            try
            {
                await foreach (var data in _send.Reader.ReadAllAsync(Stop.Token))
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Stop.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    await socket.SendAsync(data.AsMemory(), WebSocketMessageType.Binary, true, timeout.Token);
                }
                using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", closing.Token);
            }
            catch (Exception error) when (error is OperationCanceledException or WebSocketException or IOException) { }
            finally { Abort(); }
        }
    }
    private readonly SessionHub _hub;
    private readonly WebApplication _app;
    private readonly Channel<Action> _events = Channel.CreateBounded<Action>(new BoundedChannelOptions(1024) { SingleReader = true });
    private readonly SemaphoreSlim _handshakes;
    private WebSocketHost(SessionHub hub, int port, string bind, int maximum)
    {
        _hub = hub;
        // Unvalidated pre-join sockets are bounded separately from reserved player slots.
        _handshakes = new(maximum * 2);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{bind}:{port}");
        _app = builder.Build();
        _app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
        _app.MapGet("/health/network", () => Results.Ok(new { status = "ok", transports = new[] { "Udp", "Wss" } }));
        _app.Map("/game", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
            if (!await _handshakes.WaitAsync(0, context.RequestAborted)) { context.Response.StatusCode = 503; return; }
            try { await Run(context); } finally { _handshakes.Release(); }
        });
    }
    public static async Task<WebSocketHost?> StartFromEnvironment(SessionHub hub, int maximum)
    {
        var enabled = Environment.GetEnvironmentVariable("WS_ENABLED") ?? "false";
        if (!bool.TryParse(enabled, out var value)) throw new ArgumentException("WS_ENABLED phải true/false.");
        if (!value) return null;
        var bind = Environment.GetEnvironmentVariable("WS_BIND_ADDRESS") ?? "0.0.0.0";
        if (!IPAddress.TryParse(bind, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("WS_BIND_ADDRESS phải IPv4.");
        if (!int.TryParse(Environment.GetEnvironmentVariable("WS_PORT") ?? "7781", out var port) || port is < 1 or > 65535)
            throw new ArgumentException("WS_PORT không hợp lệ.");
        var host = new WebSocketHost(hub, port, bind, maximum);
        try { await host._app.StartAsync(); } catch { await host.DisposeAsync(); throw; }
        Console.WriteLine($"[websocket] enabled port={port} path=/game"); return host;
    }
    public void PollEvents() { for (var i = 0; i < 256 && _events.Reader.TryRead(out var action); i++) action(); }
    private bool Post(Peer peer, Action action)
    {
        if (_events.Writer.TryWrite(action)) return true;
        peer.Abort(); return false;
    }
    private async Task Run(HttpContext context)
    {
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var peer = new Peer(socket);
        using var stopping = context.RequestAborted.Register(peer.Abort);
        using var appStopping = _app.Lifetime.ApplicationStopping.Register(peer.Abort);
        var sender = peer.SendLoop();
        var first = true;
        try
        {
            while (peer.IsOpen)
            {
                var buffer = new byte[NetworkProtocol.MaxAuthBytes + 1];
                var used = 0;
                ValueWebSocketReceiveResult received;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(peer.Stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                do
                {
                    received = await socket.ReceiveAsync(buffer.AsMemory(used), timeout.Token);
                    if (received.MessageType == WebSocketMessageType.Close) return;
                    if (received.MessageType != WebSocketMessageType.Binary) { peer.Close("BinaryRequired"); return; }
                    used += received.Count;
                    if (used > NetworkProtocol.MaxAuthBytes) { peer.Close("PayloadTooLarge"); return; }
                } while (!received.EndOfMessage);
                var packet = buffer[..used];
                if (first)
                {
                    first = false;
                    if (!Post(peer, () =>
                    {
                        if (!peer.IsOpen) return;
                        if (!NetworkProtocol.TryReadConnect(new NetDataReader(packet), out var request, out var reason)) _hub.Reject(peer, reason);
                        else if (_hub.Join(request!, peer)) _hub.Connected(peer);
                    })) return;
                }
                else if (!Post(peer, () => _hub.Receive(peer, packet, true))) return;
            }
        }
        catch (Exception error) when (error is OperationCanceledException or WebSocketException or IOException) { peer.Close("ConnectionTimeout"); }
        finally
        {
            peer.Close("Disconnected");
            try { await sender.WaitAsync(TimeSpan.FromSeconds(3)); } catch (TimeoutException) { peer.Abort(); }
            peer.Abort(); Post(peer, () => _hub.Disconnect(peer));
        }
    }
    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        await _app.StopAsync(timeout.Token); await _app.DisposeAsync(); _handshakes.Dispose();
    }
}
