using Tempest.Core.DependencyInjection;
using Tempest.Core.Licensing;

namespace Tempest.Core.Tests.Licensing;

// Proves the approved Licensing contract against the real LicenseProvider
// implementation - a read-only, O(1) capability lookup over an
// already-validated, immutable license, safe for concurrent reads by
// construction.
public class LicenseProviderTests
{
    [Fact]
    public void CurrentLicense_ReturnsTheLicenseSuppliedAtConstruction()
    {
        var license = new License("Acme Corp", null, ["feature.a"]);
        var provider = new LicenseProvider(license);

        Assert.Same(license, provider.CurrentLicense);
    }

    [Fact]
    public void HasCapability_EnabledCapability_ReturnsTrue()
    {
        var provider = new LicenseProvider(new License("Acme Corp", null, ["feature.a", "feature.b"]));

        Assert.True(provider.HasCapability("feature.a"));
        Assert.True(provider.HasCapability("feature.b"));
    }

    [Fact]
    public void HasCapability_NotEnabledCapability_ReturnsFalse()
    {
        var provider = new LicenseProvider(new License("Acme Corp", null, ["feature.a"]));

        Assert.False(provider.HasCapability("feature.z"));
    }

    [Fact]
    public void HasCapability_NoCapabilitiesEnabled_ReturnsFalseForAnyKey()
    {
        var provider = new LicenseProvider(new License(LicenseValidator.UnlicensedLicenseeName, null, []));

        Assert.False(provider.HasCapability("anything"));
    }

    [Fact]
    public void HasCapability_IsCaseSensitive()
    {
        var provider = new LicenseProvider(new License("Acme Corp", null, ["Feature.A"]));

        Assert.False(provider.HasCapability("feature.a"));
    }

    [Fact]
    public void HasCapability_NullCapability_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new LicenseProvider(new License("Acme Corp", null, [])).HasCapability(null!));

    [Fact]
    public void HasCapability_BlankCapability_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => new LicenseProvider(new License("Acme Corp", null, [])).HasCapability("   "));

    [Fact]
    public void Constructor_NullLicense_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new LicenseProvider(null!));

    // ------------------------------------------------------------------
    // Concurrency (immutable by construction, not by explicit synchronization)
    // ------------------------------------------------------------------

    [Fact]
    public async Task HasCapability_ManyConcurrentCalls_AllReturnTheCorrectResult()
    {
        var provider = new LicenseProvider(new License("Acme Corp", null, ["feature.a"]));

        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => provider.HasCapability(i % 2 == 0 ? "feature.a" : "feature.z")))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.All(results.Where((_, i) => i % 2 == 0), Assert.True);
        Assert.All(results.Where((_, i) => i % 2 != 0), Assert.False);
    }

    // ------------------------------------------------------------------
    // Platform Service registration (Composition-Root-constructed,
    // AddInstance, mirroring IPlatformVersionProvider/IDiagnosticsProvider)
    // ------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_AddInstanceRegistration_ResolvesILicenseProviderToTheSameInstance()
    {
        var licenseProvider = new LicenseProvider(new License("Acme Corp", null, []));
        var services = new ServiceCollection();
        services.AddInstance<ILicenseProvider>(licenseProvider);
        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService(typeof(ILicenseProvider));

        Assert.Same(licenseProvider, resolved);
    }
}
