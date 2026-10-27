using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JungleMaze.Domain.Audio;
using JungleMaze.Presentation.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JungleMaze.Tests.Audio
{
    /// <summary>
    /// ART-05 levels measured on the imported data (AudioClip.GetData), so importer flags cannot hide a change
    /// (AC16, AC18, AC19, AC20, AC40). The clips import as PCM: ADPCM pushed transient peaks up to +1 dB (hurt_local
    /// reached 0 dBFS), so the ceiling keeps only a 0.05 dB rounding margin.
    /// </summary>
    public sealed class ArtAudioLevelTests
    {
        private const float Ceiling = -1.0f, Rounding = 0.05f;
        private SfxCatalog _catalog;

        [SetUp]
        public void SetUp() => _catalog = ArtAudioSpec.LoadCatalog();

        private IEnumerable<(ArtAudioSpec.Row row, AudioClip clip)> AllClips()
        {
            foreach (var row in ArtAudioSpec.Table)
                foreach (var clip in _catalog.Find(row.Id).clips)
                    yield return (row, clip);
        }

        // AC16: Decompress On Load, PCM (spec 2.7 allows it; ADPCM broke the ceiling), no force-to-mono, no normalize, one channel after import.
        [Test]
        public void Importer_DecompressOnLoad_NoNormalize_Mono()
        {
            foreach (var (row, clip) in AllClips())
            {
                var path = AssetDatabase.GetAssetPath(clip);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, settings.loadType, path);
                Assert.AreEqual(AudioCompressionFormat.PCM, settings.compressionFormat, path);
                Assert.IsFalse(importer.forceToMono, path);
                // AudioImporter has no public normalize flag; read it from the .meta.
                StringAssert.Contains("\n  normalize: 0", File.ReadAllText(ArtAudioSpec.ProjectPath(path + ".meta")).Replace("\r", ""), path);
                Assert.AreEqual(1, clip.channels, path);
            }
        }

        // AC18: peak ≤ −1 dBFS, no 3 samples in a row at full scale, 2 ms head and tail ≤ −40 dBFS.
        [Test]
        public void Peaks_FullScaleRuns_Edges()
        {
            foreach (var (row, clip) in AllClips())
            {
                var x = ArtAudioSpec.Samples(clip);
                Assert.LessOrEqual(ArtAudioSpec.Peak(x), Ceiling + Rounding, clip.name + " peak");
                var run = 0;
                foreach (var s in x)
                {
                    run = Mathf.Abs(s) >= 0.9999f ? run + 1 : 0;
                    Assert.Less(run, 3, clip.name + " clips");
                }
                Assert.LessOrEqual(ArtAudioSpec.Edge(x, clip.frequency, true), -40f, clip.name + " head");
                Assert.LessOrEqual(ArtAudioSpec.Edge(x, clip.frequency, false), -40f, clip.name + " tail");
            }
        }

        // AC40: baked level L −24 ± 2 for footsteps, −20…−15 for everything else; variants of one id within 1 dB.
        [Test]
        public void BakedLevel_InRange_VariantsMatch()
        {
            foreach (var row in ArtAudioSpec.Table)
            {
                float min = float.MaxValue, max = float.MinValue;
                foreach (var clip in _catalog.Find(row.Id).clips)
                {
                    var level = ArtAudioSpec.Level(ArtAudioSpec.Samples(clip));
                    Assert.That(level, Is.InRange(row.MinLevel - 0.2f, row.MaxLevel + 0.2f), $"{row.Id}: {clip.name} L");
                    min = Mathf.Min(min, level);
                    max = Mathf.Max(max, level);
                }
                Assert.LessOrEqual(max - min, 1f, row.Id + " variants");
            }
        }

        private Dictionary<SfxId, float> Effective()
        {
            var e = new Dictionary<SfxId, float>();
            foreach (var row in ArtAudioSpec.Table)
            {
                var entry = _catalog.Find(row.Id);
                var sum = 0f;
                foreach (var clip in entry.clips) sum += ArtAudioSpec.Level(ArtAudioSpec.Samples(clip));
                e[row.Id] = sum / entry.clips.Length + ArtAudioSpec.Db(entry.volume);
            }
            return e;
        }

        // AC19, AC20: E = L + 20·log10(volume) inside its tier ± 3; HitMarker ≥ every T2–T4 id + 3 and ≥ FootstepGrass + 12.
        [Test]
        public void EffectiveLevel_InTier_HitMarkerStandsOut()
        {
            var e = Effective();
            var loudestBelowT1 = float.MinValue;
            foreach (var row in ArtAudioSpec.Table)
            {
                Assert.AreEqual(row.TierLevel, e[row.Id], 3f, $"{row.Id} E");
                if (row.Tier > 1) loudestBelowT1 = Mathf.Max(loudestBelowT1, e[row.Id]);
            }
            Assert.GreaterOrEqual(e[SfxId.HitMarker], loudestBelowT1 + 3f, "HitMarker over T2–T4");
            Assert.GreaterOrEqual(e[SfxId.HitMarker], e[SfxId.FootstepGrass] + 12f, "HitMarker over footsteps");
            Assert.GreaterOrEqual(_catalog.Find(SfxId.HitMarker).volume, 1f);
        }

        // AC16: the imported peak equals the peak the manifest measured on the WAV (± 0.5 dB), so no flag rescaled it.
        [Test]
        public void ImportedPeak_MatchesManifest()
        {
            var checkedClips = 0;
            foreach (var r in ArtAudioSpec.ReadManifest())
            {
                if (r["row_type"] != "clip" || !r["file"].StartsWith("Sfx/")) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_JungleMaze/Art/Audio/" + r["file"]);
                Assert.IsNotNull(clip, r["file"]);
                var expected = float.Parse(r["peak_after_dbfs"], CultureInfo.InvariantCulture);
                Assert.AreEqual(expected, ArtAudioSpec.Peak(ArtAudioSpec.Samples(clip)), 0.5f, r["file"]);
                checkedClips++;
            }
            Assert.AreEqual(40, checkedClips);
        }
    }
}
