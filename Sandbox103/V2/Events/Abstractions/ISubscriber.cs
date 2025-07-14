namespace Sandbox103.V2.Events;

public interface ISubscriber<TNotification>
    where TNotification : INotification
{
    public void Notify(PendingNotification<TNotification> message);
}
