using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox103.V2;

internal sealed class ArchiveFileIndex : IArchiveFileIndex
{
    private readonly record struct Key(string Path);
    private readonly record struct Value(IArchiveFile ArchiveFile);

    private readonly ConcurrentDictionary<Key, Value> _cache = new(KeyComparer.OrdinalIgnoreCase);

    public IArchiveFile this[string path]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return _cache[new Key(path)].ArchiveFile;
        }
    }

    public bool TryGetValue(string path, [NotNullWhen(true)] out IArchiveFile? archiveFile)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (_cache.TryGetValue(new Key(path), out Value value))
        {
            archiveFile = value.ArchiveFile;
            return true;
        }

        archiveFile = default;
        return false;
    }

    public bool TryAdd(string path, IArchiveFile archiveFile)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return _cache.TryAdd(new Key(path), new Value(archiveFile));
    }

    // The archive files are missing ':'. I think MSBuild is removing it for some reason, but
    // only in the archive file paths - not in the project file paths in the binlog events.
    // Example.
    //   archive: F\Users\Satya\build\...
    //   binlog: F:\Users\Satya\build\...
    private sealed class KeyComparer : IEqualityComparer<Key>
    {
        public static readonly KeyComparer Ordinal = new KeyComparer(StringComparer.Ordinal);
        public static readonly KeyComparer OrdinalIgnoreCase = new KeyComparer(StringComparer.OrdinalIgnoreCase);

        private readonly IEqualityComparer<string> _pathComparer;

        public KeyComparer(IEqualityComparer<string> pathComparer)
        {
            ArgumentNullException.ThrowIfNull(pathComparer);
            _pathComparer = pathComparer;
        }

        public bool Equals(Key x, Key y)
        {
            string? px = x.Path;
            string? py = y.Path;

            if (_pathComparer.Equals(px, py))
            {
                return true;
            }

            if (px is null || py is null)
            {
                return false;
            }

            int ix = px.IndexOf(':');
            int iy = py.IndexOf(':');
            bool bx = ix >= 0;
            bool by = iy >= 0;
            if ((bx ^ by) && Math.Abs(px.Length - py.Length) == 1)
            {
                return bx ? _pathComparer.Equals(RemoveAt(px, ix), py) : _pathComparer.Equals(px, RemoveAt(py, iy));
            }

            return false;
        }

        public int GetHashCode([DisallowNull] Key obj)
        {
            if (obj.Path is null)
            {
            }
            string path = obj.Path!;
            return RemoveAt(path, path.IndexOf(':')).GetHashCode();
        }

        private static string RemoveAt(string path, int index)
        {
            return index < 0 ? path :
                string.Create(path.Length - 1, state: (path, index), static (buffer, state) =>
                {
                    (string path, int index) = state;
                    ReadOnlySpan<char> cstr = path;
                    cstr[..index].CopyTo(buffer[..index]);
                    cstr[(index + 1)..].CopyTo(buffer[index..]);
                });
        }
    }
}
