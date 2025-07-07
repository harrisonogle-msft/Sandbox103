namespace Sandbox103;

internal readonly record struct BuildDropProject
{
    public required string ProjectPath { get; init; }

    public required string BinaryPath { get; init; }
}
