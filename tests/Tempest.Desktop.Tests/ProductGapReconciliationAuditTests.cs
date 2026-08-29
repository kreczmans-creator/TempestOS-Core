using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Shell;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Product Gap Reconciliation audit's own standing evidence: for every
/// area the product <em>declares</em>, what the running application
/// <em>actually shows</em>.
/// </summary>
/// <remarks>
/// <para>
/// The defect this guards against has happened twice — `TD-102` found two
/// project areas marked <c>Implemented</c> that drew a
/// <see cref="DeclaredCapabilityView"/> and nothing else, and this audit
/// found the global Tasks module still declaring that no task surface had
/// been built after one was. Both are the same failure: a descriptor and a
/// surface disagreeing, with nothing checking.
/// </para>
/// <para>
/// So this is deliberately a check of the <b>relationship</b> between the
/// two tables and the real shell, not of any one area's content. It reads
/// the descriptor tables at run time, so an area added later is covered
/// without anyone remembering to add a test.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProductGapReconciliationAuditTests
{
    // ================================================================
    // Project areas: declared status must match the rendered surface
    // ================================================================

    [AvaloniaFact]
    public async Task EveryImplementedProjectArea_RendersARealSurface_NotACapabilityCard()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var project = await host.ProjectDirectory!.CreateAsync("P-AUDIT", "Audit");

            foreach (var descriptor in ProjectAreas.All.Where(d => d.Availability == NavigationAvailability.Implemented))
            {
                // Engineering is implemented by *leaving* the project
                // workspace for the Engineering Workspace, so it has no
                // surface of its own inside the tab host. Asserted
                // separately below rather than excused silently.
                if (descriptor.Area == ProjectArea.Engineering)
                    continue;

                await host.ShellNavigator!.OpenProjectAsync(project.Id, descriptor.Area);
                await window.RenderCurrentModuleAsync();

                var workspace = window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().Distinct().Single();
                var selected = SelectedAreaContent(workspace);

                Assert.True(
                    selected is not DeclaredCapabilityView,
                    $"Project area '{descriptor.Title}' is declared Implemented but renders a DeclaredCapabilityView.");

                Assert.True(
                    descriptor.TrackedBy is null,
                    $"Project area '{descriptor.Title}' is declared Implemented but still names debt '{descriptor.TrackedBy}'.");
            }
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task EveryDeclaredProjectArea_SaysWhatIsMissing_AndNamesWhatTracksIt()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var project = await host.ProjectDirectory!.CreateAsync("P-AUDIT2", "Audit");

            foreach (var descriptor in ProjectAreas.All.Where(d => d.Availability == NavigationAvailability.Declared))
            {
                await host.ShellNavigator!.OpenProjectAsync(project.Id, descriptor.Area);
                await window.RenderCurrentModuleAsync();

                var workspace = window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().Distinct().Single();

                Assert.True(
                    SelectedAreaContent(workspace) is DeclaredCapabilityView,
                    $"Project area '{descriptor.Title}' is declared Declared but does not render a DeclaredCapabilityView.");

                // An unbuilt capability with nothing tracking it is how
                // work gets forgotten.
                Assert.False(
                    string.IsNullOrWhiteSpace(descriptor.TrackedBy),
                    $"Project area '{descriptor.Title}' is unbuilt and names no tracking item.");
            }
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    // ================================================================
    // Shell modules: the same check, one level up
    // ================================================================

    [AvaloniaFact]
    public async Task EveryDeclaredShellModule_RendersItsOwnCapabilityCard()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            foreach (var descriptor in ShellAreas.All.Where(d => d.Availability == NavigationAvailability.Declared))
            {
                await host.ShellNavigator!.GoToModuleAsync(descriptor.Area);
                await window.RenderCurrentModuleAsync();

                var card = window.GetLogicalDescendants().OfType<DeclaredCapabilityView>().Distinct().SingleOrDefault();

                Assert.True(card is not null, $"Shell module '{descriptor.Title}' renders no capability card.");
                Assert.False(string.IsNullOrWhiteSpace(descriptor.TrackedBy), $"Shell module '{descriptor.Title}' names no tracking item.");
            }
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// The global Tasks module and the project Tasks area are different
    /// capabilities, and the audit found them describing each other's
    /// state.
    /// </summary>
    /// <remarks>
    /// The project area is built; a cross-project task module is not. This
    /// asserts that the shell descriptor does not deny the existence of the
    /// surface the product actually ships — which it did, verbatim, until
    /// this audit.
    /// </remarks>
    [Fact]
    public void TheShellTasksModule_DoesNotDenyTheProjectTasksSurfaceThatExists()
    {
        Assert.True(ProjectAreas.IsImplemented(ProjectArea.Tasks));

        var shellNote = ShellAreas.For(ShellArea.Tasks).Note;

        Assert.DoesNotContain("no task surface", shellNote, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("assignment workflow or board has been built", shellNote, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // TD-75: what the sample assembly is actually load-bearing for
    // ================================================================

    /// <summary>
    /// `TD-75` is recorded as a packaging problem. It is not: the product's
    /// own discipline navigation and its engineering calculation catalogue
    /// are declared inside <c>Tempest.Samples</c>.
    /// </summary>
    /// <remarks>
    /// This test states the coupling as it is today so that the size of the
    /// remaining work is visible rather than inferred. It is expected to be
    /// deleted — not weakened — by the work package that moves this content
    /// into the product.
    /// </remarks>
    [Fact]
    public void TheProductsOwnDisciplineNavigationAndCalculations_StillLiveInTheSampleAssembly()
    {
        var samples = typeof(Tempest.Samples.MechanicalWorkspaceExplorerModule).Assembly;
        Assert.Equal("Tempest.Samples", samples.GetName().Name);

        // Every real discipline explorer area takes its identity from a
        // type in the sample assembly.
        foreach (var typeName in new[]
        {
            "MechanicalWorkspaceExplorerModule",
            "DocumentsWorkspaceExplorerModule",
            "RequirementsWorkspaceExplorerModule",
            "VerificationWorkspaceExplorerModule",
            "CalculationsWorkspaceExplorerModule",
            "ManufacturingWorkspaceExplorerModule",
        })
        {
            var type = samples.GetType($"Tempest.Samples.{typeName}");
            Assert.True(type is not null, $"{typeName} was expected in Tempest.Samples.");
            Assert.NotNull(type!.GetField("NavigationItemId"));
        }

        // And so does the engineering calculation catalogue.
        foreach (var typeName in new[]
        {
            "BoltShearCapacityCalculationDefinition",
            "BeamBendingStressCalculationDefinition",
            "BearingLoadCapacityCalculationDefinition",
            "PressureVesselWallThicknessCalculationDefinition",
            "MaterialSelectionMarginCalculationDefinition",
        })
        {
            Assert.True(
                samples.GetType($"Tempest.Samples.{typeName}") is not null,
                $"{typeName} was expected in Tempest.Samples.");
        }
    }

    /// <summary>`TD-75`'s user-visible half, stated as it is: fictional sample content is on screen in a real launch.</summary>
    [AvaloniaFact]
    public async Task FictionalSampleContent_IsStillVisibleToEndUsers_InARealLaunch()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();

            // The sample harness seeds fictional engineering objects into
            // the very same object graph the user's own work lives in —
            // which is what makes this a product problem rather than a
            // build-configuration one.
            var domain = (Tempest.Core.EngineeringDomain.EngineeringDomainContext)host.Services!
                .GetService(typeof(Tempest.Core.EngineeringDomain.EngineeringDomainContext));

            var all = await domain.Repository.ListAllAsync();
            var fictional = all
                .Where(o => (o as Tempest.Core.EngineeringDomain.IHasBusinessIdentifier)?.Identifier?.StartsWith("SAMPLE-", StringComparison.Ordinal) == true)
                .ToList();

            Assert.True(
                fictional.Count > 0,
                "TD-75 records that fictional sample objects are visible to end users. None were found — the row may now be stale.");
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static object? SelectedAreaContent(ProjectWorkspaceView workspace)
    {
        var tabs = workspace.GetLogicalDescendants().OfType<TabControl>().Distinct().First();
        var content = (tabs.SelectedItem as TabItem)?.Content;

        return content is ContentControl host ? host.Content : content;
    }
}
