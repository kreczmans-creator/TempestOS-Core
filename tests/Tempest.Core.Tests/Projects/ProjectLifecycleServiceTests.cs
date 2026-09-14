using Tempest.Core.Audit;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.Runtime;
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

    private static IProjectLifecycleService Lifecycle(ITempestHost host) =>
        (IProjectLifecycleService)host.Services!.GetService(typeof(IProjectLifecycleService));
}
