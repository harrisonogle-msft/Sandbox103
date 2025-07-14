using Microsoft.Extensions.Logging;
using Sandbox103.V2.Notifications;

namespace Sandbox103.V2;

internal sealed class LogDropSubscriber : ISubscriber<LogDropNotification>
{
    private readonly ILogger<LogDropSubscriber> _logger;

    public LogDropSubscriber(
        ILogger<LogDropSubscriber> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public void Notify(PendingNotification<LogDropNotification> message)
    {
        ILogDrop logDrop = message.Notification.LogDrop;

        _logger.LogInformation($"Received log drop '{logDrop.Path}' with {logDrop.BinaryLogs.Count} binary logs.");
    }
}
