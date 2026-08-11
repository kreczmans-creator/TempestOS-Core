using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates `WP 10.1A`'s own central claim: every Engineering Cockpit
/// region is either real, live data (upgraded from a disclosed placeholder
/// by this Work Package) or an honest, disclosed placeholder — never
/// fabricated content — proven against a real, running
/// <see cref="WorkspaceHost"/> and its own real sample data, never a mock.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class EngineeringCockpitTests
{
    [AvaloniaFact]
    public async Task RealData_OpenDecisions_ReflectsTheLiveSeededDecision()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            // WP 9.4A seeded exactly one real, live Decision
            // ("SAMPLE-DEC-001", baselining the GA Drawing configuration).
            Assert.NotEmpty(cockpit.OpenDecisions);
            Assert.Contains(cockpit.OpenDecisions, d => d.Contains("Baseline", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RealData_RiskSummary_ReportsAnHonestRealCountNeverTheOldFixedPlaceholder()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            // `TD-37` fixed, `WP 10.1B`: EngineeringDomainSampleModule's own
            // idempotent-restart guard (root cause: a durable, cross-launch
            // persistence store colliding with its own prior run, never a
            // double-invocation) now lets its full sample graph seed
            // successfully on every genuinely fresh store — this test's own
            // isolated per-test persistence root (WorkspacePersistenceCollection)
            // guarantees exactly that. One real Risk ("SAMPLE-RISK-001",
            // Severity "Medium") is now live, stable Cockpit data, not an
            // honest-empty placeholder path.
            Assert.DoesNotContain("placeholder", cockpit.RiskSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("1 open — 1 Medium.", cockpit.RiskSummary);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `WP 10.7A` (Feature Completion) — the Engineering Health Summary's
    /// own "Risks" card, previously hardcoded <c>IsPlaceholder: true</c>
    /// regardless of any real data, now reads the identical real
    /// <see cref="EngineeringCockpit"/>-internal risk read
    /// <see cref="RealData_RiskSummary_ReportsAnHonestRealCountNeverTheOldFixedPlaceholder"/>
    /// already proves ("1 open — 1 Medium.") — the same one real, live
    /// Risk, now surfaced as a genuine KPI count too.
    /// </summary>
    [AvaloniaFact]
    public async Task RealData_KpiCards_RisksCard_ReportsARealCountNeverTheOldFixedPlaceholder()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            var risksCard = cockpit.KpiCards.Single(c => c.Label == "Risks");
            Assert.False(risksCard.IsPlaceholder);
            Assert.Equal("1 total", risksCard.Value);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `WP 10.7A` (Feature Completion) — the Engineering Health Summary's
    /// own "Review" card, previously hardcoded <c>IsPlaceholder: true</c>
    /// regardless of any real data, now sums each discipline's own
    /// already-computed in-review count. Self-consistency, not a
    /// hardcoded expected number — the same live sample data.
    /// </summary>
    [AvaloniaFact]
    public async Task RealData_KpiCards_ReviewCard_SumsEachDisciplinesOwnRealInReviewCount()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            var requirementsReview = int.Parse(cockpit.RequirementsKpiCards.Single(c => c.Label == "Review").Value);
            var calculationsReview = int.Parse(cockpit.CalculationsKpiCards.Single(c => c.Label == "Review").Value);
            var documentsReview = cockpit.OutstandingDocumentReviews;
            var expectedTotal = requirementsReview + calculationsReview + documentsReview;

            var reviewCard = cockpit.KpiCards.Single(c => c.Label == "Review");
            if (expectedTotal > 0)
            {
                Assert.False(reviewCard.IsPlaceholder);
                Assert.Equal($"{expectedTotal} total", reviewCard.Value);
            }
            else
            {
                Assert.True(reviewCard.IsPlaceholder);
            }
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RealData_UpcomingMilestones_ReflectsTheLiveSeededMilestone()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            // `TD-37` fixed, `WP 10.1B` (see RiskSummary's own remarks,
            // above): the base sample's own real Milestone
            // ("SAMPLE-MS-001", target date three months out) now seeds
            // successfully and stably every run.
            Assert.NotEmpty(cockpit.UpcomingMilestones);
            Assert.Contains(cockpit.UpcomingMilestones, m => m.Contains("Sample Milestone", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RealData_BlockedItems_ReflectsTheRealFailedVerificationResult()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            // WP 9.3A's own sample data deliberately records one real,
            // honest Fail outcome (its own disclosed "honest failure
            // demonstration") — BlockedItems must surface it by name,
            // synthesised from that already-real signal, not fabricated.
            Assert.NotEmpty(cockpit.BlockedItems);
            Assert.Contains(cockpit.BlockedItems, item => item.Contains("Fail outcome", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RealData_HealthAndScore_ComputeARealRollupNotAFixedPlaceholder()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            // Real sample data exists across every discipline, so the
            // rollup must report real reporting disciplines, never the
            // old fixed "— (not yet available)" placeholder.
            Assert.NotEqual("— (not yet available)", cockpit.HealthScoreDisplay);
            Assert.Contains("disciplines reporting", cockpit.HealthScoreDisplay);
            Assert.NotEqual(EngineeringHealthStatus.Unknown, cockpit.Health);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RealData_DigitalThreadSummary_ReportsARealAggregateNotAFixedPlaceholder()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            Assert.DoesNotContain("placeholder", cockpit.DigitalThreadSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("link(s) tracked", cockpit.DigitalThreadSummary);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task HonestPlaceholder_FavouriteProjectsAndOverdueActions_RemainEmpty_AndOpenTaskCountReflectsTheLiveSeededTask()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            // No platform capability exists for favouriting a project, or
            // for a due date on a Task/Action — both must remain honestly
            // empty, never fabricated, per this Work Package's own
            // explicit instruction.
            Assert.Empty(cockpit.FavouriteProjects);
            Assert.Empty(cockpit.OverdueActions);

            // The closest honest, real substitute for "overdue" — a real
            // open-Task count. `TD-37` fixed, `WP 10.1B` (see RiskSummary's
            // own remarks, above): the base sample's own real Task
            // ("SAMPLE-TASK-001", Draft) now seeds successfully every run,
            // so this is stable, live data, not the disclosed-empty state.
            Assert.Equal(1, cockpit.OpenTaskCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task CockpitView_ConstructsAndRefreshesOverRealData_WithoutThrowing()
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

            view.Refresh();
            view.Refresh(); // idempotent — a second call must not throw or duplicate state incorrectly

            Assert.NotNull(view);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `WP 10.7A` (Feature Completion) — the "Favourite Projects" card,
    /// previously always the fixed "no platform capability" message
    /// (still true, unmodified, at the <see cref="EngineeringCockpit"/>
    /// App-layer — <see cref="HonestPlaceholder_FavouriteProjectsAndOverdueActions_RemainEmpty_AndOpenTaskCountReflectsTheLiveSeededTask"/>
    /// still proves that), now reads a real, Desktop-layer
    /// <see cref="FavouriteObjectsState"/> instead, when threaded through
    /// — clicking a real favourited Project invokes the real open
    /// callback with its own real Id/Kind.
    /// </summary>
    [AvaloniaFact]
    public async Task CockpitView_WithARealFavouritedProject_RendersItAsAClickableAction()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var cockpit = ((Workspace)workspace).Cockpit;
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));
            var favourites = new FavouriteObjectsState(settingsProvider);
            var favouriteProjectId = Guid.NewGuid();
            favourites.Add(favouriteProjectId, "Project", "WP10.7A Test Favourite Project");
            favourites.Add(Guid.NewGuid(), "Calculation", "Not a Project — must not appear on this card");

            var opened = new List<(Guid Id, string Kind)>();
            var view = new CockpitView(
                cockpit,
                workspace.Navigation.Areas,
                onContinue: () => { },
                onOpenRecent: _ => { },
                onOpenCommandPalette: () => { },
                onSwitchArea: _ => { },
                favourites: favourites,
                onOpenFavourite: (id, kind) => opened.Add((id, kind)));

            var favouriteButton = view.GetLogicalDescendants().OfType<Button>()
                .Single(b => b.Content is string s && s.EndsWith("WP10.7A Test Favourite Project", StringComparison.Ordinal));

            favouriteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.Single(opened);
            Assert.Equal((favouriteProjectId, "Project"), opened[0]);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public void DocumentAreaView_SetHomeTab_AddsAPermanentFirstTab()
    {
        var documentArea = new DocumentAreaView();
        Assert.Equal(0, documentArea.TabCount);

        documentArea.SetHomeTab(new Border());
        Assert.Equal(1, documentArea.TabCount);

        // Setting it again replaces, rather than accumulating, home tabs.
        documentArea.SetHomeTab(new Border());
        Assert.Equal(1, documentArea.TabCount);
    }
}
