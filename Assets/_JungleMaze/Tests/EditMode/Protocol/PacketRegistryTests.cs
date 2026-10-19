using JungleBackend.Protocol.Gameplay;
using LiteNetLib.Utils;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleMaze.Tests.Protocol
{
    public sealed class PacketRegistryTests
    {
        private const int HashBytes = 8;
        private PacketRegistry _registry;
        private NetDataWriter _writer;
        private NetDataReader _reader;
        private PlayerInputPacket _input;
        private PlayerStatePacket _state;
        private EventPacket _event;
        private int _received;

        [SetUp]
        public void SetUp()
        {
            _registry = new PacketRegistry();
            _writer = new NetDataWriter(true, 256);
            _reader = new NetDataReader();
            _received = 0;
            _registry.Subscribe<PlayerInputPacket>(p => { _input = p; _received++; });
            _registry.Subscribe<PlayerStatePacket>(p => { _state = p; _received++; });
            _registry.Subscribe<EventPacket>(p => { _event = p; _received++; });
        }

        private void RoundTrip()
        {
            _reader.SetSource(_writer.Data, 0, _writer.Length);
            Assert.IsTrue(_registry.TryReadAll(_reader));
        }

        [Test]
        public void PlayerInput_RoundTrip_BoundaryValues()
        {
            var packet = new PlayerInputPacket
            {
                Tick = uint.MaxValue, InputSeq = ushort.MaxValue,
                Buttons = InputButtons.Attack | InputButtons.Interact | InputButtons.Reload | InputButtons.Dash | InputButtons.UseItem
            };
            packet.SetMove(-5f, 1f);
            packet.SetAimDegrees(-90f);
            _registry.Write(_writer, ref packet);
            Assert.AreEqual(HashBytes + PlayerInputPacket.PayloadBytes, _writer.Length);
            RoundTrip();
            Assert.AreEqual(packet, _input);
            Assert.AreEqual(-127, _input.MoveX);
            Assert.AreEqual(127, _input.MoveY);
            Assert.AreEqual(270f, _input.AimDegrees, 0.01f);
            Assert.IsTrue(_input.IsValid);
            Assert.IsTrue(_input.Has(InputButtons.Dash));
        }

        [Test]
        public void PlayerInput_NaNMove_BecomesZero()
        {
            var packet = new PlayerInputPacket();
            packet.SetMove(float.NaN, 0.5f);
            Assert.AreEqual(0, packet.MoveX);
            Assert.AreEqual(64, packet.MoveY);
        }

        [Test]
        public void PlayerState_RoundTrip()
        {
            var packet = new PlayerStatePacket
            {
                Tick = 123456, NetId = 9, X = -12.5f, Y = 1000.25f, LastProcessedInputSeq = 65000,
                Hp = 255, Flags = PlayerStateFlags.Dead | PlayerStateFlags.Invulnerable
            };
            packet.SetRotationDegrees(359.99f);
            _registry.Write(_writer, ref packet);
            Assert.AreEqual(HashBytes + PlayerStatePacket.PayloadBytes, _writer.Length);
            RoundTrip();
            Assert.AreEqual(packet, _state);
            Assert.IsTrue(_state.IsDead);
        }

        [Test]
        public void Event_RoundTrip()
        {
            var packet = new EventPacket
            {
                Tick = 77, EventType = GameEventType.PlayerHit, SourceNetId = 1,
                TargetNetId = EventPacket.NoTarget, Param1 = int.MinValue, Param2 = int.MaxValue
            };
            _registry.Write(_writer, ref packet);
            Assert.AreEqual(HashBytes + EventPacket.PayloadBytes, _writer.Length);
            RoundTrip();
            Assert.AreEqual(packet, _event);
        }

        [Test]
        public void SeveralPacketsInOneWriter_AllDelivered()
        {
            var input = new PlayerInputPacket { Tick = 1 };
            var state = new PlayerStatePacket { Tick = 2 };
            var evt = new EventPacket { Tick = 3, EventType = GameEventType.MatchStarted };
            _registry.Write(_writer, ref input);
            _registry.Write(_writer, ref state);
            _registry.Write(_writer, ref evt);
            RoundTrip();
            Assert.AreEqual(3, _received);
        }

        [Test]
        public void Frame_HasMarker_AndReadsSeveralPackets()
        {
            var input = new PlayerInputPacket { Tick = 1 };
            var evt = new EventPacket { Tick = 2, EventType = GameEventType.MatchEnded };
            _registry.WriteFrame(_writer, ref input);
            _registry.Write(_writer, ref evt);
            Assert.AreEqual(GameplayProtocol.FrameMarker, _writer.Data[0]);
            _reader.SetSource(_writer.Data, 0, _writer.Length);
            Assert.IsTrue(PacketRegistry.IsFrame(_reader));
            Assert.IsTrue(_registry.TryReadFrame(_reader));
            Assert.AreEqual(2, _received);
        }

        [Test]
        public void ControlMessage_IsNotFrame()
        {
            // Ping v1: type 1 + sequence; không được nhầm là frame gameplay.
            JungleBackend.Protocol.NetworkProtocol.Write(_writer, JungleBackend.Protocol.NetworkProtocol.Ping, 5);
            _reader.SetSource(_writer.Data, 0, _writer.Length);
            Assert.IsFalse(PacketRegistry.IsFrame(_reader));
            Assert.IsFalse(_registry.TryReadFrame(_reader));
        }

        [Test]
        public void TruncatedPacket_IsRejected()
        {
            var packet = new PlayerStatePacket { Tick = 5 };
            _registry.Write(_writer, ref packet);
            _reader.SetSource(_writer.Data, 0, _writer.Length - 1);
            Assert.IsFalse(_registry.TryReadAll(_reader));
            Assert.AreEqual(0, _received);
        }

        [Test]
        public void UnknownHash_IsRejected()
        {
            _writer.Put(0x1234567890ABCDEFUL);
            _writer.Put(42);
            _reader.SetSource(_writer.Data, 0, _writer.Length);
            Assert.IsFalse(_registry.TryReadAll(_reader));
        }

        [Test]
        public void SerializeDeserialize_AllPackets_ZeroGcAlloc()
        {
            var input = new PlayerInputPacket { Tick = 1, InputSeq = 1, Buttons = InputButtons.Attack };
            var state = new PlayerStatePacket { Tick = 1, NetId = 1, X = 1f, Y = 2f, Hp = 100 };
            var evt = new EventPacket { Tick = 1, EventType = GameEventType.PlayerDied, Param1 = 5 };
            // Warm-up: JIT, hash cache, buffer writer đủ lớn.
            for (var i = 0; i < 4; i++) WriteReadAll(ref input, ref state, ref evt);

            Assert.That(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    input.Tick = state.Tick = evt.Tick = (uint)i;
                    input.SetMove(0.5f, -0.5f);
                    WriteReadAll(ref input, ref state, ref evt);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(9999u, _event.Tick);
        }

        private void WriteReadAll(ref PlayerInputPacket input, ref PlayerStatePacket state, ref EventPacket evt)
        {
            _writer.Reset();
            _registry.Write(_writer, ref input);
            _registry.Write(_writer, ref state);
            _registry.Write(_writer, ref evt);
            _reader.SetSource(_writer.Data, 0, _writer.Length);
            _registry.TryReadAll(_reader);
        }
    }
}
