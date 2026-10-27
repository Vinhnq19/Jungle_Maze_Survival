using JungleMaze.App.Audio;
using JungleMaze.App.MatchUi;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Audio;
using JungleMaze.Domain.MatchUi;
using NUnit.Framework;

namespace JungleMaze.Tests.MatchUi
{
    public sealed class PlayerSettingsServiceTests
    {
        private EventBus _bus;
        private FakeSettingsStore _store;
        private FakeVolumeAudio _audio;
        private PlayerSettingsService _service;
        private Recorder<PlayerSettingsChanged> _busEvents;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _store = new FakeSettingsStore();
            _audio = new FakeVolumeAudio();
            _service = new PlayerSettingsService(_store, _bus, _audio);
            _busEvents = new Recorder<PlayerSettingsChanged>();
            _bus.Subscribe(_busEvents.Handler);
        }

        [Test]
        public void Defaults_WhenNothingSaved()
        {
            Assert.AreEqual(1f, _service.MouseSensitivity);
            Assert.IsTrue(_service.ScreenShakeEnabled);
        }

        // AC10
        [Test]
        public void Sensitivity_RangeStep_SameCallEvent()
        {
            var csharp = 0;
            _service.Changed += _ => csharp++;
            _service.SetMouseSensitivity(1.73f);
            Assert.AreEqual(1.7f, _service.MouseSensitivity, 1e-5f);
            Assert.AreEqual(1, csharp, "C# event raised inside the call");
            Assert.AreEqual(1, _busEvents.Count, "EventBus event raised inside the call");
            Assert.AreEqual(PlayerSettingKind.MouseSensitivity, _busEvents.Items[0].Kind);
            Assert.AreEqual(1.7f, _busEvents.Items[0].MouseSensitivity, 1e-5f);
            _service.SetMouseSensitivity(1.71f);
            Assert.AreEqual(1, _busEvents.Count, "same step: no event");
            _service.SetMouseSensitivity(5f);
            Assert.AreEqual(2f, _service.MouseSensitivity);
            _service.SetMouseSensitivity(0.1f);
            Assert.AreEqual(0.5f, _service.MouseSensitivity);
        }

        // AC10: a reader multiplying mouse delta doubles at 2.0 compared with 1.0.
        [Test]
        public void FakeSensitivityReader_DeltaTimesTwo_IsDouble()
        {
            var reader = new FakeSensitivityReader(_service);
            var atOne = reader.Apply(7.5f);
            _service.SetMouseSensitivity(2f);
            Assert.AreEqual(atOne * 2f, reader.Apply(7.5f), 1e-5f);
        }

        // AC11
        [Test]
        public void ScreenShakeOff_FakeShaker_ZeroShakes()
        {
            var shaker = new FakeShaker(_service);
            shaker.Request();
            Assert.AreEqual(1, shaker.Performed);
            _service.SetScreenShake(false);
            Assert.IsFalse(_service.ScreenShakeEnabled, "reader sees false in the same frame");
            Assert.AreEqual(PlayerSettingKind.ScreenShake, _busEvents.Items[0].Kind);
            Assert.IsFalse(_busEvents.Items[0].ScreenShake);
            for (var i = 0; i < 10; i++) shaker.Request();
            Assert.AreEqual(1, shaker.Performed);
        }

        // AC12
        [Test]
        public void DebounceOneSecond_FlushOnClose_NoPerFrameWrites()
        {
            for (var frame = 0; frame < 60; frame++)
            {
                _service.SetMouseSensitivity(frame % 2 == 0 ? 1.2f : 1.3f);
                _service.Tick(1f / 60f);
            }
            Assert.AreEqual(0, _store.Saves, "dragging for a second keeps postponing the write");
            // Last change was at t = 59/60 s; stay just under one second after it.
            for (var frame = 0; frame < 58; frame++) _service.Tick(1f / 60f);
            Assert.AreEqual(0, _store.Saves);
            _service.Tick(1f / 30f);
            Assert.AreEqual(1, _store.Saves, "written ≤ 1 s after the last change");
            Assert.AreEqual(_service.MouseSensitivity, _store.Sensitivity);
            for (var frame = 0; frame < 120; frame++) _service.Tick(1f / 60f);
            Assert.AreEqual(1, _store.Saves, "nothing pending, nothing written");

            _service.SetScreenShake(false);
            _service.Flush();
            Assert.AreEqual(2, _store.Saves, "closing the menu forces the write");
            Assert.IsFalse(_store.Shake);
            _service.Flush();
            Assert.AreEqual(2, _store.Saves);
        }

        [Test]
        public void SavedValues_AreRestored_AndBadOnesClamped()
        {
            var store = new FakeSettingsStore { Has = true, Sensitivity = 1.3f, Shake = false };
            var restored = new PlayerSettingsService(store, null);
            Assert.AreEqual(1.3f, restored.MouseSensitivity, 1e-5f);
            Assert.IsFalse(restored.ScreenShakeEnabled);

            Assert.AreEqual(2f, new PlayerSettingsService(new FakeSettingsStore { Has = true, Sensitivity = 99f }, null).MouseSensitivity);
            Assert.AreEqual(1f, new PlayerSettingsService(new FakeSettingsStore { Has = true, Sensitivity = float.NaN }, null).MouseSensitivity);
            Assert.AreEqual(1f, new PlayerSettingsService(new FakeSettingsStore { Has = true, Sensitivity = float.NegativeInfinity }, null).MouseSensitivity);
        }

        // AC13
        [Test]
        public void ResetDefaults_OneEventPerChangedValue_AndAudioDefaults()
        {
            _service.SetMouseSensitivity(1.8f);
            _service.SetScreenShake(false);
            _busEvents.Items.Clear();
            _service.ResetDefaults();
            Assert.AreEqual(1f, _service.MouseSensitivity);
            Assert.IsTrue(_service.ScreenShakeEnabled);
            Assert.AreEqual(2, _busEvents.Count);
            Assert.AreEqual(PlayerSettingKind.MouseSensitivity, _busEvents.Items[0].Kind);
            Assert.AreEqual(PlayerSettingKind.ScreenShake, _busEvents.Items[1].Kind);
            Assert.AreEqual(AudioVolume.DefaultVolume, _audio.GetVolume(AudioBus.Master));
            Assert.AreEqual(AudioVolume.DefaultVolume, _audio.GetVolume(AudioBus.Sfx));
            Assert.AreEqual(AudioVolume.DefaultVolume, _audio.GetVolume(AudioBus.Music));
            Assert.IsFalse(_audio.MusicEnabled);

            _busEvents.Items.Clear();
            _service.SetScreenShake(false);
            _busEvents.Items.Clear();
            _service.ResetDefaults();
            Assert.AreEqual(1, _busEvents.Count, "only the value that changed raises an event");
        }

        // AC8: volumes never reach the UI-03 store; they live in AUD-01.
        [Test]
        public void NeverStoresVolume()
        {
            _service.ResetDefaults();
            _service.SetMouseSensitivity(1.5f);
            _service.Flush();
            Assert.AreEqual(1, _store.Saves);
            Assert.AreEqual(3, _audio.SetVolumeCalls, "reset writes volumes through IAudioService only");
            Assert.AreEqual(1.5f, _store.Sensitivity, 1e-5f);
            foreach (var method in typeof(IPlayerSettingsStore).GetMethods())
            foreach (var parameter in method.GetParameters())
                StringAssert.DoesNotContain("volume", parameter.Name.ToLowerInvariant(), method.Name);
        }
    }
}
