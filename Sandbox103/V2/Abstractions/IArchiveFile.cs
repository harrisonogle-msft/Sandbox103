using Microsoft.AspNetCore.Http.Features;

namespace Sandbox103.V2.Abstractions;

/// <summary>
/// Represents a file embedded in a binary log emitted by MSBuild.
/// </summary>
// NOTE: By design, this interface does not expose a way to read
// the contents of the file. According to the design of this project,
// the contents of the file should be read at most once, and any
// computed features should be cached after that read.
// EDIT: After doing a size analysis, there is only 10MB of distinct
// archive file content in an XSU build. So I'm going to go ahead and
// buffer everything...
public interface IArchiveFile
{
    /// <summary>
    /// Snapshot of the path to the archive file at build time.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Cache of features or statistics about this embedded archive file.
    /// </summary>
    public IFeatureCollection Features { get; }

    /// <summary>
    /// Read only build-time snapshot of the contents of the embedded file.
    /// </summary>
    /// <remarks>
    /// This stream is read-only.
    /// </remarks>
    public Stream Content { get; }
}
