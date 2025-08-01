﻿namespace Sandbox103.V2.Abstractions;

/// <summary>
/// Reads log drops generated by (remote) builds that produce MSBuild <c>.binlog</c> files.
/// </summary>
public interface ILogDropReader
{
    /// <summary>
    /// Read a log drop and index its <c>.binlog</c> files.
    /// </summary>
    /// <param name="options">Options to configure reading the log drop.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Indexed log drop.</returns>
    public Task<ILogDrop> ReadAsync(LogDropReaderOptions options, CancellationToken cancellationToken);
}
