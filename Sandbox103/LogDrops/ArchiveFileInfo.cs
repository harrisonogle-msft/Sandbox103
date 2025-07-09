namespace Sandbox103.LogDrops;

/// <summary>
/// Represents some computed statistics or observations of a file archived in a binlog,
/// such as a project import.
/// </summary>
public sealed record class ArchiveFileInfo
{
    /// <summary>
    /// The path to the archive file as reported by the binlog.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Whether or not the archive file represents a project file
    /// that contains <c><Reference/></c> item(s).
    /// </summary>
    public required bool HasReferenceItems { get; init; }
}
