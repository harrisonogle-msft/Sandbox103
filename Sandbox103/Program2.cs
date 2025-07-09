using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using Sandbox103.Extensions;
using System.Collections;
using System.IO.Compression;

using BuildEventArgsReader = Microsoft.Build.Logging.BuildEventArgsReader;
using BinaryLogger = Microsoft.Build.Logging.BinaryLogger;
//using ArchiveFileEventArgs = ArchiveFileEventArgs;
using System.Diagnostics;
using ArchiveFileEventArgs = Microsoft.Build.Logging.ArchiveFileEventArgs;
using ArchiveFile = Microsoft.Build.Logging.ArchiveFile;

FindReferences2(@"C:\Users\harrisonogle\source\repos\sandbox\Sandbox104\Sandbox104\Sandbox104v4.binlog");
Console.WriteLine("Done.");

static void FindReferences2(string binLogPath)
{
    ArgumentException.ThrowIfNullOrEmpty(binLogPath);

    static BuildEventArgsReader OpenReader(BinaryReader binaryReader)
    {
        const int ForwardCompatibilityMinimalVersion = 18;

        int fileFormatVersion = binaryReader.ReadInt32();
        bool hasEventOffsets = fileFormatVersion >= ForwardCompatibilityMinimalVersion;
        int minimumReaderVersion = hasEventOffsets
            ? binaryReader.ReadInt32()
            : fileFormatVersion;

        //EnsureFileFormatVersionKnown(fileFormatVersion, minimumReaderVersion);

        var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);

        reader.SkipUnknownEventParts = hasEventOffsets;
        reader.SkipUnknownEvents = hasEventOffsets;

        // Ensure some handler is subscribed, even if we are not interested in the events
        reader.RecoverableReadError += (_ => { });

        return reader;
    }

    using var stream = new FileStream(binLogPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
    using var bufferedStream = new BufferedStream(gzipStream, 32768);
    using var binaryReader = new BinaryReader(bufferedStream);
    using BuildEventArgsReader reader = OpenReader(binaryReader);

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
    }

    Console.WriteLine($"Displaying count by event type.");
    foreach (var entry in types)
    {
        Console.WriteLine($"  {entry.Key,-30} : {entry.Value}");
    }
}

static void FindReferences(string binLogPath)
{
    ArgumentException.ThrowIfNullOrEmpty(binLogPath);

    var reader = new BinLogReader();

    var types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    foreach (Record record in reader.ReadRecords(binLogPath))
    {
        BuildEventArgs? args = record.Args;

        if (args is ProjectEvaluationStartedEventArgs projectEvaluationStartedEventArgs)
        {
            HandleProjectEvaluationStartedEventArgs(projectEvaluationStartedEventArgs);
        }
        else if (args is ProjectEvaluationFinishedEventArgs projectEvaluationFinishedEventArgs)
        {
            HandleProjectEvaluationFinishedEventArgs(projectEvaluationFinishedEventArgs);
        }
        else if (args is ProjectFinishedEventArgs projectFinishedEventArgs)
        {
            HandleProjectFinishedEventArgs(projectFinishedEventArgs);
        }
        else if (args is ProjectStartedEventArgs projectStartedEventArgs)
        {
            HandleProjectStartedEventArgs(projectStartedEventArgs);
        }
        else if (args is ProjectImportedEventArgs projectImportedEventArgs)
        {
            HandleProjectImportedEventArgs(projectImportedEventArgs);
        }
        else if (args is TaskParameterEventArgs taskParameterEventArgs)
        {
            HandleTaskParameterEventArgs(taskParameterEventArgs);
        }
    }

    static void HandleProjectEvaluationStartedEventArgs(ProjectEvaluationStartedEventArgs args)
    {
    }

    static void HandleProjectEvaluationFinishedEventArgs(ProjectEvaluationFinishedEventArgs args)
    {
        List<KeyValuePair<string, ITaskItem>> list = args.EnumerateItems().OrderBy(static kvp => kvp.Key).ToList();

        ITaskItem? reference = args.EnumerateItems().Where(IsMyReference).Select(static item => item.Value).SingleOrDefault();

        string? projectFile = args.ProjectFile;

        if (reference is not null)
        {
        }
    }

    static void HandleProjectFinishedEventArgs(ProjectFinishedEventArgs args)
    {
    }

    static void HandleProjectStartedEventArgs(ProjectStartedEventArgs args)
    {
        List<KeyValuePair<string, ITaskItem>> list = args.EnumerateItems().OrderBy(static kvp => kvp.Key).ToList();

        ITaskItem? reference = args.EnumerateItems().Where(IsMyReference).Select(static item => item.Value).SingleOrDefault();

        string? projectFile = args.ProjectFile;

        if (reference is not null)
        {
        }
    }

    static void HandleProjectImportedEventArgs(ProjectImportedEventArgs args)
    {
    }

    static void HandleTaskParameterEventArgs(TaskParameterEventArgs args)
    {
        var projectFile = args.ProjectFile;
        var file = args.File;
        var senderName = args.SenderName;
        var items = args.Items;

        if (args.Kind == TaskParameterMessageKind.AddItem)
        {
            if (string.Equals(args.ItemType, "Reference", StringComparison.Ordinal))
            {
            }
        }
    }
}

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
