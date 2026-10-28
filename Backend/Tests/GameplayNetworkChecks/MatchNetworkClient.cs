using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using JungleBackend.Protocol;
using JungleBackend.Protocol.Gameplay;
using LiteNetLib;
using LiteNetLib.Utils;

// Real application-packet probe shared by local integration checks; it is not a gameplay takeover bot.
public sealed class MatchNetworkClient : IAsyncDisposable
{
    private readonly PacketRegistry _registry = new();
    private readonly NetDataWriter _writer = new();
    private readonly ConcurrentQueue<byte[]> _received = new();
    private readonly CancellationTokenSource _stop = new();
    private NetManager? _udp;
    private NetPeer? _peer;
    private ClientWebSocket? _ws;
    private Task? _reader;
    private ushort _sequence;
    private bool _loaded;
    private long _nextPing;
    private int _pingSequence;
    private uint _lastTick;
    private bool _hasSnapshot;
    public string Transport { get; }
    public Guid AttemptId { get; } = Guid.NewGuid();
    public string? PlayerId { get; private set; }
    public string? Rejected { get; private set; }
    public Guid MatchId { get; private set; }
    public MatchPhase Phase { get; private set; }
    public byte OwnNetId { get; private set; } = 255;
    public Guid ResumeProof { get; private set; }
    public WorldSnapshotPacket Snapshot { get; private set; }
    public MatchResultPacket? Result { get; private set; }
    public ZoneStatePacket Zone { get; private set; }
    public readonly List<KillcamDataChunkPacket> KillcamChunks = new();
    public int SnapshotCount { get; private set; }
    public int DuplicateSnapshots { get; private set; }
    public int InvalidFrames { get; private set; }
    public int SentInputs { get; private set; }
    public bool AutoReady { get; set; } = true;

    public MatchNetworkClient(string transport)
    {
        Transport = transport;
        _registry.Subscribe<MatchPhasePacket>(p =>
        {
            if (MatchId != p.MatchId) { _hasSnapshot = false; _loaded = false; }
            MatchId = p.MatchId; Phase = p.Phase;
            if (Phase != MatchPhase.Loading) _loaded = false;
        });
        _registry.Subscribe<WorldSnapshotPacket>(p =>
        {
            if (p.MatchId != MatchId) return;
            if (_hasSnapshot && p.ServerTick == _lastTick) { DuplicateSnapshots++; return; }
            if (_hasSnapshot && unchecked((int)(p.ServerTick - _lastTick)) < 0) return;
            _hasSnapshot = true; _lastTick = p.ServerTick; Snapshot = p; SnapshotCount++;
        });
        _registry.Subscribe<ResumeTokenPacket>(p => { if (p.MatchId == MatchId) { OwnNetId = p.NetId; ResumeProof = p.ResumeToken; } });
        _registry.Subscribe<ZoneStatePacket>(p => { if (p.MatchId == MatchId) Zone = p; });
        _registry.Subscribe<MatchResultPacket>(p => { if (p.MatchId == MatchId) Result = p; });
        _registry.Subscribe<MatchRosterPacket>(_ => { });
        _registry.Subscribe<MatchEntityEventPacket>(_ => { });
        _registry.Subscribe<MatchEliminationEventPacket>(_ => { });
        _registry.Subscribe<KillcamDataChunkPacket>(p => { if (p.MatchId == MatchId && KillcamChunks.Count < 2) KillcamChunks.Add(p); });
        _registry.Subscribe<EventPacket>(_ => { });
    }

