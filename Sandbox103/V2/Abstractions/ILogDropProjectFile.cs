using Microsoft.AspNetCore.Http.Features;

namespace Sandbox103.V2.Abstractions;

// NOT associated with a particular `.binlog` file.
// That is, features cached here are scoped to the
// log drop.
public interface ILogDropProjectFile
{
    /// <summary>
    /// Snapshot of the path to the project file at build time.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Cache of features or statistics about this project file.
    /// </summary>
    public IFeatureCollection Features { get; }
}
