using Sandbox103.BuildDrops;
using Sandbox103.Helpers;
using System.Reflection;

namespace Sandbox103.Extensions;

public static class LocalAssemblyExtensions
{
    public static BinaryReference ToBinaryReference(this LocalAssembly localAssembly)
    {
        AssemblyName assemblyName = localAssembly.AssemblyName;
        string name = assemblyName.Name ?? Path.GetFileNameWithoutExtension(localAssembly.Path);
        string version = GetVersion(localAssembly);
        return new BinaryReference(name, version);
    }

    public static string GetVersion(this LocalAssembly localAssembly)
    {
        AssemblyName? assemblyName = localAssembly.AssemblyName;

        if (assemblyName is null)
        {
            throw new ArgumentException(nameof(localAssembly));
        }

        // Intune uses file version to track the version of an assembly,
        // probably to avoid the headache of managing strong versioning
        // for .NET Framework assemblies.
        // The rest of the world uses assembly version.

        if (AssemblyHelper.IsIntuneAssembly(assemblyName))
        {
            if (localAssembly.FileVersion is not string fileVersion)
            {
                throw new ArgumentException("Missing file version.", nameof(localAssembly));
            }
            return fileVersion;
        }
        else
        {
            if (assemblyName.Version is not Version version)
            {
                throw new ArgumentException("Missing assembly version.", nameof(assemblyName));
            }
            return version.ToString();
        }
    }
}
