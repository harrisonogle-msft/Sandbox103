namespace Sandbox103.V2.Notifications;

internal sealed class SdkStyleConversionNotification : INotification
{
    public required SdkStyleConversionOptions Options { get; init; }
}
