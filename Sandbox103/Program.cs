using Sandbox103;
using Sandbox103.BuildDrops;
using Sandbox103.Extensions;
using Sandbox103.Helpers;
using Sandbox103.LogDrops;
using System.Collections.Concurrent;
using System.Diagnostics;
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
IEnumerable<ProjectFile> locationServiceClientLibrary =
    conversion.ProjectFiles.Where(projectFile =>
    Path.GetFileName(projectFile.Path).Equals("LocationService.ClientLibrary.csproj", StringComparison.OrdinalIgnoreCase));

IEnumerable<ProjectFile> projectFiles = all;

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

// If there are inconsistencies between corext.config and packages.props, best to resolve them immediately, so break the build.
const bool CheckExistingPackagesProps = false;

XmlHelper.AddPackageReferencesToProject(
    packagesProps,
    corextPackages.Select(kvp => new BinaryReference(kvp.Key, kvp.Value)),
    "Update",
    "Version",
    itemGroupLabel: "corext.config",
    itemGroupAttributes: [
        new KeyValuePair<string, string>("Condition", tfmCondition.ToString()),
    ],
    checkExisting: CheckExistingPackagesProps);

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

        // V1
        //RemoveLegacyPackageImports(projectFile, project);
        //AddPackageReferencesV1(conversion, projectFile, project, packagesProps);

        // V2
        RemoveImportsAndAddPackageReferences(conversion, projectFile, project, packagesProps);

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
        XmlHelper.RemoveReferenceItems(project);

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

