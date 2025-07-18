﻿using Microsoft.Build.Logging;

namespace Sandbox103.Helpers;

/// <summary>
/// Helper methods to manipulate <c>.binlog</c> files generated by MSBuild.
/// </summary>
public static class BinLogHelper
{
    /// <summary>
    /// Creates a <see cref="BinaryReader"/> for the provided binary log file.
    /// Performs decompression and buffering in the optimal way.
    /// Caller is responsible for disposing the returned reader.
    /// </summary>
    /// <param name="sourceFilePath">Path to the <c>.binlog</c> file.</param>
    /// <returns>A <see cref="BuildEventArgsReader"/> for the given binlog file.</returns>
    public static BuildEventArgsReader OpenBuildEventsReader(string sourceFilePath)
    {
        BuildEventArgsReader? reader = null;

        try
        {
            reader = BinaryLogReplayEventSource.OpenBuildEventsReader(
                BinaryLogReplayEventSource.OpenReader(sourceFilePath),
                closeInput: true, 
                allowForwardCompatibility: true);

            EnableForwardCompatibilityIfSupported(reader);

            return reader;
        }
        catch
        {
            using (reader)
            {
                throw;
            }
        }
    }

    private static void EnableForwardCompatibilityIfSupported(BuildEventArgsReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        // We want to set these property mutators so that the version of MSBuild consumed by
        // this code can process binlogs generated by newer versions of MSBuild with higher
        // binary logger format versions.
        //
        // But before version 18, forward compatibility (event offsets) are not supported,
        // so the property mutators will throw an exception.
        //
        // The goal here is to support parsing binlogs generated by all versions of MSBuild.
        // Attempting to enable forward compatibility and swallowing any exceptions covers
        // all cases.
        //
        // Case I: the file being parsed is version >= 18
        //   In this case, forward compatibility is enabled without error, so all versions >= 18
        //   can be parsed by this library.
        //
        // Case II: the file being parsed is version < 18
        //   In this case, forward compatibility can't be enabled, but because this library
        //   is compiled against a version of MSBuild with binary logger format version >= 18,
        //   this library can handle the old format without needing to enable forwards compat.

        try
        {
            reader.SkipUnknownEvents = true;
            reader.SkipUnknownEventParts = true;

            // Subscription to RecoverableReadError is mandatory during forward compatible reading.
            reader.RecoverableReadError += static _ => { };
        }
        catch
        {
        }
    }
}
