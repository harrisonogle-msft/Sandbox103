using Sandbox103.BuildDrops;
using System.Globalization;
using System.Text;
using Xunit.Abstractions;

namespace Sandbox103.Test;

public class BuildDropTests
{
    private readonly ITestOutputHelper _output;

    public BuildDropTests(ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;
    }

    [Fact]
    public void EnumerateProjects_Test()
    {
        var buildDrop = new BuildDrop(Constants.BuildDrop.FullName);

        foreach (BuildDropProject project in buildDrop.EnumerateProjects())
        {
            var helper = new AssemblyEnumerator();
            LocalAssembly info = LocalAssembly.FromPath(project.BinaryPath);

            if (info.AssemblyName.Name?.Contains("Microsoft.Management.Services.LocationService.RestLocationServiceProxy") is not true)
            {
                // Testing purposes.
                continue;
            }

            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"{info.FileVersion,-20} {info.AssemblyName.FullName}");

            foreach (LocalAssembly dep in helper.EnumerateDependencies(project.BinaryPath, null))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"   {dep.FileVersion,-20} {dep.AssemblyName.FullName}");
            }

            _output.WriteLine(sb.ToString());
        }
    }
}