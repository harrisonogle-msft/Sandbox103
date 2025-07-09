namespace Sandbox103.BuildDrops;

public readonly record struct BuildDropProject
{
    public required string ProjectPath { get; init; }

    public required string BinaryPath { get; init; }
}
