using Tempest.Core.DependencyInjection;
using Tempest.Core.Versioning;

namespace Tempest.Core.Tests.Versioning;

/// <summary>
/// A stand-in for a runtime service consuming the platform version via
/// ordinary constructor injection, proving <see cref="IPlatformVersionProvider"/>
/// is genuinely resolvable by any DI-registered service — not merely
/// constructible directly — the same way
/// <c>ConfigurationDependencyInjectionTests</c> proves it for
/// <see cref="Configuration.IConfigurationProvider"/>.
/// </summary>
internal sealed class VersionConsumingService
{
    public VersionConsumingService(IPlatformVersionProvider platformVersionProvider)
    {
        SemanticVersion = platformVersionProvider.Version.SemanticVersion;
    }

    public string SemanticVersion { get; }
}

public class PlatformVersionDependencyInjectionTests
{
    [Fact]
    public void GetService_ResolvesPlatformVersionProvider_RegisteredAsInstance()
    {
        var versionProvider = new PlatformVersionProvider();

        var services = new ServiceCollection();
        services.AddInstance<IPlatformVersionProvider>(versionProvider);

        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService<IPlatformVersionProvider>();

        Assert.Same(versionProvider, resolved);
    }

    [Fact]
    public void GetService_RuntimeServiceDependingOnPlatformVersion_ReceivesItViaConstructorInjection()
    {
        var versionProvider = new PlatformVersionProvider();

        var services = new ServiceCollection();
        services.AddInstance<IPlatformVersionProvider>(versionProvider);
        services.Transient<VersionConsumingService>();

        var provider = new TempestServiceProvider(services);

        var consumingService = provider.GetService<VersionConsumingService>();

        Assert.Equal(versionProvider.Version.SemanticVersion, consumingService.SemanticVersion);
    }

    [Fact]
    public void GetService_PlatformVersionProvider_IsTheSameInstanceAcrossMultipleConsumers()
    {
        var versionProvider = new PlatformVersionProvider();

        var services = new ServiceCollection();
        services.AddInstance<IPlatformVersionProvider>(versionProvider);
        services.Transient<VersionConsumingService>();

        var provider = new TempestServiceProvider(services);

        var directlyResolved = provider.GetService<IPlatformVersionProvider>();
        var consumingService = provider.GetService<VersionConsumingService>();

        Assert.Same(versionProvider, directlyResolved);
        Assert.Equal(versionProvider.Version.SemanticVersion, consumingService.SemanticVersion);
    }
}
