namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// The live smoke tests are skipped, never failed, unless the operator asks
/// for them (`v0.24.0` task X8): checked here against a hand-built
/// environment, so CI proves the gate without touching its own variables.
/// </summary>
public sealed class XeroLiveFactAttributeTests
{
    [Fact]
    public void WithoutTheLiveVariable_TheTestIsSkipped_WithTheReasonAndTheVariable()
    {
        var reason = new XeroLiveFactAttribute().SkipReason(Settings());

        Assert.NotNull(reason);
        Assert.Contains(XeroLiveSettings.LiveVariable, reason, StringComparison.Ordinal);
        Assert.Contains("never failed", reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("yes")]
    public void AnythingButOneOrTrue_StillSkips(string value)
    {
        Assert.NotNull(new XeroLiveFactAttribute().SkipReason(Settings((XeroLiveSettings.LiveVariable, value))));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData(" TRUE ")]
    public void WithTheLiveVariable_TheTestRuns(string value)
    {
        Assert.Null(new XeroLiveFactAttribute().SkipReason(Settings((XeroLiveSettings.LiveVariable, value))));
    }

    [Fact]
    public void AProbeNeedingAnotherVariable_IsSkippedUntilItIsSet()
    {
        var attribute = new XeroLiveFactAttribute { AlsoRequires = XeroLiveSettings.KeyWindowVariable };

        Assert.Contains(XeroLiveSettings.KeyWindowVariable, attribute.SkipReason(Settings((XeroLiveSettings.LiveVariable, "1"))), StringComparison.Ordinal);
        Assert.Null(attribute.SkipReason(Settings((XeroLiveSettings.LiveVariable, "1"), (XeroLiveSettings.KeyWindowVariable, "1"))));
    }

    [Fact]
    public void TheLiveTests_CarryTheXeroLiveCategory_AndTheLiveAttribute()
    {
        var type = typeof(XeroLiveSmokeTests);
        var trait = Assert.Single(type.CustomAttributes, a => a.AttributeType == typeof(TraitAttribute));
        Assert.Equal("Category", trait.ConstructorArguments[0].Value);
        Assert.Equal("XeroLive", trait.ConstructorArguments[1].Value);

        var tests = type.GetMethods().Where(m => m.GetCustomAttributes(typeof(FactAttribute), inherit: true).Length > 0).ToList();
        Assert.NotEmpty(tests);
        Assert.All(tests, m => Assert.IsType<XeroLiveFactAttribute>(Assert.Single(m.GetCustomAttributes(typeof(FactAttribute), inherit: true))));
    }

    [Fact]
    public void Settings_ReadEachSwitch()
    {
        var settings = Settings(
            (XeroLiveSettings.KeepVariable, "1"),
            (XeroLiveSettings.ConnectVariable, "true"),
            (XeroLiveSettings.AccessTokenVariable, "  token  "),
            (XeroLiveSettings.DataFolderVariable, " C:\\Tempest "),
            (XeroLiveSettings.ReportVariable, "   "));

        Assert.True(settings.Keep);
        Assert.True(settings.Connect);
        Assert.True(settings.UsesSuppliedToken);
        Assert.Equal("C:\\Tempest", settings.DataFolder);
        Assert.Null(settings.ReportPath);
        Assert.False(settings.Enabled);
    }

    private static XeroLiveSettings Settings(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
        return new XeroLiveSettings(name => map.TryGetValue(name, out var value) ? value : null);
    }
}
