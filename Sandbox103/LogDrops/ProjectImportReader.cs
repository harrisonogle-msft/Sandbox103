using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Sandbox103.Helpers;

namespace Sandbox103.LogDrops;

public class ProjectImportReader
{
    public ProjectImportReader()
    {
    }

    public ProjectImportGraph Build(string path)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(path);

        Console.WriteLine($"Building project import graph for binlog located at: '{path}'");

        using BuildEventArgsReader reader = BinLogHelper.OpenBuildEventsReader(path);

        var graph = new ProjectImportGraph();

        while (reader.Read() is BuildEventArgs buildEventArgs)
        {
            if (buildEventArgs is ProjectImportedEventArgs args)
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
