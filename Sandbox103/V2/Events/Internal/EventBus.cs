using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Threading.Channels;

namespace Sandbox103.V2.Events;

internal sealed class EventBus : BackgroundService, IEventBus
{
    private readonly Channel<PendingNotification> _channel;
    private readonly ILogger<EventBus> _logger;
    private readonly EventBusOptions _options;
    private readonly IDispatcher _dispatcher;

    public EventBus(
        ILogger<EventBus> logger,
        IOptions<EventBusOptions> options,
        IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _logger = logger;
        _options = options.Value;
        _dispatcher = dispatcher;

        int? queueCapacity = _options.QueueCapacity;
        if (queueCapacity.HasValue)
        {
            _channel = Channel.CreateBounded<PendingNotification>(
                new BoundedChannelOptions(queueCapacity.Value)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                });
        }
        else
        {
            _channel = Channel.CreateUnbounded<PendingNotification>();
        }
    }

    public ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();

        var pendingNotification = PendingNotification.Create(notification, DateTimeOffset.UtcNow);

        return _channel.Writer.TryWrite(pendingNotification) ?
            ValueTask.CompletedTask :
            SlowPublishAsync(pendingNotification, cancellationToken);
    }

    private ValueTask SlowPublishAsync(PendingNotification pendingNotification, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(pendingNotification, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (PendingNotification pendingNotification in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            Debug.Assert(pendingNotification.Notification.GetType().IsAssignableTo(pendingNotification.NotificationType));
            Debug.Assert(pendingNotification.NotificationType.IsAssignableTo(typeof(INotification)));

            _dispatcher.Dispatch(pendingNotification);
        }
    }
}
