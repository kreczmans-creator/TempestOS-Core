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
//
// WP 4.4E: Tempest.Samples now compiles two real modules - ClockModule and
// its companion, ClockLifecycleObserverModule - so an assembly-scoped scan
// finds both. Tests that need exactly ClockModule alone use the type-list
// overload instead; tests scoping to the whole assembly now assert on both.
//
// WP 5.0B: Tempest.Samples grew three more real modules -
// NavigationSampleModule, SecondaryNavigationSampleModule, and
// DuplicateNavigationSampleModule (see NavigationSampleModuleIntegrationTests)
// - so an assembly-scoped scan now finds five modules in total.
//
// WP 5.1B: Tempest.Samples grew CommandSampleModule, bringing the total to
// six.
//
// WP 5.2: Tempest.Samples grew DiagnosticsSampleModule, bringing the total
// to seven.
//
// WP 6.1: Tempest.Samples grew IdentitySampleModule, bringing the total to
// eight.
//
// WP 6.4: Tempest.Samples grew SettingsSampleModule, bringing the total to
// nine.
//
// WP 6.5: Tempest.Samples grew AuditSampleModule, bringing the total to
// ten.
//
// WP 6.2: Tempest.Samples grew NotificationSampleModule, bringing the
// total to eleven. (NotificationSampleHostedService, added the same Work
// Package, is not a module - it is discovered separately, by hosted
// service discovery, and does not affect this count.)
//
// WP 6.0: Tempest.Samples grew ReportingSampleModule, bringing the
// total to twelve.
public class ClockModuleDiscoveryTests
{
    // ----------------------------------------------------------------
    // Successful discovery / module metadata correctness
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_ScopedToClockModuleType_FindsClockModule_WithCorrectMetadata_ViaAttribute()
    {
        // ClockModule now carries [ModuleMetadata] (ADR-0027), so Discovery
        // reads its Id/Name/Version from the attribute without ever
        // constructing it - proven here by the fact this succeeds with no
        // IEventBus available anywhere in this test, even though
        // ClockModule's own constructor requires one.
        var service = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]);

        var result = service.DiscoverModules([typeof(ClockModule)]);

        var descriptor = Assert.Single(result);
        Assert.Equal("tempest.samples.clock", descriptor.Id);
        Assert.Equal("System Clock", descriptor.Name);
        Assert.Equal("1.0.0", descriptor.Version);
        Assert.Equal(typeof(ClockModule), descriptor.ModuleType);
    }

    [Fact]
    public void DiscoverModules_ScopedToObserverType_FindsClockLifecycleObserverModule_WithCorrectMetadata_ViaAttribute()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules([typeof(ClockLifecycleObserverModule)]);

        var descriptor = Assert.Single(result);
        Assert.Equal("tempest.samples.clock.observer", descriptor.Id);
        Assert.Equal("Clock Lifecycle Observer", descriptor.Name);
        Assert.Equal("1.0.0", descriptor.Version);
        Assert.Equal(typeof(ClockLifecycleObserverModule), descriptor.ModuleType);
    }

    [Fact]
    public void DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule()
    {
        var service = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]);

        var result = service.DiscoverModules();

        Assert.Equal(12, result.Count);
        Assert.Contains(result, d => d.Id == "tempest.samples.clock" && d.ModuleType == typeof(ClockModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.clock.observer" && d.ModuleType == typeof(ClockLifecycleObserverModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.navigation" && d.ModuleType == typeof(NavigationSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.navigation.secondary" && d.ModuleType == typeof(SecondaryNavigationSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.navigation.zzz-duplicate" && d.ModuleType == typeof(DuplicateNavigationSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.commands" && d.ModuleType == typeof(Tempest.Samples.CommandSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.diagnostics" && d.ModuleType == typeof(Tempest.Samples.DiagnosticsSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.identity" && d.ModuleType == typeof(Tempest.Samples.IdentitySampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.settings" && d.ModuleType == typeof(Tempest.Samples.SettingsSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.audit" && d.ModuleType == typeof(Tempest.Samples.AuditSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.notifications" && d.ModuleType == typeof(Tempest.Samples.NotificationSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.reporting" && d.ModuleType == typeof(Tempest.Samples.ReportingSampleModule));
    }

    // ----------------------------------------------------------------
    // Repeatable discovery
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_CalledTwiceOnTheSameType_ReturnsTheSameResultBothTimes()
    {
        var service = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]);

        var first = service.DiscoverModules([typeof(ClockModule)]);
        var second = service.DiscoverModules([typeof(ClockModule)]);

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
        var first = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]).DiscoverModules([typeof(ClockModule)]);
        var second = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly]).DiscoverModules([typeof(ClockModule)]);

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
