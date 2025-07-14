namespace Sandbox103.V2.Abstractions;

internal interface IProjectFileTransformer
{
    public Task TransformAsync(IProjectFile projectFile, ProjectFileTransformation transformaton, CancellationToken cancellationToken);
}
