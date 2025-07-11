using Sandbox103.BuildDrops;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace Sandbox103.Helpers;

public static class AssemblyHelper
{
    // https://www.intunewiki.com/wiki/Setup_new_machine#Disable_strong_name_validation
    private static readonly IReadOnlySet<string> s_intunePublicKeyTokens = new HashSet<string>([
        "31bf3856ad364e35",
        "b03f5f7f11d50a3a",
        "1a5b963c6f0fbeab",
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Walk the assembly dependency DAG using a breadth-first traversal starting at the given root assembly.
    /// </summary>
    /// <param name="rootAssembly">The root assembly to start the walk</param>
    /// <param name="callback">Callback invoked when an assembly dependency is observed.</param>
    /// <returns>The full path names of the locations of all transitive dependency assemblies</returns>
    /// <remarks>
    /// Assembly metadata is used so that no assemblies are loaded during the course of the walk.
    /// </remarks>
    public static IEnumerable<LocalAssembly> EnumerateDependencies(string rootAssembly, Action<LocalAssembly, LocalAssembly>? callback = null)
    {
        ArgumentNullException.ThrowIfNull(rootAssembly, nameof(rootAssembly));

        return EnumerateDependencies([rootAssembly], callback);
    }

    /// <summary>
    /// Walk the assembly dependency DAG using a breadth-first traversal starting with the given seed assemblies.
    /// </summary>
    /// <param name="seedAssemblies">The seed assemblies to start the walk</param>
    /// <param name="callback">Callback invoked when an assembly dependency is observed.</param>
    /// <returns>The full path names of the locations of all transitive dependency assemblies</returns>
    /// <remarks>
    /// Assembly metadata is used so that no assemblies are loaded during the course of the walk.
    /// </remarks>
    public static IEnumerable<LocalAssembly> EnumerateDependencies(IEnumerable<string> seedAssemblies, Action<LocalAssembly, LocalAssembly>? callback)
    {
        ArgumentNullException.ThrowIfNull(seedAssemblies, nameof(seedAssemblies));

        var queue = new Queue<LocalAssembly>();
        var visited = new HashSet<string>();

        foreach (string seedAssemblyPath in seedAssemblies)
        {
            if (string.IsNullOrEmpty(seedAssemblyPath))
            {
                continue;
            }

            string assemblyPath = Path.GetFullPath(seedAssemblyPath);

            if (visited.Add(assemblyPath))
            {
                using (var streamReader = new StreamReader(assemblyPath))
                using (var portableExecutableReader = new PEReader(streamReader.BaseStream))
                {
                    MetadataReader metadataReader = portableExecutableReader.GetMetadataReader();
                    if (metadataReader.IsAssembly)
                    {
                        var seed = new LocalAssembly(assemblyPath, metadataReader.GetAssemblyDefinition().GetAssemblyName(), FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion);
                        foreach (LocalAssembly dependency in EnumerateDirectDependencies(assemblyPath))
                        {
                            callback?.Invoke(seed, dependency);
                            queue.Enqueue(dependency);
                        }
                    }
                }
            }
        }

        while (queue.Count > 0)
        {
            LocalAssembly item = queue.Dequeue();
            string assemblyPath = Path.GetFullPath(item.Path);

            if (!visited.Add(assemblyPath))
            {
                continue;
            }

            yield return item;

            foreach (LocalAssembly dependency in EnumerateDirectDependencies(assemblyPath))
            {
                callback?.Invoke(item, dependency);
                queue.Enqueue(dependency);
            }
        }
    }

    /// <summary>
    /// Enumerate the the direct dependencies of the assembly located at the given path.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly for which to retrieve the dependency assembly paths</param>
    /// <returns>An enumeration of the direct dependency assembly details.</returns>
    public static IEnumerable<LocalAssembly> EnumerateDirectDependencies(string assemblyPath)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath, nameof(assemblyPath));
        assemblyPath = Path.GetFullPath(assemblyPath);

        foreach (string dependencyPath in EnumerateDirectDependencyPaths(assemblyPath))
        {
            using (var streamReader = new StreamReader(dependencyPath))
            using (var portableExecutableReader = new PEReader(streamReader.BaseStream))
            {
                MetadataReader metadataReader = portableExecutableReader.GetMetadataReader();
                if (metadataReader.IsAssembly)
                {
                    var dependency = new LocalAssembly(dependencyPath, metadataReader.GetAssemblyDefinition().GetAssemblyName(), FileVersionInfo.GetVersionInfo(dependencyPath).FileVersion);
                    yield return dependency;
                }
            }
        }
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
    public static IEnumerable<string> EnumerateDirectDependencyPaths(string assemblyPath)
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

    /// <summary>
    /// Determines whether or not the given assembly reference is an Intune assembly reference.
    /// </summary>
    /// <param name="assemblyName">The assembly reference to qualify.</param>
    /// <returns><see langword="true"/> if the given assembly reference is an Intune assembly reference, otherwise <see langword="false"/>.</returns>
    public static bool IsIntuneAssembly(AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);

        byte[]? publicKeyTokenBytes = assemblyName.GetPublicKeyToken();

        if (publicKeyTokenBytes is not null)
        {
            string publicKeyToken = string.Format("{0:x2}", publicKeyTokenBytes);
            if (s_intunePublicKeyTokens.Contains(publicKeyToken))
            {
                return true;
            }
        }

        byte[]? publicKeyBytes = assemblyName.GetPublicKey();

        if (publicKeyBytes is not null)
        {
            string publicKey = string.Format("{0:x2}", publicKeyBytes);
            if (s_intunePublicKeyTokens.Contains(publicKey))
            {
                return true;
            }
        }

        if (publicKeyTokenBytes is null && publicKeyBytes is null)
        {
            throw new InvalidOperationException("Unable to get public key token.");
        }

        string fullName = assemblyName.FullName;

        if (fullName.Contains("Intune", StringComparison.OrdinalIgnoreCase) ||
            fullName.StartsWith("Microsoft.Management.Services", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
