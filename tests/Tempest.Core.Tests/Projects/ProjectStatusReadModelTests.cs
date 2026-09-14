using Tempest.Workspace.Projects;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Persistence;
using Tempest.Core.Projects;
using Tempest.Core.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;
using Tempest.Core.Timesheets;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// The `WP 19.5C` acceptance journey for <see cref="ProjectStatusReadModel"/>:
/// a fixture of six open projects, one per <see cref="ProjectHealthStatus"/>
/// (five rule-driven statuses plus the On track default), each with the
/// reason; the counts; the schedule fields (start/target date, quoted
/// hours, recorded hours, milestones) from a quote and milestones.
/// </summary>
public sealed class ProjectStatusReadModelTests
{
    [Fact]
    public async Task SixProjects_OnePerStatus_EachWithItsOwnReason_CountsAndSchedule()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var lifecycle = (IProjectLifecycleService)host.Services!.GetService(typeof(IProjectLifecycleService));
        var deliverables = QuotationTestHost.Deliverables(host);
        var quotations = QuotationTestHost.Quotations(host);
        var commercial = QuotationTestHost.ProjectCommercial(host);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ---- On hold ----
        var onHoldId = await QuotationTestHost.CreateProjectAsync(host, "STAT-HOLD");
        await commercial.SetDatesAsync(onHoldId, today, today.AddDays(60));
        Assert.True((await lifecycle.HoldAsync(onHoldId, "Client paused.")).Succeeded);

        // ---- Blocked: an accepted quote, no deliverable started ----
        var (blockedId, _) = await SetUpBillableProjectAsync(host, "BLOCK");
        var blockedQuote = await quotations.CreateAsync(blockedId);
        Assert.True(blockedQuote.Succeeded, blockedQuote.Reason);
        await quotations.AddLineAsync(blockedQuote.Quotation!.Id, "Detailed design", 20m, new Money(100m, CurrencyCode.Gbp), null);
        await quotations.SendAsync(blockedQuote.Quotation.Id);
        var blockedAccepted = await quotations.AcceptAsync(blockedQuote.Quotation.Id);
        Assert.True(blockedAccepted.Succeeded, blockedAccepted.Reason);

        // ---- Overdue: a deliverable whose milestone's target date has passed, not complete ----
        var overdueId = await QuotationTestHost.CreateProjectAsync(host, "STAT-OVERDUE");
        await deliverables.AddDeliverableAsync(overdueId, "Late deliverable", today.AddDays(-10));

        // ---- At risk: a deliverable due within seven days, not complete ----
        var atRiskId = await QuotationTestHost.CreateProjectAsync(host, "STAT-ATRISK");
        await deliverables.AddDeliverableAsync(atRiskId, "Due soon deliverable", today.AddDays(3));

        // ---- Ready to invoice: a completion with no live invoice request ----
        var readyId = await QuotationTestHost.CreateProjectAsync(host, "STAT-READY");
        var readyDeliverable = await deliverables.AddDeliverableAsync(readyId, "Finished deliverable", today.AddDays(60));
        var readyCompletion = await deliverables.CompleteAsync(readyDeliverable.Id, readyId, today);
        Assert.True(readyCompletion.Succeeded, readyCompletion.Reason);

        // ---- On track: nothing outstanding ----
        var onTrackId = await QuotationTestHost.CreateProjectAsync(host, "STAT-TRACK");
        await commercial.SetDatesAsync(onTrackId, today, today.AddDays(90));

        var reader = new ProjectStatusReadModel((IQueryablePersistenceStore)host.Services!.GetService(typeof(IQueryablePersistenceStore)));
        var snapshot = await reader.ReadAsync();

        var byId = snapshot.Projects.ToDictionary(r => r.ProjectId);

