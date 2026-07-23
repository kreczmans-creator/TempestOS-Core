using Tempest.Core.Configuration;
using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

public class LoggerFactoryTests
{
    private static IConfigurationProvider ConfigurationWithMinimumLevel(string? value)
    {
        var builder = new ConfigurationBuilder();

        if (value is not null)
        {
            builder.AddSource(new MemoryConfigurationSource(new[]
            {
                new KeyValuePair<string, string>(LoggerFactory.MinimumLevelConfigurationKey, value),
            }));
        }

        return builder.Build();
    }

    [Fact]
    public void CreateLogger_ReturnsALoggerForTheRequestedCategory()
    {
        var sink = new RecordingLogSink();
        var factory = new LoggerFactory(ConfigurationWithMinimumLevel(null), sink);

        var logger = factory.CreateLogger("Discovery");
        logger.Information("message");

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("Discovery", entry.Category);
    }

    [Fact]
    public void CreateLogger_CalledWithDifferentCategories_ProducesIndependentLoggers()
    {
        var sink = new RecordingLogSink();
        var factory = new LoggerFactory(ConfigurationWithMinimumLevel(null), sink);

        factory.CreateLogger("Discovery").Information("from discovery");
        factory.CreateLogger("Configuration").Information("from configuration");

        Assert.Equal(2, sink.Entries.Count);
        Assert.Contains(sink.Entries, entry => entry.Category == "Discovery");
        Assert.Contains(sink.Entries, entry => entry.Category == "Configuration");
    }

    [Fact]
    public void Constructor_WhenMinimumLevelKeyIsMissing_DefaultsToInformation()
    {
        var sink = new RecordingLogSink();
        var factory = new LoggerFactory(ConfigurationWithMinimumLevel(null), sink);
        var logger = factory.CreateLogger("Category");

        logger.Debug("filtered - below Information");
        logger.Information("kept - at Information");

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("kept - at Information", entry.Message);
    }

    [Fact]
    public void Constructor_WhenMinimumLevelIsConfigured_UsesTheConfiguredLevel()
    {
        var sink = new RecordingLogSink();
        var factory = new LoggerFactory(ConfigurationWithMinimumLevel("Warning"), sink);
        var logger = factory.CreateLogger("Category");

        logger.Information("filtered - below Warning");
        logger.Warning("kept - at Warning");

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("kept - at Warning", entry.Message);
    }

    [Fact]
    public void Constructor_MinimumLevelIsCaseInsensitive()
    {
        var sink = new RecordingLogSink();
        var factory = new LoggerFactory(ConfigurationWithMinimumLevel("warning"), sink);
        var logger = factory.CreateLogger("Category");

        logger.Information("filtered");
        logger.Warning("kept");

        Assert.Single(sink.Entries);
    }

    [Fact]
    public void Constructor_ThrowsConfigurationException_WhenMinimumLevelIsInvalid()
    {
        var sink = new RecordingLogSink();

        Assert.Throws<ConfigurationException>(() =>
            new LoggerFactory(ConfigurationWithMinimumLevel("NotARealLevel"), sink));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenConfigurationIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new LoggerFactory(null!, new RecordingLogSink()));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSinkIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new LoggerFactory(ConfigurationWithMinimumLevel(null), null!));
    }

    [Fact]
    public void CreateLogger_ThrowsArgumentNullException_WhenCategoryIsNull()
    {
        var factory = new LoggerFactory(ConfigurationWithMinimumLevel(null), new RecordingLogSink());

        Assert.Throws<ArgumentNullException>(() => factory.CreateLogger(null!));
    }
}
