using System.IO;
using JungleMaze.App.Audio;
using JungleMaze.Presentation.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JungleMaze.Tests.Audio
{
    /// <summary>ART-05 music: three compressed tracks under Art/Audio/Music, streamed, wired in AudioRuntimeSet (AC16, AC29, AC31).</summary>
    public sealed class ArtAudioMusicTests
    {
        private const string RuntimeSetPath = "Assets/_JungleMaze/Art/Audio/Resources/AudioRuntimeSet.asset";

        [TestCase(MusicTrack.WaitingRoom, "waiting_room.ogg", 145.53f)]
        [TestCase(MusicTrack.Loading, "loading.ogg", 43.65f)]
        [TestCase(MusicTrack.Match, "match.ogg", 123.48f)]
        public void Track_Compressed_Streamed_Wired(MusicTrack track, string file, float sourceSeconds)
        {
            var set = AssetDatabase.LoadAssetAtPath<AudioRuntimeSet>(RuntimeSetPath);
            var clip = set.MusicFor(track);
            Assert.IsNotNull(clip, track.ToString());
            var path = AssetDatabase.GetAssetPath(clip);
            Assert.AreEqual(ArtAudioSpec.MusicFolder + file, path);
            Assert.LessOrEqual(new FileInfo(ArtAudioSpec.ProjectPath(path)).Length, 3_000_000, path + " ≤ 3.0 MB");
            Assert.AreEqual(sourceSeconds, clip.length, 0.1f, path + " length");
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(AudioClipLoadType.Streaming, importer.defaultSampleSettings.loadType, path);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, importer.defaultSampleSettings.compressionFormat, path);
            Assert.IsTrue(importer.loadInBackground, path);
            StringAssert.Contains("\n  normalize: 0", File.ReadAllText(ArtAudioSpec.ProjectPath(path + ".meta")).Replace("\r", ""), path);
        }

        [Test]
        public void WaitingRoom_NoLongerBorrowsLoading()
        {
            var set = AssetDatabase.LoadAssetAtPath<AudioRuntimeSet>(RuntimeSetPath);
            Assert.AreNotSame(set.MusicFor(MusicTrack.Loading), set.MusicFor(MusicTrack.WaitingRoom));
        }
    }
}
