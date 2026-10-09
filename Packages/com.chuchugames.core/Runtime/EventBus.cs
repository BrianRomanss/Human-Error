using System;
using System.Collections.Generic;

namespace ChuchuGames.Core
{
    /// <summary>
    /// Typed publish/subscribe for decoupling systems (e.g. "guest moved on" → audio, achievements, UI).
    /// Subscribing returns a token; dispose it to unsubscribe.
    /// </summary>
    public sealed class EventBus
    {
        readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryGetValue(typeof(T), out var list)) _handlers[typeof(T)] = list = new List<Delegate>();
            list.Add(handler);
            return new Token(() => list.Remove(handler));
        }

        public void Publish<T>(T message)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list)) return;
            // Copy so handlers may unsubscribe while being called.
            foreach (var h in list.ToArray()) ((Action<T>)h)(message);
        }

        sealed class Token : IDisposable
        {
            Action _dispose;
            public Token(Action dispose) => _dispose = dispose;
            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
