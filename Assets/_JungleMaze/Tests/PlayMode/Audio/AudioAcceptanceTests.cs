#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JungleMaze.App.Audio;
using JungleMaze.App.Audio.Mocks;
using JungleMaze.App.Flow;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Audio;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace JungleMaze.Tests.PlayMode
{
    /// <summary>
    /// AUD-01 on real AudioSources and the real mixer/catalog: one voice per request, bounded pool with priorities,
    /// mixer decibels, server-event dedupe, music on/off, ducking and a 10 s stress burst. No scene is loaded; the
    /// test composes the same objects as Bootstrap.Audio. Volumes are kept in memory, never in PlayerPrefs.
    /// </summary>
    public sealed class AudioAcceptanceTests
    {
        private const float Dt = 1f / 60f;
        private static readonly string[] MixerParams =
        {
            AudioRuntimeSet.MasterVolumeParam, AudioRuntimeSet.SfxVolumeParam, AudioRuntimeSet.AlertVolumeParam,
            AudioRuntimeSet.MusicVolumeParam, AudioRuntimeSet.MusicDuckParam
        };

        private readonly List<Object> _objects = new List<Object>();
        private AudioRuntimeSet _set;
        private AudioManager _manager;
        private HudStateStore _hud;
        private InventoryStateStore _inventory;
        private MusicPlan _music;
        private SfxPlaybackService _service;
        private MockAudioEventSource _mock;
        private SfxRouter _router;

        private sealed class MemorySettings : IAudioSettingsStore
        {
            private readonly float[] _v = { 1f, 1f, 1f, 1f };
            private bool _music;
            public float Load(AudioBus bus, float fallback) => _v[(int)bus];
            public void Save(AudioBus bus, float volume01) => _v[(int)bus] = volume01;
            public bool LoadMusicEnabled(bool fallback) => _music;
            public void SaveMusicEnabled(bool enabled) => _music = enabled;
            public void Flush() { }
        }

        [SetUp]
        public void SetUp()
        {
            _set = Resources.Load<AudioRuntimeSet>(AudioRuntimeSet.ResourcePath);
            Assert.IsNotNull(_set, "Art/Audio/Resources/AudioRuntimeSet.asset");
            Compose(_set);
        }

        [TearDown]
        public void TearDown()
        {
            _router?.Dispose();
            _service?.StopAll();
            foreach (var p in MixerParams) _set.Mixer.ClearFloat(p);
            for (var i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        private void Compose(AudioRuntimeSet set)
        {
            _router?.Dispose();
            var bus = new EventBus();
            var balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
            _hud = new HudStateStore(bus, balance);
            _inventory = new InventoryStateStore(bus, balance);
            _hud.SetPose(0f, 0f, 0f);
            var go = new GameObject("AudioAcceptance");
            _objects.Add(go);
            if (Object.FindAnyObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
            _manager = go.AddComponent<AudioManager>().Configure(set);
            _music = new MusicPlan();
            _service = new SfxPlaybackService(set.Catalog.BuildRules(), _manager, _hud, new MemorySettings(), _music);
            _manager.BindMusic(_music);
            _mock = new MockAudioEventSource();
            _router = new SfxRouter(_service, _hud, _inventory, bus, balance, new IAudioEventSource[] { _mock }, _music);
            _router.Initialize();
        }

        private void Step(float dt = Dt)
        {
            _mock.Tick(dt);
            _router.Tick(dt);
            _service.Tick(dt);
            _manager.Music.Apply();
        }

        private int PlayingVoices()
        {
            var n = 0;
            for (var i = 0; i < _manager.VoiceCount; i++) if (_manager.IsVoicePlaying(i)) n++;
            return n;
        }

        private int VoiceOf(SfxId id)
        {
            var clip = _set.Catalog.GetClip(id, 0);
            for (var i = 0; i < _manager.VoiceCount; i++)
                if (_manager.IsVoicePlaying(i) && _manager.VoiceSource(i).clip == clip) return i;
            return -1;
        }

        private float MixerDb(string param)
        {
            Assert.IsTrue(_set.Mixer.GetFloat(param, out var db), param);
            return db;
        }

        [Test]
        public void PlaySfx_EachOfThe26_OneVoiceRightClipAndBus()
        {
            for (var k = 1; k <= SfxIds.Count; k++)
            {
                var id = (SfxId)k;
                _service.StopAll();
                Assert.AreEqual(0, PlayingVoices());
                _service.PlaySfx(id);
                Assert.AreEqual(1, PlayingVoices(), id.ToString());
                var voice = VoiceOf(id);
                Assert.GreaterOrEqual(voice, 0, $"{id} plays its catalog clip");
                var expected = id == SfxId.ZoneBell3 || id == SfxId.DragonRoar ? _set.AlertGroup : _set.SfxGroup;
                Assert.AreSame(expected, _manager.VoiceSource(voice).outputAudioMixerGroup, id.ToString());
                Assert.AreEqual(0f, _manager.VoiceSource(voice).spatialBlend);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Spam100_NeverMoreThan16_NoNewAudioSources()
        {
            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length;
            Assert.AreEqual(18, _manager.GetComponentsInChildren<AudioSource>(true).Length, "16 SFX voices + 2 music");
            for (var frame = 0; frame < 30; frame++)
            {
                _mock.Spam(100);
                Assert.LessOrEqual(PlayingVoices(), 16);
                Step();
                yield return null;
            }
            Assert.AreEqual(sources, Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PoolFullOfLow_CriticalEvictsExactlyOne()
        {
            foreach (var id in new[] { SfxId.FootstepGrass, SfxId.FootstepSwamp, SfxId.Reload, SfxId.BushEnter })
                for (var s = 0; s < 6; s++) _service.Play(AudioCue.Global(id, s));
            Assert.AreEqual(16, PlayingVoices());
            _service.PlaySfx(SfxId.HitMarker);
            Assert.AreEqual(16, PlayingVoices());
            Assert.GreaterOrEqual(VoiceOf(SfxId.HitMarker), 0, "HitMarker plays in the same frame");
            Assert.AreEqual(15, CountLow());
        }

        private int CountLow()
        {
            var n = 0;
            var low = new HashSet<AudioClip>();
            foreach (var id in new[] { SfxId.FootstepGrass, SfxId.FootstepSwamp, SfxId.Reload, SfxId.BushEnter }) low.Add(_set.Catalog.GetClip(id, 0));
            for (var i = 0; i < _manager.VoiceCount; i++) if (_manager.IsVoicePlaying(i) && low.Contains(_manager.VoiceSource(i).clip)) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator SetVolume_MixerDecibels()
        {
            yield return null;
            _service.SetVolume(AudioBus.Master, 0.5f);
            _service.SetVolume(AudioBus.Sfx, 1f);
            _service.SetVolume(AudioBus.Music, 0f);
            _service.SetVolume(AudioBus.Alert, 2f);
            yield return null;
            Assert.AreEqual(-6.02f, MixerDb(AudioRuntimeSet.MasterVolumeParam), 0.1f);
            Assert.AreEqual(0f, MixerDb(AudioRuntimeSet.SfxVolumeParam), 0.1f);
            Assert.AreEqual(-80f, MixerDb(AudioRuntimeSet.MusicVolumeParam), 0.1f);
            Assert.AreEqual(0f, MixerDb(AudioRuntimeSet.AlertVolumeParam), 0.1f, "clamped to 1");
            Assert.AreEqual(0.5f, _service.GetVolume(AudioBus.Master), 0.001f);
            Assert.AreEqual(1f, _service.GetVolume(AudioBus.Alert), 0.001f);
        }

        [Test]
        public void RepeatServerEvent_AddsAtMostOneVoice()
        {
            var before = PlayingVoices();
            _mock.RepeatServerEvent(SfxId.Eliminated, 5);
            Assert.AreEqual(before + 1, PlayingVoices());
            _mock.RepeatServerEvent(SfxId.Eliminated, 5);
            Assert.LessOrEqual(PlayingVoices(), before + 2);
        }

        [Test]
        public void HitMarker_StartsSameFrame_With15Others()
        {
            foreach (var id in new[] { SfxId.ShootRanged, SfxId.MeleeHit, SfxId.MeleeSwing })
                for (var s = 0; s < 6; s++) _service.Play(AudioCue.Global(id, s));
            Assert.AreEqual(14, PlayingVoices());
            _service.PlaySfx(SfxId.Explosion);
            Assert.AreEqual(15, PlayingVoices());
            _service.PlaySfx(SfxId.HitMarker);
            Assert.GreaterOrEqual(VoiceOf(SfxId.HitMarker), 0);
            Assert.AreEqual(16, PlayingVoices());
        }

        [UnityTest]
        public IEnumerator ZoneBell_ThreeBellsOnAlert_DucksMusic_SfxUntouched()
        {
            _service.MusicEnabled = true;
            _music.SetScreen(AppScreen.InMatch);
            for (var t = 0f; t < 1.2f; t += Time.unscaledDeltaTime) { Step(Time.unscaledDeltaTime); yield return null; }
            Assert.AreEqual(1, _manager.Music.PlayingSources);
            var sfxDb = MixerDb(AudioRuntimeSet.SfxVolumeParam);

            var bells = 0;
            var bellClip = _set.Catalog.GetClip(SfxId.ZoneBell3, 0);
            var wasRinging = false;
            var lastTime = 0f;
            _service.PlaySfx(SfxId.ZoneBell3);
            var elapsed = 0f;
            var duckedBy = -1f;
            var released = -1f;
            while (elapsed < 4f)
            {
                var ringing = false;
                var time = 0f;
                for (var i = 0; i < _manager.VoiceCount; i++)
                {
                    var src = _manager.VoiceSource(i);
                    if (!src.isPlaying || src.clip != bellClip) continue;
                    ringing = true;
                    time = src.time;
                }
                if (ringing && (!wasRinging || time < lastTime - 0.05f)) bells++;
                wasRinging = ringing;
                lastTime = time;
                if (duckedBy < 0f && MixerDb(AudioRuntimeSet.MusicDuckParam) <= -8.95f) duckedBy = elapsed;
                if (duckedBy >= 0f && released < 0f && !_service.AlertActive) released = elapsed;
                if (released >= 0f && MixerDb(AudioRuntimeSet.MusicDuckParam) >= -0.01f) break;
                Assert.LessOrEqual(PlayingVoices(), 16);
                Assert.LessOrEqual(_manager.Music.PlayingSources, 2);
                Assert.AreEqual(sfxDb, MixerDb(AudioRuntimeSet.SfxVolumeParam), 0.01f, "SFX bus is never ducked");
                var dt = Time.unscaledDeltaTime;
                Step(dt);
                elapsed += dt;
                yield return null;
            }
            Assert.AreEqual(0, _service.DroppedNoVoice(SfxPriority.Critical), "no bell lost to a full pool");
            Assert.AreEqual(3, bells, "three bell starts");
            Assert.GreaterOrEqual(duckedBy, 0f, "music ducked");
            Assert.LessOrEqual(duckedBy, 0.25f, "duck within 0.2 s (+1 frame)");
            Assert.Greater(released, 1.5f, "alert stays active across the three bells");
            Assert.AreEqual(0f, MixerDb(AudioRuntimeSet.MusicDuckParam), 0.01f, "duck released");
            Assert.LessOrEqual(elapsed - released, 1.05f, "released within 1 s");
        }

        [UnityTest]
        public IEnumerator MusicOffByDefault_NoMusicSource()
        {
            Assert.IsFalse(_service.MusicEnabled);
            foreach (AppScreen screen in System.Enum.GetValues(typeof(AppScreen)))
            {
                _music.SetScreen(screen);
                for (var i = 0; i < 10; i++) { Step(); yield return null; }
                Assert.AreEqual(0, _manager.Music.PlayingSources, screen.ToString());
            }
        }

        [UnityTest]
        public IEnumerator EnableMusic_PlaysScreenTrackWithin1s_DisableFadesOut()
        {
            _music.SetScreen(AppScreen.MainMenu);
            Step();
            _service.MusicEnabled = true;
            var t = 0f;
            while (_manager.Music.PlayingSources == 0 && t < 1f) { Step(); t += Dt; yield return null; }
            Assert.Less(t, 1f);
            Assert.AreEqual(1, _manager.Music.PlayingSources);
            Assert.AreSame(_set.MusicFor(MusicTrack.WaitingRoom), PlayingMusicClip());

            for (var i = 0; i < 70; i++) { Step(); yield return null; }
            _music.SetScreen(AppScreen.InMatch);
            for (var i = 0; i < 30; i++) { Step(); yield return null; Assert.LessOrEqual(_manager.Music.PlayingSources, 2); }
            for (var i = 0; i < 40; i++) { Step(); yield return null; }
            Assert.AreEqual(1, _manager.Music.PlayingSources, "one source after the crossfade");
            Assert.AreSame(_set.MusicFor(MusicTrack.Match), PlayingMusicClip());

            _service.MusicEnabled = false;
            for (var i = 0; i < 36; i++) { Step(); yield return null; }
            Assert.AreEqual(0, _manager.Music.PlayingSources, "silent 0.6 s after turning music off");
        }

        private AudioClip PlayingMusicClip()
        {
            for (var i = 0; i < 2; i++) if (_manager.Music.Source(i).isPlaying) return _manager.Music.Source(i).clip;
            return null;
        }

        [UnityTest]
        public IEnumerator Burst10s_2000Requests_NoErrors_CriticalNotLost()
        {
            _mock.StartBurst(10f, 4);
            var raisedBefore = _mock.Raised;
            for (var frame = 0; frame < 600; frame++)
            {
                Step();
                Assert.LessOrEqual(PlayingVoices(), 16);
                if (frame % 30 == 0) yield return null;
            }
            Assert.GreaterOrEqual(_mock.Raised - raisedBefore, 2000);
            Assert.AreEqual(0, _service.DroppedNoVoice(SfxPriority.Critical), "Critical sounds are never lost to the pool");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DebugPanel_PlayButtonsCallService()
        {
            var panel = _manager.gameObject.AddComponent<AudioDebugPanel>();
            panel.Construct(_service, _mock, _hud, _hud, _music);
            panel.PlayButton(SfxId.HitMarker);
            Assert.AreEqual(1, _service.CountActive(SfxId.HitMarker));
            panel.PlayButton(SfxId.DragonRoar);
            Assert.AreEqual(1, _service.CountActive(SfxId.DragonRoar));
            panel.SpamButton();
            Assert.LessOrEqual(PlayingVoices(), 16);
            var before = _service.CountActive(SfxId.Eliminated);
            panel.RepeatButton();
            Assert.LessOrEqual(_service.CountActive(SfxId.Eliminated), before + 1);
        }

        [Test]
        public void FallbackListener_OnlyWhenSceneHasNone()
        {
            Assert.IsFalse(_manager.OwnsListener, "the test scene already has a listener");
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) Object.DestroyImmediate(l);
            var go = new GameObject("AudioFallback");
            _objects.Add(go);
            var manager = go.AddComponent<AudioManager>().Configure(_set);
            Assert.IsTrue(manager.OwnsListener, "no listener in the scene: the manager provides one");
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator FallbackListener_YieldsToLateListener_AndComesBack()
        {
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.transform.parent == null) Object.DestroyImmediate(l); // the test scene's own listener
            _manager.EnsureListener();
            Assert.IsTrue(_manager.OwnsListener);
            Assert.AreEqual(1, CountEnabledListeners());

            // A camera listener that appears after the scene loaded (Unity logs "2 audio listeners" until the check).
            var camera = new GameObject("LateCameraListener");
            _objects.Add(camera);
            camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            var start = Time.realtimeSinceStartup;
            while (_manager.OwnsListener && Time.realtimeSinceStartup - start < 3f) yield return null;
            Assert.IsFalse(_manager.OwnsListener, "fallback switched off");
            Assert.LessOrEqual(Time.realtimeSinceStartup - start, AudioManager.ListenerCheckSeconds + 0.3f);
            Assert.AreEqual(1, CountEnabledListeners(), "exactly one listener once the scene has its own");

            Object.DestroyImmediate(camera);
            start = Time.realtimeSinceStartup;
            while (!_manager.OwnsListener && Time.realtimeSinceStartup - start < 3f) yield return null;
            Assert.IsTrue(_manager.OwnsListener, "fallback back when the scene listener goes away");
            Assert.LessOrEqual(Time.realtimeSinceStartup - start, AudioManager.ListenerCheckSeconds + 0.3f);
            Assert.AreEqual(1, CountEnabledListeners());
        }

        [Test]
        public void PeriodicListenerCheck_DoesNotAllocate()
        {
            // Fallback on, no camera listener.
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.transform.parent == null) Object.DestroyImmediate(l);
            _manager.EnsureListener();
            Assert.IsTrue(_manager.OwnsListener);
            _manager.CheckListener();
            Assert.That(() => { for (var i = 0; i < 50; i++) _manager.CheckListener(); }, Is.Not.AllocatingGCMemory());

            // Fallback off behind a camera listener.
            var camera = new GameObject("CameraListener");
            _objects.Add(camera);
            camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            _manager.CheckListener();
            Assert.IsFalse(_manager.OwnsListener);
            Assert.That(() => { for (var i = 0; i < 50; i++) _manager.CheckListener(); }, Is.Not.AllocatingGCMemory());
        }

        private static int CountEnabledListeners()
        {
            var n = 0;
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.isActiveAndEnabled) n++;
            return n;
        }

        [Test]
        public void SwapClipInCatalogCopy_NoCodeChange()
        {
            var catalog = Object.Instantiate(_set.Catalog);
            var set = Object.Instantiate(_set);
            _objects.Add(catalog);
            _objects.Add(set);
            typeof(AudioRuntimeSet).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(set, catalog);
            var original = catalog.GetClip(SfxId.MeleeHit, 0);
            var variant = catalog.GetClip(SfxId.MeleeSwing, 0);
            catalog.Find(SfxId.MeleeHit).clips = new[] { variant, original };
            Compose(set);

            AudioClip last = null;
            var seen = new HashSet<AudioClip>();
            for (var i = 0; i < 12; i++)
            {
                _service.StopAll();
                for (var k = 0; k < 10; k++) Step(); // past the 100 ms cooldown
                _service.PlaySfx(SfxId.MeleeHit);
                AudioClip played = null;
                for (var v = 0; v < _manager.VoiceCount; v++) if (_manager.IsVoicePlaying(v)) played = _manager.VoiceSource(v).clip;
                Assert.IsNotNull(played);
                Assert.AreNotSame(last, played, "no immediate repeat with two variants");
                Assert.LessOrEqual(Mathf.Abs(_manager.VoiceSource(0).pitch - 1f), 0.05f + 1e-4f);
                seen.Add(played);
                last = played;
            }
            CollectionAssert.AreEquivalent(new[] { variant, original }, seen);
        }
    }
}
#endif
