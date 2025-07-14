namespace Sandbox103.V2.Notifications;

/// <summary>
/// Indicates there is an indexed log drop ready for further processing.
/// </summary>
internal sealed class LogDropNotification : INotification
{
    public LogDropNotification(SdkStyleConversionOptions options, ILogDrop logDrop)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logDrop);

        Options = options;
        LogDrop = logDrop;
    }

    /// <summary>
    /// The SDK-style conversion inputs.
    /// </summary>
    public SdkStyleConversionOptions Options { get; }

    /// <summary>
    /// The indexed log drop.
    /// </summary>
    public ILogDrop LogDrop { get; }
}
