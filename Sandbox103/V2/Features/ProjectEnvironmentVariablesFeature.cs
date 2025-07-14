namespace Sandbox103.V2;

internal sealed record class ProjectEnvironmentVariablesFeature(IDictionary<string, string?> EnvironmentVariables) : IProjectEnvironmentVariablesFeature
{
    public ProjectEnvironmentVariablesFeature() : this(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase))
    {
    }
}
