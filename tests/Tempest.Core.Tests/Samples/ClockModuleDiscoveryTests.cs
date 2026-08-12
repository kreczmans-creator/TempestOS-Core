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
//
// WP 6.3: Tempest.Samples grew ApiSampleModule, bringing the total to
// thirteen. (RestApiHostedService, added the same Work Package, lives
// in Tempest.Core and is not a module - it is discovered separately, by
// hosted service discovery, and does not affect this count.)
//
// WP 6.7: Tempest.Samples grew ExportImportSampleModule, bringing the
// total to fourteen.
//
// WP 6.6: Tempest.Samples grew LicensingSampleModule, bringing the
// total to fifteen.
//
// WP 7.1A: Tempest.Samples grew EngineeringDataSampleModule, bringing the
// total to sixteen.
//
// WP 7.1C: Tempest.Samples grew MaterialsSampleModule, bringing the
// total to seventeen. (WP 7.1B, Units & Quantities, added no sample
// module - a pure mathematical library with no lifecycle to demonstrate.)
//
// WP 7.1D: Tempest.Samples grew CalculationSampleModule, bringing the
// total to eighteen.
//
// WP 7.1E: Tempest.Samples grew VerificationSampleModule, bringing the
// total to nineteen.
//
// WP 7.3A: Tempest.Samples grew RequirementsSampleModule, bringing the
// total to twenty.
//
// WP 8.1B: Tempest.Samples grew WorkspaceExplorerSampleModule (the
// Project Explorer's own living reference content's navigation area),
// bringing the total to twenty-one.
//
// WP 8.2C: Tempest.Samples grew EngineeringDomainSampleModule (the
// Engineering Domain's own representative object graph), bringing the
// total to twenty-two.
//
// WP 9.0A: Tempest.Samples grew two more - MechanicalWorkspaceExplorerModule
// (the Mechanical Product Structure area's own navigation item, mirroring
// WorkspaceExplorerSampleModule's own identical shape) and
// MechanicalProductStructureSampleModule (the Mechanical Product
// Structure's own representative object graph, mirroring
// EngineeringDomainSampleModule's own identical shape) - bringing the
// total to twenty-four.
//
// WP 9.5A: Tempest.Samples grew two more - ManufacturingWorkspaceExplorerModule
// (the Manufacturing area's own navigation item, mirroring every prior
// discipline's own identical Explorer-module shape) and
// EngineeringManufacturingWorkspaceSampleModule (the Manufacturing
// discipline's own representative object graph, mirroring
// EngineeringVerificationWorkspaceSampleModule's own identical shape) -
// bringing the total to thirty-four (32 confirmed present directly, before
// this Work Package's own +2, by direct count of this method's own
// Assert.Contains lines, per WP 9.3A's own disclosed "never carry a stated
// total forward unchecked" discipline).
//
// WP 12.3B (ADR-0102): DuplicateNavigationSampleModule moved out of
// Tempest.Samples entirely, into Tempest.Validation.FaultInjection as
// DuplicateNavigationModule - it was never a genuine reference module, only
// a deliberately-always-failing fault-injection fixture, and its presence
// here meant every real Tempest.App/Tempest.Desktop run permanently carried
// one module in ModuleState.Failed. Bringing the total back down to
// thirty-three. See FaultInjectionModuleDiscoveryTests.cs (tests/Tempest.Core.Tests/Modules/)
// for its own, now-separate discovery coverage.
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

        Assert.Equal(33, result.Count);
        Assert.Contains(result, d => d.Id == "tempest.samples.clock" && d.ModuleType == typeof(ClockModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.clock.observer" && d.ModuleType == typeof(ClockLifecycleObserverModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.navigation" && d.ModuleType == typeof(NavigationSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.navigation.secondary" && d.ModuleType == typeof(SecondaryNavigationSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.commands" && d.ModuleType == typeof(Tempest.Samples.CommandSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.diagnostics" && d.ModuleType == typeof(Tempest.Samples.DiagnosticsSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.identity" && d.ModuleType == typeof(Tempest.Samples.IdentitySampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.settings" && d.ModuleType == typeof(Tempest.Samples.SettingsSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.audit" && d.ModuleType == typeof(Tempest.Samples.AuditSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.notifications" && d.ModuleType == typeof(Tempest.Samples.NotificationSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.reporting" && d.ModuleType == typeof(Tempest.Samples.ReportingSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.api" && d.ModuleType == typeof(Tempest.Samples.ApiSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.exportimport" && d.ModuleType == typeof(Tempest.Samples.ExportImportSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.licensing" && d.ModuleType == typeof(Tempest.Samples.LicensingSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.engineeringdata" && d.ModuleType == typeof(Tempest.Samples.EngineeringDataSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.materials" && d.ModuleType == typeof(Tempest.Samples.MaterialsSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.calculations" && d.ModuleType == typeof(Tempest.Samples.CalculationSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.verification" && d.ModuleType == typeof(Tempest.Samples.VerificationSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.requirements" && d.ModuleType == typeof(Tempest.Samples.RequirementsSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.workspace-explorer" && d.ModuleType == typeof(Tempest.Samples.WorkspaceExplorerSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.engineeringdomain" && d.ModuleType == typeof(Tempest.Samples.EngineeringDomainSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.mechanical-workspace-explorer" && d.ModuleType == typeof(Tempest.Samples.MechanicalWorkspaceExplorerModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.mechanicalproductstructure" && d.ModuleType == typeof(Tempest.Samples.MechanicalProductStructureSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.requirements-workspace-explorer" && d.ModuleType == typeof(Tempest.Samples.RequirementsWorkspaceExplorerModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.requirementsworkspace" && d.ModuleType == typeof(Tempest.Samples.RequirementsWorkspaceSampleModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.calculations-workspace-explorer" && d.ModuleType == typeof(Tempest.Samples.CalculationsWorkspaceExplorerModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.workspacecalculations" && d.ModuleType == typeof(Tempest.Samples.EngineeringCalculationsWorkspaceSampleModule));
        // WP 9.4A: +2 (Documents Workspace Explorer, Documents Workspace Sample).
        Assert.Contains(result, d => d.Id == "tempest.samples.documents-workspace-explorer" && d.ModuleType == typeof(Tempest.Samples.DocumentsWorkspaceExplorerModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.workspacedocuments" && d.ModuleType == typeof(Tempest.Samples.EngineeringDocumentsWorkspaceSampleModule));
        // WP 9.3A: +2 (Verification Workspace Explorer, Verification Workspace Sample).
        Assert.Contains(result, d => d.Id == "tempest.samples.verification-workspace-explorer" && d.ModuleType == typeof(Tempest.Samples.VerificationWorkspaceExplorerModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.workspaceverification" && d.ModuleType == typeof(Tempest.Samples.EngineeringVerificationWorkspaceSampleModule));
        // WP 9.5A: +2 (Manufacturing Workspace Explorer, Manufacturing Workspace Sample).
        Assert.Contains(result, d => d.Id == "tempest.samples.manufacturing-workspace-explorer" && d.ModuleType == typeof(Tempest.Samples.ManufacturingWorkspaceExplorerModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.workspacemanufacturing" && d.ModuleType == typeof(Tempest.Samples.EngineeringManufacturingWorkspaceSampleModule));
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
