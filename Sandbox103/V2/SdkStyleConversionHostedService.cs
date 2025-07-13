using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Sandbox103.V2;

public sealed class SdkStyleConversionHostedService : IHostedService
{
    private readonly ILogger<SdkStyleConversionHostedService> _logger;
    private readonly SdkStyleConversionOptions _options;
    private readonly ILogDropReader _logDropReader;
    private readonly IArchiveFileIndex _archiveFileIndex;

    public SdkStyleConversionHostedService(
        ILogger<SdkStyleConversionHostedService> logger,
        IOptions<SdkStyleConversionOptions> options,
        ILogDropReader logDropReader,
        IArchiveFileIndex archiveFileIndex)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logDropReader);
        ArgumentNullException.ThrowIfNull(archiveFileIndex);

        _logger = logger;
        _options = options.Value;
        _logDropReader = logDropReader;
        _archiveFileIndex = archiveFileIndex;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartAsync begin.");
        long t0 = Stopwatch.GetTimestamp();
        try
        {
            await StartAsyncCore(cancellationToken);
        }
        finally
        {
            _logger.LogInformation($"StartAsync finished. ({Stopwatch.GetElapsedTime(t0)})");
        }
    }

    // TODO: Remove IHostedService and just do it in Program.cs.
    private async Task StartAsyncCore(CancellationToken cancellationToken)
    {
        // Read and index every `.binlog` file in the log drop.
        ILogDrop logDrop = await _logDropReader.ReadAsync(new LogDropReaderOptions { Path = _options.LogDropPath }, cancellationToken);

        foreach (IArchiveFile projectFile in logDrop.BinaryLogs.Select(static binlog => binlog.ProjectFile))
        {
            HandleProject(projectFile);
        }
    }

    // TODO: Create a component that handles this. (modifies project files)
    private void HandleProject(IArchiveFile projectFile)
    {
        var topLevelPrivateTargets = new HashSet<IArchiveFile>();
        var importsToRemove = new HashSet<DirectImport>();

        foreach (DirectImport directImport in projectFile.GetImports())
        {
            int count = Traverse(directImport, topLevelPrivateTargets);
            if (count > 0)
            {
                // We should remove the import, since it (transitively) imported `count` private targets.
                importsToRemove.Add(directImport);
            }
        }

        // Remove direct imports that (transitively) import private targets.

        string projectFileName = Path.GetFileName(projectFile.Path);

        if (importsToRemove.Count < 1)
        {
            Console.WriteLine($"No imports to remove from '{projectFileName}'!");
        }
        else
        {
            Console.WriteLine($"Found {importsToRemove.Count} project import(s) to remove from '{projectFileName}'.");

            foreach (DirectImport importToRemove in importsToRemove.OrderBy(static x => x.File.Path))
            {
                string unexpandedProjectFile = importToRemove.UnexpandedProjectName ??
                    throw new InvalidOperationException($"Direct import '{importToRemove.File.Path}' is missing unexpanded project file.");

                Console.WriteLine($"  {unexpandedProjectFile}");
            }

            Predicate<string> shouldRemove = (string projectName) => importsToRemove.Any(x => string.Equals(x.UnexpandedProjectName, projectName, StringComparison.OrdinalIgnoreCase));
            //int numRemoved = XmlHelper.RemoveProjectImports(project, shouldRemove);
            int numRemoved = -9999;
            Console.WriteLine($"Removed {numRemoved} project import(s).");
        }

        // TODO: Finish!
    }

    // We'll use this function to determine whether a project file might contain CoreXT package name and version.
    static bool IsPrivateTargets(IArchiveFile node)
    {
        return node.Path.EndsWith(".private.targets", StringComparison.OrdinalIgnoreCase);
    }

    // Does a DFT on the import graph. If private targets are found, it does not continue searching
    // for "strictly transitive" private targets.
    static int Traverse(DirectImport node, HashSet<IArchiveFile> privateTargets)
    {
        if (IsPrivateTargets(node.File))
        {
            privateTargets.Add(node.File);
            return 1;
        }
        else if (node.File.Features.Get<IContainsReferenceItemFeature>()?.ContainsReferenceItem is true)
        {
            return 1;
        }
        else
        {
            int count = 0;

            foreach (DirectImport transitiveImport in node.File.GetImports())
            {
                count += Traverse(transitiveImport, privateTargets);
            }

            return count;
        }
    }
}
