namespace Sandbox103.V2;

internal sealed class SourceRepository : ISourceRepository
{
    public SourceRepository(
        IReadOnlyCollection<IProjectFile> projects,
        string packagesPropsPath,
        string corextConfigPath)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ThrowHelper.ThrowIfFileNotFound(packagesPropsPath);
        ThrowHelper.ThrowIfFileNotFound(corextConfigPath);

        Projects = projects;
        PackagesPropsPath = packagesPropsPath;
        CorextConfigPath = corextConfigPath;
    }

    public IReadOnlyCollection<IProjectFile> Projects { get; }

    public string PackagesPropsPath { get; }

    public string CorextConfigPath { get; }
}
