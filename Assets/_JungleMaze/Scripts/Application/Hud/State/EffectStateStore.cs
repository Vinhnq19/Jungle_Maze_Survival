using System;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Hud;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Hud.State
{
    /// <summary>
    /// Default effect state: a fixed-size list of active effects that counts down every frame (registered in
    /// TickRunner) and removes expired entries, raising <see cref="Changed"/> for add, refresh and removal.
    /// Expiry lives here, not in the HUD, so every buff/debuff reuses the same mechanism (UI-01-P2).
    /// </summary>
    public sealed class EffectStateStore : IEffectState, IEffectStateWriter, ITickable, IInitializable, IDisposable
    {
        private readonly EffectList _effects = new EffectList();
        private readonly IEventBus _bus;
        private readonly Action<NetworkStateChanged> _onNetwork;
        private readonly Action<EffectKind> _onExpired;

        public EffectStateStore(IEventBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _onNetwork = OnNetworkStateChanged;
            _onExpired = kind => Changed?.Invoke(new EffectChange(kind, EffectChangeType.Removed));
        }

        public event Action<EffectChange> Changed;

        public int Count => _effects.Count;
        public ActiveEffect GetAt(int index) => _effects.GetAt(index);
        public bool TryGet(EffectKind kind, out ActiveEffect effect) => _effects.TryGet(kind, out effect);

        public void Initialize() => _bus.Subscribe(_onNetwork);
        public void Dispose() => _bus.Unsubscribe(_onNetwork);

        public void Apply(EffectKind kind, float durationSeconds, float remainingSeconds)
        {
            if (_effects.Apply(kind, durationSeconds, remainingSeconds, out var type)) Changed?.Invoke(new EffectChange(kind, type));
        }

        public void Remove(EffectKind kind)
        {
            if (_effects.Remove(kind)) Changed?.Invoke(new EffectChange(kind, EffectChangeType.Removed));
        }

        /// <summary>Removes every effect, raising <see cref="Changed"/> (Removed) once per removed effect.</summary>
        public void Reset()
        {
            while (_effects.Count > 0) Remove(_effects.GetAt(_effects.Count - 1).Kind);
        }

        public void Tick(float deltaTime) => _effects.Tick(deltaTime, _onExpired);

        private void OnNetworkStateChanged(NetworkStateChanged change)
        {
            if (change.Current == NetworkState.Disconnected || change.Current == NetworkState.Failed) Reset();
        }
    }
}
