using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using JungleMaze.Domain.Player;
using LiteNetLib.Utils;

namespace JungleMaze.Tests.Movement
{
    /// <summary>Điều kiện mạng của một chiều truyền. Reliable = mô phỏng WSS/TCP: không mất, không đảo thứ tự.</summary>
    public sealed class LinkProfile
    {
        public string Name = "ideal";
        public float RttMs, JitterMs, Loss, Duplicate;
        public bool Reliable;
        public int Seed = 1;
        public override string ToString() => Name;
    }

    /// <summary>
    /// Một chiều mạng giả: trễ RTT/2 ± jitter, mất gói, nhân đôi gói (UDP) hoặc giữ thứ tự (WSS).
    /// Gói "control" (phase/welcome — ReliableOrdered ở bản thật) không bao giờ mất.
    /// Toàn bộ chạy bằng đồng hồ giả lập, tất định theo Seed.
    /// </summary>
    public sealed class SimulatedLink
    {
        private struct Item { public double At; public long Order; public byte[] Data; }
        private readonly List<Item> _queue = new List<Item>();
        private readonly LinkProfile _profile;
        private readonly Random _rng;
        private double _lastOrderedAt;
        private long _order;
        public int Sent, Dropped, Duplicated, Delivered;

        public SimulatedLink(LinkProfile profile, int seedOffset)
        {
            _profile = profile;
            _rng = new Random(profile.Seed * 7919 + seedOffset);
        }

        public void Send(double now, byte[] data, bool reliable)
        {
            Sent++;
            var ordered = reliable || _profile.Reliable;
            if (!ordered && _rng.NextDouble() < _profile.Loss) { Dropped++; return; }
            Enqueue(now, data, ordered);
            if (!ordered && _rng.NextDouble() < _profile.Duplicate) { Duplicated++; Enqueue(now, data, false); }
        }

        public void Deliver(double now, Action<byte[]> handler)
        {
            while (true)
            {
                var best = -1;
                for (var i = 0; i < _queue.Count; i++)
                    if (_queue[i].At <= now && (best < 0 || _queue[i].At < _queue[best].At
                        || (_queue[i].At == _queue[best].At && _queue[i].Order < _queue[best].Order))) best = i;
                if (best < 0) return;
                var item = _queue[best];
                _queue.RemoveAt(best);
                Delivered++;
                handler(item.Data);
            }
        }

        private void Enqueue(double now, byte[] data, bool ordered)
        {
            var delayMs = _profile.RttMs / 2 + (_rng.NextDouble() * 2 - 1) * _profile.JitterMs;
            var at = now + Math.Max(0, delayMs) / 1000.0;
            // TCP/ReliableOrdered: gói sau không thể tới trước gói trước (head-of-line blocking).
            if (ordered) { at = Math.Max(at, _lastOrderedAt); _lastOrderedAt = at; }
            _queue.Add(new Item { At = at, Order = _order++, Data = data });
        }
    }

    /// <summary>
    /// Fake authoritative server theo contract CLI-02 và luật của MatchRuntime (SRV-03): 30Hz, input theo tick/seq,
    /// tối đa 2 input/tick, seq cũ bị bỏ, input cũ quá 250ms thì đứng, di chuyển bằng cùng công thức physics.
    /// Ghi lại vị trí thật theo tick để test so sánh hình client vẽ với "sự thật" của server.
    /// </summary>
    public sealed class FakeAuthoritativeServer
    {
        public sealed class Peer
        {
            public byte NetId;
            public SimulatedLink Up, Down;
            public float X, Y;
            public ushort Aim, LastSeq;
            public sbyte MoveX, MoveY;
            public bool HasInput;
            public uint LastInputTick, BudgetTick;
            public int Budget, Accepted, Rejected;
            public readonly Dictionary<uint, (float X, float Y)> History = new Dictionary<uint, (float, float)>();
        }

        private const uint StaleTicks = 8;
        public readonly Guid MatchId = Guid.NewGuid();
        public readonly List<Peer> Peers = new List<Peer>();
        private readonly MovementSimulation _world;
        private readonly PacketRegistry _registry = new PacketRegistry();
        private readonly NetDataWriter _writer = new NetDataWriter(true, 512);
        private readonly NetDataReader _reader = new NetDataReader();
        private Peer _sender;
        private double _nextTickAt;
        private double _now;

