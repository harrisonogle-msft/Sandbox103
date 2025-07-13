using System.Text;
using System.Xml;

namespace Sandbox103.Helpers;

// Need `Namespaces = false` property of `XmlTextWriter`, but also `OmitXmlDeclaration` behavior of `XmlWriterSettings`.
// But `XmlTextWriter` does not use `XmlWriterSettings`, and `XmlWriter.Create` does not allow for creation of a writer
// with `Namespaces = false` behavior. The solution implemented here is to override `WriteStartDocument` to do nothing.
public sealed class ProjectFileXmlWriter : XmlTextWriter
{
    public ProjectFileXmlWriter(Stream stream) : base(stream, new UTF8Encoding(false))
    {
        Initialize();
    }

    private void Initialize()
    {
        Namespaces = false;
        Formatting = Formatting.Indented;
        Indentation = 2;
    }

    public override void WriteStartDocument()
    {
    }

    public override void WriteStartDocument(bool standalone)
    {
    }
}

