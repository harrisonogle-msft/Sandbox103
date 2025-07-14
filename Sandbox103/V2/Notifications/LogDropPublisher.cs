using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sandbox103.V2;

internal sealed class LogDropPublisher : ISubscriber<SdkStyleConversionNotification>
{
    private readonly ILogger<LogDropPublisher> _logger;
    private readonly ILogDropReader _logDropReader;
    private readonly IEventBus _eventBus;
    private readonly IHostApplicationLifetime _applicationLifetime;

    public LogDropPublisher(
        ILogger<LogDropPublisher> logger,
        ILogDropReader logDropReader,
        IArchiveFileIndex archiveFileIndex,
        IEventBus eventBus,
        IHostApplicationLifetime applicationLifetime)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(logDropReader);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(applicationLifetime);

        _logger = logger;
        _logDropReader = logDropReader;
        _eventBus = eventBus;
        _applicationLifetime = applicationLifetime;
    }

    public async void Notify(PendingNotification<SdkStyleConversionNotification> message)
    {
        try
        {
            CancellationToken cancellationToken = _applicationLifetime.ApplicationStopping;
            SdkStyleConversionOptions options = message.Notification.Options;

            // Read and index every `.binlog` file in the log drop.
            ILogDrop logDrop = await _logDropReader.ReadAsync(
                new LogDropReaderOptions { Path = options.LogDropPath },
                cancellationToken);

            await _eventBus.PublishAsync(new LogDropNotification(options, logDrop), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error indexing log drop '{message.Notification?.Options?.LogDropPath}'.");
        }
    }
}
