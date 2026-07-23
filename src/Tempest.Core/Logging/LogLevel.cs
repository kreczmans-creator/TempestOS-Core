namespace Tempest.Core.Logging;

/// <summary>
/// Describes the severity of a log message, and the minimum severity an
/// <see cref="ILogger"/> is configured to emit.
/// </summary>
/// <remarks>
/// Values are ordered by increasing severity. A message is emitted only if its
/// <see cref="LogLevel"/> is greater than or equal to the configured minimum —
/// see <see cref="LoggerFactory"/> and <see cref="Logger"/>.
/// </remarks>
public enum LogLevel
{
    /// <summary>The most verbose level, for fine-grained diagnostic detail.</summary>
    Trace,

    /// <summary>Detail useful during development and troubleshooting.</summary>
    Debug,

    /// <summary>Routine information about normal operation.</summary>
    Information,

    /// <summary>An unexpected condition that is not, by itself, an error.</summary>
    Warning,

    /// <summary>A failure affecting the current operation.</summary>
    Error,

    /// <summary>A failure severe enough to threaten the runtime as a whole.</summary>
    Critical,

    /// <summary>
    /// Not a real severity — a sentinel value meaning "never emit," used as a
    /// minimum level to silence logging entirely.
    /// </summary>
    None
}
