using Sandbox103;
using Sandbox103.BuildDrops;
using Sandbox103.Extensions;
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

foreach (ProjectFile projectFile in all)
{
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

    var doc = new XmlDocument();
    doc.Load(reader);

    Console.WriteLine("---");
    RemoveLegacyPackageImports(projectFile, doc);
    AddPackageReferences(conversion, projectFile, doc);

    doc.Save(writer);
    outputStream.SetLength(outputStream.Position);
}

Console.WriteLine("\nDone.");

static void AddPackageReferences(RepoConversion conversion, ProjectFile projectFile, XmlDocument doc)
{
    ArgumentNullException.ThrowIfNull(conversion);
    ArgumentNullException.ThrowIfNull(projectFile);
    ArgumentNullException.ThrowIfNull(doc);

    var projectReferences = new HashSet<ProjectFile>();
    var packageReferences = new HashSet<BinaryReference>();
    var implicitPackageReferences = new Dictionary<string, BinaryReference>(StringComparer.OrdinalIgnoreCase);

    Console.WriteLine($"Finding package references.");

    foreach (string directDependencyPath in AssemblyHelper.EnumerateDirectDependencyPaths(projectFile.BinaryPath))
    {
        foreach (LocalAssembly transitiveDependency in AssemblyHelper.EnumerateDependencies(directDependencyPath))
        {
            BinaryReference packageReference = transitiveDependency.ToBinaryReference();
            implicitPackageReferences[packageReference.Name] = packageReference;
        }
    }

    foreach (LocalAssembly localAssembly in AssemblyHelper.EnumerateDirectDependencies(projectFile.BinaryPath))
    {
        BinaryReference reference = localAssembly.ToBinaryReference();

        if (conversion.ProjectReferences.TryGetValue(reference, out ProjectFile? projectReference))
        {
            projectReferences.Add(projectReference);
        }
        else
        {
            bool shouldAdd = false;

            if (implicitPackageReferences.TryGetValue(reference.Name, out BinaryReference implicitPackageReference))
            {
                if (Version.TryParse(reference.Version, out Version? directVersion) &&
                    Version.TryParse(implicitPackageReference.Version, out Version? transitiveVersion))
                {
                    shouldAdd = directVersion > transitiveVersion;
                }
                else
                {
                    shouldAdd = true;
                }
            }
            else
            {
                shouldAdd = true;
            }

            if (shouldAdd)
            {
                Console.WriteLine($"  {reference.Name}/{reference.Version} (Intune: {AssemblyHelper.IsIntuneAssembly(localAssembly.AssemblyName)})");
                packageReferences.Add(reference);
            }
        }
    }

    List<BinaryReference> packageReferenceList = packageReferences.ToList();
    packageReferenceList.Sort(static (x, y) => x.Name?.CompareTo(y.Name) ?? 0);
    XmlHelper.AddPackageReferencesToProject(doc, packageReferenceList, "Include");

    // Assume this is already done - otherwise there wouldn't be a DLL in the build output.
    // XmlHelper.AddProjectReferencesToProject(...);
}

static int RemoveLegacyPackageImports(ProjectFile projectFile, XmlDocument doc)
{
    ArgumentNullException.ThrowIfNull(projectFile);
    ArgumentNullException.ThrowIfNull(doc);

    string projectFileName = Path.GetFileName(projectFile.Path);

    IReadOnlySet<DirectProjectImport> importsToRemove = projectFile.GetImportsToRemove();

    if (importsToRemove.Count < 1)
    {
        Console.WriteLine($"No imports to remove from '{projectFileName}'!");
        return 0;
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

        Predicate<string> shouldRemove = (string projectName) => importsToRemove.Any(x => string.Equals(x.Info.UnexpandedProjectFile, projectName, StringComparison.OrdinalIgnoreCase));
        int numRemoved = XmlHelper.RemoveProjectImports(doc, shouldRemove);
        Console.WriteLine($"Removed {numRemoved} project import(s).");

        return numRemoved;
    }
}
