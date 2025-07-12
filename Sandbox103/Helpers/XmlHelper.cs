using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Sandbox103.BuildDrops;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace Sandbox103.Helpers;

public static class XmlHelper
{
    private const string ToolsVersion = nameof(ToolsVersion);
    private const string DefaultTargets = nameof(DefaultTargets);
    private const string xmlns = nameof(xmlns);
    private const string Sdk = nameof(Sdk);

    public static bool TryParsePrivateTargets(
        XmlDocument document,
        string fileName,
        [NotNullWhen(true)] out string? packageId,
        [NotNullWhen(true)] out string? packageVersion)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        const string PrivateTargetsSuffix = ".private.targets";
        const string DependencyToolImportedPrefix = "IMPORTED_Pkg";

        if (!fileName.EndsWith(PrivateTargetsSuffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("File is not a '.private.targets' file.", nameof(fileName));
        }

        string packageName = fileName.Substring(0, fileName.Length - PrivateTargetsSuffix.Length);
        string propertyName = $"{DependencyToolImportedPrefix}{packageName.Replace(".", "_")}";

        if (!TryGetProject(document, out XmlElement? project))
        {
            goto Failed;
        }

        if (GetProperty(document, propertyName) is string propertyValue)
        {
            packageId = packageName;
            packageVersion = propertyValue;
            return true;
        }

    Failed:
        packageId = null;
        packageVersion = null;
        return false;
    }

    public static string? GetProperty(XmlDocument document, string name)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfInvalidTag(name);

        XmlElement project = GetProject(document);

        if (project.SelectSingleNode($"PropertyGroup/{name}") is XmlElement property)
        {
            return property.InnerText;
        }

