namespace Sandbox103.V2;

internal readonly record struct DirectImport
{
    public DirectImport(IArchiveFile file, string? unexpandedProjectName)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrEmpty(unexpandedProjectName);

        File = file;
        UnexpandedProjectName = unexpandedProjectName;
    }

    public IArchiveFile File { get; }

    public string UnexpandedProjectName { get; }

    //public override bool Equals([NotNullWhen(true)] object? obj) =>
    //    obj is DirectImport other &&
    //    File.Equals(other.File);

    //public override int GetHashCode() => File.GetHashCode();
}

