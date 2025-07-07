using Sandbox103;
using System.Globalization;
using System.Text;

const string DropPath = @"C:\Users\harrisonogle\temp\2025-07-06\drop";

var buildDrop = new BuildDrop(DropPath);

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

    Console.WriteLine(sb);
}

Console.WriteLine("Done.");
