using Microsoft.Build.Logging;
using Microsoft.Extensions.Logging;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Text;

namespace Sandbox103.V2;

public sealed class LogDropReader : ILogDropReader
{
    private static readonly EnumerationOptions s_validationEnumerationOptions = CreateEnumerationOptions(5, MatchCasing.PlatformDefault);
    private static readonly EnumerationOptions s_srcDirectorySearchEnumerationOptions = CreateEnumerationOptions(5, MatchCasing.PlatformDefault);
    private static readonly EnumerationOptions s_binaryLogSearchEnumerationOptions = CreateEnumerationOptions(100, MatchCasing.CaseInsensitive);

    private readonly ILogger<LogDropReader> _logger;
    private readonly IBinaryLogReader _binaryLogReader;
    private readonly IArchiveFileIndex _archiveFileIndex;

    public LogDropReader(
        ILogger<LogDropReader> logger,
        IBinaryLogReader binaryLogReader,
        IArchiveFileIndex archiveFileIndex)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(binaryLogReader);
        ArgumentNullException.ThrowIfNull(archiveFileIndex);

        _logger = logger;
        _binaryLogReader = binaryLogReader;
        _archiveFileIndex = archiveFileIndex;
    }

    public async Task<ILogDrop> ReadAsync(LogDropReaderOptions options, CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfDirectoryNotFound(options.Path);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLogDropPath(options.Path);

        // 1. Find the "src" directories in the log drop.
        // 2. Scan the log drop for all the `.binlog` files.
        // 3. Read all of the `.binlog` files.

        // Scoping to the 'src' directory excludes QuickBuild `.binlog`s and other builds that aren't ours.
        // Globbing library is VERY slow, so do this instead.
        string[] srcDirs = Directory.GetDirectories(options.Path, "src", s_srcDirectorySearchEnumerationOptions);
        string[] binaryLogPaths = srcDirs.SelectMany(src => Directory.EnumerateFiles(src, "*.binlog", s_binaryLogSearchEnumerationOptions)).ToArray();
        _logger.LogInformation($"Found {binaryLogPaths.Length} binlog file(s).");

        var binaryLogs = new List<IBinaryLog>();

        long t0 = Stopwatch.GetTimestamp();
        _logger.LogInformation($"Scanning {binaryLogPaths.Length} binlog file(s) for archive files.");

        // Read the binlog files.
        foreach (string path in binaryLogPaths)
        {
            var binaryLogReaderOptions = new BinaryLogReaderOptions
            {
                Path = path,
            };
            IBinaryLog binaryLog = await _binaryLogReader.ReadAsync(binaryLogReaderOptions, cancellationToken);
            binaryLogs.Add(binaryLog);
        }

        return new LogDrop(options.Path, binaryLogs.AsReadOnly());
    }

    private static void ValidateLogDropPath(string path)
    {
        // Validate this appears to be a log drop, so that we don't end up
        // recursing the whole filesystem e.g. if a consumer passes the root
        // as the log drop path.
        _ = Directory.EnumerateFiles(path, "*.log", s_validationEnumerationOptions) ??
            throw new InvalidOperationException("Invalid log drop path.");
    }

    private static EnumerationOptions CreateEnumerationOptions(int maxRecursionDepth, MatchCasing matchCasing) => new EnumerationOptions
    {
        MatchCasing = MatchCasing.PlatformDefault,
        IgnoreInaccessible = true,
        MaxRecursionDepth = maxRecursionDepth,
        RecurseSubdirectories = true,
        ReturnSpecialDirectories = true,
    };
}
