using Sandbox103;
using Sandbox103.BuildDrops;
using Sandbox103.Extensions;
using Sandbox103.Helpers;
using Sandbox103.LogDrops;
using System.Globalization;
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

IEnumerable<ProjectFile> projectFiles = rualsV1;

using var packagesPropsSourceStream = new MemoryStream(File.ReadAllBytes(conversion.PackagesPropsFile));
using var packagesPropsReader = new XmlTextReader(packagesPropsSourceStream) { Namespaces = false };
var packagesProps = new XmlDocument();
packagesProps.Load(packagesPropsReader);

using var corextConfigSourceStream = File.OpenRead(conversion.Repo.CorextConfig);
using var corextConfigReader = new XmlTextReader(corextConfigSourceStream) { Namespaces = false };
var corextConfig = new XmlDocument();
corextConfig.Load(corextConfigReader);

IReadOnlyDictionary<string, string> corextPackages = XmlHelper.GetCorextPackages(corextConfig);
Console.WriteLine($"Found {corextPackages.Count} corext package(s).");
foreach ((string id, string version) in corextPackages)
{
    Console.WriteLine($"  {id} {version}");
}

var tfmCondition = new StringBuilder();
IEnumerable<string> tfms = ["net45", "net451", "net452", "net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481"];
using (var it = tfms.GetEnumerator())
{
    if (it.MoveNext())
    {
        tfmCondition.Append(CultureInfo.InvariantCulture, $"'$(TargetFramework)' == '{it.Current}'");
        while (it.MoveNext())
        {
            tfmCondition.Append(CultureInfo.InvariantCulture, $" OR '$(TargetFramework)' == '{it.Current}'");
        }
    }
}

XmlHelper.AddPackageReferencesToProject(
    packagesProps,
    corextPackages.Select(kvp => new BinaryReference(kvp.Key, kvp.Value)),
    "Update",
    "Version",
    itemGroupLabel: "corext.config",
    itemGroupAttributes: [
        new KeyValuePair<string, string>("Condition", tfmCondition.ToString()),
    ]);

foreach (ProjectFile projectFile in projectFiles)
{
    Console.WriteLine("---");

    try
    {
        // Read the project file from the local git repo into memory and write the edits to file.
        string projectFilePath = projectFile.Path;
        using var inputStream = new MemoryStream(File.ReadAllBytes(projectFilePath));
        using var outputStream = File.Open(projectFile.Path, FileMode.Open, FileAccess.Write, FileShare.None);
        using var reader = new XmlTextReader(inputStream)
        {
            Namespaces = false,
        };

        var project = new XmlDocument();
        project.Load(reader);
        if (XmlHelper.IsSdkStyleProject(project))
        {
            Console.WriteLine($"Skipping SDK-style project '{Path.GetFileName(projectFilePath)}'.");
            continue;
        }

        using var writer = new ProjectFileXmlWriter(outputStream);

        RemoveLegacyPackageImports(projectFile, project);
        AddPackageReferences(conversion, projectFile, project, packagesProps);

        XmlHelper.RemoveLegacyProjectAttributes(project);
        XmlHelper.RemoveSdkElements(project);
        XmlHelper.AddSdkElement(project, "Corext.Before", [("Condition", $"'$({Constants.EnableCorextProjectSdk})' == 'true'")]);
        XmlHelper.AddSdkElement(project, "Microsoft.NET.Sdk");
        XmlHelper.AddSdkElement(project, "Corext.After", [("Condition", $"'$({Constants.EnableCorextProjectSdk})' == 'true'")]);
        XmlHelper.RemoveProjectImports(project, static name => name?.EndsWith("Microsoft.CSharp.targets", StringComparison.OrdinalIgnoreCase) is true);
        XmlHelper.RemoveProjectImports(project, static name => name?.EndsWith("\\Environment.props", StringComparison.OrdinalIgnoreCase) is true);
        XmlHelper.RemoveProjectImports(project, static name => string.Equals(name, "$(EnvironmentConfig)", StringComparison.OrdinalIgnoreCase));
        if (XmlHelper.GetProperty(project, "TargetFrameworks") is null &&
            XmlHelper.GetProperty(project, "TargetFramework") is null)
        {
            string? tfm = LocalAssembly.GetTargetFrameworkMoniker(projectFile.BinaryPath);
            if (!string.IsNullOrEmpty(tfm))
            {
                XmlHelper.SetProperty(project, "TargetFramework", tfm);
            }
        }
        XmlHelper.RemoveCompileItems(project, projectFile.Path);

        project.Save(writer);
        outputStream.SetLength(outputStream.Position);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in project '{Path.GetFileName(projectFile.Path)}'. {ex.GetType().FullName}: {ex.Message}");
    }
}

