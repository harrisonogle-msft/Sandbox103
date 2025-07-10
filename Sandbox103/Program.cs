using Sandbox103;
using Sandbox103.LogDrops;

var conversion = new RepoConversion(new RepoConversionOptions
{
    RepoPath = Constants.Repo.FullName,
    LogDropPath = Constants.LogDrop.FullName,
    BuildDropPath = Constants.BuildDrop.FullName,
});

// Test a subset of csproj files for now.
IEnumerable<ProjectFile> projectFiles = conversion.ProjectFiles.Where(projectFile =>
    Path.GetFileName(projectFile.Path).Contains("LocationService", StringComparison.OrdinalIgnoreCase));

foreach (ProjectFile projectFile in projectFiles)
{
    Console.WriteLine("---");

    IReadOnlySet<ProjectImport> importsToRemove = conversion.GetImportsToRemove(projectFile);

    string projectFileName = Path.GetFileName(projectFile.Path);

    if (importsToRemove.Count < 1)
    {
        Console.WriteLine($"No imports to remove from '{projectFileName}'!");
    }
    else
    {
        Console.WriteLine($"Found {importsToRemove.Count} project imports to remove from '{projectFileName}'.");
        foreach (ProjectImport importToRemove in importsToRemove.OrderBy(static x => x.ProjectFile))
        {
            Console.WriteLine($"   {importToRemove.ProjectFile}");
        }
    }
}

Console.WriteLine("\nDone.");
