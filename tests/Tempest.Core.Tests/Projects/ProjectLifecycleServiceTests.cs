using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tasks;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// The `WP 19.5C` acceptance journey: hold, resume, sign off (who, when, a
/// statement, recorded as a <see cref="ProjectSignOff"/>; audit rows for
/// every step), reopen within 90 days allowed, after 90 days refused; a
/// project signed off 91 days ago is Archive and a commercial write on it
/// is refused as archived.
/// </summary>
public sealed class ProjectLifecycleServiceTests
{
    [Fact]
    public async Task HoldAsync_ThenResumeAsync_RoundTrips_AndEachIsRefusedWhenAlreadyInThatState()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "LIFE-HOLD");
        var lifecycle = Lifecycle(host);

        var held = await lifecycle.HoldAsync(projectId, "Client paused the engagement.");
        Assert.True(held.Succeeded, held.Reason);
        Assert.True(held.Project!.Held);
        Assert.Equal("Client paused the engagement.", held.Project.HoldReason);

        var heldAgain = await lifecycle.HoldAsync(projectId, "Again.");
        Assert.False(heldAgain.Succeeded);
        Assert.Equal(ProjectLifecycleRefusal.AlreadyHeld, heldAgain.Refusal);

        var resumed = await lifecycle.ResumeAsync(projectId);
        Assert.True(resumed.Succeeded, resumed.Reason);
        Assert.False(resumed.Project!.Held);
        Assert.Null(resumed.Project.HoldReason);

        var resumedAgain = await lifecycle.ResumeAsync(projectId);
        Assert.False(resumedAgain.Succeeded);
        Assert.Equal(ProjectLifecycleRefusal.NotHeld, resumedAgain.Refusal);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task SignOffAsync_RecordsWhoWhenAndStatement_ClosesTheProject_WithAnAuditRow()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, "sign-off-principal");

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "LIFE-SIGN");
        var lifecycle = Lifecycle(host);

        var before = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await lifecycle.SignOffAsync(projectId, "  Delivered in full; client satisfied.  ");

        Assert.True(result.Succeeded, result.Reason);
        var project = result.Project!;
        Assert.NotNull(project.SignOff);
        Assert.Equal("sign-off-principal", project.SignOff!.PrincipalId);
        Assert.Equal("Delivered in full; client satisfied.", project.SignOff.Statement);
        Assert.True(project.SignOff.SignedOn >= before);
        Assert.NotNull(project.ClosedOn);
        Assert.Equal(project.SignOff.SignedOn, project.ClosedOn);
        Assert.Equal(ProjectListingGroup.Closed, ProjectArchival.ListingGroupOf(project, DateTimeOffset.UtcNow));

        // A second sign-off is refused as already closed.
        var second = await lifecycle.SignOffAsync(projectId, "Again.");
        Assert.False(second.Succeeded);
        Assert.Equal(ProjectLifecycleRefusal.AlreadyClosed, second.Refusal);

        var auditQuery = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
        var audit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: projectId));
        Assert.NotEmpty(audit);
        Assert.Contains(audit, a => a.Detail.GetValueOrDefault(AuditTransactionWriter.DetailKey, string.Empty).Contains("Signed off", StringComparison.Ordinal));

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task ReopenAsync_WithinNinetyDays_Succeeds_RefusedOnceArchived()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "LIFE-REOPEN");
        var lifecycle = Lifecycle(host);

        var signOff = await lifecycle.SignOffAsync(projectId, "Closed for the reopen test.");
        Assert.True(signOff.Succeeded, signOff.Reason);

        // Within the window (checked "today", 0 days later): reopen succeeds.
        var reopened = await lifecycle.ReopenAsync(projectId);
        Assert.True(reopened.Succeeded, reopened.Reason);
        Assert.Null(reopened.Project!.ClosedOn);
        Assert.NotNull(reopened.Project.SignOff); // The last sign-off stays recorded as history.
        Assert.Equal(ProjectListingGroup.Open, ProjectArchival.ListingGroupOf(reopened.Project, DateTimeOffset.UtcNow));

        // Not closed: reopening again is refused.
        var reopenNotClosed = await lifecycle.ReopenAsync(projectId);
        Assert.False(reopenNotClosed.Succeeded);
        Assert.Equal(ProjectLifecycleRefusal.NotClosed, reopenNotClosed.Refusal);

        // Sign off again, then check "91 days later": the reopen window has elapsed.
        await lifecycle.SignOffAsync(projectId, "Closed again, for the 91-day check.");
        var ninetyOneDaysLater = new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(91));
        var lifecycleLater = new ProjectLifecycleService(QuotationTestHost.Domain(host), ninetyOneDaysLater);

        var reopenTooLate = await lifecycleLater.ReopenAsync(projectId);
        Assert.False(reopenTooLate.Succeeded);
        Assert.Equal(ProjectLifecycleRefusal.ReopenWindowElapsed, reopenTooLate.Refusal);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task AProjectSignedOff91DaysAgo_IsArchive_AndACommercialWriteOnIt_IsRefusedAsArchived()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "LIFE-ARCH");
        var lifecycle = Lifecycle(host);

        var signOff = await lifecycle.SignOffAsync(projectId, "Closed for the archive test.");
        Assert.True(signOff.Succeeded, signOff.Reason);

        var ninetyOneDaysLater = new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(91));
        var project = signOff.Project!;
        Assert.True(ProjectArchival.IsArchived(project, ninetyOneDaysLater.GetUtcNow()));
        Assert.Equal(ProjectListingGroup.Archive, ProjectArchival.ListingGroupOf(project, ninetyOneDaysLater.GetUtcNow()));

        // A commercial write, checked at that same later instant, is refused as archived.
        var commercialLater = new ProjectCommercialService(
            QuotationTestHost.Domain(host), QuotationTestHost.RateCards(host), ninetyOneDaysLater);

        var write = await commercialLater.SetPurchaseOrderAsync(projectId, "PO-TOO-LATE");
        Assert.False(write.Succeeded);
        Assert.Equal(ProjectCommercialRefusal.ProjectArchived, write.Refusal);
        Assert.Contains("archived", write.Reason, StringComparison.OrdinalIgnoreCase);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    // ================================================================
    // `WP 20.10E` (Product Owner finding D18): sign-off is refused while
    // work is still open against the quote, unless a change order carries
    // it.
    // ================================================================

    [Fact]
    public async Task SignOffAsync_RefusedWhileADeliverableIsOpen_NamedInTheMessage_ThenSucceedsOnceItIsCompleted()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "OPEN-DELIVERABLE");
        var lifecycle = Lifecycle(host);
        var deliverables = QuotationTestHost.Deliverables(host);

        var deliverable = await deliverables.AddDeliverableAsync(projectId, "Bracket redesign");

        var openWork = await lifecycle.GetOpenWorkAsync(projectId);
        var item = Assert.Single(openWork);
        Assert.Equal(deliverable.Id, item.ObjectId);
        Assert.Equal("Deliverable", item.Kind);
        Assert.True(item.IsBlocking);

        var refused = await lifecycle.SignOffAsync(projectId, "Delivered.");
        Assert.False(refused.Succeeded);
        Assert.Equal(ProjectLifecycleRefusal.WorkStillOpen, refused.Refusal);
        Assert.Contains("Bracket redesign", refused.Reason, StringComparison.Ordinal);
        Assert.Contains("Deliverable", refused.Reason, StringComparison.Ordinal);
        Assert.Contains("change order", refused.Reason, StringComparison.OrdinalIgnoreCase);

        var completion = await deliverables.CompleteAsync(deliverable.Id, projectId, DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.True(completion.Succeeded, completion.Reason);

        Assert.Empty(await lifecycle.GetOpenWorkAsync(projectId));

        var signedOff = await lifecycle.SignOffAsync(projectId, "Delivered.");
        Assert.True(signedOff.Succeeded, signedOff.Reason);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task SignOffAsync_RefusedWhileAManualTaskIsOpen_NamedInTheMessage_ThenSucceedsOnceItIsDone()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "OPEN-TASK");
        var lifecycle = Lifecycle(host);
        var tasks = QuotationTestHost.Tasks(host);

        var created = await tasks.CreateAsync("Chase the client for sign-off", projectId, dueDate: null);
        Assert.True(created.Succeeded, created.Reason);

        var refused = await lifecycle.SignOffAsync(projectId, "Delivered.");
        Assert.False(refused.Succeeded);
        Assert.Equal(ProjectLifecycleRefusal.WorkStillOpen, refused.Refusal);
        Assert.Contains("Chase the client for sign-off", refused.Reason, StringComparison.Ordinal);
        Assert.Contains(ManualTask.CanonicalKind, refused.Reason, StringComparison.Ordinal);

        var completed = await tasks.CompleteAsync(created.Task!.Id);
        Assert.True(completed.Succeeded, completed.Reason);

        var signedOff = await lifecycle.SignOffAsync(projectId, "Delivered.");
        Assert.True(signedOff.Succeeded, signedOff.Reason);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task SignOffAsync_RefusedWhileACalculationIsIncomplete_NamedInTheMessage_ThenSucceedsOnceItIsComplete()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "OPEN-CALC");
        var lifecycle = Lifecycle(host);
        var domain = QuotationTestHost.Domain(host);

        var calculation = await CreateCalculationAsync(domain, projectId, "Impeller stress check");

        var refused = await lifecycle.SignOffAsync(projectId, "Delivered.");
        Assert.False(refused.Succeeded);
        Assert.Equal(ProjectLifecycleRefusal.WorkStillOpen, refused.Refusal);
        Assert.Contains("Impeller stress check", refused.Reason, StringComparison.Ordinal);
        Assert.Contains("Calculation", refused.Reason, StringComparison.Ordinal);

        await calculation.MarkCompletedAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        var signedOff = await lifecycle.SignOffAsync(projectId, "Delivered.");
        Assert.True(signedOff.Succeeded, signedOff.Reason);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task SignOffAsync_AChangeOrderCarryingTheOpenDeliverable_LiftsTheRefusal_WhileStillDraft()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "CO-CARRIES");
        var lifecycle = Lifecycle(host);
        var deliverables = QuotationTestHost.Deliverables(host);
        var quotations = QuotationTestHost.Quotations(host);

        var deliverable = await deliverables.AddDeliverableAsync(projectId, "Extra site survey");

        var refused = await lifecycle.SignOffAsync(projectId, "Delivered.");
        Assert.Equal(ProjectLifecycleRefusal.WorkStillOpen, refused.Refusal);

        var changeOrder = await quotations.CreateAsync(projectId, kind: QuotationKind.ChangeOrder);
        Assert.True(changeOrder.Succeeded, changeOrder.Reason);
        Assert.StartsWith("CO-", changeOrder.Quotation!.Reference, StringComparison.Ordinal);

        var lineAdded = await quotations.AddLineAsync(
            changeOrder.Quotation.Id, "Extra site survey — additional scope", null, null, new Money(500m, CurrencyCode.Gbp),
            carriedDeliverableId: deliverable.Id);
        Assert.True(lineAdded.Succeeded, lineAdded.Reason);

        // Still Draft — carrying starts the moment the change order exists (`WP 20.10E` scope item 3: "Draft or later, not Declined").
        var openWork = await lifecycle.GetOpenWorkAsync(projectId);
        var item = Assert.Single(openWork);
        Assert.Equal(changeOrder.Quotation.Reference, item.CarriedByReference);
        Assert.False(item.IsBlocking);

        var signedOff = await lifecycle.SignOffAsync(projectId, "Delivered, remaining scope carried by change order.");
        Assert.True(signedOff.Succeeded, signedOff.Reason);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task SignOffAsync_ADeclinedChangeOrder_DoesNotLiftTheRefusal()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "CO-DECLINED");
        var lifecycle = Lifecycle(host);
        var deliverables = QuotationTestHost.Deliverables(host);
        var quotations = QuotationTestHost.Quotations(host);

        var deliverable = await deliverables.AddDeliverableAsync(projectId, "Extra structural check");

        var changeOrder = await quotations.CreateAsync(projectId, kind: QuotationKind.ChangeOrder);
        var lineAdded = await quotations.AddLineAsync(
            changeOrder.Quotation!.Id, "Extra structural check — additional scope", null, null, new Money(400m, CurrencyCode.Gbp),
            carriedDeliverableId: deliverable.Id);
        Assert.True(lineAdded.Succeeded, lineAdded.Reason);

        Assert.True((await quotations.SendAsync(changeOrder.Quotation.Id)).Succeeded);
        var declined = await quotations.DeclineAsync(changeOrder.Quotation.Id);
        Assert.True(declined.Succeeded, declined.Reason);

        var openWork = await lifecycle.GetOpenWorkAsync(projectId);
        var item = Assert.Single(openWork);
        Assert.Null(item.CarriedByReference);
        Assert.True(item.IsBlocking);

        var refused = await lifecycle.SignOffAsync(projectId, "Delivered.");
        Assert.False(refused.Succeeded);
        Assert.Equal(ProjectLifecycleRefusal.WorkStillOpen, refused.Refusal);

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

    private static IProjectLifecycleService Lifecycle(ITempestHost host) =>
        (IProjectLifecycleService)host.Services!.GetService(typeof(IProjectLifecycleService));
}
