using System;
using System.Collections.Generic;

namespace RichCoast.Core
{
    /// <summary>
    /// Typed publish/subscribe bus — the only channel through which zones talk.
    ///
    /// Keyed by payload TYPE rather than by an event-name string, so a typo is a compile error
    /// and the payload type is always known. Payloads are structs and handlers are
    /// <see cref="Action{T}"/>, so a dispatch allocates nothing on the managed heap (the
    /// per-frame allocation budget on the target hardware is zero).
    ///
    /// Subscribing or unsubscribing from inside a handler is safe: the change is queued and
    /// applied once the current dispatch finishes, so the handler list can never be mutated
    /// mid-iteration.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, object> _channels = new Dictionary<Type, object>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            GetOrCreate<T>().Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_channels.TryGetValue(typeof(T), out var channel))
            {
                ((Channel<T>)channel).Remove(handler);
            }
        }

        /// <summary>Dispatch to every current subscriber. No subscribers is not an error.</summary>
        public void Emit<T>(T payload)
        {
            if (_channels.TryGetValue(typeof(T), out var channel))
            {
                ((Channel<T>)channel).Emit(payload);
            }
        }

        /// <summary>Emit a payload type that carries no data (a pure signal).</summary>
        public void Emit<T>() where T : struct => Emit(default(T));

        public int SubscriberCount<T>() =>
            _channels.TryGetValue(typeof(T), out var channel) ? ((Channel<T>)channel).Count : 0;

        /// <summary>Drop every subscription. Called on teardown so a reloaded run starts clean.</summary>
        public void Clear() => _channels.Clear();

        private Channel<T> GetOrCreate<T>()
        {
            if (!_channels.TryGetValue(typeof(T), out var channel))
            {
                channel = new Channel<T>();
                _channels[typeof(T)] = channel;
            }
            return (Channel<T>)channel;
        }

        private sealed class Channel<T>
        {
            private readonly List<Action<T>> _handlers = new List<Action<T>>();
            private readonly List<Action<T>> _pendingAdds = new List<Action<T>>();
            private readonly List<Action<T>> _pendingRemoves = new List<Action<T>>();
            private bool _dispatching;

            public int Count => _handlers.Count;

            public void Add(Action<T> handler)
            {
                if (_dispatching) _pendingAdds.Add(handler);
                else _handlers.Add(handler);
            }

            public void Remove(Action<T> handler)
            {
                if (_dispatching) _pendingRemoves.Add(handler);
                else _handlers.Remove(handler);
            }

            public void Emit(T payload)
            {
                _dispatching = true;
                try
                {
                    // Index-based so a handler removed mid-dispatch (queued, applied after)
                    // still receives this event — every subscriber sees a consistent snapshot.
                    for (var i = 0; i < _handlers.Count; i++)
                    {
                        _handlers[i](payload);
                    }
                }
                finally
                {
                    _dispatching = false;
                    Flush();
                }
            }

            private void Flush()
            {
                for (var i = 0; i < _pendingRemoves.Count; i++) _handlers.Remove(_pendingRemoves[i]);
                for (var i = 0; i < _pendingAdds.Count; i++) _handlers.Add(_pendingAdds[i]);
                _pendingRemoves.Clear();
                _pendingAdds.Clear();
            }
        }
    }
}
