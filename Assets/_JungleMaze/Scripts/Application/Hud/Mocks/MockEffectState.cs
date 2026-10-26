#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using JungleMaze.App.Hud.State;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.Mocks
{
    /// <summary>
    /// Dev-only stand-in for the effect owner (Game-10) (G-8, AC16). The 8 s default is a test value only: the real
    /// duration comes from Game-10's config (Q3). Not compiled into release builds.
    /// </summary>
    public sealed class MockEffectState
    {
        public const float DefaultAdrenalineSeconds = 8f;
        private readonly IEffectStateWriter _writer;

        public MockEffectState(IEffectStateWriter writer) => _writer = writer ?? throw new ArgumentNullException(nameof(writer));

        public void Adrenaline(float totalSeconds = DefaultAdrenalineSeconds, float remainingSeconds = DefaultAdrenalineSeconds) =>
            _writer.Apply(EffectKind.Adrenaline, totalSeconds, remainingSeconds);

        public void Apply(EffectKind kind, float totalSeconds, float remainingSeconds) => _writer.Apply(kind, totalSeconds, remainingSeconds);
        public void Remove(EffectKind kind) => _writer.Remove(kind);
        public void Clear() => _writer.Reset();
    }
}
#endif
