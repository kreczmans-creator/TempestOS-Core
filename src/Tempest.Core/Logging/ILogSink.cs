namespace Tempest.Core.Logging;

/// <summary>
/// A destination log entries are written to.
/// </summary>
/// <remarks>
/// WP 2.6 implements exactly one sink, <see cref="ConsoleLogSink"/>. Future work
/// packages may implement additional sinks (file, database, telemetry, network)
/// behind this same contract, without requiring any change to <see cref="ILogger"/>,
/// <see cref="ILoggerFactory"/>, or any component that logs. A sink is invoked
/// synchronously and is expected not to block unreasonably; TempestOS does not
/// implement asynchronous or batched logging.
/// </remarks>
public interface ILogSink
{
    /// <summary>
    /// Writes a log entry to this sink.
    /// </summary>
    /// <param name="entry">The entry to write.</param>
    /// <remarks>
    /// Called only for entries that have already passed minimum-level
    /// filtering — a sink never sees, and never has to filter, an entry that
    /// was below the configured minimum level.
    /// </remarks>
    void Write(LogEntry entry);
}
