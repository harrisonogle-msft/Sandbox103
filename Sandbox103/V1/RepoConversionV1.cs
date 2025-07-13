using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Extensions.FileSystemGlobbing;
using Sandbox103.Extensions;
using Sandbox103.Helpers;
using Sandbox103.Options;
using Sandbox103.V1.BuildDrops;
using Sandbox103.V1.LogDrops;
using Sandbox103.V1.Repos;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Sandbox103.V1;

public class RepoConversionV1
{
    private readonly LocalGitRepoV1 _repo;
    private readonly LogDropV1 _logDrop;
    private readonly BuildDropV1 _buildDrop;
    private string? _packagesPropsFile;
    private string? _srcDirectoryBuildPropsFile;
    private IReadOnlyDictionary<BinaryReference, ProjectFileV1>? _projectReferences;

    private List<ProjectFileV1>? _projectFiles;

    public RepoConversionV1(SdkStyleConversionOptions options) : this(
        new LocalGitRepoV1(options.RepoPath),
        new LogDropV1(options.LogDropPath),
        new BuildDropV1(options.BuildDropPath))
    {
    }

    public RepoConversionV1(LocalGitRepoV1 repo, LogDropV1 logDrop, BuildDropV1 buildDrop)
    {
        _repo = repo;
        _logDrop = logDrop;
        _buildDrop = buildDrop;
    }

    public LocalGitRepoV1 Repo => _repo;

    public LogDropV1 LogDrop => _logDrop;

    public BuildDropV1 BuildDrop => _buildDrop;

    public string PackagesPropsFile => _packagesPropsFile ??= GetOrCreatePackagesPropsFile();

    public string SrcDirectoryBuildPropsFile => _srcDirectoryBuildPropsFile ??= GetOrCreateSrcDirectoryBuildPropsFile();

    public IReadOnlyList<ProjectFileV1> ProjectFiles => _projectFiles ??= GetProjectFiles(_repo, _logDrop, _buildDrop);

    public IReadOnlyDictionary<BinaryReference, ProjectFileV1> ProjectReferences => _projectReferences ??= GetProjectReferences();

    public List<ProjectFileV1> GetProjectFiles(LocalGitRepoV1 repo, LogDropV1 logDrop, BuildDropV1 buildDrop)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(logDrop);
        ArgumentNullException.ThrowIfNull(buildDrop);

        long t0 = Stopwatch.GetTimestamp();

        var projectFiles = new List<ProjectFileV1>();

        foreach (string relativeCsprojPath in repo.EnumerateProjectFiles(fileExtension: ".csproj", relativePaths: true))
        {
            string binLogPath;

            // TODO: Consider reading all binlogs first and attempting to find the .csproj in the local repo using information from the binlog.
            // TODO: Consider writing a method to determine the root project of a binlog. (is it sufficient to assume it's the first built project in the binlog?)
            var glob = new Matcher(StringComparison.OrdinalIgnoreCase);
            string csprojDir = Path.GetDirectoryName(relativeCsprojPath) ?? throw new DirectoryNotFoundException($"Unable to get containing directory of csproj file '{relativeCsprojPath}'.");
            glob.AddInclude(Path.Join("**", csprojDir, "**", "*.binlog"));

            using (var it = logDrop.Glob(glob).GetEnumerator())
            {
                if (!it.MoveNext())
                {
                    // This happens when a project in the local repo is not included in the official build.
                    Console.WriteLine($"Unable to find binlog under project directory '{csprojDir}'.");
                    continue;
                }

                binLogPath = it.Current;

                if (it.MoveNext())
                {
                    throw new InvalidOperationException($"Unexpected error: found multiple binlogs under project directory '{csprojDir}'.");
                }
            }

            IDictionary<string, string> properties = GetProperties(binLogPath, Path.GetFileName(relativeCsprojPath), ["TargetPath", "TargetDir", "OutDir"]);
            if (!properties.TryGetValue("TargetPath", out string? targetPath) ||
                !properties.TryGetValue("TargetDir", out string? targetDir) ||
                !properties.TryGetValue("OutDir", out string? outDir))
            {
                throw new InvalidOperationException("Missing required properties.");
            }

            string? binaryPath = null;

            string buildDropPath = buildDrop.Path;

            static bool TryFindPath(string buildDropPath, string targetPath, [NotNullWhen(true)] out string? binaryPath)
            {
                foreach (string path in EnumerateRelativeSubPaths(targetPath))
                {
                    string buildDropTargetPath = Path.Join(buildDropPath, path);
                    if (File.Exists(buildDropTargetPath))
                    {
                        binaryPath = buildDropTargetPath;
                        return true;
                    }
                }
                binaryPath = null;
                return false;
            }

            if (!TryFindPath(buildDropPath, targetPath, out binaryPath) &&
                string.Equals(Path.GetFileName(targetPath), targetPath, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryFindPath(buildDropPath, Path.Join(targetDir, targetPath), out binaryPath))
                {
                    TryFindPath(buildDropPath, Path.Join(outDir, targetPath), out binaryPath);
                }
            }

            if (string.IsNullOrEmpty(binaryPath))
            {
                throw new InvalidOperationException($"Unexpected error: binary path not found for project '{relativeCsprojPath}'.");
            }

            string relativeBinLogPath = Path.GetRelativePath(LogDrop.Path, binLogPath);
            string relativeBinaryPath = Path.GetRelativePath(BuildDrop.Path, binaryPath);

            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"Project: {relativeCsprojPath}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  binlog: {relativeBinLogPath}");
            sb.Append/**/(CultureInfo.InvariantCulture, $"  binary: {relativeBinaryPath}");
            Console.WriteLine(sb.ToString());

