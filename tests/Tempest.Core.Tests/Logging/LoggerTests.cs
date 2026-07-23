using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

public class LoggerTests
{
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void Log_AtOrAboveMinimumLevel_InvokesSink(LogLevel minimumLevel)
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Category", minimumLevel, sink);

        Invoke(logger, minimumLevel, "message");

        Assert.Single(sink.Entries);
    }

    [Fact]
    public void Log_BelowMinimumLevel_DoesNotInvokeSink()
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Category", LogLevel.Warning, sink);

        logger.Information("this should be filtered");

        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void Log_WithMinimumLevelNone_FiltersEveryMessage()
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Category", LogLevel.None, sink);

        logger.Trace("trace");
        logger.Debug("debug");
        logger.Information("information");
        logger.Warning("warning");
        logger.Error("error");
        logger.Critical("critical");

        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void Log_SetsCategoryAndLevelOnTheEntry()
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Discovery", LogLevel.Trace, sink);

        logger.Warning("message");

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("Discovery", entry.Category);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("message", entry.Message);
    }

    [Fact]
    public void Log_SetsTimestampAndThreadId()
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Category", LogLevel.Trace, sink);

        var before = DateTime.UtcNow;
        logger.Information("message");
        var after = DateTime.UtcNow;

        var entry = Assert.Single(sink.Entries);
        Assert.InRange(entry.Timestamp, before, after);
        Assert.Equal(Environment.CurrentManagedThreadId, entry.ThreadId);
    }

    [Fact]
    public void Log_WithStructuredProperties_PassesThemThroughUnchanged()
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Category", LogLevel.Trace, sink);
        var properties = new Dictionary<string, object?> { ["ModuleId"] = "tempest.sample", ["Attempt"] = 3 };

        logger.Information("message", properties: properties);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("tempest.sample", entry.Properties["ModuleId"]);
        Assert.Equal(3, entry.Properties["Attempt"]);
    }

    [Fact]
    public void Log_WithoutProperties_UsesEmptyProperties()
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Category", LogLevel.Trace, sink);

        logger.Information("message");

        var entry = Assert.Single(sink.Entries);
        Assert.Empty(entry.Properties);
    }

    [Fact]
    public void Error_WithException_AttachesItToTheEntry()
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Category", LogLevel.Trace, sink);
        var exception = new InvalidOperationException("boom");

        logger.Error("operation failed", exception);

        var entry = Assert.Single(sink.Entries);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public void Critical_WithException_AttachesItToTheEntry()
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Category", LogLevel.Trace, sink);
        var exception = new InvalidOperationException("catastrophic");

        logger.Critical("unrecoverable failure", exception);

        var entry = Assert.Single(sink.Entries);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public void Log_FilteredMessage_NeverConstructsAnEntryTheSinkCanObserve()
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Category", LogLevel.Critical, sink);

        logger.Trace("t");
        logger.Debug("d");
        logger.Information("i");
        logger.Warning("w");
        logger.Error("e");

        Assert.Empty(sink.Entries);

        logger.Critical("c");

        Assert.Single(sink.Entries);
    }

    [Fact]
    public void Log_SinkThrows_DoesNotPropagateToTheCaller()
    {
        var sink = new ThrowingLogSink();
        var logger = new Logger("Category", LogLevel.Trace, sink);

        var exception = Record.Exception(() => logger.Information("message"));

        Assert.Null(exception);
    }

    [Fact]
    public void Log_SinkThrows_ReportsTheFailureToConsoleError()
    {
        var sink = new ThrowingLogSink(new InvalidOperationException("simulated sink failure"));
        var logger = new Logger("Category", LogLevel.Trace, sink);

        var output = CaptureConsoleError(() => logger.Information("message"));

        Assert.Contains(nameof(ThrowingLogSink), output);
        Assert.Contains("simulated sink failure", output);
    }

    [Fact]
    public void Log_SinkThrows_SubsequentLogCallsStillAttemptTheSink()
    {
        var sink = new ThrowingLogSink();
        var logger = new Logger("Category", LogLevel.Trace, sink);

        CaptureConsoleError(() =>
        {
            logger.Information("first");
            logger.Information("second");
        });

        Assert.Equal(2, sink.WriteAttempts);
    }

    [Fact]
    public void Log_SinkThrows_DoesNotPreventOtherLoggersSharingTheSameCategoryFromWorking()
    {
        var throwingSink = new ThrowingLogSink();
        var recordingSink = new RecordingLogSink();
        var failingLogger = new Logger("Category", LogLevel.Trace, throwingSink);
        var healthyLogger = new Logger("Category", LogLevel.Trace, recordingSink);

        CaptureConsoleError(() => failingLogger.Information("this sink is broken"));
        healthyLogger.Information("this sink is fine");

        Assert.Single(recordingSink.Entries);
    }

    [Fact]
    public void Log_FromMultipleThreadsConcurrently_RecordsEveryMessageWithoutError()
    {
        var sink = new RecordingLogSink();
        var logger = new Logger("Category", LogLevel.Trace, sink);

        const int threadCount = 8;
        const int messagesPerThread = 100;

        Parallel.For(0, threadCount, threadIndex =>
        {
            for (var i = 0; i < messagesPerThread; i++)
                logger.Information($"thread {threadIndex} message {i}");
        });

        Assert.Equal(threadCount * messagesPerThread, sink.Entries.Count);
    }

    private static string CaptureConsoleError(Action action)
    {
        var originalError = Console.Error;

        try
        {
            using var writer = new StringWriter();
            Console.SetError(writer);

            action();

            return writer.ToString();
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    private static void Invoke(ILogger logger, LogLevel level, string message)
    {
        switch (level)
        {
            case LogLevel.Trace:
                logger.Trace(message);
                break;
            case LogLevel.Debug:
                logger.Debug(message);
                break;
            case LogLevel.Information:
                logger.Information(message);
                break;
            case LogLevel.Warning:
                logger.Warning(message);
                break;
            case LogLevel.Error:
                logger.Error(message);
                break;
            case LogLevel.Critical:
                logger.Critical(message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level));
        }
    }
}
