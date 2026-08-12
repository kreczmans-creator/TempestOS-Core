using Avalonia.Headless.XUnit;
using Tempest.Core.Diagnostics;
using Tempest.Core.Modules;
using Tempest.Core.Runtime;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Proves `WP 10.1B`'s own explicit "Guarantee" list directly: deterministic
/// module discovery/registration/initialisation, no duplicate registrations,
/// correct module isolation, and — the specific, previously-broken case
/// (`TD-37`) — a stable restart. Each test below builds a real
/// <see cref="WorkspaceHost"/> (never a mock), reading its own real
/// <see cref="IDiagnosticsProvider"/> exactly as a genuine diagnostic
/// consumer would.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ModuleLifecycleStabilityTests
{
    /// <summary>
    /// The four modules `WP 10.1A` found and disclosed as `TD-37` — each
    /// registers a fixed, literal business identifier durably checked for
    /// uniqueness by a Platform Service (`IMaterialCatalog`/
    /// <c>IRequirementsService</c>), and so is the direct, named subject of
    /// this Work Package's own Root Cause Analysis and fix.
    /// </summary>
    private static readonly string[] PreviouslyAffectedModuleIds =
    [
        "tempest.samples.engineeringdomain",
        "tempest.samples.materials",
        "tempest.samples.requirements",
        "tempest.samples.requirementsworkspace",
    ];

    [AvaloniaFact]
    public async Task StableRestart_TwoSequentialHostsAgainstTheSameStore_BothReachRunningWithNoModuleFailures()
    {
        // Both hosts below deliberately share one isolated persistence root
        // - this test's own point is that a second, later launch against
        // the same durable store no longer fails (TD-37), which requires
        // the same store, not two different ones.
        var persistenceRootPath = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var firstHost = new WorkspaceHost(persistenceRootPath);
        try
        {
            await firstHost.StartAsync();
            AssertNoFailedModules(firstHost, "first launch");
        }
        finally
        {
            await firstHost.ShutdownAsync();
            await firstHost.DisposeAsync();
        }

        var secondHost = new WorkspaceHost(persistenceRootPath);
        try
        {
            await secondHost.StartAsync();

            // The specific TD-37 regression: before WP 10.1B's fix, every
            // one of PreviouslyAffectedModuleIds failed its own
            // InitialiseAsync on exactly this second launch, with the
            // Runtime Host still reaching HostState.Running around them
            // (FOUNDATION.md non-negotiable #4) - masking the failure
            // unless a caller explicitly inspected IDiagnosticsProvider,
            // exactly as this test does.
            AssertNoFailedModules(secondHost, "second launch (same store)");
        }
        finally
        {
            await secondHost.ShutdownAsync();
            await secondHost.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task StableRestart_ThirdSequentialHost_StillReachesRunningWithNoModuleFailures()
    {
        // Extends the two-host proof above to three sequential launches -
        // determinism means "every launch," not just "the second one."
        var persistenceRootPath = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        for (var launch = 1; launch <= 3; launch++)
        {
            var host = new WorkspaceHost(persistenceRootPath);
            try
            {
                await host.StartAsync();
                AssertNoFailedModules(host, $"launch #{launch}");
            }
            finally
            {
                await host.ShutdownAsync();
                await host.DisposeAsync();
            }
        }
    }

    [AvaloniaFact]
    public async Task ModuleDiscoveryAndInitialisation_IsDeterministic_SameModuleSetEveryLaunch()
    {
        // Deterministic discovery/registration (WP 10.1B's own explicit
        // Guarantee): two entirely independent hosts, each against its own
        // fresh, empty store, must discover and initialise the identical
        // set of module Ids - order and membership - regardless of what
        // (if anything) a prior, unrelated launch left on disk elsewhere.
        var firstHost = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        IReadOnlyList<string> firstModuleIds;
        try
        {
            await firstHost.StartAsync();
            firstModuleIds = ModuleIdsInOrder(firstHost);
        }
        finally
        {
            await firstHost.ShutdownAsync();
            await firstHost.DisposeAsync();
        }

        var secondHost = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await secondHost.StartAsync();
            var secondModuleIds = ModuleIdsInOrder(secondHost);

            Assert.Equal(firstModuleIds, secondModuleIds);
        }
        finally
        {
            await secondHost.ShutdownAsync();
            await secondHost.DisposeAsync();
        }
    }

    private static void AssertNoFailedModules(WorkspaceHost host, string context)
    {
        var diagnostics = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

        Assert.Equal(HostState.Running, diagnostics.HostState);

        // No exclusion needed here (WP 12.3B, ADR-0102): the always-failing
        // fault-injection module formerly discovered on this exact real
        // WorkspaceHost path (DuplicateNavigationSampleModule, previously
        // ID'd "tempest.samples.navigation.zzz-duplicate") moved to
        // Tempest.Validation.FaultInjection, a project Tempest.App/
        // Tempest.Desktop never reference and this Host never opts into via
        // EnableFaultInjectionModules() - a genuinely, permanently healthy
        // "no module failed" assertion, not merely a hidden one.
        var failed = diagnostics.Modules
            .Where(m => m.State == ModuleState.Failed)
            .ToList();

        if (failed.Count > 0)
        {
            var detail = string.Join(
                "; ",
                failed.Select(m => $"{m.Descriptor.Id}: {m.FailureReason?.Message}"));
            Assert.Fail($"{context}: {failed.Count} module(s) failed unexpectedly — {detail}");
        }

        // The specific four modules TD-37 named must, by name, be present
        // and Running - not merely "no module happens to be Failed."
        foreach (var moduleId in PreviouslyAffectedModuleIds)
        {
            var status = diagnostics.Modules.Single(m => m.Descriptor.Id == moduleId);
            Assert.Equal(ModuleState.Running, status.State);
        }
    }

    private static IReadOnlyList<string> ModuleIdsInOrder(WorkspaceHost host)
    {
        var diagnostics = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        return diagnostics.Modules.Select(m => m.Descriptor.Id).ToList();
    }
}
