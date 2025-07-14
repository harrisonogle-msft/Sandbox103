using Microsoft.Extensions.Logging;

namespace Sandbox103.V2;

internal class SourceRepositoryReader : ISourceRepositoryReader
{
    private static readonly EnumerationOptions TopDirectoryOnly = new EnumerationOptions
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = false,
    };

    private static readonly EnumerationOptions AllDirectories = new EnumerationOptions
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = true,
    };

    private readonly ILogger<SourceRepositoryReader> _logger;

    public SourceRepositoryReader(
        ILogger<SourceRepositoryReader> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public Task<ISourceRepository> ReadAsync(string repositoryPath, ILogDrop logDrop, CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfDirectoryNotFound(repositoryPath);
        ArgumentNullException.ThrowIfNull(logDrop);
        cancellationToken.ThrowIfCancellationRequested();

        var archiveFileMap = new Dictionary<string, IBinaryLog>(StringComparer.OrdinalIgnoreCase);
        foreach (IBinaryLog binlog in logDrop.BinaryLogs)
        {
            IArchiveFile archiveFile = binlog.ProjectFile;
            string normalizedArchiveFilePath = PathHelper.NormalizePath(archiveFile.Path);
            if (archiveFileMap.TryGetValue(normalizedArchiveFilePath, out IBinaryLog? existing))
            {
                if (!existing.ProjectFile.Equals(archiveFile))
                {
                    throw new InvalidOperationException($"Path normalization inconsistency for normalized path '{normalizedArchiveFilePath}': '{existing.Path}' != '{archiveFile.Path}'");
                }
            }
            archiveFileMap[normalizedArchiveFilePath] = binlog;
        }

        int warnings = 0;
        var projectFiles = new List<ProjectFile>();

        foreach (string csprojFile in Directory.EnumerateFiles(repositoryPath, "*.csproj", AllDirectories))
        {
            string relativeCsprojFile = Path.GetRelativePath(repositoryPath, csprojFile);
            _logger.LogInformation($"Indexing project file '{relativeCsprojFile}'.");

            string normalizedRelativeCsprojFile = PathHelper.NormalizePath(relativeCsprojFile);

            IBinaryLog? binlog = archiveFileMap
                .Where(kvp => kvp.Key.EndsWith(normalizedRelativeCsprojFile, StringComparison.Ordinal))
                .Select(static kvp => kvp.Value)
                .SingleOrDefault();

            if (binlog is null)
            {
                _logger.LogWarning($"Failed to associate an archived project file (from the .binlog) with the local repository project file '{normalizedRelativeCsprojFile}'. This can happen when the project is not included in the build.");
                warnings++;
                continue;
            }

            projectFiles.Add(new ProjectFile(csprojFile, binlog));
        }

        _logger.LogInformation($"Finished building the repository index of {projectFiles.Count} files with {warnings} warning(s).");

        string packagesPropsPath;
        try
        {
            packagesPropsPath =
                Directory.EnumerateDirectories(repositoryPath, "src", AllDirectories )
                    .SelectMany(src => Directory.EnumerateFiles(src, "packages.props", TopDirectoryOnly))
                    .SingleOrDefault() ??
                Directory.EnumerateFiles(repositoryPath, "packages.props", AllDirectories)
                    .Single();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find 'packages.props' file.");
            throw;
        }

        string corextConfigPath;
        try
        {
            corextConfigPath = Path.Join(repositoryPath, "build", "corext", "corext.config");
            if (!File.Exists(corextConfigPath))
            {
                corextConfigPath = Directory.EnumerateFiles(repositoryPath, "corext.config", AllDirectories).SingleOrDefault() ??
                    throw new FileNotFoundException("Unable to find corext.config.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find 'corext.config' file.");
            throw;
        }

        _logger.LogInformation($"Found 'packages.props' path: {packagesPropsPath}");

        return Task.FromResult<ISourceRepository>(new SourceRepository(
            projectFiles.AsReadOnly(),
            packagesPropsPath,
            corextConfigPath));
    }
}
