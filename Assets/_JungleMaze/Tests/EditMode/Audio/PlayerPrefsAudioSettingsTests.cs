using JungleMaze.Domain.Audio;
using JungleMaze.Infrastructure.Audio;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.Audio
{
    /// <summary>The PlayerPrefs store only stages values; Flush is the single disk write. The user's values are restored.</summary>
    public sealed class PlayerPrefsAudioSettingsTests
    {
        private const string SfxKey = "jm.audio.sfx";
        private bool _hadSfx, _hadMusic;
        private float _sfx;
        private int _music;

        [SetUp]
        public void SetUp()
        {
            _hadSfx = PlayerPrefs.HasKey(SfxKey);
            _sfx = PlayerPrefs.GetFloat(SfxKey);
            _hadMusic = PlayerPrefs.HasKey(PlayerPrefsAudioSettings.MusicEnabledKey);
            _music = PlayerPrefs.GetInt(PlayerPrefsAudioSettings.MusicEnabledKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSfx) PlayerPrefs.SetFloat(SfxKey, _sfx); else PlayerPrefs.DeleteKey(SfxKey);
            if (_hadMusic) PlayerPrefs.SetInt(PlayerPrefsAudioSettings.MusicEnabledKey, _music);
            else PlayerPrefs.DeleteKey(PlayerPrefsAudioSettings.MusicEnabledKey);
        }

        [Test]
        public void SaveStagesValues_FlushWritesOnce()
        {
            var store = new PlayerPrefsAudioSettings();
            Assert.IsFalse(store.HasPendingWrites);
            store.Save(AudioBus.Sfx, 0.25f);
            store.SaveMusicEnabled(true);
            Assert.IsTrue(store.HasPendingWrites, "staged, not yet written to disk");
            Assert.AreEqual(0.25f, store.Load(AudioBus.Sfx, 1f), 1e-4f, "readable right away");
            Assert.IsTrue(store.LoadMusicEnabled(false));
            store.Flush();
            Assert.IsFalse(store.HasPendingWrites);
            store.Flush();
            Assert.IsFalse(store.HasPendingWrites);
        }
    }
}
