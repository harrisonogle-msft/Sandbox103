namespace Sandbox103.V2;

internal interface ISourceRepositoryReader
{
    public Task<ISourceRepository> ReadAsync(string repositoryPath, ILogDrop logDrop, CancellationToken cancellationToken);
}
