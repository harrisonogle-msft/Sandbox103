namespace Sandbox103.V2.Abstractions;

internal interface IProjectFile
{
    /// <summary>
    /// Path on local filesystem to the project within the source repository.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The indexed <c>.binlog</c> file emitted by MSBuild during this project's build.
    /// </summary>
    public IBinaryLog BinaryLog { get; }
}
