using System.Reflection;

namespace Sandbox103.V1;

public static class ConstantsV1
{
    private static DirectoryInfo? s_binDirectory;
    private static DirectoryInfo? s_assetsDirectory;
    private static DirectoryInfo? s_buildDropPath;
    private static DirectoryInfo? s_logDropPath;
    private static FileInfo? s_binLog;
    private static DirectoryInfo? s_repoDirectory;

    public const string EnableCorextProjectSdk = nameof(EnableCorextProjectSdk);

    public static DirectoryInfo BinDirectory => s_binDirectory ??= GetBinDirectory();

    public static DirectoryInfo AssetsDirectory => s_assetsDirectory ??= GetAssetsDirectory();

    public static DirectoryInfo BuildDrop => s_buildDropPath ??= GetBuildDropPath();

    public static DirectoryInfo LogDrop => s_logDropPath ??= GetLogDropPath();

    public static FileInfo BinLog => s_binLog ??= GetBinLog();

    public static DirectoryInfo Repo => s_repoDirectory ??= GetRepoDirectory();


    private static DirectoryInfo GetBinDirectory()
    {
        string binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
            throw new DirectoryNotFoundException("Bin directory not found.");

        var info = new DirectoryInfo(binDir);

        if (!info.Exists)
        {
            throw new DirectoryNotFoundException("Bin directory info not found.");
        }

        return info;
    }

    private static DirectoryInfo GetAssetsDirectory()
    {
        DirectoryInfo binDir = BinDirectory;
        string assetsDir = Path.Join(binDir.FullName, "Assets");

        var directory = new DirectoryInfo(assetsDir);

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException("Assets directory not found.");
        }

        return directory;
    }

    private static DirectoryInfo GetBuildDropPath()
    {
        const string VariableName = "Sandbox103_BuildDrop";
        string buildDrop = GetRequiredEnvironmentVariable(VariableName);
        var directory = new DirectoryInfo(buildDrop);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException(buildDrop);
        }
        return directory;
    }

    private static DirectoryInfo GetLogDropPath()
    {
        const string VariableName = "Sandbox103_LogDrop";
        string logDrop = GetRequiredEnvironmentVariable(VariableName);
        var directory = new DirectoryInfo(logDrop);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException(logDrop);
        }
        return directory;
    }

    private static FileInfo GetBinLog()
    {
        DirectoryInfo assetsDir = AssetsDirectory;
        string binLog = Directory.EnumerateFiles(assetsDir.FullName, "*.binlog", SearchOption.TopDirectoryOnly).Single();
        var file = new FileInfo(binLog);

        if (!file.Exists)
        {
            throw new FileNotFoundException("Bin log not found.");
        }

        return file;
    }

    private static DirectoryInfo GetRepoDirectory()
    {
        const string VariableName = "Sandbox103_Repo";
        string repoRoot = GetRequiredEnvironmentVariable(VariableName);
        var directory = new DirectoryInfo(repoRoot);

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException(repoRoot);
        }

        return directory;
    }

    private static string GetRequiredEnvironmentVariable(string variable)
    {
        ArgumentException.ThrowIfNullOrEmpty(variable);

        return Environment.GetEnvironmentVariable(variable) ??
            throw new InvalidOperationException($"Missing environment variable: '{variable}'.");
    }
}
