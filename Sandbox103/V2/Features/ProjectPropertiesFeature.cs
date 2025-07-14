namespace Sandbox103.V2;

internal sealed record class ProjectPropertiesFeature(IDictionary<string, string> Properties) : IProjectPropertiesFeature
{
    public ProjectPropertiesFeature() : this(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)) // MSBuild properties are case-insensitive
    {
    }
}
