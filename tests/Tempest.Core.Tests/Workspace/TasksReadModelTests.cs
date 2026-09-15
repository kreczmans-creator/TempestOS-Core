using Tempest.Workspace.Tasks;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
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

    /// <summary>
    /// `TD-180` (`WP 20.1B`): the Finance bucket now reads each request's
    /// own <c>DueOn</c> — a Sent request on Days30 terms, sent moments
    /// ago, is nowhere near its own due date and must not appear, unlike
    /// the identical-status request the acceptance journey above sends
    /// thirty-one days in the past.
    /// </summary>
    [Fact]
    public async Task ASentRequestNotYetPastItsOwnDueDate_DoesNotAppearInFinance()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var deliverables = QuotationTestHost.Deliverables(host);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var (projectId, organisationId) = await SetUpBillableProjectAsync(host, "NOTYETDUE");
        await QuotationTestHost.Organisations(host).ReviseAsync(
            organisationId, OperationsFixtures.Organisation(organisationId) with { PaymentTerms = PaymentTerms.Days30 },
            OperationsFixtures.Verified(), "Thirty-day terms for this fixture.");

        var deliverable = await deliverables.AddDeliverableAsync(projectId, "Just billed", today.AddDays(60));
        var completion = await deliverables.CompleteAsync(deliverable.Id, projectId, today, fixedPriceValue: new Money(500m, CurrencyCode.Gbp));
        Assert.True(completion.Succeeded, completion.Reason);

        var request = await QuotationTestHost.RequestRaisedByCompletionAsync(host, completion.Completion!.Id);
        var sent = await QuotationTestHost.Invoicing(host).SendAsync(request.Id);
        Assert.True(sent.Succeeded, sent.Reason);
        Assert.Equal(today.AddDays(30), sent.Request!.DueOn);

        var store = (IQueryablePersistenceStore)host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var snapshot = await new TasksReadModelService(store).ReadAsync();

        Assert.DoesNotContain(snapshot.Finance, i => i.ObjectId == request.Id);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    // ---- `WP 20.1B` (`TD-181`): a calculation is a task from creation ----

    [Fact]
    public async Task ACalculationCreatedUnderAProject_AppearsInTheCalculationsBucket_UntilItIsCompleted()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var domain = QuotationTestHost.Domain(host);
        var projectId = await QuotationTestHost.CreateProjectAsync(host, "TASK-CALC");
        var calculation = await CreateCalculationAsync(domain, projectId, "Bracket check");

        var store = (IQueryablePersistenceStore)host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var reader = new TasksReadModelService(store);

        var before = await reader.ReadAsync();
        Assert.Contains(before.Calculations, i => i.ObjectId == calculation.Id && i.Title == "Bracket check" && i.ProjectId == projectId);
        Assert.True(before.Counts[TaskBucket.Calculations] >= 1);

        await calculation.MarkCompletedAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        var after = await reader.ReadAsync();
        Assert.DoesNotContain(after.Calculations, i => i.ObjectId == calculation.Id);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    /// <summary>`TD-181`'s own "whichever first": issuing evidence citing a calculation closes it exactly as completing it directly does — no separate flag needs setting.</summary>
    [Fact]
    public async Task IssuedEvidenceCitingACalculation_RemovesItFromTheBucket_EvenWithoutCompletingItDirectly()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var domain = QuotationTestHost.Domain(host);
        var evidenceService = (IEvidenceService)host.Services!.GetService(typeof(IEvidenceService));
        var projectId = await QuotationTestHost.CreateProjectAsync(host, "TASK-CALC-EV");
        var calculation = await CreateCalculationAsync(domain, projectId, "Beam check");

        var evidence = await evidenceService.CreateAsync(projectId, "Beam check report", EvidenceClassification.Calculation, calculation.Id);
        await evidence.AttachContentAsync("beam.xlsx", "application/vnd.ms-excel", new byte[] { 1, 2, 3 });
        var checked_ = await evidenceService.RecordCheckAsync(evidence.Id, "J. Reviewer", "Client Co", "Reviewed.", CheckOutcome.Accepted);
        Assert.True(checked_.Succeeded, checked_.Reason);
        var issued = await evidenceService.IssueAsync(evidence.Id, "ISS-1", "A", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);

        var store = (IQueryablePersistenceStore)host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var reader = new TasksReadModelService(store);

        var snapshot = await reader.ReadAsync();
        Assert.DoesNotContain(snapshot.Calculations, i => i.ObjectId == calculation.Id);

        // The calculation's own Completed flag was never set directly —
        // the issued evidence alone closed it.
        var reloaded = (Calculation)(await domain.Repository.FindAsync(calculation.Id))!;
        Assert.False(reloaded.Completed);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task ACalculationWithNoProjectAncestorAtAll_DoesNotAppear()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var domain = QuotationTestHost.Domain(host);

        // Created, but never moved under any project — the lead's own
        // default (brief-20.1B.md §2's own open question, resolved "no").
        var factory = new EngineeringObjectFactory<Calculation>(
            "Calculation", domain, (doc, rev) => new Calculation(doc, rev, domain, identifier: null, "Orphan calculation", EngineeringObjectMetadata.Empty));
        var orphan = (Calculation)await factory.CreateAsync("Orphan calculation — no project.");

        var store = (IQueryablePersistenceStore)host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var reader = new TasksReadModelService(store);

        var snapshot = await reader.ReadAsync();
        Assert.DoesNotContain(snapshot.Calculations, i => i.ObjectId == orphan.Id);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    private static async Task<Calculation> CreateCalculationAsync(EngineeringDomainContext domain, Guid projectId, string title)
    {
        var factory = new EngineeringObjectFactory<Calculation>(
            "Calculation", domain, (doc, rev) => new Calculation(doc, rev, domain, identifier: null, title, EngineeringObjectMetadata.Empty));
        var created = (Calculation)await factory.CreateAsync($"{title} — for test purposes.");

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId);

        return created;
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
