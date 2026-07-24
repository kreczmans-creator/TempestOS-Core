using Tempest.Core.Modules;
using Tempest.Core.Tests.Modules;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Discovery is scoped precisely, never to a full, unrestricted AppDomain
// scan: the test assembly contains a deliberately-invalid-Id fixture
// (InvalidIdModule, ModuleFixtures.cs) that would fault a genuinely
// unrestricted scan for reasons having nothing to do with ClockModule -
// see Sample Module Architecture.md's Testing Strategy for the full
// reasoning this design follows.
public class ClockModuleDiscoveryTests
{
    // ----------------------------------------------------------------
    // Successful discovery / module metadata correctness
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_ScopedToSampleAssembly_FindsClockModule_WithCorrectMetadata()
    {
        var service = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]);

        var result = service.DiscoverModules();

        var descriptor = Assert.Single(result);
        Assert.Equal("tempest.samples.clock", descriptor.Id);
        Assert.Equal("System Clock", descriptor.Name);
        Assert.Equal("1.0.0", descriptor.Version);
        Assert.Equal(typeof(ClockModule), descriptor.ModuleType);
    }

    // ----------------------------------------------------------------
    // Repeatable discovery
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_CalledTwiceOnTheSameAssembly_ReturnsTheSameResultBothTimes()
    {
        var service = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]);

        var first = service.DiscoverModules();
        var second = service.DiscoverModules();

        var firstDescriptor = Assert.Single(first);
        var secondDescriptor = Assert.Single(second);

        Assert.Equal(firstDescriptor.Id, secondDescriptor.Id);
        Assert.Equal(firstDescriptor.Name, secondDescriptor.Name);
        Assert.Equal(firstDescriptor.Version, secondDescriptor.Version);
        Assert.Equal(firstDescriptor.ModuleType, secondDescriptor.ModuleType);
    }

    [Fact]
    public void DiscoverModules_FreshServiceInstance_IsRepeatable()
    {
        // A new ReflectionFrameworkDiscoveryService instance, scanning the
        // same assembly independently, must find the same module - proving
        // discovery is a deterministic function of the assembly's own
        // content, not of any state held by a specific service instance.
        var first = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]).DiscoverModules();
        var second = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]).DiscoverModules();

        Assert.Equal(Assert.Single(first).Id, Assert.Single(second).Id);
    }

    // ----------------------------------------------------------------
    // Isolation from existing modules
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_AlongsideAnUnrelatedModule_FindsBothWithoutInterference()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules([typeof(ClockModule), typeof(SampleModuleA)]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, d => d.Id == "tempest.samples.clock" && d.ModuleType == typeof(ClockModule));
        Assert.Contains(result, d => d.Id == "tempest.sample.alpha" && d.ModuleType == typeof(SampleModuleA));
    }

    [Fact]
    public void DiscoverModules_ScopedToSampleAssembly_DoesNotIncludeUnrelatedTestFixtures()
    {
        // Scoping Discovery to exactly Tempest.Samples's own compiled
        // assembly means none of the test assembly's own IModule fixtures
        // (SampleModuleA/B/C, HealthyHostTestModuleAlpha, and so on) can
        // possibly appear in the result - proving isolation structurally,
        // not merely by absence of collision in this one run.
        var service = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]);

        var result = service.DiscoverModules();

        Assert.All(result, descriptor => Assert.Equal(typeof(ClockModule).Assembly, descriptor.ModuleType.Assembly));
    }
}