        public FakeAuthoritativeServer(MovementSimulation world)
        {
            _world = world;
            _registry.Subscribe<MatchInputPacket>(OnInput);
            _registry.Subscribe<ClientHelloPacket>(_ => { });
            _registry.Subscribe<SnapshotAckPacket>(_ => { });
            _registry.Subscribe<LoadAckPacket>(_ => { });
        }

        public uint Tick { get; private set; } = 100;

        public Peer AddPlayer(byte netId, float x, float y, SimulatedLink up, SimulatedLink down)
        {
            var peer = new Peer { NetId = netId, X = x, Y = y, Up = up, Down = down };
            Peers.Add(peer);
            return peer;
        }

        /// <summary>Gửi phase InGame + welcome (NetId, resume token) như server thật, qua kênh tin cậy.</summary>
        public void Start(double now)
        {
            _now = now;
            _nextTickAt = now;
            foreach (var peer in Peers)
            {
                var phase = new MatchPhasePacket { MatchId = MatchId, Phase = MatchPhase.InGame, ServerTick = Tick, PlayerCount = (byte)Peers.Count,
                    MinPlayers = 2, MaxPlayers = 10, ConfigVersion = "1.0.0", ConfigHash = "test" };
                SendTo(peer, ref phase, reliable: true);
                var welcome = new ResumeTokenPacket { MatchId = MatchId, NetId = peer.NetId, ResumeToken = Guid.NewGuid() };
                SendTo(peer, ref welcome, reliable: true);
            }
        }

        public void Update(double now)
        {
            _now = now;
            foreach (var peer in Peers)
            {
                _sender = peer;
                peer.Up.Deliver(now, data => { _reader.SetSource(data); _registry.TryReadFrame(_reader); });
            }
            while (now >= _nextTickAt)
            {
                Step();
                _nextTickAt += MovementSimulation.TickSeconds;
            }
        }

        /// <summary>Vị trí thật của một người chơi tại tick lẻ (nội suy giữa hai tick đã mô phỏng).</summary>
        public bool TryGetTruth(byte netId, double tick, out float x, out float y)
        {
            x = y = 0;
            foreach (var peer in Peers)
            {
                if (peer.NetId != netId) continue;
                var a = (uint)Math.Floor(tick);
                if (!peer.History.TryGetValue(a, out var pa) || !peer.History.TryGetValue(a + 1, out var pb)) return false;
                var t = (float)(tick - a);
                x = pa.X + (pb.X - pa.X) * t;
                y = pa.Y + (pb.Y - pa.Y) * t;
                return true;
            }
            return false;
        }

        private void OnInput(MatchInputPacket packet)
        {
            var peer = _sender;
            if (packet.MatchId != MatchId || !packet.Input.IsValid) { peer.Rejected++; return; }
            var tickDelta = unchecked((int)(packet.Input.Tick - Tick));
            if (tickDelta > 30 || tickDelta < -60) { peer.Rejected++; return; }
            if (peer.BudgetTick != Tick) { peer.BudgetTick = Tick; peer.Budget = 0; }
            if (++peer.Budget > 2) { peer.Rejected++; return; }
            if (peer.HasInput && unchecked((short)(packet.Input.InputSeq - peer.LastSeq)) <= 0) { peer.Rejected++; return; }
            peer.HasInput = true;
            peer.LastSeq = packet.Input.InputSeq;
            peer.LastInputTick = Tick;
            peer.MoveX = packet.Input.MoveX;
            peer.MoveY = packet.Input.MoveY;
            peer.Aim = packet.Input.Aim;
            peer.Accepted++;
        }

