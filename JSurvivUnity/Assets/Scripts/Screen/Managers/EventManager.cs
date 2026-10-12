using System;
using System.Collections.Generic;
using UnityEngine;

namespace Managers
{
    public class EventManager
    {
        public static EventManager I { get; } = new EventManager();

        private readonly Dictionary<string, Action<object>> _eventRegistry = new();

        public void AddListener(string eventKey, Action<object> handler)
        {
            if (_eventRegistry.TryGetValue(eventKey, out var thisEvent))
            {
                thisEvent += handler;
                _eventRegistry[eventKey] = thisEvent;
            }
            else
            {
                _eventRegistry.Add(eventKey, handler);
            }
        }

        public void RemListener(string eventKey, Action<object> handler)
        {
            if (_eventRegistry.TryGetValue(eventKey, out var thisEvent))
            {
                thisEvent -= handler;
                _eventRegistry[eventKey] = thisEvent;
            }
        }

        public void Invoke(string eventKey, object data)
        {
            if (_eventRegistry.TryGetValue(eventKey, out var thisEvent))
            {
                thisEvent?.Invoke(data);
            }
        }

        public void ClearAllListeners()
        {
            _eventRegistry.Clear();
            Debug.Log("EventManager: All event listeners have been cleared.");
        }
    }
}