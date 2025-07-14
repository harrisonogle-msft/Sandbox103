namespace Sandbox103.V2.Abstractions;

internal readonly struct ProjectFileTransformation
{
    public ProjectFileTransformation(
        IReadOnlyCollection<DirectImport> legacyImports,
        IReadOnlyCollection<BinaryReference> packageReferences)
    {
        ArgumentNullException.ThrowIfNull(legacyImports);
        ArgumentNullException.ThrowIfNull(packageReferences);

        LegacyImports = legacyImports;
        PackageReferences = packageReferences;
    }

    public IReadOnlyCollection<DirectImport> LegacyImports { get; }
    public IReadOnlyCollection<BinaryReference> PackageReferences { get; }
}
