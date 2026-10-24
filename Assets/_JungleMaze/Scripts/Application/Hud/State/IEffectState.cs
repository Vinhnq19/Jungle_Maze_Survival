using System;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.State
{
    /// <summary>
    /// Read port of the effect state (EffectState): the list of active status effects, one per <see cref="EffectKind"/>.
    /// Remaining time counts down inside the store; <see cref="Changed"/> fires on add, refresh and removal.
    /// </summary>
    public interface IEffectState
    {
        event Action<EffectChange> Changed;
        int Count { get; }
        /// <summary>Ordered by start time (0 = oldest).</summary>
        ActiveEffect GetAt(int index);
        bool TryGet(EffectKind kind, out ActiveEffect effect);
    }

    /// <summary>Write port of the effect state (Game-10, UI-01-P2 sources, dev-only mocks).</summary>
    public interface IEffectStateWriter
    {
        /// <summary>Adds the effect, or refreshes it if already active. Duration 0 = untimed.</summary>
        void Apply(EffectKind kind, float durationSeconds, float remainingSeconds);
        void Remove(EffectKind kind);
        /// <summary>Removes every effect.</summary>
        void Reset();
    }
}
