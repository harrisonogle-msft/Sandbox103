using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Sandbox103.V2;

internal sealed class ProjectImportEventSourceSubscriber : IEventSourceSubscriber
{
    private readonly ILogger<ProjectImportEventSourceSubscriber> _logger;
    private readonly IArchiveFileIndex _archiveFileIndex;

    public ProjectImportEventSourceSubscriber(
        ILogger<ProjectImportEventSourceSubscriber> logger,
        IArchiveFileIndex archiveFileIndex)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(archiveFileIndex);

        _logger = logger;
        _archiveFileIndex = archiveFileIndex;
    }

    public void EventSourceCreated(IEventSource eventSource, IArchiveFile projectFile)
    {
        ArgumentNullException.ThrowIfNull(eventSource);
        ArgumentNullException.ThrowIfNull(projectFile);

        var importListener = new ProjectImportListener(_archiveFileIndex, _logger);
        var environmentVariableListener = new EnvironmentVariableListener(_archiveFileIndex);
        var projectEvaluationListener = new ProjectEvaluationListener(_archiveFileIndex, _logger);

        eventSource.AnyEventRaised += (s, e) =>
        {
            if (e is ProjectImportedEventArgs projectImportedEventArgs)
            {
                importListener.ProjectImported(projectImportedEventArgs);
            }
            else if (e is EnvironmentVariableReadEventArgs environmentVariableReadEventArgs)
            {
                environmentVariableListener.EnvironmentVariableRead(environmentVariableReadEventArgs);
            }
            else if (e is ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
            {
                var prev = Interlocked.Exchange(ref importListener, new ProjectImportListener(_archiveFileIndex, _logger));

                projectEvaluationListener.ProjectEvaluationFinished(
                    projectEvaluationFinishedEventArgs,
                    prev.UnprocessedEvents,
                    environmentVariableListener.EnvironmentVariables);
            }
        };
    }

    sealed class ProjectImportListener(IArchiveFileIndex archiveFileIndex, ILogger logger)
    {
        public ConcurrentQueue<ProjectImportedEvent> UnprocessedEvents { get; } = new();

        public void ProjectImported(ProjectImportedEventArgs args)
        {
            if (args.ProjectFile is string projectFile &&
                args.ImportedProjectFile is string importedProjectFile)
            {
                if (archiveFileIndex.TryGetValue(projectFile, out IArchiveFile? importerFile) &&
                    archiveFileIndex.TryGetValue(importedProjectFile, out IArchiveFile? importFile))
                {
                    logger.LogDebug($"Adding import from '{Path.GetFileName(importerFile.Path)}' to '{Path.GetFileName(importFile.Path)}'.");
                    importerFile.GetImports().Add(new DirectImport(importFile, args.UnexpandedProject));
                    importFile.GetImporters().Add(importerFile);
                    return;
                }
            }

            // Project name needs to be expanded using PropertyGroup properties, which are discovered later, so save it.
            UnprocessedEvents.Enqueue(new ProjectImportedEvent(args.ProjectFile, args.ImportedProjectFile, args.UnexpandedProject));
        }
    }

    sealed class EnvironmentVariableListener(IArchiveFileIndex archiveFileIndex)
    {
        public Dictionary<string, string?> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void EnvironmentVariableRead(EnvironmentVariableReadEventArgs args)
        {
            if (args.EnvironmentVariableName is string name)
            {
                EnvironmentVariables[name] = args.Message;

                if (args.ProjectFile is string projectFile)
                {
                    if (archiveFileIndex.TryGetValue(projectFile, out IArchiveFile? archiveFile))
                    {
                        if (archiveFile.Features.Get<IProjectEnvironmentVariablesFeature>() is not IProjectEnvironmentVariablesFeature feature)
                        {
                            archiveFile.Features.Set<IProjectEnvironmentVariablesFeature>(feature = new ProjectEnvironmentVariablesFeature());
                        }
                        feature.EnvironmentVariables[name] = args.Message;
                    }
                }
            }
        }
    }

    sealed class ProjectEvaluationListener(IArchiveFileIndex archiveFileIndex, ILogger logger)
    {
        public void ProjectEvaluationFinished(
            ProjectEvaluationFinishedEventArgs args,
            ConcurrentQueue<ProjectImportedEvent> unprocessedEvents,
            IDictionary<string, string?> environmentVariables)
        {
            if (args.ProjectFile is not string finishedProjectFile) return;

            if (archiveFileIndex.TryGetValue(finishedProjectFile, out IArchiveFile? archiveFile))
            {
                if (archiveFile.Features.Get<IProjectPropertiesFeature>() is not IProjectPropertiesFeature feature)
                {
                    archiveFile.Features.Set<IProjectPropertiesFeature>(feature = new ProjectPropertiesFeature());
                }
                TryCopy(args.GlobalProperties, feature.Properties);
                TryCopy(args.Properties, feature.Properties);
            }

            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            TryCopy(args.GlobalProperties, properties);
            TryCopy(args.Properties, properties);

            while (unprocessedEvents.Count > 0)
            {
                if (unprocessedEvents.TryDequeue(out ProjectImportedEvent e))
                {
                    string importer = e.ProjectFile;
                    string import = e.ImportedProjectFile ?? e.UnexpandedProject;

                    const string MSBuildThisFileDirectory = "$(MSBuildThisFileDirectory)";
                    const string MSBuildThisFile = "$(MSBuildThisFile)";
                    const string MSBuildThisFileName = "$(MSBuildThisFileName)";

                    if (import.Contains(MSBuildThisFileDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        import = import.Replace(MSBuildThisFileDirectory, Path.GetDirectoryName(importer), StringComparison.OrdinalIgnoreCase);
                    }

                    if (import.Contains(MSBuildThisFile, StringComparison.OrdinalIgnoreCase))
                    {
                        import = import.Replace(MSBuildThisFile, importer, StringComparison.OrdinalIgnoreCase);
                    }

                    if (import.Contains(MSBuildThisFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        import = import.Replace(MSBuildThisFileName, Path.GetFileName(importer), StringComparison.OrdinalIgnoreCase);
                    }

                    string expandedImport = TokenRegexHelper.Expand(import, CreatePropertyGetter(properties, environmentVariables));

                    if (import != expandedImport)
                    {
                        Trace.WriteLine($"Expanded project import '{import}' to '{expandedImport}'");
                    }

                    if (archiveFileIndex.TryGetValue(importer, out IArchiveFile? importerFile) &&
                        archiveFileIndex.TryGetValue(import, out IArchiveFile? importFile))
                    {
                        logger.LogDebug($"Adding import from '{Path.GetFileName(importerFile.Path)}' to '{Path.GetFileName(importFile.Path)}'.");
                        importerFile.GetImports().Add(new DirectImport(importFile, e.UnexpandedProject));
                        importFile.GetImporters().Add(importerFile);
                    }
                }
            }
        }

        private Func<string, object?, string?> CreatePropertyGetter(
            IDictionary<string, string> properties,
            IDictionary<string, string?> environmentVariables)
        {
            return GetPropertyValue;

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

                if (properties.TryGetValue(propertyName, out temp))
                {
                    SafeSet(propertyName, ref value, temp);
                }

                if (environmentVariables.TryGetValue(propertyName, out temp))
                {
                    SafeSet(propertyName, ref value, temp);
                }

                return value;
            }
        }

        private void TryCopy(IEnumerable? source, IDictionary<string, string> target)
        {
            if (source is IDictionary<string, string> globalProperties)
            {
                foreach ((string key, string value) in globalProperties)
                {
                    target[key] = value;
                }
            }
            else if (source is IEnumerable<DictionaryEntry> dictionaryEntries)
            {
                foreach (DictionaryEntry entry in dictionaryEntries)
                {
                    if (entry.Key is string key && entry.Value is string value)
                    {
                        target[key] = value;
                    }
                }
            }
            else if (source is IEnumerable<KeyValuePair<string, string>> keyValuePairs)
            {
                foreach ((string key, string value) in keyValuePairs)
                {
                    target[key] = value;
                }
            }
        }
    }

    readonly record struct ProjectImportedEvent(
        string ProjectFile,
        string? ImportedProjectFile,
        string UnexpandedProject);
}
