using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Workspace;
using Tempest.Workspace.Kpi;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Mechanical;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.ReferenceData;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.1B`'s own acceptance #2, driven through the real production
/// composition: recording time and completing a deliverable through the
/// real services, the real <see cref="CockpitView"/> showing the five KPI
/// cards for "this week", a period switch changing them, and the
/// selection surviving a real relaunch — plus the removed "Engineering
/// Overview" placeholder staying gone.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CockpitKpiJourneyTests
{
    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    private static ITimesheetService TimesheetsOf(WorkspaceHost host) =>
        (ITimesheetService)host.Services!.GetService(typeof(ITimesheetService));

    private static IDeliverableService DeliverablesOf(WorkspaceHost host) =>
        (IDeliverableService)host.Services!.GetService(typeof(IDeliverableService));

    private static IRateCardCatalog RateCardsOf(WorkspaceHost host) =>
        (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));

    private static IProjectCommercialService CommercialOf(WorkspaceHost host) =>
        (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));

    /// <summary>A fictional, fixture-only rate card's own provenance — verified, so the card can reach Released.</summary>
    private static ReferenceProvenance FixtureProvenance(DateOnly today) => new(
        SourceOrganisation: "KPI Journey Fixture",
        SourceDocument: "Fixture rate card (not a real document)",
        SourceRevision: "1",
        SourceDate: today,
        SourceLocation: "Fixture",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.")
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = today,
    };

    /// <summary>Governance carrying the one authority a rate card needs to be approved — <see cref="RateCard.IsApproved"/>.</summary>
    private static BusinessGovernanceFacts ApprovedGovernance(DateOnly today) => new()
    {
        Ownership = new BusinessOwnership("owner-1", "Managing Director"),
        Review = new ReviewSchedule(today.AddMonths(6), IntervalMonths: 12),
        Authorisations = [new BusinessAuthorisation(BusinessAuthorityKind.InternalApproval, "approver-1", "Director", today, "Fixture approval.")],
    };

    /// <summary>Creates a project with a Released rate card (billing and cost rate) pinned to it — the fixture every test below records real time and a real deliverable completion against.</summary>
    private static async Task<Guid> SetUpPricedProjectAsync(WorkspaceHost host, string identifier)
    {
        var domain = DomainOf(host);
        var factory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, domain,
            (doc, rev) => new Project(doc, rev, domain, identifier, $"KPI Journey Project {identifier}", EngineeringObjectMetadata.Empty));
        var project = (Project)await factory.CreateAsync($"KPI journey project {identifier}.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cardCode = $"KPI-JOURNEY-{identifier}";
        var rateCards = RateCardsOf(host);
        var card = new RateCard
        {
            Code = cardCode,
            Name = $"KPI Journey Rate Card {identifier}",
            EffectivePeriod = new EffectivePeriod(today.AddYears(-1), today.AddYears(1)),
            Currency = CurrencyCode.Gbp,
            Governance = ApprovedGovernance(today),
            Entries =
            [
                new RateCardEntry(
                    "ENG-1", "Senior engineering", PricingBasis.Hourly,
                    new Money(120m, CurrencyCode.Gbp), new Money(70m, CurrencyCode.Gbp), Grade: "Senior"),
            ],
        };
        await rateCards.RegisterAsync(cardCode, card, FixtureProvenance(today));
        await rateCards.SetValidationStateAsync(cardCode, ReferenceValidationState.Checked, "Checked.");
        await rateCards.SetValidationStateAsync(cardCode, ReferenceValidationState.Validated, "Rules pass.");
        await rateCards.SetValidationStateAsync(cardCode, ReferenceValidationState.Released, "Released.");

        await CommercialOf(host).PinRateCardAsync(project.Id, cardCode);

        return project.Id;
    }

    private static CockpitView BuildCockpitView(IWorkspace workspace) =>
        new(
            workspace.Cockpit,
            workspace.Navigation.Areas,
            onContinue: () => Task.CompletedTask,
            onOpenRecent: _ => Task.CompletedTask,
            onOpenCommandPalette: () => { },
            onSwitchArea: _ => { });

    private static CockpitCardControl CardTitled(CockpitView view, string title) =>
        view.GetLogicalDescendants().OfType<CockpitCardControl>().Single(c => c.Title == title);

    private static string TextOf(CockpitCardControl card) =>
        string.Join(" ", card.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    [AvaloniaFact]
    public async Task Journey_RecordTimeAndCompleteADeliverable_HomeShowsTheFiveCards_AndThePlaceholderIsGone()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var projectId = await SetUpPricedProjectAsync(host, "A");
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Record time through the real service, this week.
            var recorded = await TimesheetsOf(host).RecordAsync(projectId, today, 4m, billable: true, "Senior", "Journey work");
            Assert.True(recorded.Succeeded, recorded.Reason);

            // Complete a fixed-price deliverable through the real services, this week.
            var domain = DomainOf(host);
            var milestoneService = new ProjectMilestoneService(domain);
            var milestone = await milestoneService.CreateMilestoneAsync(projectId, "MS-JOURNEY-A", "Milestone", DateTimeOffset.UtcNow.AddDays(30));
            var deliverable = await milestoneService.CreateDeliverableAsync(projectId, milestone.Id, "DEL-JOURNEY-A", "Journey Deliverable");
            var completed = await DeliverablesOf(host).CompleteAsync(deliverable.Id, projectId, today, fixedPriceValue: new Money(500m, CurrencyCode.Gbp));
            Assert.True(completed.Succeeded, completed.Reason);

            var view = BuildCockpitView(workspace);
            await view.RefreshAsync();

            // The removed cross-discipline placeholder card must be gone.
            Assert.DoesNotContain(view.GetLogicalDescendants().OfType<CockpitCardControl>(), c => c.Title == "Engineering Overview");

            // The period selector is present, defaulted to "This week".
            var presetCombo = view.GetLogicalDescendants().OfType<ComboBox>().Single();
            Assert.Equal("This week", presetCombo.SelectedItem);

            // Utilisation: the 4h billable entry, recorded today, is inside "this week".
            Assert.Contains("4h billable", TextOf(CardTitled(view, "Utilisation")), StringComparison.Ordinal);

            // Margin per project: revenue = 4h × £120 + £500 deliverable = £980;
            // cost = 4h × £70 = £280; margin = £700.
            var marginText = TextOf(CardTitled(view, "Margin per project"));
            Assert.Contains("700.00", marginText, StringComparison.Ordinal);

            // Work in progress: the same £480 of time and £500 deliverable are
            // both still unbilled — £980 total, regardless of period.
            Assert.Contains("980.00", TextOf(CardTitled(view, "Work in progress")), StringComparison.Ordinal);

            // No InvoiceRequest or issued Evidence exists in this fixture.
            Assert.Contains("Unavailable", TextOf(CardTitled(view, "Days sales outstanding")), StringComparison.Ordinal);
            Assert.Contains("0 sheet(s)", TextOf(CardTitled(view, "Calc throughput")), StringComparison.Ordinal);

            // Switching to "Last month" — a period this fixture has no data
            // in — changes the period-scoped cards to their own honest
            // empty state (work in progress and days sales outstanding are
            // never period-scoped, ADR-0150, so they do not change).
            await workspace.Cockpit.SetKpiPeriodAsync(KpiPeriod.LastMonth(today));
            await view.RefreshAsync();

            Assert.Contains("no time recorded", TextOf(CardTitled(view, "Utilisation")), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no time or deliverables", TextOf(CardTitled(view, "Margin per project")), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("0 sheet(s)", TextOf(CardTitled(view, "Calc throughput")), StringComparison.Ordinal);
            // Still £980 unbilled — the switch did not touch this card.
            Assert.Contains("980.00", TextOf(CardTitled(view, "Work in progress")), StringComparison.Ordinal);

            var presetComboAfter = view.GetLogicalDescendants().OfType<ComboBox>().Single();
            Assert.Equal("Last month", presetComboAfter.SelectedItem);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Journey_Relaunch_TheSelectedKpiPeriod_IsRemembered()
    {
        // One persistence root stands for one machine across two launches.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            await first.Workspace!.Cockpit.SetKpiPeriodAsync(KpiPeriod.ThisQuarter(today));
        }
        finally
        {
            await first.ShutdownAsync();
            await first.DisposeAsync();
        }

        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var workspace = second.Workspace!;
            var view = BuildCockpitView(workspace);
            await view.RefreshAsync();

            Assert.Equal(KpiPeriodPreset.ThisQuarter, workspace.Cockpit.SelectedKpiPeriod.Preset);

            var presetCombo = view.GetLogicalDescendants().OfType<ComboBox>().Single();
            Assert.Equal("This quarter", presetCombo.SelectedItem);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }
}
