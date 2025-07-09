using System.Diagnostics.CodeAnalysis;

namespace Sandbox103.LogDrops;

public class ProjectImportGraph
{
    private readonly HashSet<ProjectImport> _projectImports;
    private readonly Dictionary<string, ProjectImport> _lookup;
    private readonly string _srcRoot;
    private readonly string _rootProjectFile;

    public ProjectImportGraph(
        HashSet<ProjectImport> projectImports,
        Dictionary<string, ProjectImport> lookup,
        string srcRoot,
        string rootProjectFile)
    {
        ArgumentNullException.ThrowIfNull(projectImports);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentException.ThrowIfNullOrEmpty(srcRoot);
        ArgumentException.ThrowIfNullOrEmpty(rootProjectFile);

        _projectImports = projectImports;
        _lookup = lookup;
        _srcRoot = srcRoot;
        _rootProjectFile = rootProjectFile;
    }

    public IReadOnlySet<ProjectImport> ProjectImports => _projectImports;

    public string SrcRoot => _srcRoot;

    public string RootProjectFile => _rootProjectFile;

    public bool TryGetValue(string path, [NotNullWhen(true)] out ProjectImport? value)
    {
        return _lookup.TryGetValue(path, out value);
    }

    public IEnumerable<ProjectImport> EnumerateTransitiveImports(ProjectImport projectFile)
    {
        ArgumentNullException.ThrowIfNull(projectFile);

        var visited = new HashSet<ProjectImport>();
        visited.Add(projectFile);

        return EnumerateCore(projectFile, visited, static p => p.Imports);
    }

    public IEnumerable<ProjectImport> EnumerateTransitiveImporters(ProjectImport projectFile)
    {
        ArgumentNullException.ThrowIfNull(projectFile);

        var visited = new HashSet<ProjectImport>();
        visited.Add(projectFile);

        return EnumerateCore(projectFile, visited, static p => p.Importers);
    }

    private IEnumerable<ProjectImport> EnumerateCore(ProjectImport projectFile, HashSet<ProjectImport> visited, Func<ProjectImport, IEnumerable<ProjectImport>> enumerateDirects)
    {
        foreach (ProjectImport value in enumerateDirects.Invoke(projectFile))
        {
            if (!visited.Add(value))
            {
                throw new InvalidOperationException($"Import cycle detected. ({value})");
            }

            yield return value;

            foreach (ProjectImport transitiveValue in EnumerateCore(value, visited, enumerateDirects))
            {
                yield return transitiveValue;
            }

            if (!visited.Remove(value))
            {
                throw new InvalidOperationException($"Unexpected error: corrupted import graph. ({value})");
            }
        }
    }
}
