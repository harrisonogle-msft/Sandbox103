namespace Sandbox103.V2.Events;

public sealed record class EventBusOptions
{
    public required int? QueueCapacity { get; init; }
}
