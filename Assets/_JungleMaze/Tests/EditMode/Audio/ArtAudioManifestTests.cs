using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using JungleMaze.Domain.Audio;
using NUnit.Framework;

namespace JungleMaze.Tests.Audio
{
    /// <summary>ART-05 manifest (Docs/Tickets/ART-05_audio_manifest.csv): inventory, licence vocabulary, traceability (AC1, AC3, AC4, AC6, AC41).</summary>
    public sealed class ArtAudioManifestTests
    {
        private const string JSurviv = "Assets/Imported/JSurviv/Sound";
        private static readonly string[] Forbidden = { "miễn phí", "CC0", "royalty-free", "được phép" };
        private List<Dictionary<string, string>> _rows;

        [SetUp]
        public void SetUp() => _rows = ArtAudioSpec.ReadManifest();

        private IEnumerable<Dictionary<string, string>> Rows(string type) => _rows.Where(r => r["row_type"] == type);

        // AC1
        [Test]
        public void SfxRows_FollowTheEnum_WithAStatus()
        {
            var names = Enum.GetNames(typeof(SfxId)).Where(n => n != nameof(SfxId.None)).ToArray();
            var rows = Rows("sfx").ToArray();
            Assert.AreEqual(26, names.Length);
            CollectionAssert.AreEqual(names, rows.Select(r => r["sfx_id"]).ToArray());
            foreach (var r in rows)
                CollectionAssert.Contains(new[] { "Có", "Cần chỉnh", "Thiếu" }, r["status"], r["sfx_id"]);
        }

        // AC3: one row per JSurviv file (21) and per zip member (9).
        [Test]
        public void JSurvivRows_CoverEveryFileAndZipMember()
        {
            var root = ArtAudioSpec.ImportedOrIgnore(JSurviv);
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")).ToArray();
            Assert.AreEqual(21, files.Length);
            var members = 0;
            foreach (var zip in files.Where(f => f.EndsWith(".zip")))
                using (var archive = new ZipArchive(File.OpenRead(zip), ZipArchiveMode.Read)) members += archive.Entries.Count(e => e.Length > 0);
            Assert.AreEqual(9, members);
            var rows = Rows("jsurviv").ToArray();
            Assert.AreEqual(30, rows.Length);
            foreach (var f in files)
            {
                var rel = f.Substring(root.Length + 1).Replace('\\', '/');
                Assert.AreEqual(1, rows.Count(r => r["file"] == rel && r["zip_member"] == ""), rel);
            }
            foreach (var r in rows)
            {
                CollectionAssert.Contains(new[] { "mã SFX", "BGM", "UI", "ngoài 26", "không dùng" }, r["class"], r["file"]);
                Assert.AreEqual("chưa kiểm chứng", r["licence"], r["file"]);
            }
        }

        // AC4, AC41: licence is one of three forms; evidence files exist; no free-licence words without evidence.
        [Test]
        public void Licences_UseTheVocabulary_EvidenceExists()
        {
            foreach (var r in _rows)
            {
                var label = r["row_type"] + " " + r["file"] + r["sfx_id"];
                foreach (var part in r["licence"].Split(new[] { " | " }, StringSplitOptions.None))
                {
                    if (part == "chưa kiểm chứng" || part == ArtAudioSpec.SelfDeclaredCc0 || part.StartsWith("tự tạo (")) continue;
                    StringAssert.StartsWith("có bằng chứng: ", part, label);
                    var evidence = part.Substring("có bằng chứng: ".Length);
                    StringAssert.StartsWith("Docs/Licenses/Audio/", evidence, label);
                    FileAssert.Exists(ArtAudioSpec.ProjectPath(evidence), label);
                }
                if (r["licence"].Contains("có bằng chứng: ")) continue;
                // "nguồn tự khai CC0" says whose claim it is, so it is the one allowed mention (CPO 4.9).
                foreach (var value in r.Values)
                    foreach (var word in Forbidden)
                        StringAssert.DoesNotContain(word, value.Replace("nguồn tự khai CC0", ""), label + " claims a licence without evidence");
            }
        }

        // AC41: clip rows match the files in Sfx/ and Music/ one to one; downloads carry URL, author and date.
        [Test]
        public void ClipRows_MatchTheFiles_DownloadsAreTraceable()
        {
            var clips = Rows("clip").ToArray();
            var listed = clips.Select(r => r["file"]).ToList();
            CollectionAssert.AllItemsAreUnique(listed);
            var onDisk = new List<string>();
            foreach (var folder in new[] { "Sfx", "Music" })
                foreach (var f in Directory.GetFiles(ArtAudioSpec.ProjectPath("Assets/_JungleMaze/Art/Audio/" + folder)))
                    if (!f.EndsWith(".meta")) onDisk.Add(folder + "/" + Path.GetFileName(f));
            CollectionAssert.AreEquivalent(onDisk, listed);
            foreach (var r in clips)
            {
                Assert.IsNotEmpty(r["source"], r["file"]);
                if (!r["licence"].StartsWith("có bằng chứng: ") && r["licence"] != ArtAudioSpec.SelfDeclaredCc0) continue;
                Assert.IsNotEmpty(r["url"], r["file"]);
                Assert.IsNotEmpty(r["author"], r["file"]);
                Assert.IsNotEmpty(r["download_date"], r["file"]);
            }
        }

        // The quieter-than-target clips in the manifest are exactly the test's exception table (CPO 4.9).
        [Test]
        public void LevelExceptions_MatchTheManifest()
        {
            var listed = Rows("clip").Where(r => r["exception"] != "").Select(r => Path.GetFileNameWithoutExtension(r["file"]));
            CollectionAssert.AreEquivalent(ArtAudioSpec.LevelExceptions.Keys, listed);
        }

        // AC6: heard is "chưa" or "có: <name>, <date>".
        [Test]
        public void Heard_IsNotYetOrNamed()
        {
            foreach (var r in _rows.Where(r => r["row_type"] != "jsurviv"))
                Assert.IsTrue(r["heard"] == "chưa" || (r["heard"].StartsWith("có: ") && r["heard"].Contains(",")), r["file"] + r["sfx_id"]);
        }
    }
}
