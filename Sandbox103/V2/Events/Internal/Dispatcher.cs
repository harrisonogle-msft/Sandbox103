using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Sandbox103.V2.Events;

internal sealed class Dispatcher : IDispatcher
{
    private readonly ILogger<Dispatcher> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Type, IDynamicDispatcher> _cache = new();
    private readonly Func<Type, IServiceProvider, IDynamicDispatcher> _factory;

    public Dispatcher(
        ILogger<Dispatcher> logger,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _logger = logger;
        _serviceProvider = serviceProvider;
        _factory = CreateDispatcher; // avoid allocations from method group conversion
    }

    public void Dispatch(PendingNotification pendingNotification)
    {
        Debug.Assert(pendingNotification.Notification.GetType().IsAssignableTo(pendingNotification.NotificationType));
        Debug.Assert(pendingNotification.NotificationType.IsAssignableTo(typeof(INotification)));
        IDynamicDispatcher dispatcher = GetDispatcher(pendingNotification.NotificationType);
        dispatcher.Dispatch(pendingNotification);
    }

    private IDynamicDispatcher GetDispatcher(Type notificationType)
    {
        Debug.Assert(notificationType.IsAssignableTo(typeof(INotification)));

        return _cache.GetOrAdd(notificationType, _factory, _serviceProvider);
    }

    private IDynamicDispatcher CreateDispatcher(Type notificationType, IServiceProvider serviceProvider)
    {
        Debug.Assert(notificationType.IsAssignableTo(typeof(INotification)));
        return (IDynamicDispatcher)ActivatorUtilities.CreateInstance(serviceProvider, typeof(DynamicDispatcher<>).MakeGenericType(notificationType));
    }

    private interface IDynamicDispatcher
    {
        public void Dispatch(in PendingNotification pendingNotification);
    }

    private sealed class DynamicDispatcher<TNotification> : IDynamicDispatcher
        where TNotification : INotification
    {
        private readonly ILogger<DynamicDispatcher<TNotification>> _logger;
        private readonly IEnumerable<ISubscriber<TNotification>> _subscribers;

        public DynamicDispatcher(
            ILogger<DynamicDispatcher<TNotification>> logger,
            IEnumerable<ISubscriber<TNotification>> subscribers)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(subscribers);

            _logger = logger;
            _subscribers = subscribers;
        }

        public void Dispatch(in PendingNotification pendingNotification)
        {
            if (pendingNotification.Notification is not TNotification notification)
            {
                throw new ArgumentException($"Invalid usage: notification type '{pendingNotification.Notification?.GetType()}' is not assignable to type '{typeof(TNotification)}'.", nameof(pendingNotification));
            }

            var message = new PendingNotification<TNotification>(notification, pendingNotification.PublishTimestamp);

            foreach (ISubscriber<TNotification> subscriber in _subscribers)
            {
                Notify(subscriber, message);
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async void Notify(ISubscriber<TNotification> subscriber, PendingNotification<TNotification> message)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            try
            {
                subscriber.Notify(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscriber error.");
            }
        }
    }
}
