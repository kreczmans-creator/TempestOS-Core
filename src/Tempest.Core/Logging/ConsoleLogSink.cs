namespace Tempest.Core.Logging;

/// <summary>
/// An <see cref="ILogSink"/> that writes log entries to the standard output
/// stream.
/// </summary>
/// <remarks>
/// Output is plain text — no colour, no structured/JSON formatting. Console
/// output is append-only: entries are written in the order they are received
/// and nothing already written is ever revisited or rewritten.
/// <see cref="System.Console.WriteLine(string)"/> is safe to call from multiple
/// threads concurrently, so this sink needs no locking of its own.
/// </remarks>
public sealed class ConsoleLogSink : ILogSink
{
    /// <inheritdoc />
    public void Write(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Console.WriteLine(Format(entry));
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
