using Sandbox103.LogDrops;

namespace Sandbox103;

public static class ProjectFileExtensions
{
    public static IReadOnlySet<DirectProjectImport> GetImportsToRemove(this ProjectFile projectFile)
    {
        ArgumentNullException.ThrowIfNull(projectFile);

        var builder = new ProjectImportGraphBuilder(projectFile.BinLogPath);
        ProjectImportGraph graph = builder.Build();

        string projectFileName = Path.GetFileName(projectFile.Path);

        string relativeCsprojFile = Path.GetRelativePath(graph.SrcRoot, graph.RootProjectFile);
        Console.WriteLine($"Relative csproj file: {relativeCsprojFile}");

        string logDropCsproj = graph.RootProjectFile;
        Console.WriteLine($"Found csproj file in the log drop: {logDropCsproj}");

        IEnumerable<ProjectImport> privateTargets = graph.ProjectImports.Where(
            p => p.Importers.Count > 0 &&
            (p.ProjectFile.EndsWith(".private.targets", StringComparison.OrdinalIgnoreCase) ||
            (p.ProjectFile.StartsWith(graph.SrcRoot, StringComparison.OrdinalIgnoreCase) && p.ContainsReferenceItem())));

        var privateTargetsClosure = new HashSet<ProjectImport>(privateTargets);

        foreach (ProjectImport privateTargetsFile in privateTargets)
        {
            foreach (ProjectImport transitiveImport in graph.EnumerateTransitiveImports(privateTargetsFile))
            {
                privateTargetsClosure.Add(transitiveImport);
            }

            foreach (ProjectImport transitiveImporter in graph.EnumerateTransitiveImporters(privateTargetsFile))
            {
                privateTargetsClosure.Add(transitiveImporter);
            }
        }

        privateTargetsClosure.RemoveWhere(static item => Path.GetExtension(item.ProjectFile)?.EndsWith("proj") is true);

        if (!graph.TryGetValue(logDropCsproj, out ProjectImport? rootProjectImport))
        {
            // This is an unexpected error because we already built an import graph using that csproj file's binlog as the root.
            throw new InvalidOperationException($"Unexpected error: project file '{logDropCsproj}' has no direct imports.");
        }

        var importsToRemove = new HashSet<DirectProjectImport>();

        foreach (DirectProjectImport directImport in rootProjectImport.Imports)
        {
            if (privateTargetsClosure.Contains(directImport.Value))
            {
                importsToRemove.Add(directImport);
            }
        }

        return importsToRemove;
    }

    /// <summary>
    /// Removes an import from a local project file.
    /// </summary>
    /// <param name="projectFile">The local project file.</param>
    /// <param name="importedProjectName">The import to remove.</param>
    /// <returns><see langword="true"/> if the import was removed, otherwise <see langword="false"/>.</returns>
    public static bool RemoveImport(this ProjectFile projectFile, string importedProjectName)
    {
        ArgumentNullException.ThrowIfNull(projectFile);
        ArgumentException.ThrowIfNullOrEmpty(importedProjectName);

        throw new NotImplementedException();
    }
}
