namespace Sandbox103;

public class ProjectFile
{
    private readonly string _path;
    private readonly string _binlogPath;

    public ProjectFile(string path, string binlogPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(binlogPath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Project file does not exist.", path);
        }

        if (!System.IO.Path.GetExtension(path).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The given path is not a *proj file.", nameof(path));
        }

        if (!File.Exists(binlogPath))
        {
            throw new FileNotFoundException("Binlog file does not exist.", binlogPath);
        }

        if (!System.IO.Path.GetExtension(binlogPath).EndsWith(".binlog", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The given binlog path is not a '.binlog' file.", nameof(binlogPath));
        }

        _path = path;
        _binlogPath = binlogPath;

        // TODO: validate that it's an XML file with the expected structure.
    }

    public string Path => _path;

    public string BinLogPath => _binlogPath;
}
