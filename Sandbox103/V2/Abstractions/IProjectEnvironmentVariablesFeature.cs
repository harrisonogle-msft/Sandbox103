namespace Sandbox103.V2.Abstractions;

internal interface IProjectEnvironmentVariablesFeature
{
    public IDictionary<string, string?> EnvironmentVariables { get; }
}
