using Tempest.Core.Diagnostics;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Runtime;
using Tempest.Samples;
using Tempest.Validation.FaultInjection;

namespace Tempest.Core.Tests.Modules;

// Proves WP 12.3B end-to-end (ADR-0102): the real DuplicateNavigationModule
// (Tempest.Validation.FaultInjection) is invisible to a real, unmodified
// TempestHostBuilder/TempestHost unless a caller explicitly calls
// EnableFaultInjectionModules() - the actual guarantee this Work Package
// closes, since ModuleLifecycleStabilityTests.cs (Tempest.Desktop.Tests)
// previously had to special-case-exclude this exact module's own Id from
// every "no module failed" assertion against the real
// EngineeringWorkspaceComposer/WorkspaceHost path.
//
// ReflectionFrameworkDiscoveryServiceTests.cs already proves the underlying
// filter mechanism at the unit level, against a minimal fixture
// (SampleFaultInjectionModule). This file proves the same guarantee holds
// for the real fault-injection module, through the real Host.
[Collection("Console output capture")]
public class FaultInjectionModuleDiscoveryTests
{
    [Fact]
    public async Task DefaultHost_WithNavigationAndDuplicateCandidates_NeverDiscoversTheFaultInjectionModule()
    {
        // No EnableFaultInjectionModules() call - the exact shape
        // Tempest.App's own EngineeringWorkspaceComposer/WorkspaceHost uses.
        var host = new TempestHostBuilder([typeof(NavigationSampleModule), typeof(DuplicateNavigationModule)]).Build();

        await RunUntilRunningAsync(host, async () =>
        {
            var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

            // Exactly one module - NavigationSampleModule. The candidate
            // list named the fault-injection module directly; it is still
            // never discovered, let alone initialised or failed.
            var status = Assert.Single(diagnosticsProvider.Modules);
            Assert.Equal("tempest.samples.navigation", status.Descriptor.Id);
            Assert.Equal(ModuleState.Running, status.State);

            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EnableFaultInjectionModules_WithNavigationAndDuplicateCandidates_DiscoversAndIsolatesTheFailure()
    {
        var host = new TempestHostBuilder([typeof(NavigationSampleModule), typeof(DuplicateNavigationModule)])
            .EnableFaultInjectionModules()
            .Build();

        await RunUntilRunningAsync(host, async () =>
        {
            var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

            Assert.Equal(2, diagnosticsProvider.Modules.Count);

            var navigation = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == "tempest.samples.navigation");
            Assert.Equal(ModuleState.Running, navigation.State);

            // The duplicate is discovered and attempted, but isolated
            // (ADR-0013) - the Host still reaches Running around it, exactly
            // as it always has for this module.
            var duplicate = diagnosticsProvider.Modules.Single(
                m => m.Descriptor.Id == "tempest.validation.faultinjection.navigation-duplicate");
            Assert.Equal(ModuleState.Failed, duplicate.State);
            Assert.IsType<DuplicateNavigationItemException>(duplicate.FailureReason);

            await Task.CompletedTask;
        });
    }

    private static async Task RunUntilRunningAsync(ITempestHost host, Func<Task> whileRunning)
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            Assert.Equal(HostState.Running, host.State);

            await whileRunning();

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HostState.Stopped, host.State);
    }
}
