using Microsoft.Extensions.FileSystemGlobbing;
using Sandbox103.BuildDrops;
using Sandbox103.LogDrops;
using Sandbox103.Repos;

namespace Sandbox103;

public struct RepoConversionOptions
{
    public string RepoPath { get; set; }

    public string BuildDropPath { get; set; }

    public string LogDropPath { get; set; }
}

public class RepoConversion
{
    private readonly LocalGitRepo _repo;
    private readonly LogDrop _logDrop;
    private readonly BuildDrop _buildDrop;

    private List<ProjectFile>? _projectFiles;

    public RepoConversion(RepoConversionOptions options) : this(
        new LocalGitRepo(options.RepoPath),
        new LogDrop(options.LogDropPath),
        new BuildDrop(options.BuildDropPath))
    {
    }

    public RepoConversion(LocalGitRepo repo, LogDrop logDrop, BuildDrop buildDrop)
    {
        _repo = repo;
        _logDrop = logDrop;
        _buildDrop = buildDrop;
    }

    public LocalGitRepo Repo => _repo;

    public LogDrop LogDrop => _logDrop;

    public BuildDrop BuildDrop => _buildDrop;

    public IReadOnlyList<ProjectFile> ProjectFiles => _projectFiles ??= GetProjectFiles(_repo, _logDrop);

    public IReadOnlySet<ProjectImport> GetImportsToRemove(ProjectFile projectFile)
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

        var importsToRemove = new HashSet<ProjectImport>();

        foreach (ProjectImport directImport in rootProjectImport.Imports)
        {
            if (privateTargetsClosure.Contains(directImport))
            {
                importsToRemove.Add(directImport);
            }
        }

        return importsToRemove;
    }

    public static List<ProjectFile> GetProjectFiles(LocalGitRepo repo, LogDrop logDrop)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(logDrop);

        var projectFiles = new List<ProjectFile>();

        foreach (string csproj in repo.EnumerateProjectFiles(fileExtension: ".csproj", relativePaths: true))
        {
            string csprojDir = Path.GetDirectoryName(csproj) ?? throw new DirectoryNotFoundException($"Unable to get containing directory of csproj file '{csproj}'.");
            var glob = new Matcher();
            glob.AddInclude(Path.Join("**", csprojDir, "**", "*.binlog"));

            string binlogPath;

            using (var it = logDrop.Glob(glob).GetEnumerator())
            {
                if (!it.MoveNext())
                {
                    Console.WriteLine($"Unable to find binlog under project directory '{csprojDir}'.");
                    continue;
                }

                binlogPath = it.Current;

                if (it.MoveNext())
                {
                    throw new InvalidOperationException($"Unexpected error: found multiple binlogs under project directory '{csprojDir}'.");
                }
            }

            Console.WriteLine($"Found binlog: {binlogPath}");
            projectFiles.Add(new ProjectFile(Path.Join(repo.BaseDir, csproj), binlogPath));
        }

        return projectFiles;
    }
}
