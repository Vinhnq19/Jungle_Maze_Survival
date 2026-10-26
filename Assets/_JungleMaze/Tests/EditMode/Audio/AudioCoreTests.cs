using System.Collections.Generic;
using JungleMaze.App.Audio;
using JungleMaze.Domain.Audio;
using NUnit.Framework;

namespace JungleMaze.Tests.Audio
{
    public sealed class SfxRuleTableTests
    {
        [Test]
        public void SpecTable_Has26IdsWithoutGaps()
        {
            Assert.AreEqual(26, SfxIds.Count);
            Assert.AreEqual(26, AudioSpec.Table.Length);
            for (var i = 0; i < AudioSpec.Table.Length; i++) Assert.AreEqual((SfxId)(i + 1), AudioSpec.Table[i].Id);
            var missing = new List<SfxId>();
            Assert.AreEqual(0, AudioSpec.Rules().CollectMissing(missing), string.Join(",", missing));
        }

        [Test]
        public void IdWithoutClips_IsMissing()
        {
            var table = AudioSpec.Rules(id => id == SfxId.Throw ? 0 : 1);
            var missing = new List<SfxId>();
            Assert.AreEqual(1, table.CollectMissing(missing));
            Assert.AreEqual(SfxId.Throw, missing[0]);
            Assert.IsFalse(table.TryGet(SfxId.Throw, out _));
            Assert.IsFalse(table.TryGet(SfxId.None, out _));
            Assert.IsTrue(table.TryGet(SfxId.MatchEnd, out var rule));
            Assert.AreEqual(SfxPriority.Critical, rule.Priority);
        }
    }

    public sealed class VoiceAllocatorTests
    {
        private static VoiceAllocator Full(SfxPriority priority, int voices = 16)
        {
            var a = new VoiceAllocator(voices);
            for (var i = 0; i < voices; i++)
            {
                Assert.AreEqual(i, a.Acquire(priority, out var evicted));
                Assert.IsFalse(evicted);
                a.Assign(i, SfxId.FootstepGrass, priority, AudioBus.Sfx);
            }
            return a;
        }

        [Test]
        public void CriticalEvictsLow_OldestFirst()
        {
            var a = Full(SfxPriority.Low);
            var voice = a.Acquire(SfxPriority.Critical, out var evicted);
            Assert.IsTrue(evicted);
            Assert.AreEqual(0, voice, "oldest Low voice is cut");
            a.Assign(voice, SfxId.HitMarker, SfxPriority.Critical, AudioBus.Sfx);
            Assert.AreEqual(16, a.ActiveCount);
            Assert.AreEqual(1, a.CountActive(SfxId.HitMarker));
        }

        [Test]
        public void LowDroppedWhenFullOfHigh()
        {
            var a = Full(SfxPriority.High);
            Assert.AreEqual(-1, a.Acquire(SfxPriority.Low, out var evicted));
            Assert.IsFalse(evicted);
            Assert.AreEqual(-1, a.Acquire(SfxPriority.High, out _), "equal priority never evicts");
        }

        [Test]
        public void CriticalNeverEvicted()
        {
            var a = Full(SfxPriority.Critical);
            foreach (SfxPriority p in System.Enum.GetValues(typeof(SfxPriority)))
                Assert.AreEqual(-1, a.Acquire(p, out _), p.ToString());
        }

        [Test]
        public void EvictsLowestPriorityFirst()
        {
            var a = new VoiceAllocator(3);
            a.Assign(0, SfxId.MeleeHit, SfxPriority.Normal, AudioBus.Sfx);
            a.Assign(1, SfxId.FootstepGrass, SfxPriority.Low, AudioBus.Sfx);
            a.Assign(2, SfxId.ShootRanged, SfxPriority.High, AudioBus.Sfx);
            Assert.AreEqual(1, a.Acquire(SfxPriority.Critical, out _));
        }
    }

    public sealed class SfxGateTests
    {
        [Test]
        public void CooldownPerSource()
        {
            var gate = new SfxGate();
            gate.Mark(SfxId.FootstepGrass, 1, 0f);
            Assert.IsTrue(gate.IsCoolingDown(SfxId.FootstepGrass, 1, 0.1f, 0.25f));
            Assert.IsFalse(gate.IsCoolingDown(SfxId.FootstepGrass, 2, 0.1f, 0.25f), "other source");
            Assert.IsFalse(gate.IsCoolingDown(SfxId.FootstepSwamp, 1, 0.1f, 0.25f), "other id");
        }

        [Test]
        public void ReplaysAfterCooldown()
        {
            var gate = new SfxGate();
            gate.Mark(SfxId.ShootRanged, 0, 1f);
            Assert.IsTrue(gate.IsCoolingDown(SfxId.ShootRanged, 0, 1.079f, 0.08f));
            Assert.IsFalse(gate.IsCoolingDown(SfxId.ShootRanged, 0, 1.08f, 0.08f));
        }

        [Test]
        public void ForgetsLeastRecentSourceWhenFull()
        {
            var gate = new SfxGate();
            for (var s = 0; s <= SfxGate.SourcesPerId; s++) gate.Mark(SfxId.MeleeHit, s, s * 0.001f);
            Assert.IsFalse(gate.IsCoolingDown(SfxId.MeleeHit, 0, 0.07f, 1f), "source 0 was the oldest and got forgotten");
            Assert.IsTrue(gate.IsCoolingDown(SfxId.MeleeHit, SfxGate.SourcesPerId, 0.07f, 1f));
        }
    }

