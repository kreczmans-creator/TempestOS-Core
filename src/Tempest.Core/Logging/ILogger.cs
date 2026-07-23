namespace Tempest.Core.Logging;

/// <summary>
/// The logging abstraction every TempestOS runtime component depends on.
/// </summary>
/// <remarks>
/// <para>
/// A component that logs depends only on <see cref="ILogger"/> — never on a
/// concrete logger, a sink, or any detail of where or how a message is
/// ultimately written. This is deliberate: logging is infrastructure, not
/// business behaviour, and no runtime component shall know where its logs go.
/// </para>
/// <para>
/// Every method takes a plain message string, an optional exception, and
/// optional structured properties. There is no formatting logic for a caller to
/// get right or wrong — a caller supplies the message and the data; how (or
/// whether) an <see cref="ILogSink"/> renders that data is entirely the sink's
/// concern.
/// </para>
/// </remarks>
public interface ILogger
{
    /// <summary>Logs a message at <see cref="LogLevel.Trace"/>.</summary>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">An associated exception, if any.</param>
    /// <param name="properties">Structured properties associated with the message, if any.</param>
    void Trace(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);

    /// <summary>Logs a message at <see cref="LogLevel.Debug"/>.</summary>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">An associated exception, if any.</param>
    /// <param name="properties">Structured properties associated with the message, if any.</param>
    void Debug(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);

    /// <summary>Logs a message at <see cref="LogLevel.Information"/>.</summary>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">An associated exception, if any.</param>
    /// <param name="properties">Structured properties associated with the message, if any.</param>
    void Information(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);

    /// <summary>Logs a message at <see cref="LogLevel.Warning"/>.</summary>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">An associated exception, if any.</param>
    /// <param name="properties">Structured properties associated with the message, if any.</param>
    void Warning(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);

    /// <summary>Logs a message at <see cref="LogLevel.Error"/>.</summary>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception associated with the error, if any.</param>
    /// <param name="properties">Structured properties associated with the message, if any.</param>
    void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);

    /// <summary>Logs a message at <see cref="LogLevel.Critical"/>.</summary>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception associated with the critical failure, if any.</param>
    /// <param name="properties">Structured properties associated with the message, if any.</param>
    void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);
}
