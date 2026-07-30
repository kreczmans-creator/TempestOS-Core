using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Settings;

public class SettingDefinitionTests
{
    [Fact]
    public void Constructor_ValidArguments_SetsProperties()
    {
        var definition = new SettingDefinition("sample.key", "Sample Setting", "default-value");

        Assert.Equal("sample.key", definition.Key);
        Assert.Equal("Sample Setting", definition.DisplayName);
        Assert.Equal("default-value", definition.DefaultValue);
    }

    [Fact]
    public void Constructor_EmptyDefaultValue_IsAllowed()
    {
        var definition = new SettingDefinition("sample.key", "Sample Setting", "");

        Assert.Equal("", definition.DefaultValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullEmptyOrWhitespaceKey_ThrowsArgumentException(string? key)
    {
        Assert.Throws<ArgumentException>(() => new SettingDefinition(key!, "Display", "default"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullEmptyOrWhitespaceDisplayName_ThrowsArgumentException(string? displayName)
    {
        Assert.Throws<ArgumentException>(() => new SettingDefinition("sample.key", displayName!, "default"));
    }

    [Fact]
    public void Constructor_NullDefaultValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingDefinition("sample.key", "Display", null!));
    }
}
