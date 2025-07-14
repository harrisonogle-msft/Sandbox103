using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Sandbox103.V2;

internal sealed class SourceRepositoryTransformer : ISourceRepositoryTransformer
{
    private static readonly string ConvertedProjectTfmCondition = GetTfmCondition(
        ["net45", "net451", "net452", "net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481"]);
    private readonly ILogger<SourceRepositoryTransformer> _logger;
    private readonly IProjectFileEvaluator _projectFileEvaluator;
    private readonly IProjectFileTransformer _projectFileTransformer;

    public SourceRepositoryTransformer(
        ILogger<SourceRepositoryTransformer> logger,
        IProjectFileEvaluator projectFileEvaluator,
        IProjectFileTransformer projectFileTransformer)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(projectFileEvaluator);
        ArgumentNullException.ThrowIfNull(projectFileTransformer);

        _logger = logger;
        _projectFileEvaluator = projectFileEvaluator;
        _projectFileTransformer = projectFileTransformer;
    }

    public async Task TransformAsync(ISourceRepository repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        cancellationToken.ThrowIfCancellationRequested();

        static XmlReader CreateXmlReader(byte[] bytes) =>
            new XmlTextReader(new MemoryStream(bytes)) { Namespaces = false };

        HashSet<BinaryReference> packagesPropsPackageReferences;
        using (XmlReader corextConfigXmlReader = CreateXmlReader(
            await File.ReadAllBytesAsync(repository.CorextConfigPath, cancellationToken)))
        {
            var corextConfig = new XmlDocument();
            corextConfig.Load(corextConfigXmlReader);
            packagesPropsPackageReferences = ReadPackageReferences(corextConfig);
        }

        _logger.LogInformation($"Found {packagesPropsPackageReferences.Count} corext package(s).");
        foreach ((string id, string version) in packagesPropsPackageReferences)
        {
            _logger.LogInformation($"Found corext package '{id}' version '{version}'.");
        }

        foreach (IProjectFile projectFile in repository.Projects)
        {
            ProjectFileTransformation transformation = await _projectFileEvaluator.EvaluateAsync(projectFile, cancellationToken);
            await _projectFileTransformer.TransformAsync(projectFile, transformation, cancellationToken);

            foreach (BinaryReference packageReference in transformation.PackageReferences)
            {
                // Add to packages.props in case it wouldn't otherwise be added from the corext.config.
                packagesPropsPackageReferences.Add(packageReference);
            }
        }

        using (XmlReader packagesPropsXmlReader = CreateXmlReader(
            await File.ReadAllBytesAsync(repository.PackagesPropsPath, cancellationToken)))
        {
            var packagesProps = new XmlDocument();
            packagesProps.Load(packagesPropsXmlReader);

            // Define the same packages and versions in packages.props as they are currently in
            // corext.config and/or as observed in the '.private.targets' imports of the project files.
            PropagatePackages(packagesProps, packagesPropsPackageReferences);

            using (Stream packagesPropsFileStream = File.OpenWrite(repository.PackagesPropsPath))
            using (XmlWriter packagesPropsXmlWriter = new ProjectFileXmlWriter(packagesPropsFileStream))
            {
                packagesProps.Save(packagesPropsXmlWriter);
                packagesPropsFileStream.SetLength(packagesPropsFileStream.Position);
            }
        }
    }

    private HashSet<BinaryReference> ReadPackageReferences(XmlDocument corextConfig)
    {
        IReadOnlyDictionary<string, string> corextPackages = XmlHelper.GetCorextPackages(corextConfig);
        return corextPackages.Select(static kvp => new BinaryReference(kvp.Key, kvp.Value)).ToHashSet();
    }

    private void PropagatePackages(XmlDocument packagesProps, HashSet<BinaryReference> packageReferences)
    {
        XmlHelper.AddPackageReferencesToProject(
            packagesProps,
            packageReferences,
            "Update",
            "Version",
            itemGroupLabel: "corext.config",
            itemGroupAttributes: [("Condition", ConvertedProjectTfmCondition)],
            checkExisting: false);
    }

    private static string GetTfmCondition(IEnumerable<string> tfms)
    {
        var tfmCondition = new StringBuilder();
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
        return tfmCondition.ToString();
    }
}
