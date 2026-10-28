using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.MatchUi;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Domain.MatchUi;
using JungleMaze.Tests.App;
using NUnit.Framework;

namespace JungleMaze.Tests.MatchUi
{
    public sealed class NetworkKillcamReplaySourceTests
    {
        private static readonly Guid MatchA = new Guid("11111111-2222-3333-4444-555555555555");
        private static readonly Guid MatchB = new Guid("99999999-8888-7777-6666-555555555555");

        private EventBus _bus;
        private FakeNetwork _network;
        private NetworkKillcamReplaySource _source;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _network = new FakeNetwork(_bus);
            _source = new NetworkKillcamReplaySource(_network, _network, 150);
        }

        [TearDown]
        public void TearDown()
        {
            _source.Dispose();
        }

        private static KillcamFramePayload[] CreateDummyFrames(int count, float startX)
        {
            var frames = new KillcamFramePayload[count];
            for (var i = 0; i < count; i++)
            {
                frames[i] = new KillcamFramePayload
                {
                    ZoneX = 25f,
                    ZoneY = 25f,
                    ZoneRadius = 20f,
                    VictimX = startX + i,
                    VictimY = 10f,
                    VictimHp = (byte)Math.Max(0, 100 - i),
                    VictimFlags = 0,
                    KillerX = startX + i + 5f,
                    KillerY = 12f,
                    KillerHp = 100,
                    KillerFlags = 0,
                    KillerPresent = true
                };
            }
            return frames;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void InconsistentOrEmptyChunks_AreRejected(int fault)
        {
            _source.Request(new KillcamRequest(MatchA, 1, 2, 200, 150));
            var chunk = new KillcamDataChunkPacket { Version = 1, MatchId = MatchA, VictimNetId = 1,
                KillerNetId = 2, EndTick = 200, TotalFrames = 150, TotalChunks = 2, FrameCount = 75,
                Frames = CreateDummyFrames(75, 10f) };
            if (fault == 0) { chunk.FrameCount = 0; chunk.Frames = Array.Empty<KillcamFramePayload>(); }
            _network.Push(chunk);
            if (fault != 0)
            {
                chunk.ChunkIndex = 1;
                if (fault == 1) chunk.EndTick++;
                else chunk.KillerNetId = 3;
                _network.Push(chunk);
            }
            Assert.IsTrue(_source.TryTake(out var clip, out var reason));
            Assert.IsNull(clip);
            Assert.AreEqual(KillcamUnavailableReason.NoData, reason);
        }

        [Test]
        public void IncompleteChunks_TryTakeReturnsFalse_UntilAllChunksArrive()
        {
            var request = new KillcamRequest(MatchA, victimNetId: 1, killerNetId: 2, eliminationTick: 200, frameCount: 150);
            _source.Request(request);

            Assert.AreEqual(1, _network.Sent, "request packet sent to server");
            Assert.IsFalse(_source.TryTake(out _, out _), "pending: no chunks yet");

            // Push chunk 0 of 2 (75 frames)
            var chunk0 = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 150,
                ChunkIndex = 0,
                TotalChunks = 2,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 10f)
            };
            _network.Push(chunk0);

            Assert.IsFalse(_source.TryTake(out _, out _), "still pending: chunk 1 has not arrived");

