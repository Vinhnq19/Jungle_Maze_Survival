using System;
using System.Collections.Generic;
using JungleMaze.Core.Logging;
using JungleMaze.Core.Ticking;
using UnityEngine;

namespace JungleMaze.Infrastructure.Ticking
{
    /// <summary>
    /// Một Update duy nhất gọi mọi ITickable (service thuần C#). Add/Remove trong lúc tick có hiệu lực
    /// từ frame sau, nên service có thể tự gỡ mình ngay trong Tick.
    /// </summary>
    public sealed class TickRunner : MonoBehaviour, ITickScheduler
    {
        private readonly List<ITickable> _tickables = new List<ITickable>();
        private readonly List<ITickable> _pendingAdd = new List<ITickable>();
        private readonly List<ITickable> _pendingRemove = new List<ITickable>();
        private ILog _log = NullLog.Instance;
        private bool _ticking;

        public int Count => _tickables.Count;

        public void SetLog(ILog log) => _log = log ?? NullLog.Instance;

        public void Add(ITickable tickable)
        {
            if (tickable == null) throw new ArgumentNullException(nameof(tickable));
            if (_ticking) { _pendingRemove.Remove(tickable); _pendingAdd.Add(tickable); return; }
            if (!_tickables.Contains(tickable)) _tickables.Add(tickable);
        }

        public void Remove(ITickable tickable)
        {
            if (tickable == null) return;
            if (_ticking) { _pendingAdd.Remove(tickable); _pendingRemove.Add(tickable); return; }
            _tickables.Remove(tickable);
        }

        /// <summary>Public để test/scene không có Update vẫn chạy được.</summary>
        public void TickAll(float deltaTime)
        {
            _ticking = true;
            try
            {
                for (var i = 0; i < _tickables.Count; i++)
                {
                    try { _tickables[i].Tick(deltaTime); }
                    catch (Exception e) { _log.Error($"[Tick] {_tickables[i].GetType().Name} lỗi: {e}"); }
                }
            }
            finally
            {
                _ticking = false;
                for (var i = 0; i < _pendingRemove.Count; i++) _tickables.Remove(_pendingRemove[i]);
                for (var i = 0; i < _pendingAdd.Count; i++)
                    if (!_tickables.Contains(_pendingAdd[i])) _tickables.Add(_pendingAdd[i]);
                _pendingRemove.Clear();
                _pendingAdd.Clear();
            }
        }

        private void Update() => TickAll(Time.deltaTime);
    }
}
