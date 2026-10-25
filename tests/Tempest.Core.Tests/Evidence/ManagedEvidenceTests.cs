using Tempest.Core.Evidence;
using Tempest.Core.Tests.Plugins;
using Tempest.Workspace.Composition;

namespace Tempest.Core.Tests.Evidence;

/// <summary>
/// `ADR-0173`, review H2: evidence a surface owns (a calculation worksheet's) is checked, issued and revised from that surface only. The generic
/// Evidence acts refuse it; the owner's own calls move it; ordinary evidence is unchanged; the mark survives a restart of the store.
/// </summary>
public sealed class ManagedEvidenceTests
{
    private static readonly EvidenceOwner Owner = new(Tempest.Core.Evidence.Evidence.CalcWorksheetOwner);

    [Fact]
    public async Task Managed_Evidence_Cannot_Be_Checked_Issued_Or_Revised_By_The_Generic_Acts()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path, independentCheck: false);

        try
        {
            EvidenceTestHost.SignIn(host);
            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);
            var managed = await service.CreateManagedAsync(projectId, "Lifting lug calc", EvidenceClassification.Calculation, Tempest.Core.Evidence.Evidence.CalcWorksheetOwner);
            Assert.Equal("calc.worksheet", managed.ManagedBy);

            // The generic check, issue and revise are all refused, and say where to go.
            var check = await service.RecordCheckAsync(managed.Id, "Author", "Org", "Self review.", CheckOutcome.Accepted);
            Assert.False(check.Succeeded);
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, check.Refusal);
            Assert.Contains("calculation worksheet", check.Reason, StringComparison.Ordinal);
            Assert.Contains("Open the worksheet", check.Reason, StringComparison.Ordinal);
            Assert.Equal(EvidenceStatus.Draft, managed.Status);
            Assert.Null(managed.Check);

            var issue = await service.IssueAsync(managed.Id, "REF-1", "A", "Client");
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, issue.Refusal);
            Assert.Equal(EvidenceStatus.Draft, managed.Status);

            var viaOneTransaction = await service.IssueInOneTransactionAsync(managed.Id, "REF-1", "A", "Client");
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, viaOneTransaction.Refusal);
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, (await service.RecordCheckInOneTransactionAsync(managed.Id, "Author", "Org", "x", CheckOutcome.Accepted, alsoWrite: null)).Refusal);

            // A wrong owner is no better than none.
            var wrong = await service.RecordCheckInOneTransactionAsync(managed.Id, "Author", "Org", "x", CheckOutcome.Accepted, alsoWrite: null, owner: new EvidenceOwner("someone.else"));
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, wrong.Refusal);

            // The owner moves it: check, issue, and then only the owner's surface can revise (the generic revise is refused too).
            var ownerCheck = await service.RecordCheckInOneTransactionAsync(managed.Id, "Chris Checker", "Check Co", "Checked on the worksheet.", CheckOutcome.Accepted, alsoWrite: null, owner: Owner);
            Assert.True(ownerCheck.Succeeded, ownerCheck.Reason);
            Assert.Equal(EvidenceStatus.Checked, managed.Status);

            Assert.Equal(EvidenceRefusal.ManagedElsewhere, (await service.IssueAsync(managed.Id, "REF-1", "A", "Client")).Refusal);
            var ownerIssue = await service.IssueInOneTransactionAsync(managed.Id, "REF-1", "A", "Client", renderSheetAsync: null, alsoWrite: null, owner: Owner);
            Assert.True(ownerIssue.Succeeded, ownerIssue.Reason);
            Assert.Equal(EvidenceStatus.Issued, managed.Status);

            var revise = await service.ReviseAsync(managed.Id);
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, revise.Refusal);
            Assert.Equal(EvidenceStatus.Issued, managed.Status);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Ordinary_Evidence_Is_Unchanged_And_Does_Not_Write_The_Managed_Key()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path, independentCheck: false);

        try
        {
            EvidenceTestHost.SignIn(host);
            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);
            var ordinary = await service.CreateAsync(projectId, "A report", EvidenceClassification.Report);
            Assert.Null(ordinary.ManagedBy);

            Assert.True((await service.RecordCheckAsync(ordinary.Id, "Reviewer", "Org", "Reviewed.", CheckOutcome.Accepted)).Succeeded);
            Assert.True((await service.IssueAsync(ordinary.Id, "REF-2", "A", "Client")).Succeeded);
            Assert.True((await service.ReviseAsync(ordinary.Id)).Succeeded);

            // An owner presented for ordinary evidence changes nothing either.
            var other = await service.CreateAsync(projectId, "Another report", EvidenceClassification.Report);
            Assert.True((await service.RecordCheckInOneTransactionAsync(other.Id, "Reviewer", "Org", "Reviewed.", CheckOutcome.Accepted, alsoWrite: null, owner: Owner)).Succeeded);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Existing_Evidence_Can_Be_Adopted_By_Its_Owner_And_Is_Then_Managed()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path, independentCheck: false);

        try
        {
            EvidenceTestHost.SignIn(host);
            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);
            var legacy = await service.CreateAsync(projectId, "Calc made before evidence could be managed", EvidenceClassification.Calculation);
            Assert.Null(legacy.ManagedBy);

            var adopted = await service.ManageAsync(legacy.Id, Owner);
            Assert.True(adopted.Succeeded);
            Assert.Equal("calc.worksheet", legacy.ManagedBy);
            Assert.True((await service.ManageAsync(legacy.Id, Owner)).Succeeded);
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, (await service.ManageAsync(legacy.Id, new EvidenceOwner("other"))).Refusal);
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, (await service.RecordCheckAsync(legacy.Id, "Reviewer", "Org", "x", CheckOutcome.Accepted)).Refusal);
            Assert.Equal(EvidenceRefusal.EvidenceNotFound, (await service.ManageAsync(Guid.NewGuid(), Owner)).Refusal);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task The_Managed_Mark_Survives_A_Restart()
    {
        using var temp = new TempDirectory();
        Guid evidenceId;
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path, independentCheck: false);

        try
        {
            EvidenceTestHost.SignIn(host);
            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var created = await EvidenceTestHost.Service(host).CreateManagedAsync(projectId, "Calc", EvidenceClassification.Calculation, Tempest.Core.Evidence.Evidence.CalcWorksheetOwner);
            evidenceId = created.Id;
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        var (host2, manager2) = await EvidenceTestHost.StartAsync(temp.Path, independentCheck: false);

        try
        {
            await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host2);
            EvidenceTestHost.SignIn(host2);
            var service = EvidenceTestHost.Service(host2);
            var refused = await service.RecordCheckAsync(evidenceId, "Reviewer", "Org", "x", CheckOutcome.Accepted);
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, refused.Refusal);
            Assert.Equal("calc.worksheet", refused.Evidence!.ManagedBy);
        }
        finally
        {
            await manager2.ShutdownAsync();
            await host2.DisposeAsync();
        }
    }
}