    public async Task ConnectAsync(string host, int udpPort, string wsUrl, string code, Guid resume = default)
    {
        var data = NetworkProtocol.WriteConnect(JoinAuthMode.RoomCode, code, "SameName", NetworkProtocol.Version3, AttemptId, resume);
        if (Transport == "Udp")
        {
            var listener = new EventBasedNetListener();
            listener.NetworkReceiveEvent += (_, reader, _, _) => _received.Enqueue(reader.GetRemainingBytes());
            listener.PeerDisconnectedEvent += (_, info) =>
            {
                if (NetworkProtocol.TryReadStringMessage(info.AdditionalData, NetworkProtocol.JoinRejected, 4096, 100, out var reason)) Rejected = reason;
            };
            _udp = new NetManager(listener) { AutoRecycle = true, ChannelsCount = 2, IPv6Enabled = false };
            _udp.Start(); _peer = _udp.Connect(new IPEndPoint(IPAddress.Parse(host), udpPort), data);
        }
        else
        {
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(new Uri(wsUrl), _stop.Token);
            await _ws.SendAsync(data.CopyData().AsMemory(), WebSocketMessageType.Binary, true, _stop.Token);
            _reader = ReadWebSocket();
        }
    }
    private async Task ReadWebSocket()
    {
        try
        {
            while (!_stop.IsCancellationRequested && _ws!.State == WebSocketState.Open)
            {
                var bytes = new byte[4097]; var size = 0;
                ValueWebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(bytes.AsMemory(size), _stop.Token);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    size += result.Count;
                    if (size > 4096 || result.MessageType != WebSocketMessageType.Binary) throw new InvalidOperationException("Invalid WS frame");
                } while (!result.EndOfMessage);
                _received.Enqueue(bytes[..size]);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException or IOException) { }
    }
    public void Poll()
    {
        _udp?.PollEvents();
        var now=Environment.TickCount64;
        if(PlayerId!=null&&now>=_nextPing)
        {
            var ping=new NetDataWriter();NetworkProtocol.Write(ping,NetworkProtocol.Ping,++_pingSequence);
            if(_peer!=null)_peer.Send(ping,0,DeliveryMethod.Unreliable);
            else if(_ws?.State==WebSocketState.Open)_ws.SendAsync(ping.CopyData().AsMemory(),WebSocketMessageType.Binary,true,_stop.Token).AsTask().GetAwaiter().GetResult();
            _nextPing=now+1000;
        }
        while (_received.TryDequeue(out var bytes))
        {
            var reader = new NetDataReader(bytes);
            if (PacketRegistry.IsFrame(reader)) { if (!_registry.TryReadFrame(reader)) InvalidFrames++; continue; }
            if (NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinAccepted, 256, 64, out var id))
            {
                PlayerId = id;
                if (AutoReady) Hello();
                continue;
            }
            reader.SetSource(bytes);
            if (NetworkProtocol.TryReadStringMessage(reader, NetworkProtocol.JoinRejected, 4096, 100, out var reason)) Rejected = reason;
        }
        if (AutoReady && PlayerId != null && Phase == MatchPhase.Loading && !_loaded)
        {
            var ack = new LoadAckPacket { MatchId = MatchId, Ready = true };
            Send(ref ack, true); _loaded = true;
        }
    }
    public void Hello()
    {
        var hello = new ClientHelloPacket { ProtocolVersion = MatchProtocol.Version, Ready = true };
        Send(ref hello, true);
    }
    public void Attach(string ticket)
    { var packet = new AttachIdentityPacket { Ticket = ticket }; Send(ref packet, true); }
    public void Input(float x, float y)
    {
        if (Phase != MatchPhase.InGame || !_hasSnapshot) return;
        var input = new PlayerInputPacket { Tick = Snapshot.ServerTick, InputSeq = ++_sequence };
        input.SetMove(x, y);
        var scoped = new MatchInputPacket { MatchId = MatchId, Input = input };
        Send(ref scoped, false); SentInputs++;
    }
    public void Replay()
    { var ack = new ReplayAckPacket { MatchId = MatchId }; Send(ref ack, true); }
    public void Send<T>(ref T packet, bool reliable) where T : struct, INetSerializable
    {
        _registry.WriteFrame(_writer, ref packet);
        if (_peer != null) _peer.Send(_writer, GameplayProtocol.GameplayChannel, reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable);
        else if (_ws?.State == WebSocketState.Open) _ws.SendAsync(_writer.CopyData().AsMemory(), WebSocketMessageType.Binary, true, _stop.Token).AsTask().GetAwaiter().GetResult();
    }
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel(); _udp?.Stop(); _ws?.Abort();
        if (_reader != null) try { await _reader; } catch (OperationCanceledException) { }
        _ws?.Dispose(); _stop.Dispose();
    }
}
