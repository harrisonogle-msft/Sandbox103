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
        string? projectFile = null;
        string? content = null;

        using (BuildEventArgsReader reader = BinLogHelper.OpenBuildEventsReader(_binLogPath))
        {
            reader.ArchiveFileEncountered += (ArchiveFileEventArgs args) =>
            {
                ArchiveData archiveData = args.ArchiveData;
                string fullPath = archiveData.FullPath;

                if (string.Equals(fullPath, _projectFile, StringComparison.OrdinalIgnoreCase))
                {
                    if (projectFile is not null && !string.Equals(projectFile, fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Unable to get content for archive file '{_projectFile}'; multiple matches found: '{projectFile}', '{fullPath}'");
                    }

                    string archiveFileContent = archiveData.ToArchiveFile().Content;

                    if (content is not null && !string.Equals(content, archiveFileContent, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Unable to get content for archive file '{_projectFile}'; multiple matches found with different file contents.");
                    }

                    projectFile = fullPath;
                    content = archiveFileContent;
                }
            };

            while (reader.Read() is not null)
            {
                // Read the binlog; the archive files are at the end.
            }
        }

        return content;
    }
}
