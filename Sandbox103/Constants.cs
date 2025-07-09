using System.Reflection;

namespace Sandbox103;

public static class Constants
{
    private static DirectoryInfo? s_binDirectory;
    private static DirectoryInfo? s_assetsDirectory;
    private static FileInfo? s_dropPath;
    private static FileInfo? s_logDropPath;
    private static FileInfo? s_binLog;
    private static DirectoryInfo? s_repoDirectory;

    public static DirectoryInfo BinDirectory => s_binDirectory ??= GetBinDirectory();

    public static DirectoryInfo AssetsDirectory => s_assetsDirectory ??= GetAssetsDirectory();

    public static FileInfo DropPath => s_dropPath ??= GetDropPath();

    public static FileInfo LogDrop => s_logDropPath ??= GetLogDropPath();

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

    private static FileInfo GetDropPath()
    {
        return new FileInfo(@"C:\Users\harrisonogle\temp\2025-07-06\drop");
    }

    private static FileInfo GetLogDropPath()
    {
        return new FileInfo(@"C:\Users\harrisonogle\temp\2025-07-06\logdrop");
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
        var directory = new DirectoryInfo(@"D:\msazure\Intune\Svc\ProxyFrontEnd");

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException("Unable to find repository root.");
        }

        return directory;
    }
}
