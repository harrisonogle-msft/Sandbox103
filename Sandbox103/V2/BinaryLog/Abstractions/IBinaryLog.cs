namespace Sandbox103.V2.Abstractions;

/// <summary>
/// Represents a single <c>.binlog</c> file.
/// </summary>
public interface IBinaryLog
{
    /// <summary>
    /// Path on disk to the <c>.binlog</c> file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The (first) project built in the <c>.binlog</c> file.
    /// </summary>
    public IArchiveFile ProjectFile { get; }
}
