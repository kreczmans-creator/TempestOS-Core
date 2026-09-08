using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

// WP 17.0C: writes to a private StringWriter via ConsoleLogSink's own
// internal test seam (see that class's remarks) rather than redirecting the
// process-global Console.Out - the previous approach required serialising
// this class against every other test in the assembly that might log
// through a real host at the same moment (a race found once already,
// `WP-A1`), which stopped being feasible once the suite stopped serialising
// on that shared collection. No collection is needed now: every test here
// owns its own writer.
public class ConsoleLogSinkTests
{
    private static LogEntry Entry(
        LogLevel level = LogLevel.Information,
        string message = "message",
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? properties = null) =>
        new(
            DateTime.UtcNow,
            level,
            "Category",
            message,
            exception,
            properties ?? new Dictionary<string, object?>(),
            Environment.CurrentManagedThreadId);

    private static string CaptureOutput(Action<ConsoleLogSink> action)
    {
        using var writer = new StringWriter();
        var sink = new ConsoleLogSink(writer);

        action(sink);

        return writer.ToString();
    }

    [Fact]
    public void Write_IncludesLevelCategoryAndMessage()
    {
        var output = CaptureOutput(sink => sink.Write(Entry(LogLevel.Warning, "something happened")));

        Assert.Contains("Warning", output);
        Assert.Contains("Category", output);
        Assert.Contains("something happened", output);
    }

    [Fact]
    public void Write_WithException_IncludesTheExceptionDetails()
    {
        var exception = new InvalidOperationException("boom");

        var output = CaptureOutput(sink => sink.Write(Entry(LogLevel.Error, "failed", exception)));

        Assert.Contains("boom", output);
        Assert.Contains(nameof(InvalidOperationException), output);
    }

    [Fact]
    public void Write_WithStructuredProperties_IncludesThem()
    {
        var properties = new Dictionary<string, object?> { ["ModuleId"] = "tempest.sample" };

        var output = CaptureOutput(sink => sink.Write(Entry(properties: properties)));

        Assert.Contains("ModuleId", output);
        Assert.Contains("tempest.sample", output);
    }

    [Fact]
    public void Write_ProducesNoAnsiColourEscapeCodes()
    {
        var escapeCharacter = Convert.ToChar(0x1B);

        var output = CaptureOutput(sink => sink.Write(Entry()));

        Assert.DoesNotContain(escapeCharacter, output);
    }

    [Fact]
    public void Write_ThrowsArgumentNullException_WhenEntryIsNull()
    {
        var sink = new ConsoleLogSink();

        Assert.Throws<ArgumentNullException>(() => sink.Write(null!));
    }

    [Fact]
    public void Write_FromMultipleThreadsConcurrently_DoesNotThrow()
    {
        CaptureOutput(sink =>
            Parallel.For(0, 50, i => sink.Write(Entry(message: $"message {i}"))));
    }
}
