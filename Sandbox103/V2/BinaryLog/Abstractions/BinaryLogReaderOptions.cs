namespace Sandbox103.V2.Abstractions;

/// <summary>
/// Options used to configure how <c>.binlog</c> files are read.
/// </summary>
public record struct BinaryLogReaderOptions
{
    /// <summary>
    /// Path to the <c>.binlog</c> file on the local filesystem.
    /// </summary>
    public string Path { get; set; }
}
