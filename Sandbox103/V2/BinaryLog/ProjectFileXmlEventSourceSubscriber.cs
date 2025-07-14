using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Xml;

namespace Sandbox103.V2;

internal sealed class ProjectFileXmlEventSourceSubscriber : IEventSourceSubscriber
{
    private readonly ILogger<ProjectFileXmlEventSourceSubscriber> _logger;
    private readonly IArchiveFileIndex _archiveFileIndex;

    public ProjectFileXmlEventSourceSubscriber(
        ILogger<ProjectFileXmlEventSourceSubscriber> logger,
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

        Debug.Assert(eventSource is IBuildEventArgsReaderNotifications);

        if (eventSource is not IBuildEventArgsReaderNotifications notifications)
        {
            return;
        }

        notifications.ArchiveFileEncountered += (e) =>
        {
            if (e.ArchiveData is not ArchiveData archiveData ||
                archiveData.FullPath is not string path)
            {
                return;
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".xml":
                case ".targets":
                case ".props":
                case ".csproj":
                case ".proj":
                    break;
                default:
                    if (ext.EndsWith("xml", StringComparison.OrdinalIgnoreCase) ||
                        ext.EndsWith("targets", StringComparison.OrdinalIgnoreCase) ||
                        ext.EndsWith("props", StringComparison.OrdinalIgnoreCase) ||
                        ext.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    return; // not XML
            }

            IArchiveFile? archiveFile;
            {
                IArchiveFile? archiveFileToAdd = null;
                while (!_archiveFileIndex.TryGetValue(path, out archiveFile))
                {
                    archiveFileToAdd ??= new EmbeddedArchiveFile(path, Encoding.UTF8.GetBytes(archiveData.ToArchiveFile().Content));
                    _archiveFileIndex.TryAdd(path, archiveFileToAdd);
                }
            }

            string projectFileName = Path.GetFileName(archiveFile.Path);

            bool checkReferenceItems = archiveFile.Features.Get<IContainsReferenceItemFeature>() is null;
            bool checkCorextPackage =
                projectFileName.EndsWith(".private.targets", StringComparison.OrdinalIgnoreCase) &&
                !archiveFile.TryGetCorextPackage(out _);

            if (checkReferenceItems || checkCorextPackage)
            {
                using (Stream stream = archiveFile.GetStream())
                using (TextReader textReader = new StreamReader(stream, Encoding.UTF8))
                using (var reader = new XmlTextReader(textReader) { Namespaces = false })
                {
                    var document = new XmlDocument();
                    try
                    {
                        document.Load(reader);
                    }
                    catch
                    {
                        return; // not XML
                    }

                    if (checkReferenceItems)
                    {
                        bool pathContainsSrcSegment = archiveFile.Path
                            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToHashSet<string>(StringComparer.OrdinalIgnoreCase)
                            .Contains("src");

                        bool containsReferenceItem =
                            pathContainsSrcSegment && // hacky way to ignore .NET targets and other targets not owned by us
                            document.SelectNodes("//Reference") is XmlNodeList list && list.Count > 0;

                        if (containsReferenceItem)
                        {
                            _logger.LogInformation($"Archive file '{Path.GetFileName(archiveFile.Path)}' contains a `Reference` item!");
                        }
                        archiveFile.Features.Set<IContainsReferenceItemFeature>(new ContainsReferenceItemFeature(containsReferenceItem));
                    }

                    if (checkCorextPackage)
                    {
                        if (XmlHelper.TryParsePrivateTargets(document, projectFileName, out string? packageId, out string? packageVersion))
                        {
                            _logger.LogInformation($"Parsed CoreXT package details from '{projectFileName}': {packageId}/{packageVersion}");
                            archiveFile.Features.Set<ICorextPackageFeature>(new CorextPackageFeature(packageId, packageVersion));
                        }
                    }
                }
            }
        };
    }
}