        Assert.Equal(ProjectHealthStatus.OnHold, byId[onHoldId].Status);
        Assert.Contains("hold", byId[onHoldId].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(today, byId[onHoldId].StartDate);
        Assert.Equal(today.AddDays(60), byId[onHoldId].TargetDate);

        Assert.Equal(ProjectHealthStatus.Blocked, byId[blockedId].Status);
        Assert.Contains("no deliverable has been started", byId[blockedId].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(20m, byId[blockedId].QuotedHours);
        var blockedMilestone = Assert.Single(byId[blockedId].Milestones);
        Assert.Equal(blockedQuote.Quotation.Reference, blockedMilestone.Title);

        Assert.Equal(ProjectHealthStatus.Overdue, byId[overdueId].Status);
        Assert.Contains("overdue", byId[overdueId].Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(byId[overdueId].Milestones);

        Assert.Equal(ProjectHealthStatus.AtRisk, byId[atRiskId].Status);
        Assert.Contains("at risk", byId[atRiskId].Reason, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(ProjectHealthStatus.ReadyToInvoice, byId[readyId].Status);
        Assert.Contains("ready to invoice", byId[readyId].Reason, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(ProjectHealthStatus.OnTrack, byId[onTrackId].Status);
        Assert.Equal("On track.", byId[onTrackId].Reason);

        Assert.Equal(1, snapshot.Counts[ProjectHealthStatus.OnHold]);
        Assert.Equal(1, snapshot.Counts[ProjectHealthStatus.Blocked]);
        Assert.Equal(1, snapshot.Counts[ProjectHealthStatus.Overdue]);
        Assert.Equal(1, snapshot.Counts[ProjectHealthStatus.AtRisk]);
        Assert.Equal(1, snapshot.Counts[ProjectHealthStatus.ReadyToInvoice]);
        Assert.Equal(1, snapshot.Counts[ProjectHealthStatus.OnTrack]);
        Assert.Equal(6, snapshot.Counts.Values.Sum());

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task RecordedHoursExceedingTheQuotesHours_IsAtRisk()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var (projectId, _) = await SetUpBillableProjectAsync(host, "HOURS");
        var quotations = QuotationTestHost.Quotations(host);
        var timesheets = (ITimesheetService)host.Services!.GetService(typeof(ITimesheetService));

        var quote = await quotations.CreateAsync(projectId);
        await quotations.AddLineAsync(quote.Quotation!.Id, "Design", 5m, new Money(100m, CurrencyCode.Gbp), null);
        await quotations.SendAsync(quote.Quotation.Id);
        var accepted = await quotations.AcceptAsync(quote.Quotation.Id);
        Assert.True(accepted.Succeeded, accepted.Reason);

        // Complete the one deliverable so the milestone itself no longer
        // drives Overdue/At risk, isolating the hours-exceeded rule.
        var deliverableId = accepted.Quotation!.Lines[0].DeliverableId!.Value;
        var deliverables = QuotationTestHost.Deliverables(host);
        await deliverables.CompleteAsync(deliverableId, projectId, DateOnly.FromDateTime(DateTime.UtcNow));

        var recorded = await timesheets.RecordAsync(projectId, DateOnly.FromDateTime(DateTime.UtcNow), 8m, true, "Senior", "Over-hours work");
        Assert.True(recorded.Succeeded, recorded.Reason);

        var reader = new ProjectStatusReadModel((IQueryablePersistenceStore)host.Services!.GetService(typeof(IQueryablePersistenceStore)));
        var snapshot = await reader.ReadAsync();
        var row = snapshot.Projects.Single(r => r.ProjectId == projectId);

        Assert.Equal(ProjectHealthStatus.AtRisk, row.Status);
        Assert.Contains("hour", row.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5m, row.QuotedHours);
        Assert.Equal(8m, row.RecordedHours);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    private static async Task<(Guid ProjectId, string OrganisationId)> SetUpBillableProjectAsync(ITempestHost host, string suffix)
    {
        var organisationId = $"STAT-CLIENT-{suffix}";
        var rateCardId = $"STAT-CARD-{suffix}";

        var organisations = QuotationTestHost.Organisations(host);
        var rateCards = QuotationTestHost.RateCards(host);
        var commercial = QuotationTestHost.ProjectCommercial(host);

        await organisations.RegisterAsync(organisationId, OperationsFixtures.Organisation(organisationId), OperationsFixtures.Verified());

        var card = OneGradeCard(rateCardId, "Senior", 150m, 90m);
        await rateCards.RegisterAsync(rateCardId, card, BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, rateCardId);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, $"STAT-PRJ-{suffix}");

        Assert.True((await commercial.PinRateCardAsync(projectId, rateCardId)).Succeeded);
        Assert.True((await commercial.SetClientAsync(projectId, organisationId)).Succeeded);

        return (projectId, organisationId);
    }

    private static RateCard OneGradeCard(string code, string grade, decimal billing, decimal cost) => new()
    {
        Code = code,
        Name = "Project status test rate card",
        EffectivePeriod = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
        Currency = CurrencyCode.Gbp,
        Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
        Entries =
        [
            new RateCardEntry(
                "ENG-1", $"{grade} engineering", PricingBasis.Hourly, new Money(billing, CurrencyCode.Gbp), new Money(cost, CurrencyCode.Gbp),
                Grade: grade),
        ],
    };
}
