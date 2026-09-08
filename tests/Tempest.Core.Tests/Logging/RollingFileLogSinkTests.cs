using Tempest.Core.Logging;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Logging;

/// <summary>
/// Proves <see cref="RollingFileLogSink"/>'s own file-per-day writing,
/// rolling deletion, and its never-throws-into-the-caller I/O error
/// isolation (`WP 17.2A`, ADR-0146).
/// </summary>
public class RollingFileLogSinkTests
{
    private static LogEntry Entry(DateTime? timestamp = null, LogLevel level = LogLevel.Information, string message = "message") =>
        new(
            timestamp ?? DateTime.UtcNow,
            level,
            "Category",
            message,
            null,
            new Dictionary<string, object?>(),
            Environment.CurrentManagedThreadId);

    [Fact]
    public void Write_CreatesTodaysFile_ContainingTheEntry()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "logs");

        using (var sink = new RollingFileLogSink(directory))
            sink.Write(Entry(message: "hello from the rolling sink"));

        var expectedFileName = $"tempest-{DateTime.UtcNow:yyyyMMdd}.log";
        var path = Path.Combine(directory, expectedFileName);

        Assert.True(File.Exists(path));
        Assert.Contains("hello from the rolling sink", File.ReadAllText(path));
    }

    [Fact]
    public void Write_EveryEntry_IsFlushedImmediately()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "logs");

        using var sink = new RollingFileLogSink(directory);
        sink.Write(Entry(message: "flushed without disposing"));

        var path = Path.Combine(directory, $"tempest-{DateTime.UtcNow:yyyyMMdd}.log");

        // FileInfo.Length is a metadata-only query — no handle is opened,
        // so this proves data already reached disk without contending
        // with the sink's own still-open, exclusively-buffered writer
        // (which Dispose() would otherwise be needed to flush).
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void Write_TwoEntriesOnDifferentDays_GoToDifferentFiles()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "logs");

        var dayOne = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var dayTwo = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);

        using (var sink = new RollingFileLogSink(directory))
        {
            sink.Write(Entry(dayOne, message: "day one"));
            sink.Write(Entry(dayTwo, message: "day two"));
        }

        var fileOne = Path.Combine(directory, "tempest-20260101.log");
        var fileTwo = Path.Combine(directory, "tempest-20260102.log");

        Assert.True(File.Exists(fileOne));
        Assert.True(File.Exists(fileTwo));
        Assert.Contains("day one", File.ReadAllText(fileOne));
        Assert.Contains("day two", File.ReadAllText(fileTwo));
    }

    [Fact]
    public void Write_MoreDaysThanMaxFiles_DeletesTheOldestBeyondTheLimit()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "logs");

        using (var sink = new RollingFileLogSink(directory, maxFiles: 3))
        {
            for (var day = 1; day <= 5; day++)
                sink.Write(Entry(new DateTime(2026, 1, day, 0, 0, 0, DateTimeKind.Utc), message: $"day {day}"));
        }

        var remaining = Directory.GetFiles(directory, "tempest-*.log")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // The three newest days survive; the two oldest are gone.
        Assert.Equal(["tempest-20260103.log", "tempest-20260104.log", "tempest-20260105.log"], remaining);
    }

    [Fact]
    public void Write_ToADirectoryThatCannotBeCreated_NeverThrows_AndCountsTheError()
    {
        using var temp = new TempDirectory();

        // A file where a directory needs to go: Directory.CreateDirectory
        // fails, and every subsequent write fails identically.
        var blockingFilePath = Path.Combine(temp.Path, "blocked");
        File.WriteAllText(blockingFilePath, "not a directory");

        using var errorWriter = new StringWriter();
        var sink = new RollingFileLogSink(Path.Combine(blockingFilePath, "logs"), errorWriter: errorWriter);

        var exception = Record.Exception(() => sink.Write(Entry()));

        Assert.Null(exception);
        Assert.True(sink.ErrorCount > 0);

        sink.Dispose();

        Assert.Contains("I/O error", errorWriter.ToString());
    }

    [Fact]
    public void Write_ThrowsArgumentNullException_WhenEntryIsNull()
    {
        using var temp = new TempDirectory();
        using var sink = new RollingFileLogSink(temp.Path);

        Assert.Throws<ArgumentNullException>(() => sink.Write(null!));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenDirectoryPathIsNullEmptyOrWhitespace()
    {
        Assert.Throws<ArgumentNullException>(() => new RollingFileLogSink(null!));
        Assert.Throws<ArgumentException>(() => new RollingFileLogSink(""));
        Assert.Throws<ArgumentException>(() => new RollingFileLogSink("   "));
    }

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenMaxFilesIsLessThanOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RollingFileLogSink("dir", maxFiles: 0));
    }

    [Fact]
    public void Write_FromMultipleThreadsConcurrently_DoesNotThrow_AndEveryEntryIsWritten()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "logs");

        using (var sink = new RollingFileLogSink(directory))
        {
            Parallel.For(0, 50, i => sink.Write(Entry(message: $"concurrent message {i}")));
        }

        var path = Path.Combine(directory, $"tempest-{DateTime.UtcNow:yyyyMMdd}.log");
        var lines = File.ReadAllLines(path);

        Assert.Equal(50, lines.Length);
    }

    [Fact]
    public void ThroughLoggerFactory_RuntimeLoggingMinimumLevel_IsHonoured()
    {
        // `Runtime:Logging:MinimumLevel` is honoured by every sink, because
        // filtering happens once, centrally, in Logger — before Write is
        // ever called on any sink (see ILogSink's own remarks). This is
        // the file sink's own proof of that shared guarantee.
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "logs");

        using var sink = new RollingFileLogSink(directory);
        var configuration = new Tempest.Core.Configuration.ConfigurationBuilder()
            .AddSource(new Tempest.Core.Configuration.MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(LoggerFactory.MinimumLevelConfigurationKey, "Warning"),
            ]))
            .Build();

        var loggerFactory = new LoggerFactory(configuration, sink);
        var logger = loggerFactory.CreateLogger("Test");

        logger.Information("below the minimum level - must not be written");
        logger.Warning("at the minimum level - must be written");

        sink.Dispose();

        var content = File.ReadAllText(Path.Combine(directory, $"tempest-{DateTime.UtcNow:yyyyMMdd}.log"));

        Assert.DoesNotContain("below the minimum level", content);
        Assert.Contains("at the minimum level", content);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        using var temp = new TempDirectory();
        var sink = new RollingFileLogSink(Path.Combine(temp.Path, "logs"));
        sink.Write(Entry());

        sink.Dispose();
        var exception = Record.Exception(() => sink.Dispose());

        Assert.Null(exception);
    }
}
