using Sandbox103.BuildDrops;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Xml;
using System.Xml.XPath;

namespace Sandbox103.Helpers;

public static class XmlHelper
{
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

    public static void AddPackageReferencesToProject(
        XmlDocument doc,
        IEnumerable<BinaryReference> packageReferences,
        string packageAttributeName,
        string? versionAttributeName = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(packageReferences);
        ArgumentException.ThrowIfNullOrEmpty(packageAttributeName);

        using var it = packageReferences.GetEnumerator();

        if (!it.MoveNext())
        {
            return;
        }

        XmlNode project = doc.SelectSingleNode("//Project") ?? throw new InvalidOperationException("Project node is missing.");

        XmlNode? itemGroup = doc.SelectSingleNode("//ItemGroup[PackageReference]");
        bool insertItemGroup = itemGroup is null;
        itemGroup ??= doc.CreateElement("ItemGroup");

        IReadOnlyDictionary<string, string?> existingPackageReferences = GetPackageReferences(doc);

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
            }
        }
        while (it.MoveNext());

        if (insertItemGroup)
        {
            XmlNode? insertAfter = null;
            for (XmlNode? child = project.FirstChild; child is not null; child = child.NextSibling)
            {
                if (child.Name == "PropertyGroup")
                {
                    insertAfter = child;
                    break;
                }
            }

            if (insertAfter is not null)
            {
                project.InsertAfter(itemGroup, insertAfter);
            }
            else
            {
                project.AppendChild(itemGroup);
            }
        }
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