using var packagesPropsFileStream = File.Open(conversion.PackagesPropsFile, FileMode.Open, FileAccess.Write, FileShare.None);
using var packagesPropsXmlWriter = new ProjectFileXmlWriter(packagesPropsFileStream);
packagesProps.Save(packagesPropsXmlWriter);

using var srcDirectoryBuildPropsStream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(conversion.SrcDirectoryBuildPropsFile)));
using var srcDirectoryBuildPropsTextReader = new StreamReader(srcDirectoryBuildPropsStream, Encoding.UTF8);
using var srcDirectoryBuildPropsXmlReader = new XmlTextReader(srcDirectoryBuildPropsTextReader);
using var srcDirectoryBuildPropsOutputStream = File.OpenWrite(conversion.SrcDirectoryBuildPropsFile);
using var srcDirectoryBuildPropsXmlWriter = new ProjectFileXmlWriter(srcDirectoryBuildPropsOutputStream);
var srcDirectoryBuildProps = new XmlDocument();
srcDirectoryBuildProps.Load(srcDirectoryBuildPropsXmlReader);
XmlHelper.SetProperty(srcDirectoryBuildProps, Constants.EnableCorextProjectSdk, "true");
srcDirectoryBuildProps.Save(srcDirectoryBuildPropsXmlWriter);

Console.WriteLine("\nDone.");

static void AddPackageReferencesV2(RepoConversion conversion, ProjectFile projectFile, XmlDocument project, XmlDocument packagesProps)
{
    ArgumentNullException.ThrowIfNull(conversion);
    ArgumentNullException.ThrowIfNull(projectFile);
    ArgumentNullException.ThrowIfNull(project);
    ArgumentNullException.ThrowIfNull(packagesProps);

}

static void AddPackageReferences(RepoConversion conversion, ProjectFile projectFile, XmlDocument project, XmlDocument packagesProps)
{
    ArgumentNullException.ThrowIfNull(conversion);
    ArgumentNullException.ThrowIfNull(projectFile);
    ArgumentNullException.ThrowIfNull(project);
    ArgumentNullException.ThrowIfNull(packagesProps);

    var projectReferences = new HashSet<ProjectFile>();
    var packageReferences = new HashSet<BinaryReference>();
    var implicitPackageReferences = new Dictionary<string, BinaryReference>(StringComparer.OrdinalIgnoreCase);

    foreach (LocalAssembly transitiveDependency in AssemblyHelper.EnumerateDependencies(seedAssemblies: AssemblyHelper.EnumerateDirectDependencyPaths(projectFile.BinaryPath)))
    {
        BinaryReference packageReference = transitiveDependency.ToBinaryReference();
        implicitPackageReferences[packageReference.Name] = packageReference;
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
                packageReferences.Add(reference);
            }
        }
    }

    List<BinaryReference> packageReferenceList = packageReferences.ToList();
    packageReferenceList.Sort(static (x, y) => x.Name?.CompareTo(y.Name) ?? 0);

    Console.WriteLine($"Found {packageReferenceList.Count} PackageReference(s) to add.");
    foreach (BinaryReference packageReference in packageReferenceList)
    {
        Console.WriteLine($"  {packageReference.Name} {packageReference.Version}");
    }

    XmlHelper.AddPackageReferencesToProject(project, packageReferenceList, "Include");

    // Assume this is already done - otherwise there wouldn't be a DLL in the build output.
    // XmlHelper.AddProjectReferencesToProject(...);
}

static int RemoveLegacyPackageImports(ProjectFile projectFile, XmlDocument project)
{
    ArgumentNullException.ThrowIfNull(projectFile);
    ArgumentNullException.ThrowIfNull(project);

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

            Console.WriteLine($"  {unexpandedProjectFile}");
        }

        Predicate<string> shouldRemove = (string projectName) => importsToRemove.Any(x => string.Equals(x.Info.UnexpandedProjectFile, projectName, StringComparison.OrdinalIgnoreCase));
        int numRemoved = XmlHelper.RemoveProjectImports(project, shouldRemove);
        Console.WriteLine($"Removed {numRemoved} project import(s).");

        return numRemoved;
    }
}