            // Push chunk 1 of 2 (75 frames)
            var chunk1 = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 150,
                ChunkIndex = 1,
                TotalChunks = 2,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 85f)
            };
            _network.Push(chunk1);

            Assert.IsTrue(_source.TryTake(out var clip, out var reason), "all chunks arrived");
            Assert.AreEqual(KillcamUnavailableReason.None, reason);
            Assert.IsNotNull(clip);
            Assert.AreEqual(MatchA, clip.MatchId);
            Assert.AreEqual(1, clip.VictimNetId);
            Assert.AreEqual(2, clip.KillerNetId);
            Assert.AreEqual(200, clip.EndTick);
            Assert.AreEqual(150, clip.FrameCount);
        }

        [Test]
        public void DuplicateChunks_IgnoredWithoutCorruption()
        {
            var request = new KillcamRequest(MatchA, victimNetId: 1, killerNetId: 2, eliminationTick: 200, frameCount: 150);
            _source.Request(request);

            var chunk0 = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 150,
                ChunkIndex = 0,
                TotalChunks = 2,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 10f)
            };
            _network.Push(chunk0);
            _network.Push(chunk0); // Duplicate!

            Assert.IsFalse(_source.TryTake(out _, out _), "duplicate chunk 0 does not trigger completion early");

            var chunk1 = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 150,
                ChunkIndex = 1,
                TotalChunks = 2,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 85f)
            };
            _network.Push(chunk1);

            Assert.IsTrue(_source.TryTake(out var clip, out var reason));
            Assert.AreEqual(KillcamUnavailableReason.None, reason);
            Assert.AreEqual(150, clip.FrameCount);
        }

        [Test]
        public void OutOfOrderChunks_AssembledCorrectly()
        {
            var request = new KillcamRequest(MatchA, victimNetId: 0, killerNetId: 3, eliminationTick: 300, frameCount: 150);
            _source.Request(request);

            var chunk1 = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 0,
                KillerNetId = 3,
                EndTick = 300,
                TotalFrames = 150,
                ChunkIndex = 1,
                TotalChunks = 2,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 85f)
            };
            _network.Push(chunk1); // Chunk 1 first

            Assert.IsFalse(_source.TryTake(out _, out _));

            var chunk0 = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 0,
                KillerNetId = 3,
                EndTick = 300,
                TotalFrames = 150,
                ChunkIndex = 0,
                TotalChunks = 2,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 10f)
            };
            _network.Push(chunk0); // Chunk 0 second

            Assert.IsTrue(_source.TryTake(out var clip, out var reason));
            Assert.AreEqual(KillcamUnavailableReason.None, reason);
            Assert.AreEqual(150, clip.FrameCount);
            // Verify frame 0 has startX 10 and frame 75 has startX 85
            Assert.IsTrue(clip.TryGetPosition(0, 0f, out var x0, out _));
            Assert.AreEqual(10f, x0);
            Assert.IsTrue(clip.TryGetPosition(0, 75f, out var x75, out _));
            Assert.AreEqual(85f, x75);
        }

        [Test]
        public void Cancel_DropsSubsequentLateChunks_NoResurrection()
        {
            var request = new KillcamRequest(MatchA, victimNetId: 1, killerNetId: 2, eliminationTick: 200, frameCount: 150);
            _source.Request(request);

            var chunk0 = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 150,
                ChunkIndex = 0,
                TotalChunks = 2,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 10f)
            };
            _network.Push(chunk0);

            // User cancelled/skipped
            _source.Cancel();

            // Late chunk arrives
            var chunk1 = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 150,
                ChunkIndex = 1,
                TotalChunks = 2,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 85f)
            };
            _network.Push(chunk1);

            Assert.IsFalse(_source.TryTake(out var clip, out _), "late data does not resurrect cancelled session");
            Assert.IsNull(clip);
        }

        [Test]
        public void Release_ClearsClipData()
        {
            var request = new KillcamRequest(MatchA, victimNetId: 1, killerNetId: 2, eliminationTick: 200, frameCount: 150);
            _source.Request(request);

            var chunk0 = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 150,
                ChunkIndex = 0,
                TotalChunks = 2,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 10f)
            };
            var chunk1 = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 150,
                ChunkIndex = 1,
                TotalChunks = 2,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 85f)
            };
            _network.Push(chunk0);
            _network.Push(chunk1);

            Assert.IsTrue(_source.TryTake(out var clip, out _));
            Assert.AreEqual(150, clip.FrameCount);

            _source.Release();
            Assert.IsFalse(_source.TryTake(out _, out _));
            Assert.AreEqual(0, clip.FrameCount, "clip cleared on release");
        }

        [Test]
        public void WrongMatch_Rejected()
        {
            var request = new KillcamRequest(MatchA, victimNetId: 1, killerNetId: 2, eliminationTick: 200, frameCount: 150);
            _source.Request(request);

            var wrongMatchChunk = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchB, // Different match!
                Status = 0,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 150,
                ChunkIndex = 0,
                TotalChunks = 1,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 10f)
            };
            _network.Push(wrongMatchChunk);

            Assert.IsTrue(_source.TryTake(out var clip, out var reason));
            Assert.IsNull(clip);
            Assert.AreEqual(KillcamUnavailableReason.WrongMatch, reason);
        }

        [Test]
        public void WrongPlayer_Rejected()
        {
            var request = new KillcamRequest(MatchA, victimNetId: 1, killerNetId: 2, eliminationTick: 200, frameCount: 150);
            _source.Request(request);

            var wrongPlayerChunk = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 5, // Different victim!
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 150,
                ChunkIndex = 0,
                TotalChunks = 1,
                FrameCount = 75,
                Frames = CreateDummyFrames(75, 10f)
            };
            _network.Push(wrongPlayerChunk);

            Assert.IsTrue(_source.TryTake(out var clip, out var reason));
            Assert.IsNull(clip);
            Assert.AreEqual(KillcamUnavailableReason.WrongPlayer, reason);
        }

        [Test]
        public void ServerUnavailableStatus_PropagatedImmediately()
        {
            var request = new KillcamRequest(MatchA, victimNetId: 1, killerNetId: 2, eliminationTick: 200, frameCount: 150);
            _source.Request(request);

            var unavailableChunk = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = (byte)KillcamUnavailableReason.TooShort,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 0,
                ChunkIndex = 0,
                TotalChunks = 1,
                FrameCount = 0,
                Frames = Array.Empty<KillcamFramePayload>()
            };
            _network.Push(unavailableChunk);

            Assert.IsTrue(_source.TryTake(out var clip, out var reason));
            Assert.IsNull(clip);
            Assert.AreEqual(KillcamUnavailableReason.TooShort, reason);
        }

        [Test]
        public void TotalFramesLessThanRequest_FailsWithTooShort()
        {
            var request = new KillcamRequest(MatchA, victimNetId: 1, killerNetId: 2, eliminationTick: 200, frameCount: 150);
            _source.Request(request);

            var shortChunk = new KillcamDataChunkPacket
            {
                Version = KillcamDataChunkPacket.CurrentVersion,
                MatchId = MatchA,
                Status = 0,
                VictimNetId = 1,
                KillerNetId = 2,
                EndTick = 200,
                TotalFrames = 50, // Less than 150!
                ChunkIndex = 0,
                TotalChunks = 1,
                FrameCount = 50,
                Frames = CreateDummyFrames(50, 10f)
            };
            _network.Push(shortChunk);

            Assert.IsTrue(_source.TryTake(out var clip, out var reason));
            Assert.IsNull(clip);
            Assert.AreEqual(KillcamUnavailableReason.TooShort, reason);
        }
    }
}
