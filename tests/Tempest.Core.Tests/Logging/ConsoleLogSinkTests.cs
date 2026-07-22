using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

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

    private static string CaptureConsoleOutput(Action action)
    {
        var originalOut = Console.Out;

        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);

            action();

            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Write_IncludesLevelCategoryAndMessage()
    {
        var sink = new ConsoleLogSink();

        var output = CaptureConsoleOutput(() => sink.Write(Entry(LogLevel.Warning, "something happened")));

        Assert.Contains("Warning", output);
        Assert.Contains("Category", output);
        Assert.Contains("something happened", output);
    }

    [Fact]
    public void Write_WithException_IncludesTheExceptionDetails()
    {
        var sink = new ConsoleLogSink();
        var exception = new InvalidOperationException("boom");

        var output = CaptureConsoleOutput(() => sink.Write(Entry(LogLevel.Error, "failed", exception)));

        Assert.Contains("boom", output);
        Assert.Contains(nameof(InvalidOperationException), output);
    }

    [Fact]
    public void Write_WithStructuredProperties_IncludesThem()
    {
        var sink = new ConsoleLogSink();
        var properties = new Dictionary<string, object?> { ["ModuleId"] = "tempest.sample" };

        var output = CaptureConsoleOutput(() => sink.Write(Entry(properties: properties)));

        Assert.Contains("ModuleId", output);
        Assert.Contains("tempest.sample", output);
    }

    [Fact]
    public void Write_ProducesNoAnsiColourEscapeCodes()
    {
        var sink = new ConsoleLogSink();
        var escapeCharacter = Convert.ToChar(0x1B);

        var output = CaptureConsoleOutput(() => sink.Write(Entry()));

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
        var sink = new ConsoleLogSink();

        CaptureConsoleOutput(() =>
            Parallel.For(0, 50, i => sink.Write(Entry(message: $"message {i}"))));
    }
}
