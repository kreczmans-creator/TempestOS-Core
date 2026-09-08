namespace Tempest.Core.Logging;

/// <summary>
/// An <see cref="ILogSink"/> that writes log entries to the standard output
/// stream.
/// </summary>
/// <remarks>
/// Output is plain text — no colour, no structured/JSON formatting. Console
/// output is append-only: entries are written in the order they are received
/// and nothing already written is ever revisited or rewritten. Every write
/// is serialised through this instance's own lock, so this sink is safe to
/// use from multiple threads concurrently regardless of the destination
/// writer's own thread-safety.
/// </remarks>
public sealed class ConsoleLogSink : ILogSink
{
    private readonly TextWriter _writer;
    private readonly object _gate = new();

    /// <summary>
    /// Initialises a new instance of the <see cref="ConsoleLogSink"/> class
    /// that writes to <see cref="Console.Out"/>.
    /// </summary>
    public ConsoleLogSink()
        : this(Console.Out)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ConsoleLogSink"/> class
    /// that writes to a specific <see cref="TextWriter"/>.
    /// </summary>
    /// <param name="writer">The writer every entry is written to.</param>
    /// <remarks>
    /// WP 17.0C test seam: lets a test observe exactly what this sink would
    /// write without redirecting the process-global <see cref="Console.Out"/>
    /// — the redirect this seam replaces was previously a source of
    /// cross-test races once most of the suite stopped serialising against
    /// it (see <c>ConsoleLogSinkTests</c>'s own remarks). Internal — no
    /// production caller needs anything other than the parameterless
    /// constructor's conventional <see cref="Console.Out"/> destination.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    internal ConsoleLogSink(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        _writer = writer;
    }

    /// <inheritdoc />
    public void Write(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var line = Format(entry);

        lock (_gate)
            _writer.WriteLine(line);
    }

    private static string Format(LogEntry entry)
    {
        var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] ({entry.Category}) " +
                   $"[Thread {entry.ThreadId}] {entry.Message}";

        if (entry.Properties.Count > 0)
        {
            var propertyText = string.Join(", ", entry.Properties.Select(pair => $"{pair.Key}={pair.Value}"));
            line += $" {{{propertyText}}}";
        }

        if (entry.Exception is not null)
            line += Environment.NewLine + entry.Exception;

        return line;
    }
}
