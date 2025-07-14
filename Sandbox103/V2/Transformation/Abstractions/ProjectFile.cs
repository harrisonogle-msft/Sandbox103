namespace Sandbox103.V2;

internal sealed record class ProjectFile : IProjectFile
{
    public ProjectFile(string path, IBinaryLog binaryLog)
    {
        ThrowHelper.ThrowIfFileNotFound(path);
        ArgumentNullException.ThrowIfNull(binaryLog);

        Path = path;
        BinaryLog = binaryLog;
    }

    public string Path { get; }

    public IBinaryLog BinaryLog { get; }
}
