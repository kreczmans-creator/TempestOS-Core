using Tempest.Workspace.Integration.DashboardExport;
using Tempest.Core.Configuration;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

public class DashboardExportOptionsTests
{
    private static IConfigurationProvider BuildConfiguration(params KeyValuePair<string, string>[] entries) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(entries)).Build();

    [Fact]
    public void FromConfiguration_NothingConfigured_UsesDefaultIntervalAndAPlatformDirectory()
    {
        var options = DashboardExportOptions.FromConfiguration(BuildConfiguration());

        Assert.Equal(DashboardExportOptions.DefaultIntervalSeconds, options.IntervalSeconds);
        Assert.False(string.IsNullOrWhiteSpace(options.ExportDirectory));
        Assert.Contains("dashboard-export", options.ExportDirectory);

        if (OperatingSystem.IsWindows())
            Assert.Contains("Tempest", options.ExportDirectory);
        else
            Assert.Equal("/var/lib/tempest/dashboard-export", options.ExportDirectory);
    }

    [Fact]
    public void FromConfiguration_DirectoryConfigured_UsesIt()
    {
        var options = DashboardExportOptions.FromConfiguration(BuildConfiguration(
            new KeyValuePair<string, string>(DashboardExportOptions.ExportDirectoryConfigurationKey, "/custom/export/dir")));

        Assert.Equal("/custom/export/dir", options.ExportDirectory);
    }

    [Fact]
    public void FromConfiguration_IntervalConfigured_UsesIt()
    {
        var options = DashboardExportOptions.FromConfiguration(BuildConfiguration(
            new KeyValuePair<string, string>(DashboardExportOptions.IntervalSecondsConfigurationKey, "60")));

        Assert.Equal(60, options.IntervalSeconds);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("")]
    public void FromConfiguration_IntervalInvalid_FallsBackToDefault(string configuredValue)
    {
        var options = DashboardExportOptions.FromConfiguration(BuildConfiguration(
            new KeyValuePair<string, string>(DashboardExportOptions.IntervalSecondsConfigurationKey, configuredValue)));

        Assert.Equal(DashboardExportOptions.DefaultIntervalSeconds, options.IntervalSeconds);
    }

    [Fact]
    public void FromConfiguration_DirectoryBlank_FallsBackToDefault()
    {
        var options = DashboardExportOptions.FromConfiguration(BuildConfiguration(
            new KeyValuePair<string, string>(DashboardExportOptions.ExportDirectoryConfigurationKey, "   ")));

        Assert.Contains("dashboard-export", options.ExportDirectory);
    }

    [Fact]
    public void FromConfiguration_NullProvider_Throws() =>
        Assert.Throws<ArgumentNullException>(() => DashboardExportOptions.FromConfiguration(null!));
}
