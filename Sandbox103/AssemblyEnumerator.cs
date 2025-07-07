using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace Sandbox103;

public class AssemblyEnumerator
{
    /// <summary>
    /// Walk the assembly dependency DAG using a breadth-first traversal starting at the given root assembly.
    /// </summary>
    /// <param name="rootAssembly">The root assembly to start the walk</param>
    /// <returns>The full path names of the locations of all transitive dependency assemblies</returns>
    /// <remarks>
    /// Assembly metadata is used so that no assemblies are loaded during the course of the walk.
    /// </remarks>
    internal IEnumerable<LocalAssembly> EnumerateDependencies(string rootAssembly, Action<LocalAssembly, LocalAssembly>? callback)
    {
        ArgumentNullException.ThrowIfNull(rootAssembly, nameof(rootAssembly));

        return EnumerateDependencies([rootAssembly], callback);
    }

    /// <summary>
    /// Walk the assembly dependency DAG using a breadth-first traversal starting with the given seed assemblies.
    /// </summary>
    /// <param name="seedAssemblies">The seed assemblies to start the walk</param>
    /// <returns>The full path names of the locations of all transitive dependency assemblies</returns>
    /// <remarks>
    /// Assembly metadata is used so that no assemblies are loaded during the course of the walk.
    /// </remarks>
    internal IEnumerable<LocalAssembly> EnumerateDependencies(IEnumerable<string> seedAssemblies, Action<LocalAssembly, LocalAssembly>? callback)
    {
        ArgumentNullException.ThrowIfNull(seedAssemblies, nameof(seedAssemblies));

        var queue = new Queue<LocalAssembly>();
        var visited = new HashSet<string>();

        foreach (var assemblyPath in seedAssemblies)
        {
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                using (var streamReader = new StreamReader(assemblyPath))
                using (var portableExecutableReader = new PEReader(streamReader.BaseStream))
                {
                    MetadataReader metadataReader = portableExecutableReader.GetMetadataReader();
                    if (metadataReader.IsAssembly)
                    {
                        var seed = new LocalAssembly(assemblyPath, metadataReader.GetAssemblyDefinition().GetAssemblyName(), FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion);
                        queue.Enqueue(seed);
                    }
                }
            }
        }

        do
        {
            LocalAssembly item = queue.Dequeue();
            string assemblyPath = Path.GetFullPath(item.Path);
            if (visited.Contains(assemblyPath))
            {
                continue;
            }
            visited.Add(assemblyPath);

            yield return item;

            foreach (string dependencyPath in EnumerateDirectDependencyPaths(assemblyPath))
            {
                using (var streamReader = new StreamReader(dependencyPath))
                using (var portableExecutableReader = new PEReader(streamReader.BaseStream))
                {
                    MetadataReader metadataReader = portableExecutableReader.GetMetadataReader();
                    if (metadataReader.IsAssembly)
                    {
                        var dependency = new LocalAssembly(dependencyPath, metadataReader.GetAssemblyDefinition().GetAssemblyName(), FileVersionInfo.GetVersionInfo(dependencyPath).FileVersion);
                        callback?.Invoke(item, dependency);
                        queue.Enqueue(dependency);
                    }
                }
            }
        }
        while (queue.Count > 0);
    }

    /// <summary>
    /// Enumerate the paths of the direct dependencies of the assembly located at the provided path.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly for which to retrieve the dependency assembly paths</param>
    /// <returns>The locations of all direct dependency assemblies</returns>
    /// <remarks>
    /// The function uses <see cref="MetadataReader"/> and <see cref="AssemblyDependencyResolver"/> to enumerate
    /// the vertex boundary using metadata only in order to avoid loading any assemblies because loads can affect JIT behavior.
    /// </remarks>
    internal IEnumerable<string> EnumerateDirectDependencyPaths(string assemblyPath)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath, nameof(assemblyPath));
        assemblyPath = Path.GetFullPath(assemblyPath);

        using var streamReader = new StreamReader(assemblyPath);
        using var portableExecutableReader = new PEReader(streamReader.BaseStream);
        MetadataReader metadataReader = portableExecutableReader.GetMetadataReader();
        if (!metadataReader.IsAssembly)
        {
            yield break;
        }
        var dependencyResolver = new AssemblyDependencyResolver(assemblyPath);

        // Enumerate the dependency assembly references without actually loading the assembly.
        foreach (AssemblyReferenceHandle handle in metadataReader.AssemblyReferences)
        {
            AssemblyReference assemblyReference = metadataReader.GetAssemblyReference(handle);
            AssemblyName assemblyName = assemblyReference.GetAssemblyName();

            // Resolve the path to the assembly reference without actually loading the assembly.
            string? dependencyAssemblyPath = dependencyResolver.ResolveAssemblyToPath(assemblyName);

            if (!string.IsNullOrEmpty(dependencyAssemblyPath))
            {
                yield return dependencyAssemblyPath;
            }
        }
    }
}
