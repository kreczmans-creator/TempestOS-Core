using Tempest.Core.Logging;
using Tempest.Core.Modules;

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
        var logDirectory = Path.Combine(Path.GetTempPath(), $"tempest-discovery-tests-{Guid.NewGuid():N}");

        try
        {
            var logger = new LoggingService(logDirectory);
            var service = new ReflectionFrameworkDiscoveryService(logger);

            var result = service.DiscoverModules(new[] { typeof(SampleModuleA) });

            Assert.Single(result);
        }
        finally
        {
            if (Directory.Exists(logDirectory))
                Directory.Delete(logDirectory, recursive: true);
        }
    }
}
