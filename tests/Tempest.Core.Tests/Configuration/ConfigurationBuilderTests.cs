using Tempest.Core.Configuration;
using Tempest.Core.Tests.Logging;

namespace Tempest.Core.Tests.Configuration;

public class ConfigurationBuilderTests
{
    private static MemoryConfigurationSource Source(params (string Key, string Value)[] entries) =>
        new(entries.Select(entry => new KeyValuePair<string, string>(entry.Key, entry.Value)));

    [Fact]
    public void Build_WithSingleMemorySource_ExposesConfiguredValues()
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(Source(("Runtime:Name", "TempestOS"), ("Runtime:Version", "0.1.0")));

        var provider = builder.Build();

        Assert.Equal("TempestOS", provider.Get("Runtime:Name"));
        Assert.Equal("0.1.0", provider.Get("Runtime:Version"));
    }

    [Fact]
    public void Build_WithMultipleSources_MergesKeysFromAllSources()
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(Source(("A", "1")));
        builder.AddSource(Source(("B", "2")));

        var provider = builder.Build();

        Assert.Equal("1", provider.Get("A"));
        Assert.Equal("2", provider.Get("B"));
    }

    [Fact]
    public void Build_LaterSourceOverridesEarlierSourceForSameKey()
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(Source(("Runtime:Logging:MinimumLevel", "Debug")));
        builder.AddSource(Source(("Runtime:Logging:MinimumLevel", "Warning")));

        var provider = builder.Build();

        Assert.Equal("Warning", provider.Get("Runtime:Logging:MinimumLevel"));
    }

    [Fact]
    public void Build_SupportsHierarchicalColonDelimitedKeys()
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(Source(
            ("Runtime:Logging:MinimumLevel", "Information"),
            ("Project:Retention:Days", "30")));

        var provider = builder.Build();

        Assert.Equal("Information", provider.Get("Runtime:Logging:MinimumLevel"));
        Assert.Equal("30", provider.Get("Project:Retention:Days"));
    }

    [Fact]
    public void Build_KeysAreCaseInsensitive()
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(Source(("Runtime:Name", "TempestOS")));

        var provider = builder.Build();

        Assert.Equal("TempestOS", provider.Get("runtime:name"));
        Assert.Equal("TempestOS", provider.Get("RUNTIME:NAME"));
    }

    [Fact]
    public void Build_LaterSourceOverride_IsCaseInsensitive()
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(Source(("Runtime:Name", "First")));
        builder.AddSource(Source(("runtime:name", "Second")));

        var provider = builder.Build();

        Assert.Equal("Second", provider.Get("Runtime:Name"));
    }

    [Fact]
    public void Build_ThrowsDuplicateConfigurationKeyException_WhenSameSourceRepeatsAKey()
    {
        var builder = new ConfigurationBuilder();
        var duplicateSource = new MemoryConfigurationSource(new[]
        {
            new KeyValuePair<string, string>("Runtime:Name", "First"),
            new KeyValuePair<string, string>("Runtime:Name", "Second"),
        });

        builder.AddSource(duplicateSource);

        var exception = Assert.Throws<DuplicateConfigurationKeyException>(() => builder.Build());

        Assert.Equal("Runtime:Name", exception.Key);
        Assert.Equal(typeof(MemoryConfigurationSource), exception.SourceType);
    }

    [Fact]
    public void Build_DuplicateKeyDetection_IsCaseInsensitive()
    {
        var builder = new ConfigurationBuilder();
        var duplicateSource = new MemoryConfigurationSource(new[]
        {
            new KeyValuePair<string, string>("Runtime:Name", "First"),
            new KeyValuePair<string, string>("runtime:name", "Second"),
        });

        builder.AddSource(duplicateSource);

        Assert.Throws<DuplicateConfigurationKeyException>(() => builder.Build());
    }

    [Fact]
    public void Build_DuplicateKeyAcrossDifferentSources_IsNotAnError()
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(Source(("Runtime:Name", "First")));
        builder.AddSource(Source(("Runtime:Name", "Second")));

        var provider = builder.Build();

        Assert.Equal("Second", provider.Get("Runtime:Name"));
    }

    [Fact]
    public void Build_ThrowsInvalidConfigurationEntryException_ForNullKey()
    {
        var builder = new ConfigurationBuilder();
        var source = new MemoryConfigurationSource(new[]
        {
            new KeyValuePair<string, string>(null!, "value"),
        });

        builder.AddSource(source);

        var exception = Assert.Throws<InvalidConfigurationEntryException>(() => builder.Build());
        Assert.Equal(typeof(MemoryConfigurationSource), exception.SourceType);
    }

    [Fact]
    public void Build_ThrowsInvalidConfigurationEntryException_ForEmptyKey()
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(Source(("   ", "value")));

        Assert.Throws<InvalidConfigurationEntryException>(() => builder.Build());
    }

    [Fact]
    public void Build_ThrowsInvalidConfigurationEntryException_ForNullValue()
    {
        var builder = new ConfigurationBuilder();
        var source = new MemoryConfigurationSource(new[]
        {
            new KeyValuePair<string, string>("Runtime:Name", null!),
        });

        builder.AddSource(source);

        Assert.Throws<InvalidConfigurationEntryException>(() => builder.Build());
    }

    [Fact]
    public void AddSource_ThrowsArgumentNullException_WhenSourceIsNull()
    {
        var builder = new ConfigurationBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddSource(null!));
    }

    [Fact]
    public void Build_WithNoSources_ReturnsProviderWithNoKeys()
    {
        var provider = new ConfigurationBuilder().Build();

        Assert.Empty(provider.GetAll());
    }

    [Fact]
    public void Build_WithLogger_DoesNotThrowAndRecordsProgress()
    {
        var logger = new RecordingLogger();
        var builder = new ConfigurationBuilder(logger);
        builder.AddSource(Source(("Runtime:Name", "TempestOS")));

        var provider = builder.Build();

        Assert.Equal("TempestOS", provider.Get("Runtime:Name"));
        Assert.NotEmpty(logger.Messages);
    }
}
