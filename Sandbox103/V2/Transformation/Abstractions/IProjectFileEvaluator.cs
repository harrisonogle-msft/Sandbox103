namespace Sandbox103.V2.Abstractions;

internal interface IProjectFileEvaluator
{
    public Task<ProjectFileTransformation> EvaluateAsync(IProjectFile projectFile, CancellationToken cancellationToken);
}
