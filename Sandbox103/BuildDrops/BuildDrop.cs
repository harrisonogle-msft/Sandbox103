using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

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
    private readonly string _projectsRoot;
    private readonly DirectoryInfo _root;
    private readonly DirectoryInfoWrapper _wrapper;
    private List<BuildDropProject>? _projects;

    public BuildDrop(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        _path = path;

        string retailAmd64 = System.IO.Path.Join(path, "retail-amd64");

        //_projectsRoot = Directory.Exists(retailAmd64) ? retailAmd64 : path;
        _projectsRoot = path;

        _root = new DirectoryInfo(path);

        if (!_root.Exists)
        {
            throw new DirectoryNotFoundException(path);
        }

        _wrapper = new DirectoryInfoWrapper(_root);
    }

    public string Path => _path;

    public DirectoryInfo Root => _root;

    public IReadOnlyList<BuildDropProject> Projects => _projects ??= EnumerateProjects().ToList();

    public IEnumerable<BuildDropProject> EnumerateProjects()
    {
        foreach (string projectDir in Directory.EnumerateDirectories(_projectsRoot))
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

            yield return new BuildDropProject
            {
                ProjectPath = projectDir,
                BinaryPath = binaryPath,
                RelativeProjectPath = System.IO.Path.GetRelativePath(_path, projectDir),
                RelativeBinaryPath = System.IO.Path.GetRelativePath(_path, binaryPath),
            };
        }
    }

    public IEnumerable<string> Glob(string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return Glob(new Matcher().AddInclude(pattern));
    }

    public IEnumerable<string> Glob(Matcher glob)
    {
        PatternMatchingResult searchResult = glob.Execute(_wrapper);

        if (!searchResult.HasMatches)
        {
            return Array.Empty<string>();
        }

        return searchResult.Files.Select(item => System.IO.Path.Join(_root.FullName, item.Path));
    }
}
