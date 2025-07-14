namespace Sandbox103.V2.Abstractions;

internal interface IProjectImportsFeature
{
    public ISet<DirectImport> Imports { get; }
    public ISet<IArchiveFile> Importers { get; }
}

