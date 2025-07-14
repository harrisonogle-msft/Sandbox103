namespace Sandbox103.V2.Events;

internal interface IDispatcher
{
    public void Dispatch(PendingNotification pendingNotification);
}
