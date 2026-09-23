using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Workspace;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `ADR-0155`: the Engineering Cockpit's "Recent Projects" card is now the
/// "Project Health" card — the same card, each Project row carrying its
/// own <see cref="CockpitProjectHealth"/> word and score text exactly as
/// <see cref="EngineeringCockpit.ProjectHealth"/> reports them (never
/// colour alone). This proves the binding against a real Project.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CockpitProjectHealthCardTests
{
    [AvaloniaFact]
    public async Task Refresh_RendersEachProjectsOwnHealthWordAndScore_OnTheProjectHealthCard()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-0151", "Health Card Project");

            var workspace = host.Workspace!;
            var cockpit = workspace.Cockpit;
            var view = new CockpitView(
                cockpit,
                workspace.Navigation.Areas,
                onContinue: () => Task.CompletedTask,
                onOpenRecent: _ => Task.CompletedTask,
                onOpenCommandPalette: () => { },
                onSwitchArea: _ => { });
            await view.RefreshAsync();

            var expected = cockpit.GetProjectHealth(project.Id);
            Assert.NotNull(expected);
            Assert.Equal(EngineeringHealthStatus.Unknown, expected!.Health);

            // The hero readout is titled "Project Health" too — the card is
            // the one that names the Project.
            var cards = view.GetLogicalDescendants().OfType<CockpitCardControl>().Where(c => c.Title == "Project Health").ToList();
            var card = cards.SingleOrDefault(c => CardText(c).Contains(expected.Label, StringComparison.Ordinal));
            Assert.NotNull(card);

            var text = CardText(card!);
            Assert.Contains("P-0151 Health Card Project", text);
            Assert.Contains("UNKNOWN", text);
            Assert.Contains(expected.HealthScoreDisplay, text);
            Assert.DoesNotContain("No projects yet.", text);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static string CardText(CockpitCardControl card) =>
        string.Join(" ", card.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text));
}
