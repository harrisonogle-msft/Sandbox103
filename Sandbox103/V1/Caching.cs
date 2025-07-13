using Sandbox103.V1.BuildDrops;
using System.Collections.Concurrent;

namespace Sandbox103.V1;

// TODO: ugly and bad
public static class Caching
{
    public static readonly ConcurrentDictionary<string, BinaryReference> PrivateTargetsCache = new(StringComparer.OrdinalIgnoreCase);

    public static readonly ConcurrentDictionary<string, bool> ContainsReferenceCache = new(StringComparer.OrdinalIgnoreCase);
}
