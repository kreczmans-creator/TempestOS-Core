using Tempest.Workspace.Tasks;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Evidence;
using Tempest.Core.Invoicing;
using Tempest.Core.Persistence;
using Tempest.Core.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tasks;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;
using Tempest.Core.Timesheets;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// The `WP 19.5C` acceptance journey for <see cref="TasksReadModelService"/>:
/// a fixture producing every <see cref="TaskBucket"/> (an overdue
/// deliverable; a milestone due today; evidence awaiting check and
/// awaiting issue; an unpaid invoice past terms; a quote sent eight days
/// ago; a manual task); the counts and the lists; completing the manual
/// task removes it.
/// </summary>
public sealed class TasksReadModelTests
{
    [Fact]
    public async Task EveryBucket_IsProduced_WithTheRightCountsAndLists_CompletingTheManualTaskRemovesIt()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var deliverables = QuotationTestHost.Deliverables(host);
        var quotations = QuotationTestHost.Quotations(host);
        var invoicing = QuotationTestHost.Invoicing(host);
        var evidenceService = (IEvidenceService)host.Services!.GetService(typeof(IEvidenceService));
        var taskService = (ITaskService)host.Services!.GetService(typeof(ITaskService));

        // Backdated services — used only for the two Send acts below, so
        // the invoice/quote's own SentAtUtc/SentOn reads as genuinely
        // past terms/chase-worthy relative to the real "now" the rest of
        // this fixture (and the read model itself) uses. Bucketing every
        // due-dated item off a shifted clock instead would have moved
        // "due today"/"due this week" out from under themselves too.
        var backdatedInvoicing = new InvoicingService(
            QuotationTestHost.Domain(host), QuotationTestHost.RateCards(host),
            (ITimesheetService)host.Services!.GetService(typeof(ITimesheetService)),
            deliverables, (IInvoicingConnector)host.Services!.GetService(typeof(IInvoicingConnector)),
            QuotationTestHost.Organisations(host), new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-31)));
        var backdatedQuotations = new QuotationService(
            QuotationTestHost.Domain(host), QuotationTestHost.RateCards(host), QuotationTestHost.Requirements(host),
            new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-8)));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ---- Overdue deliverable ----
        var overdueProjectId = await QuotationTestHost.CreateProjectAsync(host, "TASK-OVERDUE");
        var overdueDeliverable = await deliverables.AddDeliverableAsync(overdueProjectId, "Late deliverable", today.AddDays(-5));

        // ---- Milestone due today ----
        var dueTodayProjectId = await QuotationTestHost.CreateProjectAsync(host, "TASK-DUETODAY");
        await deliverables.AddDeliverableAsync(dueTodayProjectId, "Due today deliverable", today);

        // ---- Evidence awaiting check (Draft, with a subject and a file) ----
        var evidenceProjectId = await QuotationTestHost.CreateProjectAsync(host, "TASK-EVIDENCE");
        var awaitingCheck = await evidenceService.CreateAsync(evidenceProjectId, "Bracket calc", EvidenceClassification.Calculation, Guid.NewGuid());
        await awaitingCheck.AttachContentAsync("calc.xlsx", "application/vnd.ms-excel", new byte[] { 1, 2, 3 });

        // A Draft with no file at all is not "genuinely ready to check" and must not appear.
        var draftNoFile = await evidenceService.CreateAsync(evidenceProjectId, "Empty draft", EvidenceClassification.Report, Guid.NewGuid());

        // ---- Evidence awaiting issue (Checked) ----
        var awaitingIssue = await evidenceService.CreateAsync(evidenceProjectId, "Report ready to issue", EvidenceClassification.Report, Guid.NewGuid());
        await awaitingIssue.AttachContentAsync("report.pdf", "application/pdf", new byte[] { 4, 5, 6 });
        var checkResult = await evidenceService.RecordCheckAsync(awaitingIssue.Id, "J. Reviewer", "Client Co", "Reviewed.", CheckOutcome.Accepted);
        Assert.True(checkResult.Succeeded, checkResult.Reason);

        // ---- An unpaid invoice request, Sent, past terms ----
        var (financeProjectId, _) = await SetUpBillableProjectAsync(host, "FIN");
        var billedDeliverable = await deliverables.AddDeliverableAsync(financeProjectId, "Billed deliverable", today.AddDays(60));
        var billedCompletion = await deliverables.CompleteAsync(
            billedDeliverable.Id, financeProjectId, today, fixedPriceValue: new Money(1_000m, CurrencyCode.Gbp));
        Assert.True(billedCompletion.Succeeded, billedCompletion.Reason);
        var request = await QuotationTestHost.RequestRaisedByCompletionAsync(host, billedCompletion.Completion!.Id);
        var sentRequest = await backdatedInvoicing.SendAsync(request.Id);
        Assert.True(sentRequest.Succeeded, sentRequest.Reason);
        Assert.Equal(InvoiceRequestStatus.Sent, sentRequest.Request!.Status);

        // ---- A quote, Sent eight days ago ----
        var (quoteProjectId, _) = await SetUpBillableProjectAsync(host, "QUOTE");
        var chaseQuote = await quotations.CreateAsync(quoteProjectId);
        await quotations.AddLineAsync(chaseQuote.Quotation!.Id, "Chase me", 10m, new Money(100m, CurrencyCode.Gbp), null);
        var sentQuote = await backdatedQuotations.SendAsync(chaseQuote.Quotation.Id);
        Assert.True(sentQuote.Succeeded, sentQuote.Reason);

        // ---- A manual task, due this week ----
        var manualProjectId = await QuotationTestHost.CreateProjectAsync(host, "TASK-MANUAL");
        var manualCreated = await taskService.CreateAsync("Chase the client for sign-off", manualProjectId, today.AddDays(3));
        Assert.True(manualCreated.Succeeded, manualCreated.Reason);
        var manualTaskId = manualCreated.Task!.Id;

        // Read as of the real "now" — the invoice and quote were backdated
        // at Send time (above), not the read itself, so "due today"/"due
        // this week" still bucket off today, genuinely.
        var store = (IQueryablePersistenceStore)host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var reader = new TasksReadModelService(store);

        var snapshot = await reader.ReadAsync();

        Assert.Contains(snapshot.OpenTasks, i => i.Kind == "Deliverable" && i.ObjectId == overdueDeliverable.Id && i.Bucket == TaskBucket.Overdue);
        Assert.Contains(snapshot.OpenTasks, i => i.Kind == "Milestone" && i.ProjectId == dueTodayProjectId && i.Bucket == TaskBucket.DueToday);
        Assert.Contains(snapshot.OpenTasks, i => i.Kind == ManualTask.CanonicalKind && i.ObjectId == manualTaskId && i.Bucket == TaskBucket.DueThisWeek);

        Assert.Single(snapshot.Reviews, i => i.ObjectId == awaitingCheck.Id);
        Assert.DoesNotContain(snapshot.Reviews, i => i.ObjectId == draftNoFile.Id);
        Assert.Single(snapshot.Approvals, i => i.ObjectId == awaitingIssue.Id);

        Assert.Contains(snapshot.Finance, i => i.Kind == InvoiceRequest.CanonicalKind && i.ObjectId == request.Id);
        Assert.Contains(snapshot.Finance, i => i.Kind == Quotation.CanonicalKind && i.ObjectId == chaseQuote.Quotation.Id);

        Assert.True(snapshot.Counts[TaskBucket.Overdue] >= 1);
        Assert.True(snapshot.Counts[TaskBucket.DueToday] >= 1);
        Assert.True(snapshot.Counts[TaskBucket.DueThisWeek] >= 1);
        Assert.Equal(1, snapshot.Counts[TaskBucket.Reviews]);
        Assert.Equal(1, snapshot.Counts[TaskBucket.Approvals]);
        Assert.Equal(2, snapshot.Counts[TaskBucket.Finance]);

        // ---- Completing the manual task removes it from the open list ----
        var completed = await taskService.CompleteAsync(manualTaskId);
        Assert.True(completed.Succeeded, completed.Reason);

        var afterCompletion = await reader.ReadAsync();
        Assert.DoesNotContain(afterCompletion.OpenTasks, i => i.ObjectId == manualTaskId);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task DeletingATask_RemovesItToo()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var taskService = (ITaskService)host.Services!.GetService(typeof(ITaskService));

        // A task with no project at all — the "project?" of the brief.
        var created = await taskService.CreateAsync("No project", null, null);
        Assert.True(created.Succeeded, created.Reason);
        Assert.Null(created.Task!.DueDate);

        var store = (IQueryablePersistenceStore)host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var reader = new TasksReadModelService(store);

        var before = await reader.ReadAsync();
        Assert.Contains(before.OpenTasks, i => i.ObjectId == created.Task.Id && i.Bucket == TaskBucket.Later);

        var deleted = await taskService.DeleteAsync(created.Task.Id);
        Assert.True(deleted.Succeeded, deleted.Reason);

        var after = await reader.ReadAsync();
        Assert.DoesNotContain(after.OpenTasks, i => i.ObjectId == created.Task.Id);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    private static async Task<(Guid ProjectId, string OrganisationId)> SetUpBillableProjectAsync(ITempestHost host, string suffix)
    {
        var organisationId = $"TASK-CLIENT-{suffix}";
        var rateCardId = $"TASK-CARD-{suffix}";

        var organisations = QuotationTestHost.Organisations(host);
        var rateCards = QuotationTestHost.RateCards(host);
        var commercial = QuotationTestHost.ProjectCommercial(host);

        await organisations.RegisterAsync(organisationId, OperationsFixtures.Organisation(organisationId), OperationsFixtures.Verified());

        var card = OneGradeCard(rateCardId, "Senior", 150m, 90m);
        await rateCards.RegisterAsync(rateCardId, card, BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, rateCardId);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, $"TASK-PRJ-{suffix}");

        Assert.True((await commercial.PinRateCardAsync(projectId, rateCardId)).Succeeded);
        Assert.True((await commercial.SetClientAsync(projectId, organisationId)).Succeeded);

        return (projectId, organisationId);
    }

    private static RateCard OneGradeCard(string code, string grade, decimal billing, decimal cost) => new()
    {
        Code = code,
        Name = "Tasks read model test rate card",
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
