namespace Tempest.Core.Logging;

/// <summary>
/// An <see cref="ILogSink"/> that writes log entries to a daily-rolling
/// file under a directory, so an operator has a durable record to hand to
/// support (`WP 17.2A`, ADR-0146).
/// </summary>
/// <remarks>
/// <para>
/// One file per UTC calendar day, named <c>tempest-yyyyMMdd.log</c> (see
/// <see cref="LogEntry.Timestamp"/>, already UTC), written under the
/// directory this instance is constructed with — conventionally
/// <c>&lt;persistence root&gt;/logs</c>. Every write flushes immediately:
/// this is a durability sink, and a buffered write an operator cannot find
/// after a crash defeats the entire point of it.
/// </para>
/// <para>
/// <b>Rolling.</b> The oldest files beyond <see cref="MaxFiles"/> are
/// deleted whenever a new day's file is opened — day-named files sort
/// lexicographically in date order, so no directory listing or parsing is
/// needed beyond a plain string sort.
/// </para>
/// <para>
/// <b>Never throws into the caller.</b> An I/O failure (the disk is full,
/// the directory was removed from under this sink, a permissions problem)
/// is caught, counted, and the write is silently dropped — mirroring
/// <see cref="Logger"/>'s and <see cref="CompositeLogSink"/>'s own
/// sink-failure-isolation convention, applied here to this sink's own
/// internal file I/O rather than to a child sink. The running total is
/// exposed via <see cref="ErrorCount"/> and, since a failing file sink is
/// otherwise invisible to an operator watching only the console, is
/// reported to this instance's own error writer
/// (<see cref="Console.Error"/> unless a constructor override was
/// supplied) when this sink is disposed.
/// </para>
/// <para>
/// <b>Thread safety.</b> Every write, and the file roll it may trigger, is
/// serialised through this instance's own lock — the same
/// <see cref="ConsoleLogSink"/>-established convention applied here to file
/// I/O instead of a console stream.
/// </para>
/// </remarks>
public sealed class RollingFileLogSink : ILogSink, IDisposable
{
    /// <summary>The literal prefix every file this sink writes carries.</summary>
    public const string FileNamePrefix = "tempest-";

    /// <summary>The <see cref="DateOnly"/> format string embedded in every file name.</summary>
    public const string FileNameDateFormat = "yyyyMMdd";

    /// <summary>The literal extension every file this sink writes carries.</summary>
    public const string FileNameExtension = ".log";

    /// <summary>The default value of <see cref="MaxFiles"/> when a caller does not supply one.</summary>
    public const int DefaultMaxFiles = 14;

    private readonly string _directoryPath;
    private readonly TextWriter _errorWriter;
    private readonly object _gate = new();

    private DateOnly? _currentFileDate;
    private StreamWriter? _writer;
    private int _errorCount;
    private bool _disposed;

    /// <summary>
    /// Initialises a new instance of the <see cref="RollingFileLogSink"/> class.
    /// </summary>
    /// <param name="directoryPath">
    /// The directory day-files are written under. Created on first write
    /// if it does not already exist; never created at construction, so
    /// constructing this sink has no side effect of its own until the
    /// first entry is actually written.
    /// </param>
    /// <param name="maxFiles">
    /// The number of day-files kept before the oldest is deleted. Defaults
    /// to <see cref="DefaultMaxFiles"/> (14, roughly two weeks).
    /// </param>
    /// <param name="errorWriter">
    /// The writer this sink's own I/O error count is reported to on
    /// <see cref="Dispose"/>, if it is non-zero. Defaults to
    /// <see cref="Console.Error"/> when <see langword="null"/> or omitted —
    /// mirrors <see cref="ConsoleLogSink"/>'s and <see cref="CompositeLogSink"/>'s
    /// own identical test seam.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="directoryPath"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxFiles"/> is less than 1.</exception>
    public RollingFileLogSink(string directoryPath, int maxFiles = DefaultMaxFiles, TextWriter? errorWriter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (maxFiles < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFiles), maxFiles, "At least one file must be kept.");

        _directoryPath = directoryPath;
        MaxFiles = maxFiles;
        _errorWriter = errorWriter ?? Console.Error;
    }

    /// <summary>Gets the directory this sink writes day-files under.</summary>
    public string DirectoryPath => _directoryPath;

    /// <summary>Gets the number of day-files this sink keeps before deleting the oldest.</summary>
    public int MaxFiles { get; }

    /// <summary>
    /// Gets the number of I/O errors this sink has swallowed since
    /// construction — a write that failed, or a stale file that could not
    /// be deleted while rolling.
    /// </summary>
    public int ErrorCount
    {
        get { lock (_gate) return _errorCount; }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A failure anywhere in this method — opening today's file, writing
    /// the entry, flushing, or rolling to a new day — is caught, counted
    /// in <see cref="ErrorCount"/>, and never propagated to the caller.
    /// </remarks>
    public void Write(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            if (_disposed)
                return;

            try
            {
                EnsureWriterForDate(DateOnly.FromDateTime(entry.Timestamp));
                _writer!.WriteLine(Format(entry));
                _writer.Flush();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _errorCount++;
            }
        }
    }

    /// <summary>
    /// Disposes the currently open file writer and, if any I/O error was
    /// swallowed since construction, reports the total count to this
    /// instance's own error writer. Idempotent.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _writer?.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _errorCount++;
            }

            _writer = null;

            if (_errorCount > 0)
            {
                _errorWriter.WriteLine(
                    $"[RollingFileLogSink] {_errorCount} I/O error(s) were swallowed while writing to '{_directoryPath}'.");
            }
        }
    }

    /// <summary>Called only while holding <see cref="_gate"/>.</summary>
    private void EnsureWriterForDate(DateOnly date)
    {
        if (_writer is not null && _currentFileDate == date)
            return;

        _writer?.Dispose();
        _writer = null;

        Directory.CreateDirectory(_directoryPath);

        var fileName = $"{FileNamePrefix}{date.ToString(FileNameDateFormat)}{FileNameExtension}";
        var path = Path.Combine(_directoryPath, fileName);

        // FileShare.ReadWrite, not the narrower default a plain
        // `new StreamWriter(path, append: true)` would take: an operator
        // reading today's still-growing file with an external tool (a
        // live `tail`, a text editor) is exactly the scenario this sink
        // exists to support, and must never be blocked by this sink's own
        // open handle.
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream) { AutoFlush = false };
        _currentFileDate = date;

        PruneOldFiles();
    }

    /// <summary>Called only while holding <see cref="_gate"/>.</summary>
    private void PruneOldFiles()
    {
        IReadOnlyList<string> files;

        try
        {
            // Day-named files sort lexicographically in date order (yyyyMMdd
            // is a fixed-width, zero-padded, most-significant-first format),
            // so a plain descending string sort is the newest-first order
            // without parsing a single file name.
            files = Directory
                .GetFiles(_directoryPath, $"{FileNamePrefix}*{FileNameExtension}")
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _errorCount++;
            return;
        }

        foreach (var stale in files.Skip(MaxFiles))
        {
            try
            {
                File.Delete(stale);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _errorCount++;
            }
        }
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