        private void Step()
        {
            Tick++;
            foreach (var peer in Peers)
            {
                var stale = !peer.HasInput || Tick - peer.LastInputTick >= StaleTicks;
                var mx = stale ? 0f : MovementSimulation.Dequantize(peer.MoveX);
                var my = stale ? 0f : MovementSimulation.Dequantize(peer.MoveY);
                (peer.X, peer.Y) = _world.Move(peer.X, peer.Y, mx, my, MovementSimulation.TickSeconds);
                peer.History[Tick] = (peer.X, peer.Y);
            }
            var snapshot = new WorldSnapshotPacket { MatchId = MatchId, ServerTick = Tick, PlayerCount = (byte)Peers.Count };
            for (var i = 0; i < Peers.Count; i++)
            {
                var p = Peers[i];
                snapshot.SetPlayer(i, new PlayerStatePacket { Tick = Tick, NetId = p.NetId, X = p.X, Y = p.Y, Rotation = p.Aim,
                    LastProcessedInputSeq = p.LastSeq, Hp = 100 });
            }
            foreach (var peer in Peers) SendTo(peer, ref snapshot, reliable: false);
        }

        private void SendTo<T>(Peer peer, ref T packet, bool reliable) where T : struct, INetSerializable
        {
            _registry.WriteFrame(_writer, ref packet);
            peer.Down.Send(_now, _writer.CopyData(), reliable);
        }
    }

    /// <summary>
    /// Client thật (MatchSession + LocalPrediction + RemotePlayerInterpolator + PlayerPoseSource) nối vào link giả.
    /// Thay NetworkClient: encode/decode bằng PacketRegistry giống hệt, phát packet qua EventBus giống IPacketReceiver thật.
    /// </summary>
    public sealed class TestMovementClient : INetworkClient, IPacketSender, IPacketReceiver, IDisposable
    {
        public readonly EventBus Bus = new EventBus();
        public readonly PlayerRegistry Players = new PlayerRegistry();
        public readonly MatchSession Match;
        public readonly RemotePlayerInterpolator Remote;
        public readonly PlayerPoseSource Poses;
        public readonly SimulatedLink Up, Down;
        private readonly PacketRegistry _registry = new PacketRegistry();
        private readonly EventBus _packets = new EventBus();
        private readonly HashSet<Type> _registered = new HashSet<Type>();
        private readonly NetDataWriter _writer = new NetDataWriter(true, 256);
        private readonly NetDataReader _reader = new NetDataReader();
        private double _now;

        public TestMovementClient(MovementSimulation world, LinkProfile profile, int index)
        {
            Up = new SimulatedLink(profile, index * 2);
            Down = new SimulatedLink(profile, index * 2 + 1);
            Match = new MatchSession(this, this, this, Bus, Players);
            Match.Prediction.SetWorld(world);
            Remote = new RemotePlayerInterpolator(Bus, Players);
            Poses = new PlayerPoseSource(Match, Remote);
            Match.Initialize();
            Remote.Initialize();
        }

        public NetworkState State { get; private set; }
        public DisconnectReason LastReason => DisconnectReason.None;
        public string PlayerId => "test";
        public string Endpoint => "simulated";
        public NetworkStats Stats => new NetworkStats { Transport = "Simulated" };
        public void Connect(in ConnectRequest request) { }
        public void Disconnect() => SetState(NetworkState.Disconnected);

        public void Join() => SetState(NetworkState.Joined);

        public void SetState(NetworkState state)
        {
            var previous = State;
            State = state;
            Bus.Publish(new NetworkStateChanged(previous, state, DisconnectReason.None));
        }

        /// <summary>Thứ tự giống frame Unity: NetworkManager nhận gói → TickRunner (MatchSession, interpolator).</summary>
        public void Frame(double now, float deltaTime)
        {
            _now = now;
            Down.Deliver(now, data => { _reader.SetSource(data); _registry.TryReadFrame(_reader); });
            Match.Tick(deltaTime);
            Remote.Tick(deltaTime);
        }

        public bool Send<T>(ref T packet, PacketDelivery delivery) where T : struct, INetSerializable
        {
            if (State != NetworkState.Joined) return false;
            _registry.WriteFrame(_writer, ref packet);
            Up.Send(_now, _writer.CopyData(), delivery == PacketDelivery.ReliableOrdered);
            return true;
        }

        public void Subscribe<T>(Action<T> handler) where T : struct, INetSerializable
        {
            if (_registered.Add(typeof(T))) _registry.Subscribe<T>(Publish);
            _packets.Subscribe(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct, INetSerializable => _packets.Unsubscribe(handler);

        private void Publish<T>(T packet) where T : struct => _packets.Publish(packet);

        public void Dispose()
        {
            Remote.Dispose();
            Match.Dispose();
        }
    }
}
