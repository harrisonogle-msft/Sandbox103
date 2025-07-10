using Microsoft.Build.Logging;
using Sandbox103.Helpers;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox103.LogDrops;

public readonly struct DirectProjectImport
{
    public DirectProjectImport(ProjectImport value, ProjectImportInfo info)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!string.Equals(value.ProjectFile, info.ProjectFile, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Project file mismatch: '{value.ProjectFile}' != '{info.ProjectFile}'");
        }

        Value = value;
        Info = info;
    }

    public ProjectImport Value { get; }

    public ProjectImportInfo Info { get; }

    public override int GetHashCode() => Value.GetHashCode();

    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is DirectProjectImport other &&
        Value.Equals(other.Value);
}

/// <summary>
/// Represents a project import within a .binlog file.
/// </summary>
public class ProjectImport
{
    private readonly string _binLogPath;
    private readonly string _projectFile;
    private readonly string _relativePath;
    private readonly Dictionary<ProjectImport, DirectProjectImport> _imports = new();
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

    /// <summary>
    /// Path to the project file according to the binlog.
    /// </summary>
    public string ProjectFile => _projectFile;

    /// <summary>
    /// Relative path to the project file according to the binlog.
    /// </summary>
    public string RelativePath => _relativePath;

    /// <summary>
    /// Archived file content of the project file, if the project file is archived in the binlog.
    /// </summary>
    public string? ProjectFileContent => _projectFileContent ??= GetProjectFileContent();

    /// <summary>
    /// Projects imported directly within the project file.
    /// </summary>
    public IEnumerable<DirectProjectImport> Imports => _imports.Values;

    /// <summary>
    /// Projects directly importing the project file.
    /// </summary>
    public IReadOnlySet<ProjectImport> Importers => _importers;

    public bool ContainsImport(ProjectImport projectImport)
    {
        ArgumentNullException.ThrowIfNull(projectImport);

        return _imports.ContainsKey(projectImport);
    }

    internal void AddImport(ProjectImport value, ProjectImportInfo info)
    {
        var newValue = new DirectProjectImport(value, info);

        if (_imports.TryGetValue(value, out DirectProjectImport existingValue))
        {
            if (!newValue.Equals(existingValue))
            {
                throw new InvalidOperationException($"Unable to add '{value.ProjectFile}'.");
            }
        }

        _imports[value] = newValue;
    }

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
