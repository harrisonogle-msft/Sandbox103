using Sandbox103.BuildDrops;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace Sandbox103.Helpers;

public static class XmlHelper
{
    private static readonly Regex s_itemGroupLabelRegex = new Regex(@"[\s\w_-]+", RegexOptions.Compiled);

    public static int RemoveProjectImports(XmlReader reader, XmlWriter writer, Predicate<string> predicate)
    {
        var doc = new XmlDocument();
        doc.Load(reader);
        int result = RemoveProjectImports(doc, predicate);
        doc.Save(writer);
        return result;
    }

    public static int RemoveProjectImports(XmlDocument doc, Predicate<string> predicate)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(predicate);

        XPathNodeIterator? it = doc.CreateNavigator()?.Select("//Import[@Project]");

        // https://learn.microsoft.com/en-us/dotnet/api/system.xml.xpath.xpathnodeiterator#remarks
        // When using the XPathNodeIterator, if you edit the current node or any of its ancestors,
        // your current position is lost. If you want to edit a number of nodes that you have selected,
        // create a XPathNavigator array, copy all of the nodes from the XPathNodeIterator into the array,
        // then iterate through the array and modify the nodes.
        var results = new List<XPathNavigator>();

        while (it?.MoveNext() is true && it?.Current is XPathNavigator navigator)
        {
            Debug.Assert(navigator.NodeType == XPathNodeType.Element && navigator.Name == "Import");
            Debug.Assert(navigator.CanEdit);

            bool shouldRemove = false;

            if (navigator.MoveToFirstAttribute())
            {
                do
                {
                    if (navigator.NodeType == XPathNodeType.Attribute &&
                        navigator.Name == "Project")
                    {
                        if (predicate.Invoke(navigator.Value))
                        {
                            shouldRemove = true;
                            break;
                        }
                    }
                }
                while (navigator.MoveToNextAttribute());

                navigator.MoveToParent();
            }

            Debug.Assert(navigator.NodeType == XPathNodeType.Element && navigator.Name == "Import");

            if (shouldRemove)
            {
                results.Add(navigator.Clone());
            }
        }

        foreach (XPathNavigator result in results)
        {
            Trace.WriteLine($"Deleting XML element: {result.OuterXml}");
            result.DeleteSelf();
        }

