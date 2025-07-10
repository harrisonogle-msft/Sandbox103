using Sandbox103;
using Sandbox103.Helpers;
using Sandbox103.LogDrops;
using System.Text;
using System.Xml;

var conversion = new RepoConversion(new RepoConversionOptions
{
    RepoPath = Constants.Repo.FullName,
    LogDropPath = Constants.LogDrop.FullName,
    BuildDropPath = Constants.BuildDrop.FullName,
});

IEnumerable<ProjectFile> all = conversion.ProjectFiles;
IEnumerable<ProjectFile> rualsV1 =
    conversion.ProjectFiles.Where(projectFile =>
    Path.GetFileName(projectFile.Path).Equals("RestLocationServiceProxy.csproj", StringComparison.OrdinalIgnoreCase));

foreach (ProjectFile projectFile in rualsV1)
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

        // Read the project file from the local git repo into memory and write the edits to file.
        string projectFilePath = projectFile.Path;
        using var inputStream = new MemoryStream(File.ReadAllBytes(projectFilePath));
        using var outputStream = File.Open(projectFile.Path, FileMode.Open, FileAccess.Write, FileShare.None);
        using var reader = new XmlTextReader(inputStream)
        {
            Namespaces = false,
        };
        using var writer = new XmlTextWriter(outputStream, Encoding.UTF8)
        {
            Namespaces = false,
            Formatting = Formatting.Indented,
            Indentation = 2,
        };

        Predicate<string> shouldRemove = (string projectName) => importsToRemove.Any(x => string.Equals(x.Info.UnexpandedProjectFile, projectName, StringComparison.OrdinalIgnoreCase));
        int numRemoved = XmlHelper.RemoveProjectImports(reader, writer, shouldRemove);
        Console.WriteLine($"Removed {numRemoved} project import(s).");
        outputStream.SetLength(outputStream.Position);
    }
}

Console.WriteLine("\nDone.");