static void RemoveImportsAndAddPackageReferences(RepoConversion conversion, ProjectFile projectFile, XmlDocument project, XmlDocument packagesProps)
{
    ArgumentNullException.ThrowIfNull(conversion);
    ArgumentNullException.ThrowIfNull(projectFile);
    ArgumentNullException.ThrowIfNull(project);
    ArgumentNullException.ThrowIfNull(packagesProps);

    // 1. Traverse import graph for DT files (`.private.targets`)
    //     - Stop depth traversal when one is encountered (avoid strictly transitive private targets).
    //         - But continue traversal; we want all private targets imported by the "direct".
    //     - Save the direct import (the "direct") in a hash set. The "direct" is the deepest import in the DFT stack (first chronologically encountered in the stack).
    // 2. Extract the CoreXT package `id` and `version` from the private targets file.
    // 3. Check if it's in packages.props already.
    //     - Version resolution: choose the higher version or something.
    // 4. Add `PackageReference` items.

    // We'll use this function to determine whether a project file might contain CoreXT package name and version.
    static bool IsPrivateTargets(ProjectImport node)
    {
        return node.ProjectFile.EndsWith(".private.targets", StringComparison.OrdinalIgnoreCase);
    }

    // Does a DFT on the import graph. If private targets are found, it does not continue searching
    // for "strictly transitive" private targets.
    static int Traverse(DirectProjectImport node, HashSet<ProjectImport> privateTargets)
    {
        if (IsPrivateTargets(node.Value))
        {
            privateTargets.Add(node.Value);
            return 1;
        }
        else if (node.Value.ContainsReferenceItem())
        {
            return 1;
        }
        else
        {
            int count = 0;

            foreach (DirectProjectImport transitiveImport in node.Value.Imports)
            {
                count += Traverse(transitiveImport, privateTargets);
            }

            return count;
        }
    }

    ProjectImportGraph graph = projectFile.Imports;
    ProjectImport root = graph.RootProject;

    var topLevelPrivateTargets = new HashSet<ProjectImport>();
    var importsToRemove = new HashSet<DirectProjectImport>();

    foreach (DirectProjectImport directImport in root.Imports)
    {
        int count = Traverse(directImport, topLevelPrivateTargets);
        if (count > 0)
        {
            // We should remove the import, since it (transitively) imported `count` private targets.
            importsToRemove.Add(directImport);
        }
    }

    // Remove direct imports that (transitively) import private targets.

    string projectFileName = Path.GetFileName(projectFile.Path);

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

            Console.WriteLine($"  {unexpandedProjectFile}");
        }

        Predicate<string> shouldRemove = (string projectName) => importsToRemove.Any(x => string.Equals(x.Info.UnexpandedProjectFile, projectName, StringComparison.OrdinalIgnoreCase));
        int numRemoved = XmlHelper.RemoveProjectImports(project, shouldRemove);
        Console.WriteLine($"Removed {numRemoved} project import(s).");
    }

    // Parse private targets files for package name and version, then add `PackageReference` items.

    var packageReferences = new HashSet<BinaryReference>();

    foreach (ProjectImport privateTargets in topLevelPrivateTargets)
    {
        if (TryParsePrivateTargets(privateTargets, out BinaryReference packageReference))
        {
            packageReferences.Add(packageReference);
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

    // Add to packages.props here if it wasn't already added from the corext.config.
    // That can happen if some DT '.targets' file actually represents a metapackage
    // (imports multiple '.private.targets' files).
    XmlHelper.AddPackageReferencesToProject(packagesProps, packageReferenceList, "Update", "Version", "corext.config", checkExisting: CheckExistingPackagesProps);

    // Assume this is already done - otherwise there wouldn't be a DLL in the build output.
    // XmlHelper.AddProjectReferencesToProject(...);
}

static bool TryParsePrivateTargets(ProjectImport privateTargets, out BinaryReference packageReference)
{
    string fileName = Path.GetFileName(privateTargets.RelativePath);

    ConcurrentDictionary<string, BinaryReference> cache = Caching.PrivateTargetsCache;

    if (cache.TryGetValue(privateTargets.RelativePath, out packageReference))
    {
        Trace.WriteLine($"**** Got '{fileName}' from cache: '{packageReference.Name}'/'{packageReference.Version}'");
        return true;
    }

    if (TryParsePrivateTargetsCore(privateTargets, out packageReference))
    {
        Trace.WriteLine($"**** Found '{fileName}': '{packageReference.Name}'/'{packageReference.Version}'");
        string key = privateTargets.RelativePath;
        while (!cache.TryAdd(key, packageReference))
        {
            if (cache.TryGetValue(key, out BinaryReference other))
            {
                if (!packageReference.Equals(other))
                {
                    throw new InvalidOperationException($"Unexpected error: cache inconsistency + race condition.");
                }
                break;
            }
        }
        return true;
    }

    Trace.TraceWarning($"**** [!] Unable to find package name and version for '{fileName}'.");
    return false;

    static bool TryParsePrivateTargetsCore(ProjectImport privateTargets, out BinaryReference packageReference)
    {
        string? rawXml = privateTargets.ProjectFileContent;
        if (string.IsNullOrEmpty(rawXml))
        {
            packageReference = default;
            return false;
        }

        using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(rawXml));
        using var sourceTextReader = new StreamReader(sourceStream, Encoding.UTF8);
        using var sourceXmlReader = new XmlTextReader(sourceTextReader) { Namespaces = false };

        var document = new XmlDocument();
        document.Load(sourceXmlReader);

        string projectFileName = Path.GetFileName(privateTargets.ProjectFile);

        if (XmlHelper.TryParsePrivateTargets(document, projectFileName, out string? packageId, out string? packageVersion))
        {
            packageReference = new BinaryReference(packageId, packageVersion);
            return true;
        }

        packageReference = default;
        return false;
    }
}

// TODO: ugly and bad
static class Caching
{
    public static readonly ConcurrentDictionary<string, BinaryReference> PrivateTargetsCache = new(StringComparer.OrdinalIgnoreCase);

    public static readonly ConcurrentDictionary<string, bool> ContainsReferenceCache = new(StringComparer.OrdinalIgnoreCase);
}
