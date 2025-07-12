namespace Sandbox103.Helpers;

public static class PathHelper
{
    public static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Path.Join(Path.GetDirectoryName(path), Path.GetFileName(path));
    }
}
