namespace Sandbox103.V2;

internal sealed class SourceRepository : ISourceRepository
{
    public SourceRepository(
        IReadOnlyCollection<IProjectFile> projects,
        string packagesPropsPath,
        string corextConfigPath,
        string directoryBuildPropsPath)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ThrowHelper.ThrowIfFileNotFound(packagesPropsPath);
        ThrowHelper.ThrowIfFileNotFound(corextConfigPath);
        ThrowHelper.ThrowIfFileNotFound(directoryBuildPropsPath);

        Projects = projects;
        PackagesPropsPath = packagesPropsPath;
        CorextConfigPath = corextConfigPath;
        DirectoryBuildPropsPath = directoryBuildPropsPath;
    }

    public IReadOnlyCollection<IProjectFile> Projects { get; }

    public string PackagesPropsPath { get; }

    public string CorextConfigPath { get; }

    public string DirectoryBuildPropsPath { get; }
}
