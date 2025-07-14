namespace Sandbox103.V2;

internal class ProjectFileEvaluator : IProjectFileEvaluator
{
    public Task<ProjectFileTransformation> EvaluateAsync(IProjectFile projectFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectFile);
        cancellationToken.ThrowIfCancellationRequested();

        IArchiveFile archiveFile = projectFile.BinaryLog.ProjectFile;

        var topLevelPrivateTargets = new HashSet<IArchiveFile>();
        var importsToRemove = new HashSet<DirectImport>();

        foreach (DirectImport directImport in archiveFile.GetImports())
        {
            int count = Traverse(directImport, topLevelPrivateTargets);
            if (count > 0)
            {
                // We should remove the import, since it (transitively) imported `count` private targets.
                importsToRemove.Add(directImport);
            }
        }

        // Remove direct imports that (transitively) import private targets.

        string projectFileName = Path.GetFileName(archiveFile.Path);

        if (importsToRemove.Count < 1)
        {
            Console.WriteLine($"No imports to remove from '{projectFileName}'!");
        }
        else
        {
            Console.WriteLine($"Found {importsToRemove.Count} project import(s) to remove from '{projectFileName}'.");

            foreach (DirectImport importToRemove in importsToRemove.OrderBy(static x => x.File.Path))
            {
                string unexpandedProjectFile = importToRemove.UnexpandedProjectName ??
                    throw new InvalidOperationException($"Direct import '{importToRemove.File.Path}' is missing unexpanded project file.");

                Console.WriteLine($"  {unexpandedProjectFile}");
            }

            HashSet<string> unexpandedImportsToRemove = importsToRemove.Select(static x => x.UnexpandedProjectName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            //int numRemoved = XmlHelper.RemoveProjectImports(project, unexpandedImportsToRemove.Contains);
            //Console.WriteLine($"Removed {numRemoved} project import(s).");
        }

        // Check private targets files for package name and version, then add `PackageReference` items.

        var packageReferences = new HashSet<BinaryReference>();

        foreach (IArchiveFile privateTargets in topLevelPrivateTargets)
        {
            if (privateTargets.Features.Get<ICorextPackageFeature>() is ICorextPackageFeature feature)
            {
                packageReferences.Add(new BinaryReference(feature.Id, feature.Version));
            }
        }

        List<BinaryReference> packageReferenceList = packageReferences.ToList();
        packageReferenceList.Sort(static (x, y) => x.Name?.CompareTo(y.Name) ?? 0);

        Console.WriteLine($"Found {packageReferenceList.Count} PackageReference(s) to add.");
        foreach (BinaryReference packageReference in packageReferenceList)
        {
            Console.WriteLine($"  {packageReference.Name} {packageReference.Version}");
        }

        return Task.FromResult(new ProjectFileTransformation(
            importsToRemove.ToList().AsReadOnly(),
            packageReferenceList.AsReadOnly()));
    }

    // Determine whether a project file might contain CoreXT package name and version.
    static bool IsPrivateTargets(IArchiveFile node)
    {
        return node.Path.EndsWith(".private.targets", StringComparison.OrdinalIgnoreCase);
    }

    // Does a DFT on the import graph. If private targets are found, it does not continue searching
    // for "strictly transitive" private targets.
    static int Traverse(DirectImport node, HashSet<IArchiveFile> privateTargets)
    {
        if (IsPrivateTargets(node.File))
        {
            privateTargets.Add(node.File);
            return 1;
        }
        else if (node.File.Features.Get<IContainsReferenceItemFeature>()?.ContainsReferenceItem is true)
        {
            return 1;
        }
        else
        {
            int count = 0;

            foreach (DirectImport transitiveImport in node.File.GetImports())
            {
                count += Traverse(transitiveImport, privateTargets);
            }

            return count;
        }
    }
}
