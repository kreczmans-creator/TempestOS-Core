using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Settings;

public class ExceptionTests
{
    [Fact]
    public void SettingsException_MessageConstructor_SetsMessage()
    {
        var exception = new SettingsException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }

    [Fact]
    public void DuplicateSettingDefinitionException_IsASettingsException()
    {
        var exception = new DuplicateSettingDefinitionException("sample.key");

        Assert.IsAssignableFrom<SettingsException>(exception);
        Assert.Equal("sample.key", exception.Key);
        Assert.Contains("sample.key", exception.Message);
    }

    [Fact]
    public void SettingNotFoundException_IsASettingsException()
    {
        var exception = new SettingNotFoundException("sample.key");

        Assert.IsAssignableFrom<SettingsException>(exception);
        Assert.Equal("sample.key", exception.Key);
        Assert.Contains("sample.key", exception.Message);
    }
}
