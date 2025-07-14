using System.Diagnostics.CodeAnalysis;

namespace Sandbox103.V2;

internal static class ArchiveFileFeaturesExtensions
{
    public static ISet<DirectImport> GetImports(this IArchiveFile file)
    {
        if (file.Features.Get<IProjectImportsFeature>() is not IProjectImportsFeature feature)
        {
            file.Features.Set<IProjectImportsFeature>(feature = new ProjectImportsFeature());
        }
        return feature.Imports;
    }

    public static ISet<IArchiveFile> GetImporters(this IArchiveFile file)
    {
        if (file.Features.Get<IProjectImportsFeature>() is not IProjectImportsFeature feature)
        {
            file.Features.Set<IProjectImportsFeature>(feature = new ProjectImportsFeature());
        }
        return feature.Importers;
    }

    public static bool TryGetCorextPackage(this IArchiveFile file, out BinaryReference package)
    {
        if (file.Features.Get<ICorextPackageFeature>() is ICorextPackageFeature feature)
        {
            package = new BinaryReference(feature.Id, feature.Version);
            return true;
        }

        package = default;
        return false;
    }

    public static bool TryGetProperties(this IArchiveFile file, [NotNullWhen(true)] out IDictionary<string, string>? properties)
    {
        return (properties = file.Features.Get<IProjectPropertiesFeature>()?.Properties) is not null;
    }
}
