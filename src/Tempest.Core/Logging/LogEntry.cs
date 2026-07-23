namespace Tempest.Core.Logging;

/// <summary>
/// An immutable record of a single log message, ready to be written by an
/// <see cref="ILogSink"/>.
/// </summary>
/// <remarks>
/// <see cref="Exception"/> is preserved as the original exception object, not a
/// pre-formatted string — flattening a stack trace into text is a formatting
/// decision, and different sinks may want to render an exception differently
/// (or not at all). <see cref="LogEntry"/> itself makes no such decision.
/// </remarks>
public sealed class LogEntry
{
    /// <summary>
    /// Initialises a new instance of the <see cref="LogEntry"/> class.
    /// </summary>
    /// <param name="timestamp">The UTC time the message was logged.</param>
    /// <param name="level">The message's severity.</param>
    /// <param name="category">The logger category the message was logged through.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">The exception associated with the message, if any.</param>
    /// <param name="properties">Structured properties associated with the message.</param>
    /// <param name="threadId">The managed thread ID the message was logged from.</param>
    public LogEntry(
        DateTime timestamp,
        LogLevel level,
        string category,
        string message,
        Exception? exception,
        IReadOnlyDictionary<string, object?> properties,
        int threadId)
    {
        Timestamp = timestamp;
        Level = level;
        Category = category;
        Message = message;
        Exception = exception;
        Properties = properties;
        ThreadId = threadId;
    }

    /// <summary>
    /// Gets the UTC time the message was logged.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the message's severity.
    /// </summary>
    public LogLevel Level { get; }

    /// <summary>
    /// Gets the logger category the message was logged through (for example, "Discovery").
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the log message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the exception associated with the message, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the structured properties associated with the message. Never
    /// <see langword="null"/>; empty if none were supplied.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; }

    /// <summary>
    /// Gets the managed thread ID the message was logged from.
    /// </summary>
    public int ThreadId { get; }
}
