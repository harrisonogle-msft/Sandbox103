using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Sandbox103.Helpers;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox103.V1.LogDrops;

public readonly struct ProjectImportInfo
{
    public ProjectImportInfo(string projectFile, string? unexpandedProjectFile)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFile);

        ProjectFile = projectFile;
        UnexpandedProjectFile = unexpandedProjectFile;
    }

    public string ProjectFile { get; }

    public string? UnexpandedProjectFile { get; }

    public override int GetHashCode() => ProjectFile.GetHashCode();

    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is ProjectImportInfo other &&
        string.Equals(
            ProjectFile,
            other.ProjectFile,
            StringComparison.OrdinalIgnoreCase);
}

public class ProjectImportGraphBuilder
{
    private delegate bool TryGetValues(string projectFile, [NotNullWhen(true)] out IEnumerator<ProjectImportInfo>? values);

    private readonly string _binLogPath;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<ProjectImportInfo, byte>> _forward;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<ProjectImportInfo, byte>> _reverse;

    public ProjectImportGraphBuilder(string binLogPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(binLogPath);

        _binLogPath = binLogPath;

        _forward = new(StringComparer.OrdinalIgnoreCase);
        _reverse = new(StringComparer.OrdinalIgnoreCase);
    }

    public void AddImport(string projectFile, string importedProjectFile, string unexpandedProject)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFile);
        ArgumentException.ThrowIfNullOrEmpty(importedProjectFile);
        ArgumentException.ThrowIfNullOrEmpty(unexpandedProject);

        static void Add(ConcurrentDictionary<string, ConcurrentDictionary<ProjectImportInfo, byte>> cache, string key, ProjectImportInfo value)
        {
            ConcurrentDictionary<ProjectImportInfo, byte>? items;
            ConcurrentDictionary<ProjectImportInfo, byte>? valueToAdd = null;

            while (!cache.TryGetValue(key, out items))
            {
                cache.TryAdd(key, valueToAdd ??= new());
            }

            while (!items.ContainsKey(value))
            {
                items.TryAdd(value, default);
            }
        }

        Add(_forward, projectFile, new ProjectImportInfo(importedProjectFile, unexpandedProject));
        Add(_reverse, importedProjectFile, new ProjectImportInfo(projectFile, projectFile));
    }

    public bool TryGetImports(string projectFile, [NotNullWhen(true)] out IEnumerator<ProjectImportInfo>? values)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFile);

        if (_forward.TryGetValue(projectFile, out ConcurrentDictionary<ProjectImportInfo, byte>? items))
        {
            values = items.Select(static item => item.Key).GetEnumerator();
            return true;
        }

        values = null;
        return false;
    }

    public bool TryGetImporters(string projectFile, [NotNullWhen(true)] out IEnumerator<ProjectImportInfo>? values)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFile);

        if (_reverse.TryGetValue(projectFile, out ConcurrentDictionary<ProjectImportInfo, byte>? items))
        {
            values = items.Select(static item => item.Key).GetEnumerator();
            return true;
        }

        values = null;
        return false;
    }

    public ProjectImportGraph Build()
    {
        Trace.WriteLine($"Building project import graph for binlog located at: '{_binLogPath}'");

        string? rootProjectFile = null;
        string? srcRoot = null;

        using (BuildEventArgsReader reader = BinLogHelper.OpenBuildEventsReader(_binLogPath))
        {
            var environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var imports = new List<ProjectImportedEventArgs>();
            var directImports = new List<ProjectImportedEventArgs>();

            void ProjectEvaluationStarted(ProjectEvaluationStartedEventArgs args)
            {
                Trace.WriteLine($"[{args.Timestamp.ToString("O")}] PROJECT EVALUATION START: {args.ProjectFile}");
                if (rootProjectFile is null && args.ProjectFile is string projectFile)
                {
                    rootProjectFile = projectFile;
                    srcRoot = GetSrcRoot(rootProjectFile);

                    static string? GetSrcRoot(string path)
                    {
                        string pattern;
                        int index = path.IndexOf(pattern = "\\src\\", StringComparison.OrdinalIgnoreCase);
                        if (index != -1)
                        {
                            goto IndexFound;
                        }
                        index = path.IndexOf(pattern = "/src/", StringComparison.OrdinalIgnoreCase);
                        if (index != -1)
                        {
                            goto IndexFound;
                        }
                        index = path.IndexOf(pattern = "\\src/", StringComparison.OrdinalIgnoreCase);
                        if (index != -1)
                        {
                            goto IndexFound;
                        }
                        index = path.IndexOf(pattern = "/src\\", StringComparison.OrdinalIgnoreCase);
                        if (index != -1)
                        {
                            goto IndexFound;
                        }
                        index = path.IndexOf(pattern = "src\\", StringComparison.OrdinalIgnoreCase);
                        if (index != -1)
                        {
                            goto IndexFound;
                        }
                        index = path.IndexOf(pattern = "src/", StringComparison.OrdinalIgnoreCase);
                        if (index == -1)
                        {
                            return null;
                        }
                    IndexFound:
                        return path.Substring(0, index + pattern.Length);
                    }
                }
            }

            void ProjectEvaluationFinished(ProjectEvaluationFinishedEventArgs args)
            {
                Trace.WriteLine($"[{args.Timestamp.ToString("O")}] PROJECT EVALUATION FINISH: {args.ProjectFile}");

                IDictionary<string, string>? properties = args.Properties is not IEnumerable<DictionaryEntry> castedProps ? null :
                    castedProps.Select(p => new KeyValuePair<string, string>((string)p.Key!, (string)p.Value!)).ToDictionary();

                IDictionary<string, string>? globalProperties = args.GlobalProperties as IDictionary<string, string>;

                var temp = properties?.OrderBy(kvp => kvp.Key).ToArray();

                string? GetPropertyValue(string propertyName, object? state)
                {
                    static void SafeSet(string propertyName, ref string? currentValue, string? newValue)
                    {
                        if (currentValue is not null && !string.Equals(currentValue, newValue, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException($"Property mismatch for property '{propertyName}': '{currentValue}' != '{newValue}'.");
                        }
                        currentValue = newValue;
                    }

                    string? value = null;
                    string? temp;

                    if (properties?.TryGetValue(propertyName, out temp) is true)
                    {
                        SafeSet(propertyName, ref value, temp);
                    }

                    if (globalProperties?.TryGetValue(propertyName, out temp) is true)
                    {
                        SafeSet(propertyName, ref value, temp);
                    }

                    if (environmentVariables.TryGetValue(propertyName, out temp))
                    {
                        SafeSet(propertyName, ref value, temp);
                    }

                    return value;
                }

                List<ProjectImportedEventArgs> list;

                lock (imports)
                {
                    list = new List<ProjectImportedEventArgs>(imports);
                    imports.Clear();
                }

                foreach (ProjectImportedEventArgs projectImportedEventArgs in list)
                {
                    string projectFile = projectImportedEventArgs.ProjectFile;
                    string import = projectImportedEventArgs.ImportedProjectFile ?? projectImportedEventArgs.UnexpandedProject;

                    const string MSBuildThisFileDirectory = "$(MSBuildThisFileDirectory)";
                    const string MSBuildThisFile = "$(MSBuildThisFile)";
                    const string MSBuildThisFileName = "$(MSBuildThisFileName)";

                    if (import.Contains(MSBuildThisFileDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        import = import.Replace(MSBuildThisFileDirectory, Path.GetDirectoryName(projectFile), StringComparison.OrdinalIgnoreCase);
                    }

                    if (import.Contains(MSBuildThisFile, StringComparison.OrdinalIgnoreCase))
                    {
                        import = import.Replace(MSBuildThisFile, projectFile, StringComparison.OrdinalIgnoreCase);
                    }

                    if (import.Contains(MSBuildThisFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        import = import.Replace(MSBuildThisFileName, Path.GetFileName(projectFile), StringComparison.OrdinalIgnoreCase);
                    }

                    string expandedImport = TokenRegexHelper.Expand(import, GetPropertyValue);

                    if (import != expandedImport)
                    {
                        Trace.WriteLine($"Expanded project import '{import}' to '{expandedImport}'");
                    }

                    AddImport(projectFile, expandedImport, projectImportedEventArgs.UnexpandedProject);

                    if (string.IsNullOrEmpty(rootProjectFile))
                    {
                        throw new InvalidOperationException("Missing root project file.");
                    }

                    if (string.Equals(projectFile, rootProjectFile, StringComparison.OrdinalIgnoreCase) ||
                        Path.GetPathRoot(rootProjectFile) is string rootProjectFileRoot &&
                        projectFile.EndsWith(Path.GetRelativePath(rootProjectFileRoot, rootProjectFile), StringComparison.OrdinalIgnoreCase))
                    {
                        lock (directImports)
                        {
                            directImports.Add(projectImportedEventArgs);
                        }
                    }
                }
            }

            void ProjectImported(ProjectImportedEventArgs args)
            {
                bool shouldAdd = true;

                if (args.ImportedProjectFile is null)
                {
                    if (string.IsNullOrEmpty(srcRoot))
                    {
                        throw new InvalidOperationException("Missing src root.");
                    }

                    if (!args.ProjectFile.StartsWith(srcRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        shouldAdd = false;
                    }
                }

                if (shouldAdd)
                {
                    string? projectFile = args.ProjectFile;
                    string? importedProjectFile = args.ImportedProjectFile ?? args.UnexpandedProject;

                    if (!string.IsNullOrEmpty(projectFile) &&
                        !string.IsNullOrEmpty(importedProjectFile))
                    {
                        if (args.ImportIgnored)
                        {
                            throw new Exception($"Import ignored. project file: '{projectFile}', imported project file: '{importedProjectFile}'");
                        }

                        lock (imports)
                        {
                            imports.Add(args);
                        }
                    }
                }
            }

            while (reader.Read() is BuildEventArgs buildEventArgs)
            {
                if (buildEventArgs is ProjectEvaluationStartedEventArgs projectEvaluationStartedEventArgs)
                {
                    ProjectEvaluationStarted(projectEvaluationStartedEventArgs);
                }
                else if (buildEventArgs is ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
                {
                    ProjectEvaluationFinished(projectEvaluationFinishedEventArgs);
                }
                else if (buildEventArgs is ProjectStartedEventArgs projectStartedEventArgs)
                {
                    Trace.WriteLine($"[{projectStartedEventArgs.Timestamp.ToString("O")}] PROJECT START: {projectStartedEventArgs.ProjectFile}");
                }
                else if (buildEventArgs is ProjectFinishedEventArgs projectFinishedEventArgs)
                {
                    Trace.WriteLine($"[{projectFinishedEventArgs.Timestamp.ToString("O")}] PROJECT FINISH: {projectFinishedEventArgs.ProjectFile}");
                }
                else if (buildEventArgs is EnvironmentVariableReadEventArgs environmentVariableReadEventArgs)
                {
                    environmentVariables[environmentVariableReadEventArgs.EnvironmentVariableName] = environmentVariableReadEventArgs.Message!;
                }
                else if (buildEventArgs is ProjectImportedEventArgs projectImportedEventArgs)
                {
                    ProjectImported(projectImportedEventArgs);
                }
            }

            if (imports.Count > 0)
            {
                throw new InvalidOperationException($"Unexpected error: {imports.Count} unhandled imports.");
            }

            if (rootProjectFile is null)
            {
                throw new InvalidOperationException("Unexpected error: root project file is missing.");
            }

            if (srcRoot is null)
            {
                throw new InvalidOperationException("Unexpected error: SRCROOT is missing.");
            }
        }

        var lookup = new Dictionary<string, ProjectImport>(StringComparer.OrdinalIgnoreCase);

        foreach (string projectFile in _forward.Keys.Union(_reverse.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            lookup[projectFile] = new ProjectImport(_binLogPath, projectFile, srcRoot);
        }

        foreach (ProjectImport projectImport in lookup.Values)
        {
            if (TryGetImports(projectImport.ProjectFile, out IEnumerator<ProjectImportInfo>? imports))
            {
                using (imports)
                {
                    while (imports.MoveNext())
                    {
                        ProjectImportInfo info = imports.Current;

                        if (lookup.TryGetValue(imports.Current.ProjectFile, out ProjectImport? import))
                        {
                            projectImport.AddImport(import, info);
                        }
                        else
                        {
                            Trace.TraceWarning($"Unable to add import '{imports.Current}' to project file '{projectImport.ProjectFile}'.");
                        }
                    }
                }
            }

            if (TryGetImporters(projectImport.ProjectFile, out IEnumerator<ProjectImportInfo>? importers))
            {
                using (importers)
                {
                    while (importers.MoveNext())
                    {
                        if (lookup.TryGetValue(importers.Current.ProjectFile, out ProjectImport? importer))
                        {
                            projectImport.AddImporter(importer);
                        }
                        else
                        {
                            Trace.TraceWarning($"Unable to add importer '{importers.Current}' to project file '{projectImport.ProjectFile}'.");
                        }
                    }
                }
            }
        }

        var projectImports = new HashSet<ProjectImport>(lookup.Values);

        return new ProjectImportGraph(projectImports, lookup, srcRoot, rootProjectFile);
    }
}
