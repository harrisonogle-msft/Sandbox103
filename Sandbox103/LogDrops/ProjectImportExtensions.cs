using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace Sandbox103.LogDrops;

/// <summary>
/// Extension methods for <see cref="ProjectImport"/> instances.
/// </summary>
public static class ProjectImportExtensions
{
    /// <summary>
    /// Determines whether or not the given archived binlog file contains a `Reference` item in an `ItemGroup`.
    /// </summary>
    /// <param name="projectImport"></param>
    /// <returns><see langword="true"/> if the archived binlog file contains a Reference item, otherwise <see langword="false"/>.</returns>
    public static bool ContainsReferenceItem(this ProjectImport projectImport)
    {
        ArgumentNullException.ThrowIfNull(projectImport);

        ConcurrentDictionary<string, bool> cache = Caching.ContainsReferenceCache;

        string key = projectImport.RelativePath;
        string shortKey = Path.GetFileName(key);

        if (cache.TryGetValue(key, out bool cachedValue))
        {
            Trace.WriteLine($"**** ContainsReferenceItem CACHE HIT (value: {cachedValue}) for \"{shortKey}\"");
            return cachedValue;
        }

        bool ret = ContainsReferenceItemCore(projectImport);
        Trace.WriteLine($"**** ContainsReferenceItem CACHE MISS (value: {ret}) for \"{shortKey}\"");

        while (!cache.TryAdd(key, ret))
        {
            if (cache.TryGetValue(key, out bool newlyCachedValue))
            {
                if (ret != newlyCachedValue)
                {
                    throw new InvalidOperationException("Unexpected error: cache inconsistency + race condition.");
                }
                break;
            }
        }

        return ret;
    }

    private static bool ContainsReferenceItemCore(ProjectImport projectImport)
    {
        string? fileContent = projectImport.ProjectFileContent;

        if (fileContent is null)
        {
            Trace.TraceWarning($"Unable to get project file content for project '{projectImport.ProjectFile}'.");
            return false;
        }
        else
        {
            Trace.WriteLine($"Found project file content for project '{projectImport.ProjectFile}'.");
        }

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
            using var textReader = new StreamReader(stream, Encoding.UTF8);
            using var reader = new XmlTextReader(textReader);
            reader.Namespaces = false;
            var document = new XPathDocument(reader);
            var navigator = document.CreateNavigator();
            var it = navigator.Select("//ItemGroup/Reference");
            return it.MoveNext();
        }
        catch (Exception ex)
        {
            // Haven't encountered this case, but swallowing it anyways in case something weird happens with the XML.
            Trace.TraceError($"Unexpected error parsing project import file: {ex}");
            return false;
        }
    }
}
