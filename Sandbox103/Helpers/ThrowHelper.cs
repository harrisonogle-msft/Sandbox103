using System.Diagnostics.CodeAnalysis;

namespace Sandbox103.Helpers;

internal static class ThrowHelper
{
    public static void ThrowIfFileNotFound([NotNull] string? path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!File.Exists(path))
        {
            ThrowFileNotFoundException(path);
        }
    }

    public static void ThrowIfDirectoryNotFound([NotNull] string? path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!Directory.Exists(path))
        {
            ThrowDirectoryNotFoundException(path);
        }
    }

    [DoesNotReturn]
    private static void ThrowFileNotFoundException(string? path) =>
        throw new FileNotFoundException("File not found.", path);

    [DoesNotReturn]
    private static void ThrowDirectoryNotFoundException(string? path) =>
        throw new DirectoryNotFoundException($"Directory not found: '{path}'");
}
