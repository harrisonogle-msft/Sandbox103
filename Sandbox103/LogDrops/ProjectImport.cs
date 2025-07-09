using Microsoft.Build.Logging;
using Sandbox103.Helpers;

namespace Sandbox103.LogDrops;

public class ProjectImport
{
    private readonly string _binLogPath;
    private readonly string _projectFile;
    private readonly string _relativePath;
    private readonly HashSet<ProjectImport> _imports = new();
    private readonly HashSet<ProjectImport> _importers = new();
    private readonly int _hashCode;
    private string? _projectFileContent;

    public ProjectImport(
        string binLogPath,
        string projectFile,
        string srcRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(binLogPath);
        ArgumentException.ThrowIfNullOrEmpty(projectFile);
        ArgumentException.ThrowIfNullOrEmpty(srcRoot);

        _binLogPath = binLogPath;
        _projectFile = projectFile;
        _relativePath = System.IO.Path.GetRelativePath(srcRoot, projectFile);
        _hashCode = _relativePath.ToLowerInvariant().GetHashCode();
    }

    public string ProjectFile => _projectFile;

    public string RelativePath => _relativePath;

    public string? ProjectFileContent => _projectFileContent ??= GetProjectFileContent();

    public IReadOnlySet<ProjectImport> Imports => _imports;

    public IReadOnlySet<ProjectImport> Importers => _importers;

    internal void AddImport(ProjectImport value) => _imports.Add(value);

    internal void AddImporter(ProjectImport value) => _importers.Add(value);

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ProjectImport other)
        {
            return false;
        }
        return string.Equals(_relativePath, other._relativePath, StringComparison.OrdinalIgnoreCase);
    }

    private string? GetProjectFileContent()
    {
        // Strip the root (F:\) because the archive paths don't contain the ':' but other paths from the binlog do.
        string path = _projectFile;
        string? root = Path.GetPathRoot(path);
        if (!string.IsNullOrEmpty(root))
        {
            path = Path.GetRelativePath(root, path);
        }

        string? foundProjectFile = null;
        string? foundFileContent = null;

        using (BuildEventArgsReader reader = BinLogHelper.OpenBuildEventsReader(_binLogPath))
        {
            reader.ArchiveFileEncountered += (ArchiveFileEventArgs args) =>
            {
                ArchiveData archiveData = args.ArchiveData;
                string fullPath = archiveData.FullPath;

                if (fullPath.EndsWith(path, StringComparison.OrdinalIgnoreCase))
                {
                    if (foundProjectFile is not null && !string.Equals(foundProjectFile, fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Unable to get content for archive file '{path}'; multiple matches found: '{foundProjectFile}', '{fullPath}'");
                    }

                    string archiveFileContent = archiveData.ToArchiveFile().Content;

                    if (foundFileContent is not null && !string.Equals(foundFileContent, archiveFileContent, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Unable to get content for archive file '{path}'; multiple matches found with different file contents.");
                    }

                    foundProjectFile = fullPath;
                    foundFileContent = archiveFileContent;
                }
            };

            while (reader.Read() is not null)
            {
                // Read the binlog; the archive files are at the end.
            }
        }

        return foundFileContent;
    }
}
