using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Ports;
using JungleMaze.Domain.MatchUi;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// Real network implementation of <see cref="IKillcamReplaySource"/> over <see cref="IPacketSender"/> and <see cref="IPacketReceiver"/>.
    /// Sends a validated <see cref="KillcamRequestPacket"/> and joins bounded network chunks into a single pre-allocated <see cref="KillcamClip"/>.
    /// Protects against duplicate, out-of-order, oversized, or late-resurrected packets.
    /// </summary>
    public sealed class NetworkKillcamReplaySource : IKillcamReplaySource, IDisposable
    {
        private readonly IPacketSender _sender;
        private readonly IPacketReceiver _receiver;
        private readonly Action<KillcamDataChunkPacket> _onChunk;
        private readonly KillcamClip _clip;
        private KillcamRequest _currentRequest;
        private bool _isPending;
        private bool _isComplete;
        private KillcamClip _clipResult;
        private KillcamUnavailableReason _reason;
        private int _totalChunks;
        private int _receivedChunks;
        private int _chunkMask;
        private bool _subscribed;
        private bool _disposed;

        public NetworkKillcamReplaySource(IPacketSender sender, IPacketReceiver receiver, int frameCapacity = 150)
        {
            _sender = sender;
            _receiver = receiver;
            _clip = new KillcamClip(frameCapacity > 0 ? frameCapacity : 150);
            _onChunk = OnChunkReceived;
            if (_receiver != null)
            {
                _receiver.Subscribe(_onChunk);
                _subscribed = true;
            }
        }

        public void Request(in KillcamRequest request)
        {
            if (_disposed) return;
            _clip.Clear();
            _isPending = true;
            _isComplete = false;
            _clipResult = null;
            _reason = KillcamUnavailableReason.None;
            _currentRequest = request;
            _totalChunks = 0;
            _receivedChunks = 0;
            _chunkMask = 0;

            if (request.MatchId == Guid.Empty || request.VictimNetId >= KillcamClip.MaxPlayers)
            {
                Fail(KillcamUnavailableReason.WrongMatch);
                return;
            }

            if (_sender == null)
            {
                Fail(KillcamUnavailableReason.NoData);
                return;
            }

            var packet = new KillcamRequestPacket
            {
                Version = KillcamRequestPacket.CurrentVersion,
                MatchId = request.MatchId,
                VictimNetId = request.VictimNetId
            };

            if (!_sender.Send(ref packet, PacketDelivery.ReliableOrdered))
            {
                Fail(KillcamUnavailableReason.NoData);
            }
        }

        public bool TryTake(out KillcamClip clip, out KillcamUnavailableReason reason)
        {
            if (!_isComplete)
            {
                clip = null;
                reason = KillcamUnavailableReason.None;
                return false;
            }

            clip = _clipResult;
            reason = _reason;
            _isComplete = false;
            return true;
        }

        public void Cancel()
        {
            _isPending = false;
            _isComplete = false;
            _clipResult = null;
            _reason = KillcamUnavailableReason.None;
            _clip.Clear();
        }

        public void Release()
        {
            _isPending = false;
            _isComplete = false;
            _clipResult = null;
            _reason = KillcamUnavailableReason.None;
            _clip.Clear();
        }

        public void Dispose()
        {
            _disposed = true;
            if (_subscribed && _receiver != null)
            {
                _receiver.Unsubscribe(_onChunk);
                _subscribed = false;
            }
            Release();
        }

        private void OnChunkReceived(KillcamDataChunkPacket chunk)
        {
            // Drop late data if not pending (cancelled, finished, or match ended)
            if (!_isPending) return;

            // Validate schema / version
            if (chunk.Version != KillcamDataChunkPacket.CurrentVersion)
            {
                Fail(KillcamUnavailableReason.NoData);
                return;
            }

            // Validate MatchId
            if (chunk.MatchId != _currentRequest.MatchId)
            {
                Fail(KillcamUnavailableReason.WrongMatch);
                return;
            }

            // Validate VictimNetId
            if (chunk.VictimNetId != _currentRequest.VictimNetId)
            {
                Fail(KillcamUnavailableReason.WrongPlayer);
                return;
            }

            // Check if server reported unavailable status
            if (chunk.Status != 0)
            {
                var serverReason = (KillcamUnavailableReason)chunk.Status;
                Fail(serverReason == KillcamUnavailableReason.None ? KillcamUnavailableReason.NoData : serverReason);
                return;
            }

            if (chunk.TotalFrames < _currentRequest.FrameCount)
            { Fail(KillcamUnavailableReason.TooShort); return; }
            try { chunk.ValidateHeader(); }
            catch (InvalidOperationException) { Fail(KillcamUnavailableReason.NoData); return; }
            if (chunk.TotalFrames > _clip.FrameCapacity || chunk.Frames == null || chunk.Frames.Length != chunk.FrameCount)
            { Fail(KillcamUnavailableReason.NoData); return; }
            if (_totalChunks != 0 && (_clip.EndTick != chunk.EndTick || _clip.KillerNetId != chunk.KillerNetId || _clip.FrameCount != chunk.TotalFrames))
            { Fail(KillcamUnavailableReason.NoData); return; }
            // Validate chunk bounds
            if (chunk.TotalChunks == 0 || chunk.TotalChunks > KillcamDataChunkPacket.MaxChunks || chunk.ChunkIndex >= chunk.TotalChunks)
            {
                Fail(KillcamUnavailableReason.NoData);
                return;
            }

            if (_totalChunks == 0)
            {
                _totalChunks = chunk.TotalChunks;
                _clip.EndTick = chunk.EndTick;
                _clip.KillerNetId = chunk.KillerNetId;
                _clip.FrameCount = chunk.TotalFrames;
            }
            else if (_totalChunks != chunk.TotalChunks)
            {
                Fail(KillcamUnavailableReason.NoData);
                return;
            }

            // Deduplicate chunks
            var bit = 1 << chunk.ChunkIndex;
            if ((_chunkMask & bit) != 0)
            {
                return;
            }
            _chunkMask |= bit;
            _receivedChunks++;

            // Populate frames into _clip
            var offset = chunk.ChunkIndex * KillcamDataChunkPacket.MaxFramesPerChunk;
            var frames = chunk.Frames;
            var count = chunk.FrameCount;
            if (frames != null && count > 0)
            {
                for (var i = 0; i < count; i++)
                {
                    var targetFrame = offset + i;
                    if (targetFrame >= _clip.FrameCapacity)
                    {
                        Fail(KillcamUnavailableReason.NoData);
                        return;
                    }
                    var f = frames[i];
                    _clip.ZoneX[targetFrame] = f.ZoneX;
                    _clip.ZoneY[targetFrame] = f.ZoneY;
                    _clip.ZoneRadius[targetFrame] = f.ZoneRadius;
                    _clip.Set(targetFrame, chunk.VictimNetId, f.VictimX, f.VictimY, f.VictimHp, f.VictimFlags);
                    if (f.KillerPresent && chunk.KillerNetId < KillcamClip.MaxPlayers)
                    {
                        _clip.Set(targetFrame, chunk.KillerNetId, f.KillerX, f.KillerY, f.KillerHp, f.KillerFlags);
                    }
                }
            }

            // Check if all chunks have arrived
            if (_receivedChunks >= _totalChunks)
            {
                if (chunk.TotalFrames < _currentRequest.FrameCount)
                {
                    Fail(KillcamUnavailableReason.TooShort);
                    return;
                }

                _clip.MatchId = chunk.MatchId;
                _clip.VictimNetId = chunk.VictimNetId;
                _clip.KillerNetId = chunk.KillerNetId;
                _clip.EndTick = chunk.EndTick;
                _clip.FrameCount = chunk.TotalFrames;

                _isPending = false;
                _isComplete = true;
                _clipResult = _clip;
                _reason = KillcamUnavailableReason.None;
            }
        }

        private void Fail(KillcamUnavailableReason reason)
        {
            _isPending = false;
            _isComplete = true;
            _clipResult = null;
            _reason = reason;
            _clip.Clear();
        }
    }
}
