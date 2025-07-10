namespace Sandbox103;

public class ProjectFile
{
    private readonly string _path;
    private readonly string _binLogPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectFile"/> class.
    /// </summary>
    /// <param name="path">Path to the .*proj file in the local git repo.</param>
    /// <param name="binLogPath">Path to the .binlog file associated with the build of this project.</param>
    public ProjectFile(string path, string binLogPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(binLogPath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Project file does not exist.", path);
        }

        if (!System.IO.Path.GetExtension(path).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The given path is not a *proj file.", nameof(path));
        }

        if (!File.Exists(binLogPath))
        {
            throw new FileNotFoundException("Binlog file does not exist.", binLogPath);
        }

        if (!System.IO.Path.GetExtension(binLogPath).EndsWith(".binlog", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The given binlog path is not a '.binlog' file.", nameof(binLogPath));
        }

        _path = path;
        _binLogPath = binLogPath;

        // TODO: validate that it's an XML file with the expected structure.
    }

    public string Path => _path;

    public string BinLogPath => _binLogPath;
}
