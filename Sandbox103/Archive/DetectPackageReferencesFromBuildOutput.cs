using Sandbox103.BuildDrops;
using Sandbox103.Extensions;
using Sandbox103.Helpers;

namespace Sandbox103.Archive;

// I don't think I'll be using the build output anymore, if I can help it.
// 1. Package names differ from assembly names. That stinks.
// 2. Versions are finicky - for instance, Intune uses file version of the
//    DLL to track the package version, instead of the version baked into
//    the assembly metadata.
// 3. Downloading the build drop takes like 5 minutes, which could be annoying
//    in bulk, or when rerunning the program later with update-to-date inputs.

internal static class DetectPackageReferencesFromBuildOutput
{
    public static IReadOnlyList<BinaryReference> Archived_DetectPackageReferencesFromBuildOutput(RepoConversion conversion, ProjectFile projectFile)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        ArgumentNullException.ThrowIfNull(projectFile);

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

        return packageReferenceList.AsReadOnly();
    }
}
