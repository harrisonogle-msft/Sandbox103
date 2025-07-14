using Microsoft.Build.Framework;

namespace Sandbox103.V2.Abstractions;

public interface IEventSourceSubscriber
{
    public void EventSourceCreated(IEventSource eventSource, IArchiveFile projectFile);
}
