using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;

namespace Sandbox103.V1.BuildDrops;

public readonly record struct LocalAssembly(string Path, AssemblyName AssemblyName, string? FileVersion)
{
    private static readonly Regex s_targetFrameworkVersionRegex = new Regex(@"Version=v([\d\.]+)", RegexOptions.Compiled);

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

    public static string? GetTargetFrameworkMoniker(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        using (var streamReader = new StreamReader(assemblyPath))
        using (var portableExecutableReader = new PEReader(streamReader.BaseStream))
        {
            MetadataReader mr = portableExecutableReader.GetMetadataReader();
            if (!mr.IsAssembly)
            {
                throw new ArgumentException($"The given path does not represent an assembly: {assemblyPath}", nameof(assemblyPath));
            }

            string? tfm = null;

            AssemblyDefinition assemblyDefinition = mr.GetAssemblyDefinition();
            CustomAttributeHandleCollection customAttributeHandleCollection = assemblyDefinition.GetCustomAttributes();
            foreach (CustomAttributeHandle customAttributeHandle in customAttributeHandleCollection)
            {
                CustomAttribute attr = mr.GetCustomAttribute(customAttributeHandle);

                string? type = null;

                if (attr.Constructor.Kind == HandleKind.MethodDefinition)
                {
                    MethodDefinition mdef = mr.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor);
                    TypeDefinition tdef = mr.GetTypeDefinition(mdef.GetDeclaringType());
                    type = $"{mr.GetString(tdef.Namespace)}.{mr.GetString(tdef.Name)}";
                }
                else if (attr.Constructor.Kind == HandleKind.MemberReference)
                {
                    MemberReference mref = mr.GetMemberReference((MemberReferenceHandle)attr.Constructor);

                    if (mref.Parent.Kind == HandleKind.TypeReference)
                    {
                        TypeReference tref = mr.GetTypeReference((TypeReferenceHandle)mref.Parent);
                        type = $"{mr.GetString(tref.Namespace)}.{mr.GetString(tref.Name)}";
                    }
                    else if (mref.Parent.Kind == HandleKind.TypeDefinition)
                    {
                        TypeDefinition tdef = mr.GetTypeDefinition((TypeDefinitionHandle)mref.Parent);
                        type = $"{mr.GetString(tdef.Namespace)}.{mr.GetString(tdef.Name)}";
                    }
                }

                if (type is not null && type.Contains("TargetFrameworkAttribute", StringComparison.Ordinal))
                {
                    byte[] data = mr.GetBlobBytes(attr.Value);

                    // There's probably a better way to deserialize this and avoid pattern matching.
                    string text = Encoding.UTF8.GetString(data);

                    string net = text.Contains("NETStandard", StringComparison.Ordinal) ? "netstandard" : "net";

                    Match match = s_targetFrameworkVersionRegex.Match(text);
                    if (match.Success)
                    {
                        GroupCollection groups = match.Groups;
                        if (groups.Count == 2)
                        {
                            string version = groups[1].Value;

                            if (!string.IsNullOrEmpty(version))
                            {
                                if (version[0] == '4')
                                {
                                    tfm = $"{net}{version.Replace(".", "")}";
                                }
                                else
                                {
                                    tfm = $"{net}{version}";
                                }
                                break;
                            }
                        }
                    }
                }
            }

            return tfm;
        }
    }
}
