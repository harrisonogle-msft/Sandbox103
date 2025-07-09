namespace Sandbox103.BuildDrops;

public class BuildDrop
{
    private static readonly EnumerationOptions s_projectOutputEnumerationOptions =
        new EnumerationOptions
        {
            MaxRecursionDepth = 5,
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchType = MatchType.Simple,
            ReturnSpecialDirectories = true,
        };

    private readonly string _path;
    private readonly string _retailAmd64;

    public BuildDrop(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        _path = path;

        string retailAmd64 = System.IO.Path.Join(path, "retail-amd64");

        if (!Directory.Exists(retailAmd64))
        {
            throw new DirectoryNotFoundException("Unable to locate 'retail-amd64' directory under the build drop root.");
        }

        _retailAmd64 = retailAmd64;
    }

    public string Path => _path;

    public IEnumerable<BuildDropProject> EnumerateProjects()
    {
        foreach (string projectDir in Directory.EnumerateDirectories(_retailAmd64))
        {
            var projectDirInfo = new DirectoryInfo(projectDir);
            string projectName = projectDirInfo.Name;

            static string? Search(string projectDir, string projectName, EnumerationOptions enumerationOptions, string extension)
            {
                string? binaryPath = null;

                IEnumerable<string> search = Directory.EnumerateFiles(projectDir, $"{projectName}.{extension}", enumerationOptions)
                    .Where(p => System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(p)) != "ref");

                using (var it = search.GetEnumerator())
                {
                    if (it.MoveNext())
                    {
                        binaryPath = it.Current;

                        if (it.MoveNext())
                        {
                            throw new InvalidOperationException($"Found multiple output binaries for project '{projectName}' rooted at '{projectDir}': '{binaryPath}', '{it.Current}'");
                        }
                    }
                }

                return binaryPath;
            }

            string? binaryPath =
                Search(projectDir, projectName, s_projectOutputEnumerationOptions, "dll") ??
                Search(projectDir, projectName, s_projectOutputEnumerationOptions, "exe");

            if (binaryPath is null)
            {
                continue;
            }

            yield return new BuildDropProject { ProjectPath = projectDir, BinaryPath = binaryPath };
        }
    }
}
