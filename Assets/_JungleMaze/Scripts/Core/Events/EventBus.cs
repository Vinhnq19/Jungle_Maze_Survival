using System;
using System.Collections.Generic;
using JungleMaze.Core.Logging;

namespace JungleMaze.Core.Events
{
    /// <summary>Observer liên module. Event là struct nên Publish không cấp phát.</summary>
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler) where T : struct;
        void Unsubscribe<T>(Action<T> handler) where T : struct;
        void Publish<T>(in T evt) where T : struct;
        int HandlerCount<T>() where T : struct;
    }

    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, object> _channels = new Dictionary<Type, object>();
        private readonly ILog _log;

        public EventBus(ILog log = null) => _log = log ?? NullLog.Instance;

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Channel<T>(true).Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler != null) Channel<T>(false)?.Remove(handler);
        }

        public void Publish<T>(in T evt) where T : struct => Channel<T>(false)?.Invoke(evt, _log);

        public int HandlerCount<T>() where T : struct => Channel<T>(false)?.Count ?? 0;

        private HandlerList<T> Channel<T>(bool create) where T : struct
        {
            if (_channels.TryGetValue(typeof(T), out var channel)) return (HandlerList<T>)channel;
            if (!create) return null;
            var created = new HandlerList<T>();
            _channels.Add(typeof(T), created);
            return created;
        }

        private sealed class HandlerList<T> where T : struct
        {
            private readonly List<Action<T>> _handlers = new List<Action<T>>();
            private int _publishDepth;
            private bool _hasRemoved;

            public int Count
            {
                get
                {
                    var count = 0;
                    for (var i = 0; i < _handlers.Count; i++) if (_handlers[i] != null) count++;
                    return count;
                }
            }

            public void Add(Action<T> handler)
            {
                if (_handlers.Contains(handler)) return; // Subscribe hai lần không gọi handler hai lần.
                _handlers.Add(handler);
            }

            public void Remove(Action<T> handler)
            {
                var index = _handlers.IndexOf(handler);
                if (index < 0) return;
                // Đang publish thì chỉ đánh dấu null, dọn sau; không làm lệch vòng lặp đang chạy.
                if (_publishDepth > 0) { _handlers[index] = null; _hasRemoved = true; }
                else _handlers.RemoveAt(index);
            }

            public void Invoke(in T evt, ILog log)
            {
                _publishDepth++;
                try
                {
                    // Handler đăng ký trong lúc publish sẽ nhận từ lần Publish sau.
                    var count = _handlers.Count;
                    for (var i = 0; i < count; i++)
                    {
                        var handler = _handlers[i];
                        if (handler == null) continue;
                        try { handler(evt); }
                        catch (Exception e) { log.Error($"Handler {typeof(T).Name} lỗi: {e}"); }
                    }
                }
                finally
                {
                    _publishDepth--;
                    if (_publishDepth == 0 && _hasRemoved)
                    {
                        _handlers.RemoveAll(h => h == null);
                        _hasRemoved = false;
                    }
                }
            }
        }
    }
}
