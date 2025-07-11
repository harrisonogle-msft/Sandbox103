namespace Sandbox103.Repos;

public class LocalGitRepo
{
    private static readonly EnumerationOptions s_corextConfigEnumerationOptions = new EnumerationOptions
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = true,
    };

    private readonly string _root;
    private readonly string _src;
    private readonly string _corextConfig;

    public LocalGitRepo(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        _root = path;

        if (!Directory.Exists(_root))
        {
            throw new DirectoryNotFoundException(path);
        }

        // Validate that the given path is actually the root of a git repository.
        using (var it = Directory.EnumerateDirectories(_root, ".git", SearchOption.TopDirectoryOnly).GetEnumerator())
        {
            if (!it.MoveNext())
            {
                throw new ArgumentException("Local git repo root is missing a '.git' subdirectory.", nameof(path));
            }

            if (it.MoveNext())
            {
                throw new InvalidOperationException("Unexpected error: local git repo has multiple '.git' subdirectories.");
            }
        }

        _src = Directory.EnumerateDirectories(_root, "src", SearchOption.TopDirectoryOnly).SingleOrDefault() ??
            Directory.EnumerateDirectories(_root, "src", SearchOption.AllDirectories).FirstOrDefault() ??
            throw new DirectoryNotFoundException("Unable to find the 'src' directory under the repository root.");

        string corextConfigPath = Path.Join(_root, "build", "corext", "corext.config");
        if (!File.Exists(corextConfigPath))
        {
            corextConfigPath = Directory.EnumerateFiles(_root, "corext.config", s_corextConfigEnumerationOptions).SingleOrDefault() ??
                throw new FileNotFoundException("Unable to find corext.config.");
        }
        _corextConfig = corextConfigPath;
    }

    public string BaseDir => _root;

    public string SrcRoot => _src;

    public string CorextConfig => _corextConfig;

    public IEnumerable<string> EnumerateProjectFiles(string? relativePath = null, string? fileExtension = null, bool relativePaths = false)
    {
        fileExtension ??= ".csproj";

        string path;

        if (string.IsNullOrEmpty(relativePath))
        {
            path = _root;
        }
        else
        {
            path = Path.Join(_root, relativePath);

            if (File.Exists(path))
            {
                if (string.Equals(Path.GetExtension(path), fileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    // If a path to a single project file was given, return it.
                    return [path];
                }
                else
                {
                    throw new ArgumentException($"The given path does not point to a project file.", nameof(relativePath));
                }
            }

            if (!Directory.Exists(path))
            {
                throw new ArgumentException("The given relative path does not point to a subdirectory of the repo root.", nameof(relativePath));
            }
        }

        string searchPattern = fileExtension.StartsWith('.') ? $"*{fileExtension}" : $"*.{fileExtension}";

        IEnumerable<string> results = Directory.EnumerateFiles(path, searchPattern, SearchOption.AllDirectories);

        if (!relativePaths)
        {
            return results;
        }
        else
        {
            return results.Select(path =>
            {
                return Path.GetRelativePath(_root, path);
            });
        }
    }
}
