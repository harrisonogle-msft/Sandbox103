using CommandLine;

namespace Sandbox103.Options;

/// <summary>
/// Options for command-line invocation.
/// </summary>
public sealed record class CommandLineOptions
{
    internal static readonly TimeSpan MinimumTimeout = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan MaximumTimeout = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    [Option("repo", Required = true, HelpText = "Path to the local git repository to modify.")]
    public required string RepoPath { get; init; }

    [Option("build-drop", Required = true, HelpText = "Path to the build drop.")]
    public required string BuildDropPath { get; init; }

    [Option("log-drop", Required = true, HelpText = "Path to the log drop.")]
    public required string LogDropPath { get; init; }

    [Option("timeout", Required = false, HelpText = "Timeout for the conversion. (HH:mm:ss[.fffffff])")]
    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    internal bool Validate()
    {
        if (Timeout < MinimumTimeout ||
            Timeout > MaximumTimeout)
        {
            return false;
        }

        return true;
    }
}
