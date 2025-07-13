namespace Sandbox103.V1.BuildDrops;

public readonly record struct BuildDropProject
{
    public required string ProjectPath { get; init; }

    public required string BinaryPath { get; init; }

    public required string RelativeProjectPath { get; init; }

    public required string RelativeBinaryPath { get; init; }
}
