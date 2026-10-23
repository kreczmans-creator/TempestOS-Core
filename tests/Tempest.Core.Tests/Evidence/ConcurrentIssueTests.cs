using Tempest.Core.Bearings;
using Tempest.Core.Configuration;
using Tempest.Core.Constants;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Standards;
using Tempest.Core.Tests.EngineeringDomain;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.Evidence;

/// <summary>
/// `v0.26.0` Phase 6 (Red PoC → Purple regression): two Issues raced on one
/// checked Evidence record used to both succeed, because the status check
/// ran before the transaction opened. The Issue transaction now re-reads
/// the stored status under its own transaction and refuses when another
/// issue committed first, so exactly one issued revision results.
/// </summary>
public sealed class ConcurrentIssueTests
{
    private static (EvidenceService Service, EngineeringDomainContext Context) NewService()
    {
        var backing = new InMemoryQueryablePersistenceStore();
        var failing = new CommitFailingPersistenceStore(backing);
        var context = TestEngineeringDomain.NewContextOver(failing, backing);
        var documentStore = new EngineeringDocumentStore(backing, new CurrentPrincipalAccessor());
        var service = new EvidenceService(
            context,
            new MaterialCatalog(documentStore, backing),
            new FastenerCatalog(documentStore, backing),
            new BearingCatalog(documentStore, backing),
            new StandardCatalog(documentStore, backing),
            new ConstantCatalog(documentStore, backing),
            new CurrentPrincipalAccessor(),
            new ConfigurationBuilder().Build());
        return (service, context);
    }

    [Fact]
    public async Task Racing_Issues_On_One_Evidence_Leave_Exactly_One_Issued_Revision()
    {
        var (service, context) = NewService();
        var created = await service.CreateAsync(null, "Raced calculation", EvidenceClassification.Calculation);
        Assert.True((await service.RecordCheckAsync(created.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Task.Run(() => service.IssueInOneTransactionAsync(
            created.Id, $"ISS-RACE-{i}", "A", "Client Co",
            renderSheetAsync: (_, _, _) => Task.FromResult<IssueSheetContent?>(new IssueSheetContent($"sheet-{i}.txt", "text/plain", new byte[] { 1, 2, 3 }))))));

        Assert.Equal(1, results.Count(r => r.Succeeded));
        Assert.All(results.Where(r => !r.Succeeded), r => Assert.Equal(EvidenceRefusal.TransitionNotPermitted, r.Refusal));

        var stored = (Tempest.Core.Evidence.Evidence)(await context.Repository.FindAsync(created.Id))!;
        Assert.Equal(EvidenceStatus.Issued, stored.Status);
        Assert.Single(await stored.GetAttachmentsAsync());
        Assert.StartsWith("ISS-RACE-", stored.Issue!.IssueReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_After_An_Issue_Is_Refused_Without_A_Second_Attachment()
    {
        var (service, context) = NewService();
        var created = await service.CreateAsync(null, "Issued twice", EvidenceClassification.Calculation);
        Assert.True((await service.RecordCheckAsync(created.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await service.IssueInOneTransactionAsync(created.Id, "ISS-1", "A", "Client Co")).Succeeded);
        var second = await service.IssueInOneTransactionAsync(created.Id, "ISS-2", "A", "Client Co");
        Assert.False(second.Succeeded);
        var stored = (Tempest.Core.Evidence.Evidence)(await context.Repository.FindAsync(created.Id))!;
        Assert.Equal("ISS-1", stored.Issue!.IssueReference);
    }
}
