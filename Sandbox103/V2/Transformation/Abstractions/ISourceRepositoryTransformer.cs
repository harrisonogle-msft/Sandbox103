namespace Sandbox103.V2;

internal interface ISourceRepositoryTransformer
{
    public Task TransformAsync(ISourceRepository repository, CancellationToken cancellationToken);
}
