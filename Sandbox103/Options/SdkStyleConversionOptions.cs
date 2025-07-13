namespace Sandbox103.Options;

public sealed record class SdkStyleConversionOptions
{
    public required string RepoPath { get; set; }

    public required string BuildDropPath { get; set; }

    public required string LogDropPath { get; set; }
}
