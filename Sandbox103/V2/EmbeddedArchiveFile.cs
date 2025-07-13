using Microsoft.AspNetCore.Http.Features;

namespace Sandbox103.V2;

internal class EmbeddedArchiveFile : IArchiveFile
{
    private readonly string _path;
    private readonly IFeatureCollection _features;
    private readonly byte[] _content;

    public EmbeddedArchiveFile(string path, byte[] content)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);

        _path = path;
        _features = new FeatureCollection();
        _content = content;
    }

    public string Path => _path;

    public IFeatureCollection Features => _features;

    public Stream Content => new MemoryStream(_content, writable: false);

    public override bool Equals(object? obj) =>
        obj is EmbeddedArchiveFile other &&
        string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => Path.GetHashCode();
}