        return results.Count;
    }

    public static bool IsSdkStyleProject(XmlDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);

        XmlElement projectEl = project.SelectSingleNode("//Project") as XmlElement ?? throw new InvalidOperationException("Project element is missing.");

        return !string.IsNullOrEmpty(projectEl.GetAttribute("Sdk"));
    }

    public static void ChangeProjectType(XmlDocument project, string sdk, bool removeLegacyAttributes = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrEmpty(sdk);

        const string ToolsVersion = nameof(ToolsVersion);
        const string DefaultTargets = nameof(DefaultTargets);
        const string xmlns = nameof(xmlns);

        XmlElement projectEl = project.SelectSingleNode("//Project") as XmlElement ?? throw new InvalidOperationException("Project element is missing.");
        if (removeLegacyAttributes)
        {
            projectEl.RemoveAttribute(ToolsVersion);
            projectEl.RemoveAttribute(DefaultTargets);
            projectEl.RemoveAttribute(xmlns);
        }
        projectEl.SetAttribute("Sdk", sdk);
    }

    public static int AddPackageReferencesToProject(
        XmlDocument doc,
        IEnumerable<BinaryReference> packageReferences,
        string packageAttributeName,
        string? versionAttributeName = null,
        string? itemGroupLabel = null,
        IEnumerable<KeyValuePair<string, string>>? itemGroupAttributes = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(packageReferences);
        ArgumentException.ThrowIfNullOrEmpty(packageAttributeName);

        if (itemGroupLabel is not null)
        {
            if (!s_itemGroupLabelRegex.IsMatch(itemGroupLabel))
            {
                throw new ArgumentException($"Invalid ItemGroup label: {itemGroupLabel}", nameof(itemGroupLabel));
            }
        }

        using var it = packageReferences.GetEnumerator();

        if (!it.MoveNext())
        {
            return 0;
        }

        XmlNode project = doc.SelectSingleNode("//Project") ?? throw new InvalidOperationException("Project node is missing.");

        XmlNode? itemGroup = itemGroupLabel is not null ?
            doc.SelectSingleNode($"//ItemGroup[@Label='{itemGroupLabel}' and PackageReference]") :
            doc.SelectSingleNode("//ItemGroup[PackageReference]");

        bool insertItemGroup = itemGroup is null;

        if (insertItemGroup)
        {
            XmlElement itemGroupEl = doc.CreateElement("ItemGroup");
            if (itemGroupLabel is not null)
            {
                itemGroupEl.SetAttribute("Label", itemGroupLabel);
            }
            if (itemGroupAttributes is not null)
            {
                foreach ((string key, string value) in itemGroupAttributes)
                {
                    itemGroupEl.SetAttribute(key, value);
                }
            }
            itemGroup = itemGroupEl;
        }

        Debug.Assert(itemGroup is not null);

        IReadOnlyDictionary<string, string?> existingPackageReferences = GetPackageReferences(doc);

        int count = 0;

        do
        {
            (string name, string version) = it.Current;

            if (!existingPackageReferences.ContainsKey(name))
            {
                XmlElement packageReference = doc.CreateElement("PackageReference");
                packageReference.SetAttribute(packageAttributeName, name);
                if (!string.IsNullOrEmpty(versionAttributeName))
                {
                    packageReference.SetAttribute(versionAttributeName, version);
                }
                itemGroup.AppendChild(packageReference);
                count++;
            }
        }
        while (it.MoveNext());

        if (insertItemGroup)
        {
            XmlNode? insertAfter =
                project.SelectSingleNode("PropertyGroup[AssemblyName]") ??
                project.SelectSingleNode("PropertyGroup[TargetFramework]") ??
                project.SelectSingleNode("PropertyGroup[TargetFrameworks]") ??
                project.SelectSingleNode("PropertyGroup");

            if (insertAfter is not null)
            {
                project.InsertAfter(itemGroup, insertAfter);
            }
            else
            {
                project.AppendChild(itemGroup);
            }
        }

        return count;
    }

    public static IReadOnlyDictionary<string, string?> GetPackageReferences(XmlDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        if (doc.SelectSingleNode("//ItemGroup[PackageReference]") is null ||
            doc.SelectNodes("//PackageReference") is not XmlNodeList packageReferences)
        {
            return FrozenDictionary<string, string?>.Empty;
        }

        var results = new Dictionary<string, string?>();

        foreach (XmlElement packageReference in packageReferences)
        {
            string update = packageReference.GetAttribute("Update");
            string include = packageReference.GetAttribute("Include");
            string packageName;

            if (!string.IsNullOrEmpty(update))
            {
                if (!string.IsNullOrEmpty(include) && update != include)
                {
                    throw new InvalidOperationException($"Invalid PackageReference item: {packageReference.OuterXml}");
                }
                else
                {
                    packageName = update;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(include))
                {
                    throw new InvalidOperationException($"PackageReference item is missing package ID: {packageReference.OuterXml}");
                }
                else
                {
                    packageName = include;
                }
            }

            string version = packageReference.GetAttribute("Version");
            string versionOverride = packageReference.GetAttribute("VersionOverride");
            string? packageVersion;

            if (!string.IsNullOrEmpty(version))
            {
                if (!string.IsNullOrEmpty(versionOverride) && versionOverride != version)
                {
                    throw new InvalidOperationException($"Invalid PackageReference version: {packageReference.OuterXml}");
                }
                else
                {
                    packageVersion = version;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(versionOverride))
                {
                    packageVersion = null;
                }
                else
                {
                    packageVersion = versionOverride;
                }
            }

            results[packageName] = packageVersion;
        }

        return results;
    }
}
