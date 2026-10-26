using JungleMaze.App.Audio;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JungleMaze.App.Audio.Mocks;
#endif
using JungleMaze.App.Flow;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;
using JungleMaze.Infrastructure.Audio;
using JungleMaze.Presentation.Audio;
using UnityEngine;

namespace JungleMaze.Composition
{
    /// <summary>
    /// AUD-01 composition: audio runtime set, voice pool, playback service, match adapter, router and (dev) the
    /// F6 panel. Kept in its own file so <c>Bootstrap.cs</c> only carries the one call.
    /// </summary>
    public sealed partial class Bootstrap
    {
        private void ComposeAudio(IEventBus bus, ITickScheduler ticks, HudStateStore hud, InventoryStateStore inventory,
            IBalanceConfigProvider balance, MatchSession match, IPacketReceiver receiver, GameFlow flow)
        {
            // Loaded once here (never per frame), like UiArtSet: an auto-created Bootstrap has no serialized reference.
            var set = Resources.Load<AudioRuntimeSet>(AudioRuntimeSet.ResourcePath);
            if (set == null || set.Catalog == null || set.Mixer == null)
            {
                _log.Warn("[Bootstrap] AudioRuntimeSet missing or incomplete; audio stays silent.");
                _registry.Register<IAudioService>(new NullAudioService());
                return;
            }

            var output = Attach<AudioManager>().Configure(set);
            var music = new MusicPlan();
            var service = new SfxPlaybackService(set.Catalog.BuildRules(), output, hud, new PlayerPrefsAudioSettings(), music, _log);
            output.BindMusic(music);
            output.BindSettingsFlush(service.FlushSettings);
            var matchSource = new MatchAudioEventSource(receiver, new MatchSessionAudioFeed(match), _registry.Resolve<IPlayerPoseSource>());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var mock = new MockAudioEventSource();
            var sources = new IAudioEventSource[] { matchSource, mock };
#else
            var sources = new IAudioEventSource[] { matchSource };
#endif
            var router = new SfxRouter(service, hud, inventory, bus, balance, sources, music, set.SettleSeconds, () => flow.Screen);

            _registry.Register<IAudioService>(service);
            _registry.Register(service);
            _registry.Register(music);
            _registry.Register(matchSource);
            _registry.Register(router);
            ticks.Add(matchSource);
            ticks.Add(router);
            ticks.Add(service);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _registry.Register(mock);
            ticks.Add(mock);
            Attach<AudioDebugPanel>();
#endif
        }
    }
}
