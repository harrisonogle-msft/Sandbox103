namespace Sandbox103.V2.Events;

public interface INotification
{
    public string Id => Guid.NewGuid().ToString();
}
