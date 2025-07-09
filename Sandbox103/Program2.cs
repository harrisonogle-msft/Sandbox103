#if false
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Sandbox103.Helpers;

//string binLogPath = @"C:\Users\harrisonogle\source\repos\sandbox\Sandbox104\Sandbox104\Sandbox104v4.binlog";
string binLogPath = @"C:\Users\harrisonogle\temp\2025-07-06\logdrop\fb3c886ec00000J\Build\src\Source\LocationService\RestLSProxy\Logs\Retail\Amd64\msbuild.binlog";
string projectFile = @"F:\dbs\el\aitp2\src\Targets\NetFx.targets";
ParseBinLog(binLogPath);
Console.WriteLine("Done.");

static void ParseBinLog(string binLogPath)
{
    ArgumentException.ThrowIfNullOrEmpty(binLogPath);

    List<string> list;

    using (BuildEventArgsReader reader = BinLogHelper.OpenBuildEventsReader(binLogPath))
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        reader.ArchiveFileEncountered += (ArchiveFileEventArgs args) =>
        {
            files.Add(args.ArchiveData.FullPath);
        };

        while (reader.Read() is not null)
        {
        }

        list = files.Order().ToList();
    }

    Task.Delay(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();

    Console.WriteLine($"Found {list.Count} files.");
    foreach (string file in list)
    {
        Console.WriteLine($"  {file}");
    }
}

static void FindReferences(string binLogPath)
{
    ArgumentException.ThrowIfNullOrEmpty(binLogPath);

    using BuildEventArgsReader reader = BinLogHelper.OpenBuildEventsReader(binLogPath);

    HashSet<string> archiveFiles = new HashSet<string>(["HasReference.targets", "DoesNotHaveReference.targets"], StringComparer.OrdinalIgnoreCase);

    reader.ArchiveFileEncountered += (ArchiveFileEventArgs args) =>
    {
        //Console.WriteLine($"================= ArchiveFileEncountered: {args.ArchiveData.FullPath}");

        ArchiveData archiveData = args.ArchiveData;

        //if (archiveData is ArchiveStream archiveStream)
        //{
        //    // Process the stream more efficiently if you want...
        //}

        if (archiveData.FullPath is string fullPath)
        {
            string fileName = Path.GetFileName(fullPath);

            if (archiveFiles.Contains(fileName))
            {
                ArchiveFile archiveFile = archiveData.ToArchiveFile();
                Console.WriteLine($"\nFound archive file '{fileName}'.");
                Console.WriteLine(archiveFile.Content);
            }
        }
    };

    BuildEventArgs? vargs = reader.Read();

    var types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    while (reader.Read() is BuildEventArgs args)
    {
        if (args.GetType()?.FullName is string fullTypeName)
        {
            if (!types.TryGetValue(fullTypeName, out int count))
            {
                count = 0;
            }
            types[fullTypeName] = count + 1;
        }

        if (args is ProjectImportedEventArgs projectImportedEventArgs)
        {
            ProjectImported(projectImportedEventArgs);
        }
    }

    static void ProjectImported(ProjectImportedEventArgs args)
    {
        string? projectFile = args.ProjectFile;
        string? file = args.File;
        string? importedProjectFile = args.ImportedProjectFile;
        bool importIgnored = args.ImportIgnored;
    }

    Console.WriteLine($"Displaying count by event type.");
    foreach (var entry in types)
    {
        Console.WriteLine($"  {entry.Key,-30} : {entry.Value}");
    }
}

//static void FindReferences(string binLogPath)
//{
//    ArgumentException.ThrowIfNullOrEmpty(binLogPath);

//    var reader = new BinLogReader();

//    var types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

//    foreach (Record record in reader.ReadRecords(binLogPath))
//    {
//        BuildEventArgs? args = record.Args;

//        if (args is ProjectEvaluationStartedEventArgs projectEvaluationStartedEventArgs)
//        {
//            HandleProjectEvaluationStartedEventArgs(projectEvaluationStartedEventArgs);
//        }
//        else if (args is ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
//        {
//            HandleProjectEvaluationFinishedEventArgs(projectEvaluationFinishedEventArgs);
//        }
//        else if (args is ProjectFinishedEventArgs projectFinishedEventArgs)
//        {
//            HandleProjectFinishedEventArgs(projectFinishedEventArgs);
//        }
//        else if (args is ProjectStartedEventArgs projectStartedEventArgs)
//        {
//            HandleProjectStartedEventArgs(projectStartedEventArgs);
//        }
//        else if (args is ProjectImportedEventArgs projectImportedEventArgs)
//        {
//            HandleProjectImportedEventArgs(projectImportedEventArgs);
//        }
//        else if (args is TaskParameterEventArgs taskParameterEventArgs)
//        {
//            HandleTaskParameterEventArgs(taskParameterEventArgs);
//        }
//    }

//    static void HandleProjectEvaluationStartedEventArgs(ProjectEvaluationStartedEventArgs args)
//    {
//    }

//    static void HandleProjectEvaluationFinishedEventArgs(ProjectEvaluationFinishedEventArgs args)
//    {
//        List<KeyValuePair<string, ITaskItem>> list = args.EnumerateItems().OrderBy(static kvp => kvp.Key).ToList();

//        ITaskItem? reference = args.EnumerateItems().Where(IsMyReference).Select(static item => item.Value).SingleOrDefault();

//        string? projectFile = args.ProjectFile;

//        if (reference is not null)
//        {
//        }
//    }

//    static void HandleProjectFinishedEventArgs(ProjectFinishedEventArgs args)
//    {
//    }

//    static void HandleProjectStartedEventArgs(ProjectStartedEventArgs args)
//    {
//        List<KeyValuePair<string, ITaskItem>> list = args.EnumerateItems().OrderBy(static kvp => kvp.Key).ToList();

//        ITaskItem? reference = args.EnumerateItems().Where(IsMyReference).Select(static item => item.Value).SingleOrDefault();

//        string? projectFile = args.ProjectFile;

//        if (reference is not null)
//        {
//        }
//    }

//    static void HandleProjectImportedEventArgs(ProjectImportedEventArgs args)
//    {
//    }

//    static void HandleTaskParameterEventArgs(TaskParameterEventArgs args)
//    {
//        var projectFile = args.ProjectFile;
//        var file = args.File;
//        var senderName = args.SenderName;
//        var items = args.Items;

//        if (args.Kind == TaskParameterMessageKind.AddItem)
//        {
//            if (string.Equals(args.ItemType, "Reference", StringComparison.Ordinal))
//            {
//            }
//        }
//    }
//}

static bool IsMyReference(KeyValuePair<string, ITaskItem> item)
{
    return item.Key is string key &&
        !string.IsNullOrEmpty(key) &&
        string.Equals(key, "Reference", StringComparison.Ordinal) &&
        item.Value is ITaskItem taskItem &&
        taskItem.ItemSpec is string itemSpec &&
        !string.IsNullOrEmpty(itemSpec) &&
        string.Equals(itemSpec, "netstandard") &&
        taskItem.GetMetadata("Author") is string author &&
        string.Equals(author, "Harrison", StringComparison.Ordinal);
}
#endif