        return null;
    }

    public static void SetProperty(XmlDocument document, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ThrowIfInvalidTag(name);
        ThrowIfInvalidText(value);

        const string PropertyGroup = nameof(PropertyGroup);

        XmlElement project = GetProject(document);

        if (project.SelectSingleNode($"PropertyGroup/{name}") is XmlElement existingProperty)
        {
            existingProperty.InnerText = value;
            return;
        }

        XmlElement property = document.CreateElement(name);
        property.InnerText = value;

        if (project.SelectSingleNode(PropertyGroup) is XmlElement existingPropertyGroup)
        {
            existingPropertyGroup.AppendChild(property);
        }
        else
        {
            XmlElement propertyGroup = document.CreateElement(PropertyGroup);
            propertyGroup.AppendChild(property);
            project.AppendChild(propertyGroup);
        }
    }

    public static void RemoveCompileItems(XmlDocument document, string projectPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(projectPath);

        var projectFile = new FileInfo(projectPath);
        if (!projectFile.Exists)
        {
            throw new FileNotFoundException("Project file not found.", projectPath);
        }
        if (projectFile.Directory is not DirectoryInfo projectDirectory)
        {
            throw new DirectoryNotFoundException("Project directory not found.");
        }

        XmlElement project = GetProject(document);
        List<XmlElement>? elementsToRemove = null;

        if (project.SelectNodes("//ItemGroup/Compile") is XmlNodeList compileItems)
        {
            var wrapper = new DirectoryInfoWrapper(projectDirectory);

            foreach (XmlElement compile in compileItems)
            {
                string include = compile.GetAttribute("Include");

                if (!string.IsNullOrEmpty(include))
                {
                    var match = new Matcher(StringComparison.OrdinalIgnoreCase);
                    match.AddInclude(include);
                    PatternMatchingResult result = match.Execute(wrapper);
                    if (result.HasMatches)
                    {
                        (elementsToRemove ??= new()).Add(compile);
                    }
                }
            }
        }

        if (elementsToRemove is not null)
        {
            foreach (var node in elementsToRemove)
            {
                if (node.ParentNode is XmlNode parent)
                {
                    parent.RemoveChild(node);
                }
            }
        }
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

    public static bool IsSdkStyleProject(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        XmlElement project = GetProject(document);

        if (!string.IsNullOrEmpty(project.GetAttribute(Sdk)))
        {
            return true;
        }

        if (FirstChild(project, static child => child.Name == Sdk) is not null)
        {
            return true;
        }

        return false;
    }

    //public static void ChangeProjectType(XmlDocument project, string sdk, bool removeLegacyAttributes = true)
    //{
    //    ArgumentNullException.ThrowIfNull(project);
    //    ArgumentException.ThrowIfNullOrEmpty(sdk);

    //    const string ToolsVersion = nameof(ToolsVersion);
    //    const string DefaultTargets = nameof(DefaultTargets);
    //    const string xmlns = nameof(xmlns);
    //    const string Sdk = nameof(Sdk);

    //    XmlElement projectEl = project.SelectSingleNode("//Project") as XmlElement ?? throw new InvalidOperationException("Project element is missing.");
    //    if (removeLegacyAttributes)
    //    {
    //        projectEl.RemoveAttribute(ToolsVersion);
    //        projectEl.RemoveAttribute(DefaultTargets);
    //        projectEl.RemoveAttribute(xmlns);
    //    }
    //    projectEl.RemoveAttribute(Sdk);

    //    for (XmlNode? child = projectEl.FirstChild; child is not null; child = child.NextSibling)
    //    {
    //        if (child.Name == Sdk)
    //        {
    //            projectEl.RemoveChild(child);
    //        }
    //    }

    //    //projectEl.SetAttribute("Sdk", sdk);
    //}

    public static XmlElement GetProject(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (TryGetProject(document, out XmlElement? project))
        {
            return project;
        }
        else
        {
            throw new InvalidOperationException("Project element is missing.");
        }
    }

    public static bool TryGetProject(XmlDocument document, [NotNullWhen(true)] out XmlElement? project)
    {
        ArgumentNullException.ThrowIfNull(document);

        project = document.SelectSingleNode("Project") as XmlElement;

        return project is not null;
    }

    public static void RemoveLegacyProjectAttributes(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        XmlElement project = GetProject(document);

        project.RemoveAttribute(ToolsVersion);
        project.RemoveAttribute(DefaultTargets);
        project.RemoveAttribute(xmlns);
    }

    public static void RemoveSdkElements(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        XmlElement project = GetProject(document);

        project.RemoveAttribute(Sdk);

        // NOTE: Deletion invalidates the `NextSibling` pointers.
        // So we either need to iterate multiple times, or allocate.

        while (FirstChild(project, static child => child.Name == Sdk) is XmlNode child)
        {
            project.RemoveChild(child);
        }
    }

    public static XmlNode? FirstChild(this XmlElement el, Predicate<XmlNode> predicate)
    {
        ArgumentNullException.ThrowIfNull(el);

        for (XmlNode? child = el.FirstChild; child is not null; child = child.NextSibling)
        {
            if (predicate.Invoke(child))
            {
                return child;
            }
        }
        return null;
    }

    public static XmlNode? LastChild(this XmlElement el, Predicate<XmlNode> predicate)
    {
        ArgumentNullException.ThrowIfNull(el);

        if (el.ChildNodes is XmlNodeList childNodes)
        {
            for (int i = childNodes.Count - 1; i >= 0; i--)
            {
                if (childNodes[i] is XmlNode child && predicate.Invoke(child))
                {
                    return child;
                }
            }
        }
        return null;
    }

    public static void AddSdkElement(XmlDocument document, string name, IEnumerable<ValueTuple<string, string>>? attributes)
    {
        AddSdkElement(
            document,
            name,
            attributes?.Select(static attr => new KeyValuePair<string, string>(attr.Item1, attr.Item2)));
    }

    public static void AddSdkElement(XmlDocument document, string name, IEnumerable<KeyValuePair<string, string>>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(name);

        XmlElement project = GetProject(document);

        XmlElement sdk = CreateSdkElement(document, name, attributes);

        if (LastChild(project, static child => child.Name == Sdk) is XmlElement child)
        {
            project.InsertAfter(sdk, child);
        }
        else
        {
            project.PrependChild(sdk);
        }
    }

    public static XmlElement CreateSdkElement(XmlDocument document, string name, IEnumerable<KeyValuePair<string, string>>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(name);

        ThrowIfInvalidTag(name);

        XmlElement sdk = document.CreateElement(Sdk);
        sdk.SetAttribute("Name", name);

        if (attributes is not null)
        {
            foreach ((string key, string value) in attributes)
            {
                if (key == "Name" && value != name)
                {
                    throw new InvalidOperationException($"Duplicate 'Name' attribute is inconsistent with the 'name' parameter. ('{value}' != '{name}')");
                }

                ThrowIfInvalidAttributeName(key);
                ThrowIfInvalidAttributeValue(value);

                sdk.SetAttribute(key, value);
            }
        }

        return sdk;
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

        ThrowIfInvalidAttributeName(packageAttributeName);

        if (versionAttributeName is not null)
        {
            ThrowIfInvalidAttributeName(versionAttributeName);
        }

        if (itemGroupLabel is not null)
        {
            ThrowIfInvalidTag(itemGroupLabel);
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
                    ThrowIfInvalidAttributeName(key);
                    ThrowIfInvalidAttributeValue(value);

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

            ThrowIfInvalidAttributeValue(name);
            ThrowIfInvalidAttributeValue(version);

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

    public static IReadOnlyDictionary<string, string> GetCorextPackages(XmlDocument corextConfig)
    {
        ArgumentNullException.ThrowIfNull(corextConfig);

        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (corextConfig.SelectNodes("//packages/package[@dependencyToolState='TopLevel']") is XmlNodeList packages)
        {
            foreach (XmlElement package in packages)
            {
                string id = package.GetAttribute("id");

                if (string.IsNullOrEmpty(id))
                {
                    throw new InvalidOperationException($"Corext.config package element is missing 'id' attribute: {package.OuterXml}");
                }

                string version = package.GetAttribute("version");

                if (string.IsNullOrEmpty(version))
                {
                    throw new InvalidOperationException($"Corext.config package element is missing 'version' attribute: {package.OuterXml}");
                }

                results[id] = version;
            }
        }

        return results;
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

    private static void ThrowIfInvalidAttributeName(string? attributeName)
    {
        if (!SecurityElement.IsValidAttributeName(attributeName))
        {
            throw new InvalidOperationException($"Invalid attribute name: '{attributeName}'");
        }
    }

    private static void ThrowIfInvalidAttributeValue(string? attributeValue)
    {
        if (!SecurityElement.IsValidAttributeValue(attributeValue))
        {
            throw new InvalidOperationException($"Invalid attribute value: '{attributeValue}'");
        }
    }

    private static void ThrowIfInvalidText(string? text)
    {
        if (!SecurityElement.IsValidText(text))
        {
            throw new InvalidOperationException($"Invalid text: '{text}'");
        }
    }

    private static void ThrowIfInvalidTag(string? tag)
    {
        if (!SecurityElement.IsValidTag(tag))
        {
            throw new InvalidOperationException($"Invalid tag: '{tag}'");
        }
    }
}