            projectFiles.Add(new ProjectFileV1(Path.Join(repo.Path, relativeCsprojPath), binLogPath, binaryPath));
        }

        Trace.WriteLine($"Retrieved project files from local git repo. ({Stopwatch.GetElapsedTime(t0)})");

        return projectFiles;
    }

    static IEnumerable<string> EnumerateRelativeSubPaths(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string[] segments = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < segments.Length; i++)
        {
            yield return Path.Join([.. segments.Skip(i)]);
        }
    }

    static IDictionary<string, string> GetProperties(string binLogPath, string projectName, ICollection<string> propertyNames)
    {
        ArgumentException.ThrowIfNullOrEmpty(binLogPath);
        ArgumentException.ThrowIfNullOrEmpty(projectName);
        ArgumentNullException.ThrowIfNull(propertyNames);

        var results = new Dictionary<string, string>();

        using (BuildEventArgsReader reader = BinLogHelper.OpenBuildEventsReader(binLogPath))
        {
            while (reader.Read() is BuildEventArgs buildEventArgs)
            {
                if (buildEventArgs is ProjectEvaluationFinishedEventArgs args)
                {
                    if (args.ProjectFile is string projectFile &&
                        string.Equals(Path.GetFileName(projectFile), projectName, StringComparison.OrdinalIgnoreCase))
                    {
                        IDictionary<string, string>? properties = args.Properties is not IEnumerable<DictionaryEntry> castedProps ? null :
                            castedProps.Select(p => new KeyValuePair<string, string>((string)p.Key!, (string)p.Value!)).ToDictionary();

                        if (properties is not null)
                        {
                            foreach (string propertyName in propertyNames)
                            {
                                if (properties.TryGetValue(propertyName, out string? propertyValue))
                                {
                                    results[propertyName] = propertyValue;
                                }
                            }
                        }
                    }
                }
            }
        }

        foreach (string propertyName in propertyNames)
        {
            if (!results.ContainsKey(propertyName))
            {
                throw new InvalidOperationException($"Property not found: '{propertyName}'");
            }
        }

        return results;
    }

    private string GetOrCreatePackagesPropsFile()
    {
        string? packagesPropsFile = Path.Join(_repo.SrcRoot, "packages.props");
        if (!File.Exists(packagesPropsFile))
        {
            packagesPropsFile = Directory.EnumerateFiles(_repo.Path, "packages.props", SearchOption.AllDirectories).FirstOrDefault();
        }
        if (packagesPropsFile is null)
        {
            packagesPropsFile = Path.Join(_repo.SrcRoot, "packages.props");
            Trace.WriteLine($"Creating packages.props file: {packagesPropsFile}");
            File.WriteAllText(packagesPropsFile, """
                <?xml version="1.0" encoding="utf-8"?>
                <!--This props file contains all the versions for PackageReferences that are opted into CentralVersionPackageManagement: https://github.com/microsoft/MSBuildSdks/blob/master/src/CentralPackageVersions/README.md -->
                <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                </Project>
                """);
        }
        return packagesPropsFile;
    }

    private string GetOrCreateSrcDirectoryBuildPropsFile()
    {
        string file = Path.Join(_repo.SrcRoot, "directory.build.props");
        if (!File.Exists(file))
        {
            Trace.WriteLine($"Creating directory.build.props file: {file}");
            File.WriteAllText(file, """
                <Project>
                  <!-- Import the parent directory.build.props file. -->
                  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" Condition="Exists($([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../')))"/>
                  <PropertyGroup>
                  </PropertyGroup>
                </Project>
                """);
        }
        return file;
    }

    private IReadOnlyDictionary<BinaryReference, ProjectFileV1> GetProjectReferences()
    {
        var projectReferences = new Dictionary<BinaryReference, ProjectFileV1>();
        foreach (ProjectFileV1 projectFile in ProjectFiles)
        {
            LocalAssembly localAssembly = LocalAssembly.FromPath(projectFile.BinaryPath);
            BinaryReference projectReference = localAssembly.ToBinaryReference();
            projectReferences.Add(projectReference, projectFile);
        }
        return projectReferences;
    }
}
