namespace Sandbox103.V2.Events;

/// <summary>
/// Represents an in-flight notification (published but not yet dispatched).
/// </summary>
internal readonly struct PendingNotification
{
    public static PendingNotification Create<TNotification>(
        TNotification notification,
        DateTimeOffset publishTimestamp)
        where TNotification : INotification
    {
        return new PendingNotification(notification, typeof(TNotification), publishTimestamp);
    }

    private PendingNotification(
        INotification notification,
        Type notificationType,
        DateTimeOffset publishTimestamp)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationType);

        Type runtimeType = notification.GetType();
        if (!runtimeType.IsAssignableTo(notificationType))
        {
            throw new ArgumentException($"The notification runtime type '{runtimeType}' is not assignable to the given static type '{notificationType}'.", nameof(notificationType));
        }

        Notification = notification;
        NotificationType = notificationType;
        PublishTimestamp = publishTimestamp;
    }

    public INotification Notification { get; }

    public Type NotificationType { get; }

    public DateTimeOffset PublishTimestamp { get; }
}
