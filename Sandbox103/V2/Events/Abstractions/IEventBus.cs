namespace Sandbox103.V2.Events;

public interface IEventBus
{
    public ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken) where TNotification : INotification;
}
