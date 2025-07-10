using Sandbox103.Helpers;
using System.Text;
using System.Xml;
using Xunit.Abstractions;

namespace Sandbox103.Test;

public class XmlTests
{
    private readonly ITestOutputHelper _output;

    public XmlTests(ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;
    }

    [Fact]
    public void XmlHelper_RemoveProjectImports_Test()
    {
        string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="14.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Import Project="$(EnvironmentConfig)" />
              <Import Project="$(Targets)\Netfx.targets" />
              <Import Project="$(GeneratedTargets)\RemoveMe.targets" />
              <Import Project="$(Targets)\CertRuntimeV2ServiceCertInstances.targets" />
              <PropertyGroup>
                <AssemblyName>XmlHelper_RemoveProjectImports_Test</AssemblyName>
              </PropertyGroup>
            </Project>
            """;

        XmlHelper_RemoveProjectImports_Test_Core(xml, [@"$(Targets)\Netfx.targets", @"$(GeneratedTargets)\RemoveMe.targets"], 2);
    }

    private void XmlHelper_RemoveProjectImports_Test_Core(string xml, IEnumerable<string> importsToRemove, int expectedRemovals)
    {
        var set = new HashSet<string>(importsToRemove, StringComparer.OrdinalIgnoreCase);

        using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var sourceTextReader = new StreamReader(sourceStream, Encoding.UTF8);
        using var reader = new XmlTextReader(sourceTextReader)
        {
            Namespaces = false,
        };

        using var destStream = new MemoryStream();
        using var destTextWriter = new StreamWriter(destStream, Encoding.UTF8);
        using var writer = new XmlTextWriter(destTextWriter)
        {
            Namespaces = false,
            Formatting = Formatting.Indented,
            Indentation = 2,
        };

        _output.WriteLine($"XML before:\n{xml}");

        int numRemoved = XmlHelper.RemoveProjectImports(reader, writer, set.Contains);

        destStream.SetLength(destStream.Position);
        destStream.Seek(0, SeekOrigin.Begin);
        string xmlAfter = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(destStream.GetBuffer(), 0, (int)destStream.Length));
        _output.WriteLine($"\nXML after:\n{xmlAfter}");

        Assert.Equal(expectedRemovals, numRemoved);
    }
}
