using Microsoft.Extensions.FileSystemGlobbing;
using Sandbox103.BuildDrops;
using Sandbox103.LogDrops;
using Sandbox103.Repos;
using System.Diagnostics;

namespace Sandbox103;

public struct RepoConversionOptions
{
    public string RepoPath { get; set; }

    public string BuildDropPath { get; set; }

    public string LogDropPath { get; set; }
}

public class RepoConversion
{
    private readonly LocalGitRepo _repo;
    private readonly LogDrop _logDrop;
    private readonly BuildDrop _buildDrop;

    private List<ProjectFile>? _projectFiles;

    public RepoConversion(RepoConversionOptions options) : this(
        new LocalGitRepo(options.RepoPath),
        new LogDrop(options.LogDropPath),
        new BuildDrop(options.BuildDropPath))
    {
    }

    public RepoConversion(LocalGitRepo repo, LogDrop logDrop, BuildDrop buildDrop)
    {
        _repo = repo;
        _logDrop = logDrop;
        _buildDrop = buildDrop;
    }

    public LocalGitRepo Repo => _repo;

    public LogDrop LogDrop => _logDrop;

    public BuildDrop BuildDrop => _buildDrop;

    public IReadOnlyList<ProjectFile> ProjectFiles => _projectFiles ??= GetProjectFiles(_repo, _logDrop);

    public static List<ProjectFile> GetProjectFiles(LocalGitRepo repo, LogDrop logDrop)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(logDrop);

        long t0 = Stopwatch.GetTimestamp();

        var projectFiles = new List<ProjectFile>();

        foreach (string csproj in repo.EnumerateProjectFiles(fileExtension: ".csproj", relativePaths: true))
        {
            string csprojDir = Path.GetDirectoryName(csproj) ?? throw new DirectoryNotFoundException($"Unable to get containing directory of csproj file '{csproj}'.");
            var glob = new Matcher();
            glob.AddInclude(Path.Join("**", csprojDir, "**", "*.binlog"));

            string binlogPath;

            using (var it = logDrop.Glob(glob).GetEnumerator())
            {
                if (!it.MoveNext())
                {
                    // This happens when a project in the local repo is not included in the official build.
                    Console.WriteLine($"Unable to find binlog under project directory '{csprojDir}'.");
                    continue;
                }

                binlogPath = it.Current;

                if (it.MoveNext())
                {
                    throw new InvalidOperationException($"Unexpected error: found multiple binlogs under project directory '{csprojDir}'.");
                }
            }

            Console.WriteLine($"Found binlog: {binlogPath}");
            projectFiles.Add(new ProjectFile(Path.Join(repo.BaseDir, csproj), binlogPath));
        }

        Trace.WriteLine($"Retrieved project files from local git repo. ({Stopwatch.GetElapsedTime(t0)})");

        return projectFiles;
    }
}
