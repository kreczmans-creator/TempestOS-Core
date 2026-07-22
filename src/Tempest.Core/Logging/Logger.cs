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

        _sink.Write(entry);
    }
}
