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
    public async Task FavouriteProjectsRemainAnHonestPlaceholder_WhileOverdueActionsIsNowRealAndSimplyEmpty()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            // Favouriting a project still has no platform capability
            // behind it, so it stays honestly empty rather than fabricated.
            Assert.Empty(cockpit.FavouriteProjects);

            // Overdue Actions is no longer a placeholder (`TD-81`):
            // EngineeringTask now carries a due date and a work state, so
            // this is a real computation. It is empty here because the
            // seeded sample task has no due date — "nothing is overdue",
            // which is a different statement from "we cannot tell", and
            // the one the card now makes.
            Assert.Empty(cockpit.OverdueActions);
            Assert.Empty(cockpit.OverdueActionLines);

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
    /// App-layer — <see cref="FavouriteProjectsRemainAnHonestPlaceholder_WhileOverdueActionsIsNowRealAndSimplyEmpty"/>
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

    // ----------------------------------------------------------------
    // WP 12.0B (ADR-0103) — characterization tests added before the
    // per-discipline read-model decomposition, closing gaps this Work
    // Package's own investigation found in this file's pre-existing
    // coverage: AttentionItems/OpenActions (cross-discipline aggregation
    // order and content), the three per-discipline KPI card sets no
    // existing test named directly, and the Mechanical/cross-cutting
    // Workspace reads. Each asserts self-consistency against the same
    // live sample data other tests above already establish is real and
    // stable, mirroring this file's own established style, rather than
    // hardcoding a second, independent set of expected sample counts.
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public async Task RealData_AttentionItems_OneEntryPerDisciplineInFixedOrder_PlusConditionalAndTrailingEntries()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            var items = cockpit.AttentionItems;
            var titles = items.Select(i => i.Title).ToList();

            // Fixed base order: Mechanical, Requirements, [Requirements
            // attention], Calculations, [Calculations attention],
            // Documents, [Documents attention], Verification,
            // [Verification attention], Manufacturing, [Manufacturing
            // attention], trailing placeholder — each discipline's own
            // base ("X are/is live" or "No X registered yet") entry is
            // always added before its own conditional attention entry, so
            // the first index matching the discipline name is always the
            // base entry regardless of whether the conditional one exists.
            var mechanicalIndex = titles.FindIndex(t => t.Contains("Mechanical Product Structure", StringComparison.Ordinal));
            var requirementsIndex = titles.FindIndex(t => t.Contains("Requirements", StringComparison.Ordinal));
            var calculationsIndex = titles.FindIndex(t => t.Contains("Calculations", StringComparison.Ordinal));
            var documentsIndex = titles.FindIndex(t => t.Contains("Documents", StringComparison.Ordinal));
            var verificationIndex = titles.FindIndex(t => t.Contains("Verification", StringComparison.Ordinal));
            var manufacturingIndex = titles.FindIndex(t => t.Contains("Manufacturing", StringComparison.Ordinal));
            var trailingIndex = titles.FindIndex(t => t.Contains("Other disciplines still placeholder", StringComparison.Ordinal));

            Assert.True(mechanicalIndex >= 0 && mechanicalIndex < requirementsIndex);
            Assert.True(requirementsIndex < calculationsIndex);
            Assert.True(calculationsIndex < documentsIndex);
            Assert.True(documentsIndex < verificationIndex);
            Assert.True(verificationIndex < manufacturingIndex);
            Assert.True(manufacturingIndex < trailingIndex);
            Assert.Equal(trailingIndex, titles.Count - 1);

            Assert.Equal(cockpit.OutstandingRequirementActions > 0, titles.Contains("Requirements need attention"));
            Assert.Equal(cockpit.OutstandingCalculationActions > 0, titles.Contains("Calculations need attention"));
            Assert.Equal(cockpit.OutstandingDocumentActions > 0, titles.Contains("Documents need attention"));
            Assert.Equal(cockpit.OutstandingVerificationActions > 0, titles.Contains("Verification needs attention"));
            Assert.Equal(cockpit.OutstandingManufacturingActions > 0, titles.Contains("Manufacturing needs attention"));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RealData_OpenActions_TriageEntriesMatchOutstandingCounts_TrailingFixedEntriesAlwaysPresent()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            var titles = cockpit.OpenActions.Select(a => a.Title).ToList();

            Assert.Equal(cockpit.OutstandingRequirementActions > 0, titles.Any(d => d.Contains($"Triage {cockpit.OutstandingRequirementActions} outstanding Requirements", StringComparison.Ordinal)));
            Assert.Equal(cockpit.OutstandingCalculationActions > 0, titles.Any(d => d.Contains($"Triage {cockpit.OutstandingCalculationActions} outstanding Calculation", StringComparison.Ordinal)));
            Assert.Equal(cockpit.OutstandingDocumentActions > 0, titles.Any(d => d.Contains($"Triage {cockpit.OutstandingDocumentActions} outstanding Document", StringComparison.Ordinal)));
            Assert.Equal(cockpit.OutstandingVerificationActions > 0, titles.Any(d => d.Contains($"Triage {cockpit.OutstandingVerificationActions} outstanding Verification", StringComparison.Ordinal)));
            Assert.Equal(cockpit.OutstandingManufacturingActions > 0, titles.Any(d => d.Contains($"Triage {cockpit.OutstandingManufacturingActions} outstanding Manufacturing", StringComparison.Ordinal)));

            Assert.Contains(titles, d => d == "Review the Project Explorer's own sample content");
            Assert.Contains(titles, d => d == "Await the next real engineering discipline module");
            Assert.Equal("Review the Project Explorer's own sample content", titles[^2]);
            Assert.Equal("Await the next real engineering discipline module", titles[^1]);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RealData_PerDisciplineKpiCards_ManufacturingVerificationDocuments_ReportRealSelfConsistentCards()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            var manufacturing = cockpit.ManufacturingKpiCards;
            Assert.Equal(7, manufacturing.Count);
            Assert.Equal(
                new[] { "Manufacturing Objects", "Manufacturing Readiness", "Released Items", "Open Operations", "Supplier Status", "Inspection Status", "Production Health" },
                manufacturing.Select(c => c.Label));
            Assert.Equal(cockpit.ManufacturingStatus.ToString(), manufacturing.Single(c => c.Label == "Production Health").Value);

            var verification = cockpit.VerificationKpiCards;
            Assert.Equal(9, verification.Count);
            Assert.Equal(cockpit.OutstandingVerificationActions.ToString(), verification.Single(c => c.Label == "Outstanding").Value);
            Assert.Equal(cockpit.VerificationStatus.ToString(), verification.Single(c => c.Label == "Project Verification Health").Value);

            var documents = cockpit.DocumentsKpiCards;
            Assert.Equal(8, documents.Count);
            Assert.Equal(cockpit.OutstandingDocumentReviews.ToString(), documents.Single(c => c.Label == "Outstanding Reviews").Value);
            Assert.Equal(cockpit.DocumentationStatus.ToString(), documents.Single(c => c.Label == "Documentation Health").Value);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RealData_Mechanical_ProjectNameAndRecentProjects_ReflectTheLiveSeededProject()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            Assert.NotEqual("No Mechanical Project yet", cockpit.ProjectName);
            Assert.NotEmpty(cockpit.RecentProjects);
            Assert.Contains(cockpit.ProjectName, cockpit.RecentProjects);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RealData_CrossCuttingWorkspaceReads_AreLive()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var cockpit = ((Workspace)host.Workspace!).Cockpit;

            Assert.True(cockpit.AreaCount > 0);
            Assert.NotEmpty(cockpit.AvailableCommands(Tempest.Core.Commands.CommandContext.Empty));
            Assert.Equal(cockpit.RecentActivity.Count > 0, cockpit.ContinueWhereILeftOff is not null);
            Assert.Equal(cockpit.ContinueWhereILeftOff is not null, cockpit.QuickActions.Any(a => a.StartsWith("Continue:", StringComparison.Ordinal)));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
