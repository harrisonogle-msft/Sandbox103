using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;

namespace Sandbox103.LogDrops;

public class ProjectImportReader
{
    private BinLogReader? _reader;

    public ProjectImportReader()
    {
    }

    private BinLogReader Reader => _reader ?? Interlocked.CompareExchange(ref _reader, new(), null) ?? _reader!;

    public ProjectImportGraph Build(string path)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(path);

        Console.WriteLine($"Building project import graph for binlog located at: '{path}'");

        var graph = new ProjectImportGraph();

        foreach (Record record in Reader.ReadRecords(path))
        {
            if (record.Args is ProjectImportedEventArgs args)
            {
                string? importedProjectFile = args.ImportedProjectFile ?? args.UnexpandedProject;

                if (args.ProjectFile is string projectFile &&
                    importedProjectFile is not null &&
                    //!args.ImportIgnored &&
                    !string.IsNullOrEmpty(projectFile) &&
                    !string.IsNullOrEmpty(importedProjectFile))
                {
                    if (args.ImportIgnored)
                    {
                        throw new Exception($"Import ignored. project file: '{projectFile}', imported project file: '{importedProjectFile}'");
                    }
                    graph.AddImport(projectFile, importedProjectFile);
                }
            }
        }

        return graph;
    }
}
