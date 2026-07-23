namespace Tempest.Core.Logging;

/// <summary>
/// The concrete <see cref="ILogger"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Instances are created only by <see cref="LoggerFactory"/> — the constructor
/// is <see langword="internal"/>. A logger is bound, for its entire life, to one
/// category, one minimum level, and one sink; none of these can change after
/// construction.
/// </para>
/// <para>
/// <b>Filtering</b> happens before anything else: a message below the
/// configured minimum level is discarded immediately, before a
/// <see cref="LogEntry"/> is even constructed, so no allocation happens and the
/// sink is never invoked for a filtered-out message.
/// </para>
/// <para>
/// <b>Sink failures are isolated.</b> A logging failure must never terminate
/// the runtime or propagate out of a logging call to affect whatever operation
/// happened to be logging something. If <see cref="ILogSink.Write"/> throws,
/// the exception is caught here, reported directly to <see cref="Console.Error"/>
/// — bypassing the failed sink entirely — and never allowed to escape this
/// class. This closes a gap identified during the WP 2.7 architectural review
/// (see ADR-0010 and the Runtime Host architecture's Failure Behaviour
/// document): the sink was previously invoked with no exception handling at
/// all, contradicting this exact guarantee.
/// </para>
/// <para>
/// <b>Thread safety</b> follows from immutability rather than locking: every
/// field is set once, at construction, and never mutated afterward, so
/// concurrent calls from multiple threads never contend over shared mutable
/// state within this class. Whether writing itself is safe from multiple
/// threads is the sink's responsibility — see <see cref="ConsoleLogSink"/>.
/// </para>
/// </remarks>
public sealed class Logger : ILogger
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyProperties =
        new Dictionary<string, object?>();

    private readonly string _category;
    private readonly LogLevel _minimumLevel;
    private readonly ILogSink _sink;

    internal Logger(string category, LogLevel minimumLevel, ILogSink sink)
    {
        _category = category;
        _minimumLevel = minimumLevel;
        _sink = sink;
    }

    /// <inheritdoc />
    public void Trace(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        Log(LogLevel.Trace, message, exception, properties);

    /// <inheritdoc />
    public void Debug(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        Log(LogLevel.Debug, message, exception, properties);

    /// <inheritdoc />
    public void Information(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        Log(LogLevel.Information, message, exception, properties);

    /// <inheritdoc />
    public void Warning(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        Log(LogLevel.Warning, message, exception, properties);

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        Log(LogLevel.Error, message, exception, properties);

    /// <inheritdoc />
    public void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        Log(LogLevel.Critical, message, exception, properties);

    private void Log(LogLevel level, string message, Exception? exception, IReadOnlyDictionary<string, object?>? properties)
    {
        if (level < _minimumLevel)
            return;

        var entry = new LogEntry(
            DateTime.UtcNow,
            level,
            _category,
            message,
            exception,
            properties ?? EmptyProperties,
            Environment.CurrentManagedThreadId);

        try
        {
            _sink.Write(entry);
        }
        catch (Exception ex)
        {
            // A sink failure must never terminate the runtime or propagate to the
            // caller that happened to be logging something. Report it directly to
            // the console, bypassing the failed sink, and swallow it here.
            Console.Error.WriteLine(
                $"[Logger] Sink '{_sink.GetType().Name}' failed while writing a log entry: {ex}");
        }
    }
}
