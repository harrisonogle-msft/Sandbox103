using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Sandbox103;
using Sandbox103.LogDrops;
using Sandbox103.Repos;
using System.Diagnostics;

var repo = new LocalGitRepo(Constants.Repo.FullName);
var logDrop = new LogDrop(Constants.LogDrop.FullName);
List<ProjectFile> projectFiles = GetProjectFiles(repo, logDrop);

foreach (ProjectFile projectFile in projectFiles)
{
    if (!projectFile.Path.Contains("RestLocationServiceProxy.csproj"))
    {
        continue; // only test RUALSv1 for now
    }

    var importReader = new ProjectImportReader();
    ProjectImportGraph importGraph = importReader.Build(projectFile.BinLogPath);

    string projectFileName = Path.GetFileName(projectFile.Path);
    string relativeCsprojFile = NormalizePath(Path.GetRelativePath(repo.SrcRoot, projectFile.Path));
    Console.WriteLine($"Relative csproj file: {relativeCsprojFile}");

    static string NormalizePath(string path) => !path.Contains(Path.AltDirectorySeparatorChar) ? path : path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    ICollection<string> importers = importGraph.Importers;

    // TODO: Could this be determined using just the binlog file?
    string logDropCsproj =
        importers.Select(NormalizePath).Where(path => path.EndsWith(relativeCsprojFile, StringComparison.OrdinalIgnoreCase)).SingleOrDefault() ??
        importers.Where(path => string.Equals(Path.GetFileName(path), projectFileName, StringComparison.OrdinalIgnoreCase)).SingleOrDefault() ??
        throw new InvalidOperationException($"Unexpected error: unable to find '{projectFileName}' in the import graph.");

    Console.WriteLine($"Found csproj file in the log drop: {logDropCsproj}");

    IEnumerable<string> privateTargets = importGraph.Importees.Where(static importee => importee.EndsWith(".private.targets", StringComparison.OrdinalIgnoreCase));

    //string systemValueTuplePrivateTargets = privateTargets.Where(path => path.EndsWith("System.ValueTuple.private.targets", StringComparison.OrdinalIgnoreCase)).Single();
    //Console.WriteLine($"Found System.ValueTuple.private.targets importee: {systemValueTuplePrivateTargets}");

    //if (!importGraph.TryGetImports(systemValueTuplePrivateTargets, out IEnumerator<string>? it))
    //{
    //    throw new InvalidOperationException($"No importers of System.ValueTuple.private.targets.");
    //}
    //using (it)
    //{
    //    if (!it.MoveNext())
    //    {
    //        throw new InvalidOperationException($"Empty iterator for importers of System.ValueTuple.private.targets.");
    //    }

    //    Console.WriteLine($"Enumerating importers of System.ValueTuple.private.targets.");
    //    do
    //    {
    //        Console.WriteLine($"  {it.Current}");
    //    }
    //    while (it.MoveNext());
    //}

    //return;

    var privateTargetsClosure = new HashSet<string>(privateTargets, StringComparer.OrdinalIgnoreCase);

    foreach (string privateTargetsFile in privateTargets)
    {
        foreach (string transitiveImportee in importGraph.EnumerateTransitiveImports(privateTargetsFile, true))
        {
            privateTargetsClosure.Add(transitiveImportee);
        }

        foreach (string transitiveImporter in importGraph.EnumerateTransitiveImports(privateTargetsFile, false))
        {
            privateTargetsClosure.Add(transitiveImporter);
        }
    }

    privateTargetsClosure.RemoveWhere(static file => Path.GetExtension(file)?.EndsWith("proj") is true);

    Console.WriteLine($"Enumerating private targets closure of project '{projectFile.Path}'.");
    foreach (string closureFile in privateTargetsClosure.Order())
    {
        Console.WriteLine($"  {closureFile}");
    }

    if (!importGraph.TryGetImports(logDropCsproj, out IEnumerator<string>? directImports))
    {
        // This is an unexpected error because we already built an import graph using that csproj file's binlog as the root.
        throw new InvalidOperationException($"Unexpected error: project file '{logDropCsproj}' has no direct imports.");
    }

    var directImportsCollection = new List<string>(); // for logging (temporrary)

    var importsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    using (directImports)
    {
        while (directImports.MoveNext())
        {
            string directImport = directImports.Current;

            directImportsCollection.Add(directImport); // for logging (temporary)

            if (privateTargetsClosure.Contains(directImport))
            {
                importsToRemove.Add(directImport);
            }
        }
    }

    Console.WriteLine($"Enumerating direct imports of project file '{relativeCsprojFile}'.");
    directImportsCollection.Sort();
    foreach (string directImport in directImportsCollection)
    {
        Console.WriteLine($"  {directImport}");
    }

    Console.WriteLine($"Enumerating project imports to remove from '{relativeCsprojFile}'.");
    foreach (string importToRemove in importsToRemove.Order())
    {
        Console.WriteLine($"   {importToRemove}");
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
