using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Sandbox103.BuildDrops;

public readonly record struct LocalAssembly(string Path, AssemblyName AssemblyName, string? FileVersion)
{
    public static LocalAssembly FromPath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using (var streamReader = new StreamReader(path))
        using (var portableExecutableReader = new PEReader(streamReader.BaseStream))
        {
            MetadataReader metadataReader = portableExecutableReader.GetMetadataReader();
            if (!metadataReader.IsAssembly)
            {
                throw new ArgumentException($"The given path does not represent an assembly: {path}", nameof(path));
            }

            return new LocalAssembly(path, metadataReader.GetAssemblyDefinition().GetAssemblyName(), FileVersionInfo.GetVersionInfo(path).FileVersion);
        }
    }
}
