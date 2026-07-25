using Tempest.Core.BackgroundServices;

namespace Tempest.Core.Tests.BackgroundServices;

// Discovery is scoped precisely to an explicit candidate-type list, never
// to a full, unrestricted AppDomain scan, mirroring exactly the reasoning
// Module Discovery's own tests already established (Sample Module
// Architecture.md's Testing Strategy) - the test assembly contains many
// unrelated IHostedService/IModule fixtures across other test files.
public class HostedServiceDiscoveryServiceTests
{
    [Fact]
    public void DiscoverHostedServiceTypes_FindsConcreteImplementations_InDeterministicOrder()
    {
        var service = new HostedServiceDiscoveryService();

        var result = service.DiscoverHostedServiceTypes(
            [typeof(GammaHostedService), typeof(AlphaHostedService), typeof(BetaHostedService)]);

        Assert.Equal(
            [typeof(AlphaHostedService), typeof(BetaHostedService), typeof(GammaHostedService)],
            result);
    }

    [Fact]
    public void DiscoverHostedServiceTypes_NeverInstantiatesACandidate()
    {
        // ConstructorInjectedHostedService's constructor requires ILogger/IEventBus,
        // neither supplied here - if discovery ever tried to construct it (the
        // way Module Discovery's own metadata probe would for an
        // attribute-less module), this would throw. It does not, because
        // hosted services carry no metadata for discovery to read at all.
        var service = new HostedServiceDiscoveryService();

        var result = service.DiscoverHostedServiceTypes([typeof(ConstructorInjectedHostedService)]);

        Assert.Equal([typeof(ConstructorInjectedHostedService)], result);
    }

    [Fact]
    public void DiscoverHostedServiceTypes_ExcludesInterfaces()
    {
        var service = new HostedServiceDiscoveryService();

        var result = service.DiscoverHostedServiceTypes([typeof(IHostedService), typeof(AlphaHostedService)]);

        Assert.Equal([typeof(AlphaHostedService)], result);
    }

    [Fact]
    public void DiscoverHostedServiceTypes_ExcludesAbstractClasses()
    {
        var service = new HostedServiceDiscoveryService();

        var result = service.DiscoverHostedServiceTypes([typeof(AbstractHostedService), typeof(AlphaHostedService)]);

        Assert.Equal([typeof(AlphaHostedService)], result);
    }

    [Fact]
    public void DiscoverHostedServiceTypes_ExcludesOpenGenericTypeDefinitions()
    {
        var service = new HostedServiceDiscoveryService();

        var result = service.DiscoverHostedServiceTypes([typeof(GenericHostedService<>), typeof(AlphaHostedService)]);

        Assert.Equal([typeof(AlphaHostedService)], result);
    }

    [Fact]
    public void DiscoverHostedServiceTypes_ExcludesTypesNotImplementingIHostedService()
    {
        var service = new HostedServiceDiscoveryService();

        var result = service.DiscoverHostedServiceTypes([typeof(string), typeof(AlphaHostedService)]);

        Assert.Equal([typeof(AlphaHostedService)], result);
    }

    [Fact]
    public void DiscoverHostedServiceTypes_ScopedToAssembly_FindsCriticalMarkerImplementations()
    {
        // ICriticalBackgroundService extends IHostedService - a critical
        // service must be discovered exactly like an ordinary one.
        var service = new HostedServiceDiscoveryService();

        var result = service.DiscoverHostedServiceTypes([typeof(CriticalStartFailureHostedService)]);

        Assert.Equal([typeof(CriticalStartFailureHostedService)], result);
    }

    [Fact]
    public void DiscoverHostedServiceTypes_CalledTwice_ReturnsTheSameResultBothTimes()
    {
        var service = new HostedServiceDiscoveryService();
        var candidates = new[] { typeof(GammaHostedService), typeof(AlphaHostedService) };

        var first = service.DiscoverHostedServiceTypes(candidates);
        var second = service.DiscoverHostedServiceTypes(candidates);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DiscoverHostedServiceTypes_FreshServiceInstance_IsRepeatable()
    {
        var candidates = new[] { typeof(GammaHostedService), typeof(AlphaHostedService) };

        var first = new HostedServiceDiscoveryService().DiscoverHostedServiceTypes(candidates);
        var second = new HostedServiceDiscoveryService().DiscoverHostedServiceTypes(candidates);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DiscoverHostedServiceTypes_ScopedToSampleAssembly_FindsOnlyItsOwnHostedServiceTypes()
    {
        // Assembly-scoped discovery, mirroring Module/Plugin Discovery's own
        // established pattern - proves the public DiscoverHostedServiceTypes()
        // overload (assembly-scanning, not the internal explicit-list seam)
        // genuinely works against a real, compiled assembly.
        var service = new HostedServiceDiscoveryService([typeof(AlphaHostedService).Assembly]);

        var result = service.DiscoverHostedServiceTypes();

        Assert.All(result, type => Assert.Equal(typeof(AlphaHostedService).Assembly, type.Assembly));
        Assert.Contains(typeof(AlphaHostedService), result);
        Assert.Contains(typeof(BetaHostedService), result);
        Assert.Contains(typeof(GammaHostedService), result);
        Assert.DoesNotContain(typeof(AbstractHostedService), result);
        Assert.DoesNotContain(typeof(IHostedService), result);
    }
}
