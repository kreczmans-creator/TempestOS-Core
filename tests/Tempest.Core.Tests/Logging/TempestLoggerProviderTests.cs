using MEL = Microsoft.Extensions.Logging;
using Tempest.Core.Configuration;
using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

/// <summary>
/// Proves <see cref="TempestLoggerProvider"/> forwards
/// <c>Microsoft.Extensions.Logging</c> messages into the platform's own
/// sink pipeline, at the corresponding level (`WP 17.2A`, ADR-0146).
/// </summary>
public class TempestLoggerProviderTests
{
    private static Tempest.Core.Logging.ILoggerFactory BuildPlatformLoggerFactory(RecordingLogSink sink, LogLevel minimumLevel = LogLevel.Trace)
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(LoggerFactory.MinimumLevelConfigurationKey, minimumLevel.ToString()),
            ]))
            .Build();

        return new LoggerFactory(configuration, sink);
    }

    [Fact]
    public void CreateLogger_ThroughILoggerProvider_ForwardsToThePlatformSink()
    {
        var sink = new RecordingLogSink();
        MEL.ILoggerProvider provider = new TempestLoggerProvider(BuildPlatformLoggerFactory(sink));

        var meLogger = provider.CreateLogger("BridgedCategory");
        meLogger.Log(MEL.LogLevel.Information, new MEL.EventId(0), "hello from Microsoft.Extensions.Logging", null, (state, ex) => state);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("BridgedCategory", entry.Category);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("hello from Microsoft.Extensions.Logging", entry.Message);
    }

    [Fact]
    public void CreateLogger_ThroughMicrosoftILoggerFactory_ForwardsToThePlatformSink()
    {
        var sink = new RecordingLogSink();
        MEL.ILoggerFactory factory = new TempestLoggerProvider(BuildPlatformLoggerFactory(sink));

        var meLogger = factory.CreateLogger("BridgedCategory");
        meLogger.Log(MEL.LogLevel.Warning, new MEL.EventId(0), "a warning from a bridged factory", null, (state, ex) => state);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Theory]
    [InlineData(MEL.LogLevel.Trace, LogLevel.Trace)]
    [InlineData(MEL.LogLevel.Debug, LogLevel.Debug)]
    [InlineData(MEL.LogLevel.Information, LogLevel.Information)]
    [InlineData(MEL.LogLevel.Warning, LogLevel.Warning)]
    [InlineData(MEL.LogLevel.Error, LogLevel.Error)]
    [InlineData(MEL.LogLevel.Critical, LogLevel.Critical)]
    public void Log_EveryMicrosoftLevel_MapsToTheCorrespondingPlatformLevel(MEL.LogLevel meLevel, LogLevel expectedPlatformLevel)
    {
        var sink = new RecordingLogSink();
        MEL.ILoggerFactory factory = new TempestLoggerProvider(BuildPlatformLoggerFactory(sink));
        var meLogger = factory.CreateLogger("LevelMapping");

        meLogger.Log(meLevel, new MEL.EventId(0), "level-mapped message", null, (state, ex) => state);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(expectedPlatformLevel, entry.Level);
    }

    [Fact]
    public void Log_WithException_ForwardsTheException()
    {
        var sink = new RecordingLogSink();
        MEL.ILoggerFactory factory = new TempestLoggerProvider(BuildPlatformLoggerFactory(sink));
        var meLogger = factory.CreateLogger("WithException");
        var exception = new InvalidOperationException("bridged exception");

        meLogger.Log(MEL.LogLevel.Error, new MEL.EventId(0), "failed", exception, (state, ex) => state);

        var entry = Assert.Single(sink.Entries);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public void Log_BelowThePlatformsOwnMinimumLevel_IsNotForwarded()
    {
        var sink = new RecordingLogSink();
        MEL.ILoggerFactory factory = new TempestLoggerProvider(BuildPlatformLoggerFactory(sink, LogLevel.Warning));
        var meLogger = factory.CreateLogger("Filtered");

        meLogger.Log(MEL.LogLevel.Information, new MEL.EventId(0), "filtered out by the platform's own minimum level", null, (state, ex) => state);
        meLogger.Log(MEL.LogLevel.Warning, new MEL.EventId(0), "passes the platform's own minimum level", null, (state, ex) => state);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("passes the platform's own minimum level", entry.Message);
    }

    [Fact]
    public void BeginScope_ReturnsADisposableThatDoesNotThrow()
    {
        var sink = new RecordingLogSink();
        MEL.ILoggerFactory factory = new TempestLoggerProvider(BuildPlatformLoggerFactory(sink));
        var meLogger = factory.CreateLogger("Scoped");

        using var scope = meLogger.BeginScope("scope-state");

        Assert.NotNull(scope);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new TempestLoggerProvider(null!));
    }
}
