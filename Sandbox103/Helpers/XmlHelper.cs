using System.Diagnostics;
using System.Xml;
using System.Xml.XPath;

namespace Sandbox103.Helpers;

public static class XmlHelper
{
    public static int RemoveProjectImports(XmlReader reader, XmlWriter writer, Predicate<string> predicate)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(predicate);

        var doc = new XmlDocument();
        doc.Load(reader);

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

        doc.Save(writer);

        return results.Count;
    }
}
