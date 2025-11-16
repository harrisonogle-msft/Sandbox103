using Microsoft.Build.Logging;

namespace Sandbox103.V2;

internal static class BinaryLogReplayEventSourceExtensions
{
    public static void ReplayWithForwardCompatibility(this BinaryLogReplayEventSource eventSource, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventSource);
        ArgumentNullException.ThrowIfNull(path);

        using var eventsReader = BinaryLogReplayEventSource.OpenBuildEventsReader(
            BinaryLogReplayEventSource.OpenReader(path),
            closeInput: true,
            allowForwardCompatibility: true);

        eventSource.Replay(eventsReader, cancellationToken);
    }
}
