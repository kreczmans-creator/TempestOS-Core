using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

// Proves TD-02's own closure against the real CompositeLogSink
// implementation - fan-out to every child sink, per-child failure
// isolation mirroring Logger's own established convention, and
// construction validation.
public class CompositeLogSinkTests
{
    private static LogEntry CreateEntry(string message = "test") =>
        new(DateTime.UtcNow, LogLevel.Information, "Test", message, null, new Dictionary<string, object?>(), 1);

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_NullSinks_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new CompositeLogSink(null!));

    [Fact]
    public void Constructor_EmptySinks_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => new CompositeLogSink([]));

    [Fact]
    public void Constructor_SinksContainingNull_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => new CompositeLogSink([new RecordingLogSink(), null!]));

    [Fact]
    public void Sinks_ExposesTheSuppliedSinksInOrder()
    {
        var first = new RecordingLogSink();
        var second = new RecordingLogSink();

        var composite = new CompositeLogSink([first, second]);

        Assert.Equal([first, second], composite.Sinks);
    }

    // ------------------------------------------------------------------
    // Fan-out
    // ------------------------------------------------------------------

    [Fact]
    public void Write_MultipleSinks_WritesToEachInOrder()
    {
        var first = new RecordingLogSink();
        var second = new RecordingLogSink();
        var third = new RecordingLogSink();
        var composite = new CompositeLogSink([first, second, third]);
        var entry = CreateEntry("hello");

        composite.Write(entry);

        Assert.Same(entry, Assert.Single(first.Entries));
        Assert.Same(entry, Assert.Single(second.Entries));
        Assert.Same(entry, Assert.Single(third.Entries));
    }

    [Fact]
    public void Write_NullEntry_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new CompositeLogSink([new RecordingLogSink()]).Write(null!));

    [Fact]
    public void Write_RepeatedCalls_EachSinkReceivesEveryEntry()
    {
        var sink = new RecordingLogSink();
        var composite = new CompositeLogSink([sink]);

        composite.Write(CreateEntry("one"));
        composite.Write(CreateEntry("two"));
        composite.Write(CreateEntry("three"));

        Assert.Equal(3, sink.Entries.Count);
    }

    // ------------------------------------------------------------------
    // Per-child failure isolation (mirrors Logger's own convention)
    // ------------------------------------------------------------------

    [Fact]
    public void Write_OneSinkThrows_OtherSinksStillReceiveTheEntry()
    {
        var before = new RecordingLogSink();
        var throwing = new ThrowingLogSink();
        var after = new RecordingLogSink();
        var composite = new CompositeLogSink([before, throwing, after]);
        var entry = CreateEntry();

        composite.Write(entry);

        Assert.Same(entry, Assert.Single(before.Entries));
        Assert.Equal(1, throwing.WriteAttempts);
        Assert.Same(entry, Assert.Single(after.Entries));
    }

    [Fact]
    public void Write_OneSinkThrows_ExceptionNeverPropagatesToTheCaller()
    {
        var composite = new CompositeLogSink([new ThrowingLogSink(), new RecordingLogSink()]);

        var exception = Record.Exception(() => composite.Write(CreateEntry()));

        Assert.Null(exception);
    }

    [Fact]
    public void Write_AllSinksThrow_ExceptionNeverPropagatesToTheCaller()
    {
        var composite = new CompositeLogSink([new ThrowingLogSink(), new ThrowingLogSink()]);

        var exception = Record.Exception(() => composite.Write(CreateEntry()));

        Assert.Null(exception);
    }

    // ------------------------------------------------------------------
    // Real Logger integration - proving the whole chain, not just the sink
    // ------------------------------------------------------------------

    [Fact]
    public void Logger_WithCompositeSink_FansOutToEveryChildSink()
    {
        var first = new RecordingLogSink();
        var second = new RecordingLogSink();
        var composite = new CompositeLogSink([first, second]);
        var logger = new Logger("Test", LogLevel.Information, composite);

        logger.Information("hello");

        Assert.Single(first.Entries);
        Assert.Single(second.Entries);
        Assert.Equal("hello", first.Entries[0].Message);
    }
}
