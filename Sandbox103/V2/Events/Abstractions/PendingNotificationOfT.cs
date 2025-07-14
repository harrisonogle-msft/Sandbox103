namespace Sandbox103.V2.Events;

public readonly struct PendingNotification<TNotification>
    where TNotification : INotification
{
    public PendingNotification(TNotification notification, DateTimeOffset publishTimestamp)
    {
        ArgumentNullException.ThrowIfNull(notification);

        Notification = notification;
        PublishTimestamp = publishTimestamp;
    }

    public TNotification Notification { get; }

    public DateTimeOffset PublishTimestamp { get; }
}
