using System.Globalization;
using System.Reflection;
using System.Text;

namespace Sandbox103.RepoPackagesRaw;

public sealed record class PackageDependency(
    DateTimeOffset Timestamp,
    string Repo,
    string BuildVersion,
    string PackageName,
    string PackageVersion,
    string Branch,
    string Relationship,
    string DataSource);

public static class PackageDependencyHelper
{
    public static async ValueTask TestAsync()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        string binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new DirectoryNotFoundException("Unable to locate binaries directory.");
        string packageDependenciesCsvPath = Directory.EnumerateFiles(binDir, "PackageDependencies.csv", SearchOption.TopDirectoryOnly).Single();
        List<PackageDependency> packageDependencies = await ParsePackageDependencyCsvAsync(packageDependenciesCsvPath, cancellation.Token);
        string outPath = Path.Join(binDir, "PackageDependencies.kql");
        if (File.Exists(outPath))
        {
            Console.WriteLine($"Deleting file: {outPath}");
            File.Delete(outPath);
        }
        Console.WriteLine($"Writing results to: {outPath}");
        await WriteKustoDatatableLiteralAsync(outPath, packageDependencies, cancellation.Token);
    }

    public static ValueTask WriteKustoDatatableLiteralAsync(string path, IEnumerable<PackageDependency> packageDependencies, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(packageDependencies);
        cancellationToken.ThrowIfCancellationRequested();

        using (var fs = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var dest = new StreamWriter(fs))
        {
            dest.WriteLine($"datatable (Timestamp:datetime, Repo:string, BuildVersion:string, PackageName:string, PackageVersion:string, Branch:string, Relationship:string, DataSource:string) [");

            static void Append(StringBuilder sb, string value)
            {
                if (value.Contains('"', StringComparison.Ordinal))
                {
                    if (value.Contains('\'', StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Invalid value: {value}");
                    }
                    else
                    {
                        sb.Append(CultureInfo.InvariantCulture, $"'{value}',");
                    }
                }
                else
                {
                    sb.Append(CultureInfo.InvariantCulture, $"\"{value}\",");
                }
            }

            var sb = new StringBuilder();

            foreach (PackageDependency p in packageDependencies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Append(sb, p.Timestamp.ToString("O"));
                Append(sb, p.Repo);
                Append(sb, p.BuildVersion);
                Append(sb, p.PackageName);
                Append(sb, p.PackageVersion);
                Append(sb, p.Branch);
                Append(sb, p.Relationship);
                Append(sb, p.DataSource);
                dest.WriteLine(sb.ToString());
                sb.Clear();
            }

            dest.WriteLine("]");
        }

        return ValueTask.CompletedTask;
    }

    public static ValueTask<List<PackageDependency>> ParsePackageDependencyCsvAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        cancellationToken.ThrowIfCancellationRequested();

        const string SchemaV1 = @"env_time,Repo,BuildVersion,PackageName,PackageVersion,Branch,Relationship,DataSource";

        var results = new List<PackageDependency>();

        using (var fs = File.OpenRead(path))
        using (var sr = new StreamReader(fs))
        {
            if (sr.ReadLine() is not string schema)
            {
                return ValueTask.FromResult(results);
            }

            if (schema == SchemaV1)
            {
                ReadSchemaV1(sr, results, cancellationToken);
            }
            else
            {
                throw new NotSupportedException($"Unsupported CSV schema: {schema}");
            }
        }

        return ValueTask.FromResult(results);

        static void ReadSchemaV1(StreamReader sr, List<PackageDependency> results, CancellationToken cancellationToken)
        {
            int lineNumber = 1;

            while (sr.ReadLine() is string line)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lineNumber++;

                if (line.Split(',') is not [var timestampStr, var repo, var buildVersion, var packageName, var packageVersion, var branch, var relationship, var dataSource] ||
                    !DateTimeOffset.TryParse(timestampStr, out DateTimeOffset timestamp))
                {
                    throw new InvalidOperationException($"Schema violated by line {lineNumber}: {line}");
                }

                results.Add(new PackageDependency(timestamp, repo, buildVersion, packageName, packageVersion, branch, relationship, dataSource));
            }
        }
    }
}
