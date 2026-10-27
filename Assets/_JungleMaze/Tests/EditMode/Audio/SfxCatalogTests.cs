using System.Collections.Generic;
using JungleMaze.Domain.Audio;
using JungleMaze.Presentation.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace JungleMaze.Tests.Audio
{
    /// <summary>The shipped catalog, runtime set and mixer match spec table 2.1 and the placeholder rules (AC-A1, A5, A9, A19, A21).</summary>
    public sealed class SfxCatalogTests
    {
        private const string CatalogPath = "Assets/_JungleMaze/Art/Audio/SfxCatalog.asset";
        private SfxCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<SfxCatalog>(CatalogPath);
            Assert.IsNotNull(_catalog, CatalogPath);
        }

        [Test]
        public void Has26Ids_EachHasClipAndRuleFromTable()
        {
            var missing = new List<SfxId>();
            var table = _catalog.BuildRules();
            Assert.AreEqual(0, table.CollectMissing(missing), string.Join(", ", missing));
            foreach (var row in AudioSpec.Table)
            {
                Assert.IsTrue(table.TryGet(row.Id, out var rule), row.Id.ToString());
                Assert.IsNotNull(_catalog.GetClip(row.Id, 0), row.Id.ToString());
                Assert.AreEqual(row.Priority, rule.Priority, $"{row.Id} priority");
                Assert.AreEqual(row.Bus, rule.Bus, $"{row.Id} bus");
                Assert.AreEqual(row.Range, rule.RangeCells, 1e-4f, $"{row.Id} range");
                Assert.AreEqual(row.Max, rule.MaxInstances, $"{row.Id} max");
                Assert.AreEqual(row.Cooldown, rule.CooldownSeconds, 1e-4f, $"{row.Id} cooldown");
                Assert.LessOrEqual(rule.PitchJitter, 0.05f + 1e-4f, $"{row.Id} pitch ±5%");
            }
            Assert.IsTrue(table.TryGet(SfxId.ZoneBell3, out var bell));
            Assert.AreEqual(3, bell.RepeatCount);
            Assert.AreEqual(0.8f, bell.RepeatInterval, 1e-4f);
        }

        [Test]
        public void DistinguishableIdsUseDistinctClips()
        {
            var ids = new[] { SfxId.HitMarker, SfxId.HurtLocal, SfxId.ZoneBell3, SfxId.DragonRoar, SfxId.MatchEnd, SfxId.Explosion };
            var clips = new HashSet<AudioClip>();
            foreach (var id in ids) Assert.IsTrue(clips.Add(_catalog.GetClip(id, 0)), $"{id} shares a clip");
        }

        // ART-05 replaced every generated beep and JSurviv copy, and F4 removed Art/Audio/Placeholder: nothing may
        // point back into it (formerly BeepsHaveDistinctFrequencies, which counted 0 beeps once ART-05 landed).
        [Test]
        public void NoClipPointsIntoPlaceholder()
        {
            foreach (var row in AudioSpec.Table)
                foreach (var clip in _catalog.Find(row.Id).clips)
                    StringAssert.DoesNotContain("/Placeholder/", AssetDatabase.GetAssetPath(clip), row.Id.ToString());
            var set = Resources.Load<AudioRuntimeSet>(AudioRuntimeSet.ResourcePath);
            foreach (JungleMaze.App.Audio.MusicTrack track in System.Enum.GetValues(typeof(JungleMaze.App.Audio.MusicTrack)))
            {
                var clip = set.MusicFor(track);
                if (clip != null) StringAssert.DoesNotContain("/Placeholder/", AssetDatabase.GetAssetPath(clip), track.ToString());
            }
        }

        [Test]
        public void HitMarkerLoud_DragonRoarLongAndUnique()
        {
            var table = _catalog.BuildRules();
            table.TryGet(SfxId.HitMarker, out var hit);
            table.TryGet(SfxId.FootstepGrass, out var step);
            Assert.GreaterOrEqual(hit.Volume, 1f);
            Assert.GreaterOrEqual(hit.Volume, 2f * step.Volume);

            table.TryGet(SfxId.DragonRoar, out var roar);
            var roarClip = _catalog.GetClip(SfxId.DragonRoar, 0);
            Assert.GreaterOrEqual(roarClip.length, 1.5f);
            Assert.AreEqual(AudioBus.Alert, roar.Bus);
            Assert.AreEqual(5f, roar.CooldownSeconds, 1e-4f);
            foreach (var row in AudioSpec.Table)
                if (row.Id != SfxId.DragonRoar) Assert.AreNotSame(roarClip, _catalog.GetClip(row.Id, 0), row.Id.ToString());
        }

        [Test]
        public void RuntimeSet_MixerHasBusesAndExposedParams()
        {
            var set = Resources.Load<AudioRuntimeSet>(AudioRuntimeSet.ResourcePath);
            Assert.IsNotNull(set);
            Assert.AreSame(_catalog, set.Catalog);
            Assert.AreEqual(16, set.PoolSize);
            AudioMixer mixer = set.Mixer;
            Assert.IsNotNull(mixer);
            Assert.AreEqual(1, mixer.FindMatchingGroups("Master/SFX/Alert").Length, "Alert is a child of SFX");
            Assert.AreEqual(1, mixer.FindMatchingGroups("Master/Music/MusicTrack").Length);
            Assert.AreSame(set.SfxGroup, mixer.FindMatchingGroups("Master/SFX")[0]);
            Assert.AreSame(set.AlertGroup, mixer.FindMatchingGroups("Master/SFX/Alert")[0]);
            foreach (var p in new[] { AudioRuntimeSet.MasterVolumeParam, AudioRuntimeSet.SfxVolumeParam, AudioRuntimeSet.AlertVolumeParam,
                         AudioRuntimeSet.MusicVolumeParam, AudioRuntimeSet.MusicDuckParam })
                Assert.IsTrue(mixer.GetFloat(p, out _), $"exposed parameter {p}");
            Assert.IsNotNull(set.MusicFor(JungleMaze.App.Audio.MusicTrack.Match));
            Assert.IsNotNull(set.MusicFor(JungleMaze.App.Audio.MusicTrack.WaitingRoom));
        }
    }
}
