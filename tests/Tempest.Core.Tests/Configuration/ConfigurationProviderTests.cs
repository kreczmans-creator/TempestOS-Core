using Tempest.Core.Configuration;

namespace Tempest.Core.Tests.Configuration;

public class ConfigurationProviderTests
{
    private static IConfigurationProvider BuildProvider(params (string Key, string Value)[] entries)
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(new MemoryConfigurationSource(
            entries.Select(entry => new KeyValuePair<string, string>(entry.Key, entry.Value))));

        return builder.Build();
    }

    [Fact]
    public void Get_ReturnsConfiguredValue()
    {
        var provider = BuildProvider(("Runtime:Name", "TempestOS"));

        Assert.Equal("TempestOS", provider.Get("Runtime:Name"));
    }

    [Fact]
    public void Get_ThrowsConfigurationKeyNotFoundException_WhenKeyMissing()
    {
        var provider = BuildProvider(("Runtime:Name", "TempestOS"));

        var exception = Assert.Throws<ConfigurationKeyNotFoundException>(() => provider.Get("Missing:Key"));

        Assert.Equal("Missing:Key", exception.Key);
    }

    [Fact]
    public void TryGetValue_ReturnsTrueAndValue_WhenKeyPresent()
    {
        var provider = BuildProvider(("Runtime:Name", "TempestOS"));

        var result = provider.TryGetValue("Runtime:Name", out var value);

        Assert.True(result);
        Assert.Equal("TempestOS", value);
    }

    [Fact]
    public void TryGetValue_ReturnsFalse_WhenKeyMissing()
    {
        var provider = BuildProvider(("Runtime:Name", "TempestOS"));

        var result = provider.TryGetValue("Missing:Key", out var value);

        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void ContainsKey_ReturnsTrue_WhenKeyPresent()
    {
        var provider = BuildProvider(("Runtime:Name", "TempestOS"));

        Assert.True(provider.ContainsKey("Runtime:Name"));
    }

    [Fact]
    public void ContainsKey_ReturnsFalse_WhenKeyMissing()
    {
        var provider = BuildProvider(("Runtime:Name", "TempestOS"));

        Assert.False(provider.ContainsKey("Missing:Key"));
    }

    [Fact]
    public void ContainsKey_IsCaseInsensitive()
    {
        var provider = BuildProvider(("Runtime:Name", "TempestOS"));

        Assert.True(provider.ContainsKey("runtime:name"));
    }

    [Fact]
    public void GetAll_EnumeratesEveryConfiguredKey()
    {
        var provider = BuildProvider(
            ("Runtime:Name", "TempestOS"),
            ("Runtime:Version", "0.1.0"));

        var all = provider.GetAll().ToDictionary(entry => entry.Key, entry => entry.Value);

        Assert.Equal(2, all.Count);
        Assert.Equal("TempestOS", all["Runtime:Name"]);
        Assert.Equal("0.1.0", all["Runtime:Version"]);
    }

    [Fact]
    public void Get_ThrowsArgumentNullException_WhenKeyIsNull()
    {
        var provider = BuildProvider(("Runtime:Name", "TempestOS"));

        Assert.Throws<ArgumentNullException>(() => provider.Get(null!));
    }

    [Fact]
    public void TryGetValue_ThrowsArgumentNullException_WhenKeyIsNull()
    {
        var provider = BuildProvider(("Runtime:Name", "TempestOS"));

        Assert.Throws<ArgumentNullException>(() => provider.TryGetValue(null!, out _));
    }
}
