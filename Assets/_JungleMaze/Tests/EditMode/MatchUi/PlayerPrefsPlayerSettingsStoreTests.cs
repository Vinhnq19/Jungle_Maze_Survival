using JungleMaze.App.MatchUi;
using JungleMaze.Infrastructure.Settings;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.MatchUi
{
    /// <summary>Uses its own key prefix so the player's real <c>jm.settings.*</c> values are never touched.</summary>
    public sealed class PlayerPrefsPlayerSettingsStoreTests
    {
        private const string Prefix = "jm.test.ui03.settings.";
        private PlayerPrefsPlayerSettingsStore _store;

        [SetUp]
        public void SetUp()
        {
            _store = new PlayerPrefsPlayerSettingsStore(Prefix);
            DeleteKeys();
        }

        [TearDown]
        public void TearDown() => DeleteKeys();

        private void DeleteKeys()
        {
            foreach (var key in _store.Keys) PlayerPrefs.DeleteKey(key);
        }

        [Test]
        public void Empty_ReportsNothingSaved()
        {
            Assert.IsFalse(_store.TryLoad(out _, out var shake));
            Assert.IsTrue(shake);
        }

        [Test]
        public void SaveThenLoad_RoundTrips()
        {
            _store.Save(1.4f, false);
            Assert.IsTrue(_store.TryLoad(out var sensitivity, out var shake));
            Assert.AreEqual(1.4f, sensitivity, 1e-5f);
            Assert.IsFalse(shake);
        }

        // AC14: empty string or wrong type in the key → the service falls back to defaults.
        [Test]
        public void WrongTypeOrEmptyString_FallsBackToDefaults()
        {
            PlayerPrefs.SetString(Prefix + PlayerPrefsPlayerSettingsStore.SensitivityName, "");
            PlayerPrefs.SetString(Prefix + PlayerPrefsPlayerSettingsStore.ScreenShakeName, "abc");
            var service = new PlayerSettingsService(_store, null);
            Assert.AreEqual(1f, service.MouseSensitivity);
            Assert.IsTrue(service.ScreenShakeEnabled);

            PlayerPrefs.SetFloat(Prefix + PlayerPrefsPlayerSettingsStore.SensitivityName, 99f);
            Assert.AreEqual(2f, new PlayerSettingsService(_store, null).MouseSensitivity);
        }

        // AC8: the UI-03 keys are under jm.settings.* and none of them is a volume.
        [Test]
        public void Keys_AreSettingsOnly_NoVolume()
        {
            var real = new PlayerPrefsPlayerSettingsStore();
            Assert.AreEqual(2, real.Keys.Length);
            foreach (var key in real.Keys)
            {
                StringAssert.StartsWith("jm.settings.", key);
                foreach (var word in new[] { "volume", "master", "sfx", "music" })
                    StringAssert.DoesNotContain(word, key.ToLowerInvariant());
            }
        }
    }
}
