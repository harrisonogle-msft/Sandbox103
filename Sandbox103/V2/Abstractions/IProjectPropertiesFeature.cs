namespace Sandbox103.V2.Abstractions;

/// <summary>
/// Used when the <see cref="IArchiveFile"/> is a project file with
/// <c>PropertyGroup</c> properties, <c>ItemGroup</c> items, etc.
/// </summary>
internal interface IProjectPropertiesFeature
{
    /// <summary>
    /// Represents the dictionary of <c>PropertyGroup</c> properties
    /// after the project evaluation finished.
    /// </summary>
    public IDictionary<string, string> Properties { get; }
}
