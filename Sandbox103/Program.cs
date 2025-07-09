using Microsoft.Extensions.FileSystemGlobbing;
using Sandbox103;
using Sandbox103.LogDrops;
using Sandbox103.Repos;

var repo = new LocalGitRepo(Constants.Repo.FullName);
var logDrop = new LogDrop(Constants.LogDrop.FullName);
List<ProjectFile> projectFiles = GetProjectFiles(repo, logDrop);

foreach (ProjectFile projectFile in projectFiles)
{
    //if (!projectFile.Path.Contains("RestLocationServiceProxy.csproj"))
    //{
    //    continue; // only test RUALSv1 for now
    //}

    Console.WriteLine("---");

    var builder = new ProjectImportGraphBuilder(projectFile.BinLogPath);
    var importGraph = builder.Build();

    string projectFileName = Path.GetFileName(projectFile.Path);

    string relativeCsprojFile = Path.GetRelativePath(importGraph.SrcRoot, importGraph.RootProjectFile);
    Console.WriteLine($"Relative csproj file: {relativeCsprojFile}");

    string logDropCsproj = importGraph.RootProjectFile;
    Console.WriteLine($"Found csproj file in the log drop: {logDropCsproj}");

    IEnumerable<ProjectImport> privateTargets = importGraph.ProjectImports.Where(
        p => p.Importers.Count > 0 &&
        (p.ProjectFile.EndsWith(".private.targets", StringComparison.OrdinalIgnoreCase) ||
        (p.ProjectFile.StartsWith(importGraph.SrcRoot, StringComparison.OrdinalIgnoreCase) && p.ContainsReferenceItem())));

    var privateTargetsClosure = new HashSet<ProjectImport>(privateTargets);

    foreach (ProjectImport privateTargetsFile in privateTargets)
    {
        foreach (ProjectImport transitiveImport in importGraph.EnumerateTransitiveImports(privateTargetsFile))
        {
            privateTargetsClosure.Add(transitiveImport);
        }

        foreach (ProjectImport transitiveImporter in importGraph.EnumerateTransitiveImporters(privateTargetsFile))
        {
            privateTargetsClosure.Add(transitiveImporter);
        }
    }

    privateTargetsClosure.RemoveWhere(static item => Path.GetExtension(item.ProjectFile)?.EndsWith("proj") is true);

    //Console.WriteLine($"Enumerating private targets closure of project '{projectFile.Path}'.");
    //foreach (ProjectImport closureFile in privateTargetsClosure.OrderBy(static p => p.ProjectFile))
    //{
    //    Console.WriteLine($"  {closureFile.ProjectFile}");
    //}

    if (!importGraph.TryGetValue(logDropCsproj, out ProjectImport? rootProjectImport))
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
        //else
        //{
        //    if (directImport.ContainsReferenceItem())
        //    {
        //        Console.WriteLine($"(!!!) WARNING (!!!) Direct import '{directImport.ProjectFile}' contains a `Reference` item!");
        //        importsToRemove.Add(directImport);
        //    }
        //}
    }

    if (importsToRemove.Count < 1)
    {
        Console.WriteLine($"No imports to remove from '{relativeCsprojFile}'!");
    }
    else
    {
        Console.WriteLine($"Found {importsToRemove.Count} project imports to remove from '{relativeCsprojFile}'.");
        foreach (ProjectImport importToRemove in importsToRemove.OrderBy(static x => x.ProjectFile))
        {
            Console.WriteLine($"   {importToRemove.ProjectFile}");
        }
    }
}

static List<ProjectFile> GetProjectFiles(LocalGitRepo repo, LogDrop logDrop)
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

Console.WriteLine("\nDone.");
