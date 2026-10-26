#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Audio;

namespace JungleMaze.App.Audio.Mocks
{
    /// <summary>
    /// Dev-only cue source for sounds whose real source does not exist yet (✖ ids 1–10, 13, 25) and for the F6
    /// panel stress tools. It never raises anything on its own except during an explicitly started burst.
    /// </summary>
    public sealed class MockAudioEventSource : IAudioEventSource, ITickable
    {
        /// <summary>The 12 ids without a real event source yet (spec table 2.1, column "Nguồn" ✖).</summary>
        public static readonly SfxId[] Unsourced =
        {
            SfxId.FootstepGrass, SfxId.FootstepSwamp, SfxId.BushEnter, SfxId.MeleeSwing, SfxId.MeleeHit,
            SfxId.ShootRanged, SfxId.ShootCrossbow, SfxId.Throw, SfxId.Reload, SfxId.EmptyClick,
            SfxId.TrapTriggered, SfxId.DragonRoar
        };

        private const byte KindMock = 200;
        private readonly Random _random;
        private uint _serverTick = 1;
        private float _burstLeft;
        private int _burstPerTick;

        public MockAudioEventSource(int seed = 4242) => _random = new Random(seed);

        public event Action<AudioCue> CueRaised;

        public bool BurstRunning => _burstLeft > 0f;
        public int Raised { get; private set; }

        public void Raise(SfxId id) => Emit(AudioCue.Global(id));
        public void Raise(SfxId id, float x, float y, int sourceKey = 0) => Emit(AudioCue.At(id, x, y, sourceKey));
        public void RaiseKeyed(SfxId id, ulong key) => Emit(AudioCue.Global(id, 0, key));

        public void RaiseAllUnsourced()
        {
            for (var i = 0; i < Unsourced.Length; i++) Raise(Unsourced[i]);
        }

        /// <summary>One fake server event delivered <paramref name="copies"/> times (resend/reconnect): must play once.</summary>
        public void RepeatServerEvent(SfxId id = SfxId.Eliminated, int copies = 5)
        {
            var key = SfxDeduper.Key(KindMock, _serverTick++, (uint)id);
            for (var i = 0; i < copies; i++) RaiseKeyed(id, key);
        }

        /// <summary><paramref name="count"/> requests of random ids and sources in one call (seeded, mixed priorities).</summary>
        public void Spam(int count = 100)
        {
            for (var i = 0; i < count; i++) Emit(RandomCue());
        }

        /// <summary>Starts a stress run: <paramref name="perTick"/> random cues every tick for <paramref name="seconds"/>.</summary>
        public void StartBurst(float seconds = 10f, int perTick = 4)
        {
            _burstLeft = seconds;
            _burstPerTick = perTick < 1 ? 1 : perTick;
        }

        public void StopBurst() => _burstLeft = 0f;

        public void Tick(float deltaTime)
        {
            if (_burstLeft <= 0f) return;
            _burstLeft -= deltaTime;
            for (var i = 0; i < _burstPerTick; i++) Emit(RandomCue());
        }

        private AudioCue RandomCue()
        {
            var id = (SfxId)(1 + _random.Next(SfxIds.Count));
            var source = _random.Next(12);
            // A third of the cues are keyed server events, half of those resent with an old key.
            if (_random.Next(3) == 0)
            {
                var tick = _random.Next(2) == 0 ? _serverTick : _serverTick++;
                return AudioCue.Global(id, source, SfxDeduper.Key(KindMock, tick, (uint)id));
            }
            return AudioCue.Global(id, source);
        }

        private void Emit(in AudioCue cue)
        {
            Raised++;
            CueRaised?.Invoke(cue);
        }
    }
}
#endif
