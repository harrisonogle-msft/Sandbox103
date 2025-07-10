using Sandbox103;
using Sandbox103.LogDrops;

var conversion = new RepoConversion(new RepoConversionOptions
{
    RepoPath = Constants.Repo.FullName,
    LogDropPath = Constants.LogDrop.FullName,
    BuildDropPath = Constants.BuildDrop.FullName,
});

IEnumerable<ProjectFile> projectFiles =
    //conversion.ProjectFiles.Where(projectFile =>
    //Path.GetFileName(projectFile.Path).Equals("RestLocationServiceProxy.csproj", StringComparison.OrdinalIgnoreCase));
    conversion.ProjectFiles;

foreach (ProjectFile projectFile in projectFiles)
{
    Console.WriteLine("---");

    string projectFileName = Path.GetFileName(projectFile.Path);

    IReadOnlySet<DirectProjectImport> importsToRemove = projectFile.GetImportsToRemove();

    if (importsToRemove.Count < 1)
    {
        Console.WriteLine($"No imports to remove from '{projectFileName}'!");
    }
    else
    {
        Console.WriteLine($"Found {importsToRemove.Count} project import(s) to remove from '{projectFileName}'.");
        foreach (DirectProjectImport importToRemove in importsToRemove.OrderBy(static x => x.Value.ProjectFile))
        {
            string unexpandedProjectFile = importToRemove.Info.UnexpandedProjectFile ??
                throw new InvalidOperationException($"Direct import '{importToRemove.Value.ProjectFile}' is missing unexpanded project file.");

            Console.WriteLine($"   {unexpandedProjectFile}");
        }
    }
}

Console.WriteLine("\nDone.");
