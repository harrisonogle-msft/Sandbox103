namespace Sandbox103.V2;

/// <summary>
/// Represents a single <c>.binlog</c> file.
/// </summary>
internal sealed class BinaryLog : IBinaryLog
{
    private readonly string _path;
    private readonly IArchiveFile _projectFile;

    public BinaryLog(string path, IArchiveFile projectFile)
    {
        ThrowHelper.ThrowIfFileNotFound(path);
        ArgumentNullException.ThrowIfNull(projectFile);

        _path = path;
        _projectFile = projectFile;
    }

    /// <inheritdoc/>
    public string Path => _path;

    /// <inheritdoc/>
    public IArchiveFile ProjectFile => _projectFile;
}
