using System;
using System.Collections.Generic;

namespace AURA.Core.Events
{
    /// <summary>
    /// A simple synchronous in-process publish/subscribe event bus.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        private readonly object _sync = new object();

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
        {
            if (handler == null) throw new ArgumentNullException("handler");

            lock (_sync)
            {
                List<Delegate> list;
                Type type = typeof(TEvent);

                if (!_handlers.TryGetValue(type, out list))
                {
                    list = new List<Delegate>();
                    _handlers[type] = list;
                }

                list.Add(handler);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
        {
            lock (_sync)
            {
                List<Delegate> list;
                if (_handlers.TryGetValue(typeof(TEvent), out list))
                {
                    list.Remove(handler);
                }
            }
        }

        public void Publish<TEvent>(TEvent @event) where TEvent : IEvent
        {
            if (@event == null) throw new ArgumentNullException("event");

            List<Delegate> snapshot;

            lock (_sync)
            {
                List<Delegate> list;
                if (!_handlers.TryGetValue(typeof(TEvent), out list))
                {
                    return;
                }

                snapshot = new List<Delegate>(list);
            }

            foreach (Delegate handler in snapshot)
            {
                try
                {
                    ((Action<TEvent>)handler).Invoke(@event);
                }
                catch
                {
                    // Um handler que lança exceção não pode derrubar o publisher.
                }
            }
        }
    }
}
