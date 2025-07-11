using Sandbox103.BuildDrops;

namespace Sandbox103;

public class ProjectFile
{
    private readonly string _path;
    private readonly string _binLogPath;
    private readonly string _binaryPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectFile"/> class.
    /// </summary>
    /// <param name="path">Path to the .*proj file in the local git repo.</param>
    /// <param name="binLogPath">Path to the .binlog file associated with the build of this project.</param>
    /// <param name="binaryPath">Path to the binary (.dll or .exe) in the build drop.</param>
    public ProjectFile(string path, string binLogPath, string binaryPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(binLogPath);
        ArgumentException.ThrowIfNullOrEmpty(binaryPath);

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

        if (!File.Exists(binaryPath))
        {
            throw new FileNotFoundException("Binary file does not exist.", binaryPath);
        }

        if (!System.IO.Path.GetExtension(binaryPath).EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            !System.IO.Path.GetExtension(binaryPath).EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The given binary path is not a '.dll' or '.exe' file.", nameof(binaryPath));
        }

        _path = path;
        _binLogPath = binLogPath;
        _binaryPath = binaryPath!;
    }

    public string Path => _path;

    public string BinLogPath => _binLogPath;

    public string BinaryPath => _binaryPath;
}
