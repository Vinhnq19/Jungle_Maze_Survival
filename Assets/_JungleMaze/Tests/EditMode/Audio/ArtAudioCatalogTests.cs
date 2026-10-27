using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using JungleMaze.Domain.Audio;
using JungleMaze.Presentation.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JungleMaze.Tests.Audio
{
    /// <summary>ART-05: every catalog clip is a final clip in Art/Audio/Sfx, named, distinct and long enough (AC8–AC10, AC13, AC17, AC21–AC27).</summary>
    public sealed class ArtAudioCatalogTests
    {
        private SfxCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = ArtAudioSpec.LoadCatalog();
            Assert.IsNotNull(_catalog, ArtAudioSpec.CatalogPath);
        }

        private AudioClip[] Clips(SfxId id) => _catalog.Find(id).clips;

        // AC8, AC10
        [Test]
        public void EveryClip_InSfx_AsciiName_AtMostFiveVariants()
        {
            var name = new Regex("^[a-z0-9_]+\\.wav$");
            foreach (var row in ArtAudioSpec.Table)
            {
                var clips = Clips(row.Id);
                Assert.That(clips.Length, Is.InRange(1, 5), row.Id.ToString());
                foreach (var clip in clips)
                {
                    Assert.IsNotNull(clip, row.Id.ToString());
                    var path = AssetDatabase.GetAssetPath(clip);
                    StringAssert.StartsWith(ArtAudioSpec.SfxFolder, path, row.Id.ToString());
                    StringAssert.DoesNotContain("/Placeholder/", path);
                    Assert.IsTrue(name.IsMatch(Path.GetFileName(path)), path);
                    StringAssert.StartsWith(Regex.Replace(row.Id.ToString(), "(?<=[a-z])([A-Z0-9])", "_$1").ToLowerInvariant()
                        .Replace("zone_bell_3", "zone_bell"), Path.GetFileName(path), "prefix follows the SfxId");
                }
            }
        }

        // AC9: no clip from Assets/Imported, and no Sfx/Music GUID shared with a file there.
        [Test]
        public void NothingFromImported()
        {
            var imported = new HashSet<string>();
            foreach (var meta in Directory.GetFiles(ArtAudioSpec.ImportedOrIgnore(ArtAudioSpec.ImportedFolder), "*.meta", SearchOption.AllDirectories))
            {
                foreach (var line in File.ReadLines(meta))
                {
                    if (!line.StartsWith("guid: ")) continue;
                    imported.Add(line.Substring(6).Trim());
                    break;
                }
            }
            foreach (var row in ArtAudioSpec.Table)
                foreach (var clip in Clips(row.Id))
                    StringAssert.DoesNotStartWith("Assets/Imported/", AssetDatabase.GetAssetPath(clip));
            foreach (var folder in new[] { ArtAudioSpec.SfxFolder, ArtAudioSpec.MusicFolder })
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder.TrimEnd('/') }))
                    Assert.IsFalse(imported.Contains(guid), AssetDatabase.GUIDToAssetPath(guid) + " reuses an Imported GUID");
        }

        // AC13
        [Test]
        public void IdsThatMustSoundDifferent_UseDifferentClips()
        {
            var groups = new[]
            {
                new[] { SfxId.HitMarker, SfxId.HurtLocal, SfxId.ZoneBell3, SfxId.DragonRoar, SfxId.MatchEnd, SfxId.Explosion },
                new[] { SfxId.ShootRanged, SfxId.ShootCrossbow }, new[] { SfxId.MeleeSwing, SfxId.MeleeHit },
                new[] { SfxId.PickupWeapon, SfxId.PickupItem, SfxId.WearArmor }
            };
            foreach (var group in groups)
            {
                var seen = new Dictionary<AudioClip, SfxId>();
                foreach (var id in group)
                    foreach (var clip in Clips(id))
                    {
                        Assert.IsFalse(seen.TryGetValue(clip, out var other), $"{id} and {other} share {clip.name}");
                        seen[clip] = id;
                    }
            }
        }

        // AC17, AC21–AC27 (length part): every variant inside its window.
        [Test]
        public void EveryClip_LengthInsideItsWindow()
        {
            foreach (var row in ArtAudioSpec.Table)
                foreach (var clip in Clips(row.Id))
                    Assert.That(clip.length, Is.InRange(row.MinSeconds, row.MaxSeconds), $"{row.Id}: {clip.name}");
        }

        // AC11, AC28: ART-05 changes clips, volume and pitchJitter only. Every other column of the 26 entries equals
        // the AUD-01 table (AudioSpec.Table); only the bell repeats (3 strikes, 0.8 s apart).
        [Test]
        public void UnchangedColumns_EqualTheAud01Table()
        {
            foreach (var row in AudioSpec.Table)
            {
                var e = _catalog.Find(row.Id);
                Assert.That(e.volume, Is.InRange(0.05f, 1f), row.Id + " volume");
                Assert.LessOrEqual(e.pitchJitter, 0.05f + 1e-4f, row.Id + " pitchJitter");
                Assert.AreEqual(row.Priority, e.priority, row.Id + " priority");
                Assert.AreEqual(row.Bus, e.bus, row.Id + " bus");
                Assert.AreEqual(row.Range, e.rangeCells, 1e-4f, row.Id + " rangeCells");
                Assert.AreEqual(row.Max, e.maxInstances, row.Id + " maxInstances");
                Assert.AreEqual(row.Cooldown, e.cooldownSeconds, 1e-4f, row.Id + " cooldownSeconds");
                var bell = row.Id == SfxId.ZoneBell3;
                Assert.AreEqual(bell ? 3 : 1, e.repeatCount, row.Id + " repeatCount");
                Assert.AreEqual(bell ? 0.8f : 0f, e.repeatIntervalSeconds, 1e-4f, row.Id + " repeatIntervalSeconds");
            }
        }
    }
}
