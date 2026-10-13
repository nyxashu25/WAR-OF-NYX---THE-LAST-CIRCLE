using System;
using System.Collections.Generic;

namespace WarOfNyx.Core.Events
{
    /// <summary>
    /// Small typed publish/subscribe bus for decoupling systems on ONE machine
    /// (e.g. gameplay -> HUD, lifecycle -> audio). It is not a network channel:
    /// anything that must reach other players goes through the networking layer.
    ///
    /// Handlers may subscribe/unsubscribe while an event is being published.
    /// A throwing handler does not stop the remaining handlers; the exception is
    /// passed to <see cref="HandlerFailed"/> instead.
    /// Not thread-safe: use from the main thread only.
    /// </summary>
    public sealed class GameEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        /// <summary>Receives exceptions thrown by handlers. Unity code routes this to Debug.LogException.</summary>
        public event Action<Exception> HandlerFailed;

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _handlers.Add(typeof(T), list);
            }

            list.Add(handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler != null && _handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0)
                return;

            // Snapshot so handlers can change subscriptions during dispatch.
            var snapshot = list.ToArray();
            foreach (var handler in snapshot)
            {
                try
                {
                    ((Action<T>)handler)(evt);
                }
                catch (Exception ex)
                {
                    HandlerFailed?.Invoke(ex);
                }
            }
        }

        public int SubscriberCount<T>() =>
            _handlers.TryGetValue(typeof(T), out var list) ? list.Count : 0;

        public void Clear() => _handlers.Clear();

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
