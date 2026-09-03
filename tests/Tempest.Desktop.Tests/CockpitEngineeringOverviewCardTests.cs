using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// <see cref="EngineeringCockpit.KpiCards"/> — the one real cross-discipline
/// KPI aggregate (Requirements/Verification/Calculations/Documentation/
/// Review/Risks totals, `ADR-0103`) — was fully computed but never
/// rendered anywhere in <see cref="CockpitView"/> (`WP-Z4` Productisation
/// Phase 1, P1). This proves it now reaches the Cockpit as its own card.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CockpitEngineeringOverviewCardTests
{
    [AvaloniaFact]
    public async Task Refresh_RendersAnEngineeringOverviewCard_FromTheRealCrossDisciplineAggregate()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var cockpit = ((Workspace)workspace).Cockpit;

            var view = new CockpitView(
                cockpit,
                workspace.Navigation.Areas,
                onContinue: () => { },
                onOpenRecent: _ => { },
                onOpenCommandPalette: () => { },
                onSwitchArea: _ => { });

            var overviewCard = view.GetLogicalDescendants().OfType<CockpitCardControl>().SingleOrDefault(c => c.Title == "Engineering Overview");
            Assert.NotNull(overviewCard);

            // Every label the real aggregate reports must actually appear
            // on the card — never a subset, and never a card that exists
            // but renders nothing from the data it was given.
            var kpis = cockpit.KpiCards;
            Assert.NotEmpty(kpis);
            var cardText = string.Join(" ", overviewCard!.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text));
            foreach (var kpi in kpis)
                Assert.Contains(kpi.Label, cardText);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
