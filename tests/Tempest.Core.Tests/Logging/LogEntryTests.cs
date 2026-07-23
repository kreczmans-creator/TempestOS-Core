using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

public class LogEntryTests
{
    [Fact]
    public void Constructor_ExposesEverySuppliedValueThroughItsProperty()
    {
        var timestamp = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
        var exception = new InvalidOperationException("boom");
        var properties = new Dictionary<string, object?> { ["Key"] = "Value" };

        var entry = new LogEntry(
            timestamp,
            LogLevel.Warning,
            "Category",
            "Message",
            exception,
            properties,
            threadId: 7);

        Assert.Equal(timestamp, entry.Timestamp);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Category", entry.Category);
        Assert.Equal("Message", entry.Message);
        Assert.Same(exception, entry.Exception);
        Assert.Same(properties, entry.Properties);
        Assert.Equal(7, entry.ThreadId);
    }

    [Fact]
    public void Constructor_AllowsNullException()
    {
        var entry = new LogEntry(
            DateTime.UtcNow,
            LogLevel.Information,
            "Category",
            "Message",
            exception: null,
            new Dictionary<string, object?>(),
            threadId: 1);

        Assert.Null(entry.Exception);
    }

    [Fact]
    public void Properties_HasNoPublicSetter()
    {
        var type = typeof(LogEntry);

        Assert.Null(type.GetProperty(nameof(LogEntry.Properties))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(LogEntry.Message))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(LogEntry.Timestamp))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(LogEntry.Level))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(LogEntry.Category))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(LogEntry.Exception))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(LogEntry.ThreadId))!.SetMethod);
    }
}
