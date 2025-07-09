using Sandbox103.LogDrops;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Sandbox103.Test;

public class ProjectFileGraphTests
{
    private readonly ITestOutputHelper _output;

    public ProjectFileGraphTests(ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;
    }

    [Fact]
    public void ProjectImportGraphBuilder_Test()
    {
        var graph = new ProjectImportGraphBuilder(Constants.BinLog.FullName);
        graph.Build();
        ICollection<string> importers = graph.GetKeys(false);
        ICollection<string> importees = graph.GetKeys(true);

        //_output.WriteLine($"\nIMPORTERS");
        //foreach (string importer in importers)
        //    _output.WriteLine(importer);

        //_output.WriteLine($"\nIMPORTEES");
        //foreach (string importee in importees)
        //    _output.WriteLine(importee);

        string csproj = importers.First(x => string.Equals(Path.GetFileName(x), "LocationService.ClientLibrary.csproj", StringComparison.OrdinalIgnoreCase));
        _output.WriteLine($"Found csproj: {csproj}");


        _output.WriteLine($"Getting imports.");
        if (graph.TryGetImports(csproj, out IEnumerator<string>? imports))
        {
            using (imports)
            {
                while (imports.MoveNext())
                {
                    _output.WriteLine($"  {imports.Current}");
                }
            }
        }
        else
        {
            _output.WriteLine($"No imports detected!");
        }


        _output.WriteLine($"\nEnumerating transitive imports for file '{csproj}'.");
        foreach (string import in graph.EnumerateTransitiveImports(csproj))
        {
            _output.WriteLine($"  {Path.GetFileName(import)}");
        }

        const string SecuritySubsystemPrivateTargetsFilePattern = "Targets\\Generated\\FabricServiceInfraSecuritySubSystem.private.targets";
        string singlePrivateTargetsFile = importees.First(x => x.EndsWith(SecuritySubsystemPrivateTargetsFilePattern, StringComparison.OrdinalIgnoreCase));
        _output.WriteLine($"\nFound private targets: {singlePrivateTargetsFile}");

        _output.WriteLine($"Getting importees for private targets '{Path.GetFileName(singlePrivateTargetsFile)}'.");
        if (graph.TryGetImporters(singlePrivateTargetsFile, out IEnumerator<string>? importedBy))
        {
            using (importedBy)
            {
                while (importedBy.MoveNext())
                {
                    _output.WriteLine($"  {importedBy.Current}");
                }
            }
        }

        _output.WriteLine($"Getting imports for private targets '{Path.GetFileName(singlePrivateTargetsFile)}'.");
        if (graph.TryGetImports(singlePrivateTargetsFile, out IEnumerator<string>? importedBy2))
        {
            using (importedBy2)
            {
                while (importedBy2.MoveNext())
                {
                    _output.WriteLine($"  {importedBy2.Current}");
                }
            }
        }

        _output.WriteLine($"\nGetting transitive importees for private targets '{Path.GetFileName(singlePrivateTargetsFile)}'.");
        foreach (string transitiveImportee in graph.EnumerateTransitiveImports(singlePrivateTargetsFile, reverse: true))
        {
            _output.WriteLine($"  {transitiveImportee}");
        }

        _output.WriteLine($"\nGetting transitive imports for private targets '{Path.GetFileName(singlePrivateTargetsFile)}'.");
        foreach (string transitiveImport in graph.EnumerateTransitiveImports(singlePrivateTargetsFile, reverse: false))
        {
            _output.WriteLine($"  {transitiveImport}");
        }

        _output.WriteLine($"\nGetting private targets.");
        List<string> privateTargetsFiles = importees.Where(IsPrivateTargets).ToList();
        _output.WriteLine($"Found {privateTargetsFiles.Count} (imported) private targets files.");
        foreach (string privateTargetsFile in privateTargetsFiles)
        {
            _output.WriteLine($"  {privateTargetsFile}");
        }

        _output.WriteLine($"\nGetting files that import private targets.");
        var privateTargetsImporters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string privateTargetsFile in privateTargetsFiles)
        {
            if (graph.TryGetImporters(privateTargetsFile, out IEnumerator<string>? it))
            {
                using (it)
                {
                    while (it.MoveNext())
                    {
                        privateTargetsImporters.Add(it.Current);
                    }
                }
            }
        }
        _output.WriteLine($"Found {privateTargetsImporters.Count} files that import private targets.");
        foreach (string privateTargetsImporter in privateTargetsImporters)
        {
            _output.WriteLine($"  {privateTargetsImporter}");
        }
    }

    static bool IsPrivateTargets(string projectFile) => projectFile?.EndsWith(".private.targets", StringComparison.OrdinalIgnoreCase) is true;
}