    public sealed class SfxDeduperTests
    {
        [Test]
        public void FiveCopies_OnePlay_EvenAfterOneSecond()
        {
            var d = new SfxDeduper();
            var key = SfxDeduper.Key(1, 100, 2, 3);
            Assert.IsFalse(d.IsDuplicate(key, 0f));
            for (var i = 1; i < 5; i++) Assert.IsTrue(d.IsDuplicate(key, i * 0.03f));
            Assert.IsTrue(d.IsDuplicate(key, 1.0f), "resend after 1 s");
        }

        [Test]
        public void DifferentTick_Plays()
        {
            var d = new SfxDeduper();
            Assert.IsFalse(d.IsDuplicate(SfxDeduper.Key(1, 100, 2, 3), 0f));
            Assert.IsFalse(d.IsDuplicate(SfxDeduper.Key(1, 101, 2, 3), 0f));
            Assert.IsFalse(d.IsDuplicate(0, 0f), "key 0 is never a duplicate");
            Assert.IsFalse(d.IsDuplicate(0, 0f));
        }

        [Test]
        public void Remember2s_Cap128()
        {
            var d = new SfxDeduper();
            Assert.AreEqual(128, d.Capacity);
            var first = SfxDeduper.Key(9, 0);
            d.IsDuplicate(first, 0f);
            Assert.IsTrue(d.IsDuplicate(first, 1.99f));
            Assert.IsFalse(d.IsDuplicate(first, 2.0f), "forgotten after 2 s");

            var e = new SfxDeduper();
            for (uint i = 0; i < 129; i++) e.IsDuplicate(SfxDeduper.Key(9, i), 0f);
            Assert.IsFalse(e.IsDuplicate(SfxDeduper.Key(9, 0), 0.1f), "oldest key dropped when 129 keys arrived");
            Assert.IsTrue(e.IsDuplicate(SfxDeduper.Key(9, 128), 0.1f));
        }
    }

    public sealed class AudioVolumeTests
    {
        [Test]
        public void ToDecibels()
        {
            Assert.AreEqual(0f, AudioVolume.ToDecibels(1f), 0.01f);
            Assert.AreEqual(-6.02f, AudioVolume.ToDecibels(0.5f), 0.1f);
            Assert.AreEqual(-80f, AudioVolume.ToDecibels(0f), 0.001f);
            Assert.AreEqual(-80f, AudioVolume.ToDecibels(-1f), 0.001f);
            Assert.AreEqual(0f, AudioVolume.ToDecibels(3f), 0.001f, "clamped to 1");
        }
    }

    public sealed class SfxDistanceTests
    {
        [Test]
        public void LinearGain_RejectBeyondRange()
        {
            Assert.AreEqual(1f, SfxDistance.Gain(0, 0, 0, 0, 12), 1e-4f);
            Assert.AreEqual(0.5f, SfxDistance.Gain(6, 0, 0, 0, 12), 0.05f);
            Assert.AreEqual(0f, SfxDistance.Gain(12, 0, 0, 0, 12));
            Assert.AreEqual(0f, SfxDistance.Gain(30, 4, 0, 0, 12));
            Assert.AreEqual(1f, SfxDistance.Gain(30, 4, 0, 0, 0), "range 0 = 2D");
        }
    }

    public sealed class ClipPickerTests
    {
        [Test]
        public void NoImmediateRepeat_PitchWithin5pct()
        {
            var picker = new ClipPicker(7);
            var last = -1;
            var seen = new HashSet<int>();
            for (var i = 0; i < 500; i++)
            {
                var clip = picker.PickClip(SfxId.MeleeHit, 3);
                Assert.AreNotEqual(last, clip);
                Assert.That(clip, Is.InRange(0, 2));
                seen.Add(clip);
                last = clip;
                var pitch = picker.PickPitch(0.05f);
                Assert.That(pitch, Is.InRange(0.95f, 1.05f));
            }
            Assert.AreEqual(3, seen.Count);
            Assert.AreEqual(0, picker.PickClip(SfxId.MeleeHit, 1));
            Assert.AreEqual(1f, picker.PickPitch(0f));
        }
    }

    public sealed class SfxSchedulerTests
    {
        [Test]
        public void ThreeBells_08s()
        {
            var s = new SfxScheduler();
            var times = new List<float>();
            var now = 0f;
            System.Action<AudioCue> play = c => times.Add(now);
            s.Schedule(AudioCue.Global(SfxId.ZoneBell3), AudioBus.Alert, 2, 0.8f, 0f);
            Assert.IsTrue(s.AnyPendingOn(AudioBus.Alert));
            for (var frame = 1; frame <= 180; frame++)
            {
                now = frame / 60f;
                s.Update(now, play);
            }
            Assert.AreEqual(2, times.Count);
            Assert.AreEqual(0.8f, times[0], 0.05f);
            Assert.AreEqual(1.6f, times[1], 0.05f);
            Assert.IsFalse(s.AnyPendingOn(AudioBus.Alert));
        }
    }
}
