using Tempest.Core.Modules;
using Tempest.Core.Tests.Logging;

namespace Tempest.Core.Tests.Modules;

public class ReflectionFrameworkDiscoveryServiceTests
{
    [Fact]
    public void DiscoverModules_ReturnsValidModulesInAlphabeticalOrder()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules(new[]
        {
            typeof(SampleModuleC),
            typeof(SampleModuleA),
            typeof(SampleModuleB),
        });

        Assert.Equal(3, result.Count);
        Assert.Equal("tempest.sample.alpha", result[0].Id);
        Assert.Equal("tempest.sample.beta", result[1].Id);
        Assert.Equal("tempest.sample.gamma", result[2].Id);
    }

    [Fact]
    public void DiscoverModules_IgnoresInterfacesAbstractClassesGenericAndNonModuleTypes()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules(new[]
        {
            typeof(IModule),
            typeof(AbstractModule),
            typeof(GenericModule<>),
            typeof(NotAModule),
            typeof(SampleModuleA),
        });

        var descriptor = Assert.Single(result);
        Assert.Equal("tempest.sample.alpha", descriptor.Id);
        Assert.Equal(typeof(SampleModuleA), descriptor.ModuleType);
    }

    [Fact]
    public void DiscoverModules_ThrowsDuplicateModuleIdException_WhenIdsCollide()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var exception = Assert.Throws<DuplicateModuleIdException>(() =>
            service.DiscoverModules(new[]
            {
                typeof(SampleModuleA),
                typeof(SampleModuleA),
            }));

        Assert.Equal("tempest.sample.alpha", exception.ModuleId);
    }

    [Fact]
    public void DiscoverModules_ThrowsModuleDiscoveryException_WhenMetadataIsInvalid()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        Assert.Throws<ModuleDiscoveryException>(() =>
            service.DiscoverModules(new[] { typeof(InvalidIdModule) }));
    }

    // WP 5.3: a module with a constructor requiring a dependency but no
    // [ModuleMetadata] previously surfaced as a raw, unhelpful
    // MissingMethodException from Activator.CreateInstance - the exact
    // pitfall "Building a Module.md" has long warned a module author about
    // without the code itself ever enforcing it. Now a clear
    // ModuleDiscoveryException naming the actual fix.
    [Fact]
    public void DiscoverModules_ThrowsModuleDiscoveryExceptionWithActionableMessage_WhenTypeHasNoParameterlessConstructorAndNoMetadataAttribute()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var exception = Assert.Throws<ModuleDiscoveryException>(() =>
            service.DiscoverModules(new[] { typeof(ConstructorDependencyModuleWithoutMetadata) }));

        Assert.Contains("ConstructorDependencyModuleWithoutMetadata", exception.Message);
        Assert.Contains("ModuleMetadataAttribute", exception.Message);
        Assert.Contains("parameterless constructor", exception.Message);
    }

    [Fact]
    public void DiscoverModules_DoesNotThrowMissingMethodException_WhenTypeHasNoParameterlessConstructorAndNoMetadataAttribute()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var exception = Record.Exception(() =>
            service.DiscoverModules(new[] { typeof(ConstructorDependencyModuleWithoutMetadata) }));

        Assert.IsType<ModuleDiscoveryException>(exception);
    }

    [Fact]
    public void DiscoverModules_ScansSuppliedAssembly_ReturnsEmptyWhenNoModulesPresent()
    {
        // Tempest.Core does not (yet) contain any concrete IModule
        // implementations, so a real assembly scan should return an
        // empty result rather than throwing.
        var service = new ReflectionFrameworkDiscoveryService(new[] { typeof(IModule).Assembly });

        var result = service.DiscoverModules();

        Assert.Empty(result);
    }

    [Fact]
    public void DiscoverModules_WithLogger_DoesNotThrowAndRecordsProgress()
    {
        var logger = new RecordingLogger();
        var service = new ReflectionFrameworkDiscoveryService(logger);

        var result = service.DiscoverModules(new[] { typeof(SampleModuleA) });

        Assert.Single(result);
        Assert.NotEmpty(logger.Messages);
    }

    // ----------------------------------------------------------------
    // IFaultInjectionModule default-exclusion filter (WP 12.3B, ADR-0102)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_DefaultConstruction_ExcludesFaultInjectionModule_EvenWhenPassedExplicitly()
    {
        // The candidate-type-list overload, not just the AppDomain-scanning
        // one - the filter applies to both identically (this is what makes
        // it a genuine guarantee rather than a fragile "don't scan for it"
        // convention).
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules(new[] { typeof(SampleFaultInjectionModule), typeof(SampleModuleA) });

        var descriptor = Assert.Single(result);
        Assert.Equal("tempest.sample.alpha", descriptor.Id);
    }

    [Fact]
    public void DiscoverModules_IncludeFaultInjectionModulesTrue_DiscoversIt()
    {
        var service = new ReflectionFrameworkDiscoveryService(logger: null, includeFaultInjectionModules: true);

        var result = service.DiscoverModules(new[] { typeof(SampleFaultInjectionModule), typeof(SampleModuleA) });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, d => d.Id == "tempest.sample.fault-injection" && d.ModuleType == typeof(SampleFaultInjectionModule));
    }

    [Fact]
    public void DiscoverModules_AssemblyScanningConstructor_DefaultsToExcludingFaultInjectionModules()
    {
        var service = new ReflectionFrameworkDiscoveryService(new[] { typeof(SampleFaultInjectionModule).Assembly });

        var result = service.DiscoverModules(new[] { typeof(SampleFaultInjectionModule) });

        Assert.Empty(result);
    }
}
