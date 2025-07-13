using System.Diagnostics.CodeAnalysis;

namespace Sandbox103.V2.Abstractions;

/// <summary>
/// Caches instances of <see cref="IArchiveFile"/>, each uniquely identified
/// by its path, for reuse and caching.
/// </summary>
public interface IArchiveFileIndex
{
    public bool TryGetValue(string path, [NotNullWhen(true)] out IArchiveFile? archiveFile);

    public bool TryAdd(string path, IArchiveFile archiveFile);
}
