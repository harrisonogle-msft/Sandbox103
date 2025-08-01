﻿namespace Sandbox103.V2.Abstractions;

/// <summary>
/// Represents a log drop for a particular repository generated by a
/// remote build which produces MSBuild <c>.binlog</c> files.
/// </summary>
public interface ILogDrop
{
    /// <summary>
    /// The location of the log drop directory on the local filesystem.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The <c>.binlog</c> files in the log drop.
    /// </summary>
    public IReadOnlyCollection<IBinaryLog> BinaryLogs { get; }
}
