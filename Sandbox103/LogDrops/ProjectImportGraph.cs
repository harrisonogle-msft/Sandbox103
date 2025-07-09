using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox103.LogDrops;

public class ProjectImportGraph
{
    private delegate bool TryGetValues(string projectFile, [NotNullWhen(true)] out IEnumerator<string>? values);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _forward;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _reverse;

    public ProjectImportGraph()
    {
        _forward = new(StringComparer.OrdinalIgnoreCase);
        _reverse = new(StringComparer.OrdinalIgnoreCase);
    }

    public ICollection<string> GetKeys(bool reverse = false)
    {
        return reverse ? _reverse.Keys : _forward.Keys;
    }

    public ICollection<string> Importers => _forward.Keys;

    public ICollection<string> Importees => _reverse.Keys;

    public void AddImport(string projectFile, string importedProjectFile)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFile);
        ArgumentException.ThrowIfNullOrEmpty(importedProjectFile);

        static void Add(ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> cache, string key, string value)
        {
            ConcurrentDictionary<string, byte>? items;
            ConcurrentDictionary<string, byte>? valueToAdd = null;

            while (!cache.TryGetValue(key, out items))
            {
                cache.TryAdd(key, valueToAdd ??= new(StringComparer.OrdinalIgnoreCase));
            }

            while (!items.ContainsKey(value))
            {
                items.TryAdd(value, default);
            }
        }

        Add(_forward, projectFile, importedProjectFile);
        Add(_reverse, importedProjectFile, projectFile);
    }

    public bool TryGetImports(string projectFile, [NotNullWhen(true)] out IEnumerator<string>? values)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFile);

        if (_forward.TryGetValue(projectFile, out ConcurrentDictionary<string, byte>? items))
        {
            values = items.Select(static item => item.Key).GetEnumerator();
            return true;
        }

        values = null;
        return false;
    }

    public bool TryGetImporters(string projectFile, [NotNullWhen(true)] out IEnumerator<string>? values)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFile);

        if (_reverse.TryGetValue(projectFile, out ConcurrentDictionary<string, byte>? items))
        {
            values = items.Select(static item => item.Key).GetEnumerator();
            return true;
        }

        values = null;
        return false;
    }

    public IEnumerable<string> EnumerateTransitiveImports(string projectFile, bool reverse = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFile);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        visited.Add(projectFile);

        return EnumerateCore(projectFile, visited, reverse ? TryGetImporters : TryGetImports);
    }

    private IEnumerable<string> EnumerateCore(string projectFile, HashSet<string> visited, TryGetValues tryGetValues)
    {
        if (tryGetValues.Invoke(projectFile, out IEnumerator<string>? it))
        {
            using (it)
            {
                while (it.MoveNext())
                {
                    string value = it.Current;

                    if (!visited.Add(value))
                    {
                        throw new InvalidOperationException($"Import cycle detected. ({value})");
                    }

                    yield return value;

                    foreach (string transitiveValue in EnumerateCore(value, visited, tryGetValues))
                    {
                        yield return transitiveValue;
                    }

                    if (!visited.Remove(value))
                    {
                        throw new InvalidOperationException($"Unexpected error: corrupted import graph. ({value})");
                    }
                }
            }
        }
    }
}
