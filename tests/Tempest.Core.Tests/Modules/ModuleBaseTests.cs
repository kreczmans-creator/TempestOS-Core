using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

public class ModuleBaseTests
{
    [Fact]
    public void Constructor_SetsIdNameAndVersion()
    {
        var module = new MinimalSdkModule("tempest.sdk.minimal", "Minimal SDK Module", "1.0.0");

        Assert.Equal("tempest.sdk.minimal", module.Id);
        Assert.Equal("Minimal SDK Module", module.Name);
        Assert.Equal("1.0.0", module.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentException_WhenIdIsNullEmptyOrWhitespace(string? id)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new MinimalSdkModule(id!, "Minimal SDK Module", "1.0.0"));

        Assert.Equal("id", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentException_WhenNameIsNullEmptyOrWhitespace(string? name)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new MinimalSdkModule("tempest.sdk.minimal", name!, "1.0.0"));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentException_WhenVersionIsNullEmptyOrWhitespace(string? version)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new MinimalSdkModule("tempest.sdk.minimal", "Minimal SDK Module", version!));

        Assert.Equal("version", exception.ParamName);
    }

    [Fact]
    public void ModuleBase_SatisfiesIModule()
    {
        var module = new MinimalSdkModule("tempest.sdk.minimal", "Minimal SDK Module", "1.0.0");

        Assert.IsAssignableFrom<IModule>(module);
    }
}
