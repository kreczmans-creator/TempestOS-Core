using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Workspace.Shell;
using Tempest.Desktop.Views;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Manufacturing;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;
using Tempest.Core.Calculations;

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
/// <c>DeclaredCapabilityView</c> and nothing else, and an earlier audit
/// found the global Tasks module still declaring that no task surface had
/// been built after one was. Both are the same failure: a descriptor and a
/// surface disagreeing, with nothing checking.
/// </para>
/// <para>
/// <b>`WP 19.2B` (`TD-81`): the "Declared" half of this audit is retired,
/// not merely passing trivially.</b> Every remaining global module and
/// project area is now genuinely <c>Implemented</c> — the five mock-up
/// modules and the two undelivered project tabs were removed from their
/// own descriptor tables rather than dimmed, and <c>DeclaredCapabilityView</c>
/// itself is deleted. The three tests this file used to run for the
/// "Declared" side (<c>EveryDeclaredProjectArea_SaysWhatIsMissing_AndNamesWhatTracksIt</c>,
/// <c>EveryDeclaredShellModule_RendersItsOwnCapabilityCard</c>,
/// <c>TheShellTasksModule_DoesNotDenyTheProjectTasksSurfaceThatExists</c>)
/// would now iterate an empty set or call <see cref="ShellAreas.For"/> on
/// an area with no descriptor at all — deleted rather than kept passing
/// for the wrong reason. What remains checks the surviving half: every
/// declared area still renders real content, and still names no
/// outstanding debt.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProductGapReconciliationAuditTests
{
    // ================================================================
    // Project areas: every one is real, and none is unbuilt
    // ================================================================

    [AvaloniaFact]
    public async Task EveryProjectArea_RendersRealContent_AndNamesNoOutstandingDebt()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var project = await host.ProjectDirectory!.CreateAsync("P-AUDIT", "Audit");

            foreach (var descriptor in ProjectAreas.All)
            {
                await host.ShellNavigator!.OpenProjectAsync(project.Id, descriptor.Area);
                await window.RenderCurrentModuleAsync();

                var workspace = window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().Distinct().Single();
                var selected = SelectedAreaContent(workspace);

                Assert.True(selected is not null, $"Project area '{descriptor.Title}' renders no content at all.");

                Assert.True(
                    descriptor.Availability == NavigationAvailability.Implemented,
                    $"Project area '{descriptor.Title}' is declared but not implemented — `WP 19.2B` removed every such area from the tab strip rather than shipping it dimmed.");

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
