namespace Sandbox103.V2.Abstractions;

internal interface ISourceRepository
{
    public IReadOnlyCollection<IProjectFile> Projects { get; }

    public string PackagesPropsPath { get; }

    public string CorextConfigPath { get; }

    public string DirectoryBuildPropsPath { get; }
}
