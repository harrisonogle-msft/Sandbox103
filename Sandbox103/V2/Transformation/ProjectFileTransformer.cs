using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml;

namespace Sandbox103.V2;

internal sealed class ProjectFileTransformer : IProjectFileTransformer
{
    private readonly ILogger<ProjectFileTransformer> _logger;

    public ProjectFileTransformer(
        ILogger<ProjectFileTransformer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public Task TransformAsync(IProjectFile projectFile, ProjectFileTransformation transformation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectFile);
        cancellationToken.ThrowIfCancellationRequested();

        using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(projectFile.Path)));
        using var sourceTextReader = new StreamReader(sourceStream, Encoding.UTF8);
        using var sourceXmlReader = new XmlTextReader(sourceTextReader) { Namespaces = false };

        string projectFileName = Path.GetFileName(projectFile.Path);
        using var loggerScope = _logger.BeginScope($"Transform project '{projectFileName}'");

        try
        {
            var document = new XmlDocument();
            document.Load(sourceXmlReader);

            if (XmlHelper.IsSdkStyleProject(document))
            {
                _logger.LogInformation($"Skipping SDK-style project '{projectFileName}'.");
                return Task.CompletedTask;
            }

            IReadOnlyCollection<BinaryReference> packageReferenceList = transformation.PackageReferences;
            IReadOnlyCollection<DirectImport> legacyImports = transformation.LegacyImports;

            _logger.LogInformation($"Found {packageReferenceList.Count} PackageReference(s) to add.");
            foreach (BinaryReference packageReference in packageReferenceList)
            {
                _logger.LogInformation($"  {packageReference.Name} {packageReference.Version}");
            }

            // Add `PackageReference` items to the .csproj file.
            XmlHelper.AddPackageReferencesToProject(document, transformation.PackageReferences, "Include");

            cancellationToken.ThrowIfCancellationRequested();

            if (legacyImports.Count < 1)
            {
                _logger.LogInformation($"No imports to remove from '{projectFileName}'!");
            }
            else
            {
                _logger.LogInformation($"Found {legacyImports.Count} project import(s) to remove from '{projectFileName}'.");

                foreach (DirectImport legacyImport in legacyImports.OrderBy(static x => x.File.Path))
                {
                    string unexpandedProjectFile = legacyImport.UnexpandedProjectName ??
                        throw new InvalidOperationException($"Direct import '{legacyImport.File.Path}' is missing unexpanded project file.");

                    _logger.LogInformation($"Found legacy project import to remove: {unexpandedProjectFile}");
                }

                Predicate<string> shouldRemove = (string projectName) => legacyImports.Any(x => string.Equals(x.UnexpandedProjectName, projectName, StringComparison.OrdinalIgnoreCase));

                // Remove `Import`s from the .csproj file.
                int numRemoved = XmlHelper.RemoveProjectImports(document, shouldRemove);

                _logger.LogInformation($"Removed {numRemoved} project import(s).");
            }

            XmlHelper.RemoveLegacyProjectAttributes(document);
            XmlHelper.RemoveSdkElements(document);
            XmlHelper.AddSdkElement(document, "Corext.Before", [("Condition", $"'$({ConstantsV1.EnableCorextProjectSdk})' == 'true'")]);
            XmlHelper.AddSdkElement(document, "Microsoft.NET.Sdk");
            XmlHelper.AddSdkElement(document, "Corext.After", [("Condition", $"'$({ConstantsV1.EnableCorextProjectSdk})' == 'true'")]);
            XmlHelper.RemoveProjectImports(document, static name => name?.EndsWith("Microsoft.CSharp.targets", StringComparison.OrdinalIgnoreCase) is true);
            XmlHelper.RemoveProjectImports(document, static name => name?.EndsWith("\\Environment.props", StringComparison.OrdinalIgnoreCase) is true);
            XmlHelper.RemoveProjectImports(document, static name => string.Equals(name, "$(EnvironmentConfig)", StringComparison.OrdinalIgnoreCase));
            if (XmlHelper.GetProperty(document, "TargetFrameworks") is null &&
                XmlHelper.GetProperty(document, "TargetFramework") is null)
            {
                IArchiveFile archiveFile = projectFile.BinaryLog.ProjectFile;
                const string DefaultTfm = "net472";
                string tfm;
                if (archiveFile.TryGetProperties(out IDictionary<string, string>? properties) &&
                    properties.TryGetValue("TargetFramework", out string? tfmProperty) &&
                    !string.IsNullOrEmpty(tfmProperty))
                {
                    _logger.LogInformation($"Found target framework '{tfmProperty}' in project '{projectFileName}'.");
                    tfm = tfmProperty;
                }
                else
                {
                    _logger.LogWarning($"Project '{projectFileName}' does not define a TargetFramework, and a PropertyGroup value for TargetFramework was not found in the binlog. Default of '{DefaultTfm}' will be used.");
                    tfm = DefaultTfm;
                }
                XmlHelper.SetProperty(document, "TargetFramework", "net472");
            }
            XmlHelper.RemoveCompileItems(document, projectFile.Path);
            XmlHelper.RemoveReferenceItems(document);

            using var targetStream = File.OpenWrite(projectFile.Path);
            using var targetXmlWriter = new ProjectFileXmlWriter(targetStream);

            document.Save(targetXmlWriter);
            targetStream.SetLength(targetStream.Position);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to transform project file '{projectFile.Path}'.");
            throw;
        }
    }
}
