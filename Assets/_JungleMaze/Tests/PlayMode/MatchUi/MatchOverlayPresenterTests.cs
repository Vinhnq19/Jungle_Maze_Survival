#if UNITY_EDITOR
using System;
using JungleMaze.App.Audio;
using JungleMaze.App.Flow;
using JungleMaze.App.MatchUi;
using JungleMaze.App.MatchUi.Mocks;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Audio;
using JungleMaze.Presentation.UI.MatchOverlay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode.MatchUi
{
    /// <summary>The menu rows on the real prefab (EditMode, no Play Mode): values written and read back in one call.</summary>
    public sealed class MatchOverlayPresenterTests
    {
        private const string PrefabPath = "Assets/_JungleMaze/Prefabs/UI/MatchOverlay/MatchOverlay.prefab";
        private EventBus _bus;
        private MatchUiPlayFakes.VolumeAudio _audio;
        private PlayerSettingsService _settings;
        private MatchUiPlayFakes.Store _store;
        private MatchMenuController _menu;
        private MatchOverlayFlow _flow;
        private MatchOverlayRoot _root;
        private MatchOverlayPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _audio = new MatchUiPlayFakes.VolumeAudio();
            _store = new MatchUiPlayFakes.Store();
            _settings = new PlayerSettingsService(_store, _bus, _audio);
            _menu = new MatchMenuController(_bus, () => 0d, () => Guid.Empty, AppScreen.InMatch, _settings);
            _flow = new MatchOverlayFlow(_bus, () => 0d, new MockEliminationSource(), new MockSpectatorSource(), AppScreen.InMatch);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.NotNull(prefab, PrefabPath);
            _root = Object.Instantiate(prefab).GetComponent<MatchOverlayRoot>();
            _presenter = new MatchOverlayPresenter(_root, _menu, _flow, _settings, _audio);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            Object.DestroyImmediate(_root.gameObject);
            _flow.Dispose();
            _menu.Dispose();
        }

        // AC7: initial values from GetVolume, a drag reaches GetVolume in the same call.
        [Test]
        public void VolumeRows_StartFromGetVolume_WriteThrough()
        {
            _audio.Volumes[(int)AudioBus.Sfx] = 0.3f;
            _menu.Open();
            Assert.AreEqual(0.3f, _root.Menu.Sfx.value, 1e-4f);
            Assert.AreEqual("30%", _root.Menu.SfxValue.text);
            _root.Menu.Master.value = 0.42f;
            Assert.AreEqual(0.42f, _audio.GetVolume(AudioBus.Master), 0.01f);
            Assert.AreEqual("42%", _root.Menu.MasterValue.text);
            _root.Menu.Music.value = 0f;
            Assert.AreEqual(0f, _audio.GetVolume(AudioBus.Music));
            Assert.AreEqual(AudioVolume.MinDecibels, AudioVolume.ToDecibels(_audio.GetVolume(AudioBus.Music)), "0 → bus silent");
        }

        // AC9
        [Test]
        public void MusicToggle_ReflectsMusicEnabled_LabelWhenOff()
        {
            _audio.MusicEnabled = false;
            _menu.Open();
            Assert.IsFalse(_root.Menu.MusicEnabled.isOn);
            Assert.IsTrue(_root.Menu.MusicOffLabel.gameObject.activeSelf);
            Assert.AreEqual(MatchOverlayPresenter.MusicOffText, _root.Menu.MusicOffLabel.text);
            Assert.IsTrue(_root.Menu.Music.interactable, "music slider still usable while off");
            _root.Menu.MusicEnabled.isOn = true;
            Assert.IsTrue(_audio.MusicEnabled);
            Assert.IsFalse(_root.Menu.MusicOffLabel.gameObject.activeSelf);
        }

        // AC10/AC11 through the view.
        [Test]
        public void SensitivityAndShake_RowsWriteTheService()
        {
            _menu.Open();
            Assert.AreEqual(5f, _root.Menu.Sensitivity.minValue);
            Assert.AreEqual(20f, _root.Menu.Sensitivity.maxValue);
            Assert.IsTrue(_root.Menu.Sensitivity.wholeNumbers, "0.1 steps");
            Assert.AreEqual(10f, _root.Menu.Sensitivity.value, "default 1.0");
            _root.Menu.Sensitivity.value = 17f;
            Assert.AreEqual(1.7f, _settings.MouseSensitivity, 1e-5f);
            Assert.AreEqual("1,7", _root.Menu.SensitivityValue.text);
            Assert.IsTrue(_root.Menu.ScreenShake.isOn, "shake on by default");
            _root.Menu.ScreenShake.isOn = false;
            Assert.IsFalse(_settings.ScreenShakeEnabled);
        }

        // AC13 through the view: every row updates in the same call.
        [Test]
        public void Defaults_ResetsEveryRow()
        {
            _menu.Open();
            _root.Menu.Sensitivity.value = 18f;
            _root.Menu.ScreenShake.isOn = false;
            _root.Menu.Master.value = 0.2f;
            _root.Menu.MusicEnabled.isOn = true;
            _root.Menu.Defaults.onClick.Invoke();
            Assert.AreEqual(10f, _root.Menu.Sensitivity.value);
            Assert.IsTrue(_root.Menu.ScreenShake.isOn);
            Assert.AreEqual(AudioVolume.DefaultVolume, _root.Menu.Master.value);
            Assert.IsFalse(_root.Menu.MusicEnabled.isOn);
        }

        // AC1 (logic): open is clickable at once and fully visible after the fade; close stops clicks at once.
        [Test]
        public void Fade_OpenWithin200ms_CloseWithin150ms()
        {
            Assert.AreEqual(0f, _root.Menu.Group.alpha);
            _menu.Open();
            Assert.IsTrue(_root.Menu.Group.interactable);
            for (var t = 0f; t < 0.2f; t += 1f / 60f) _presenter.Tick(1f / 60f);
            Assert.AreEqual(1f, _root.Menu.Group.alpha);
            _menu.Close();
            Assert.IsFalse(_root.Menu.Group.interactable);
            for (var t = 0f; t < 0.15f; t += 1f / 60f) _presenter.Tick(1f / 60f);
            Assert.AreEqual(0f, _root.Menu.Group.alpha);
            Assert.IsFalse(_root.Menu.Group.blocksRaycasts);
        }

        // AC15/AC16 through the buttons.
        [Test]
        public void LeaveButtons_ConfirmOnce_LockButtons()
        {
            var leaves = new MatchUiPlayFakes.Recorder<LeaveMatchRequested>();
            _bus.Subscribe(leaves.Handler);
            _menu.Open();
            _root.Menu.Leave.onClick.Invoke();
            Assert.IsTrue(_root.Confirm.Group.interactable);
            _root.Confirm.Stay.onClick.Invoke();
            Assert.AreEqual(0, leaves.Count);
            _root.Menu.Leave.onClick.Invoke();
            for (var i = 0; i < 5; i++) _root.Confirm.Confirm.onClick.Invoke();
            Assert.AreEqual(1, leaves.Count);
            Assert.IsFalse(_root.Menu.Leave.interactable, "locked after the request");
            Assert.IsFalse(_root.Menu.RetryNotice.gameObject.activeSelf);
        }

        // AC12 (review Gợi ý 3): a change made less than the debounce ago is written when the app pauses or quits.
        [Test]
        public void PauseOrQuit_FlushesPendingSettings()
        {
            _root.BindSettingsFlush(_settings.Flush);
            _menu.Open();
            _root.Menu.Sensitivity.value = 15f;
            Assert.AreEqual(0, _store.Saves, "still inside the 1 s debounce");
            _root.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(1, _store.Saves);
            _root.Menu.ScreenShake.isOn = false;
            _root.SendMessage("OnApplicationQuit");
            Assert.AreEqual(2, _store.Saves);
            _root.SendMessage("OnApplicationPause", false);
            Assert.AreEqual(2, _store.Saves, "resume writes nothing");
        }

        [Test]
        public void Canvas_OffOutsideMatch()
        {
            Assert.IsTrue(_root.Canvas.enabled);
            _bus.Publish(new AppScreenChanged(AppScreen.InMatch, AppScreen.MainMenu, null));
            Assert.IsFalse(_root.Canvas.enabled);
        }
    }
}
#endif
