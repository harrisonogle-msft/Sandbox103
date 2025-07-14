namespace Sandbox103.V2;

internal sealed record class ProjectImportsFeature(ISet<DirectImport> Imports, ISet<IArchiveFile> Importers) : IProjectImportsFeature
{
    public ProjectImportsFeature() : this(new HashSet<DirectImport>(), new HashSet<IArchiveFile>())
    {
    }
}
