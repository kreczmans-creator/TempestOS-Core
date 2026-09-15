using Tempest.Core.Bearings;
using Tempest.Core.Configuration;
using Tempest.Core.Constants;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.Standards;
using Tempest.Core.Tests.EngineeringDomain;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.Evidence;

/// <summary>
/// `B2` (`WP 20.3A`): issuing evidence with a rendered sheet used to be
/// three separate commits — the issue record, the sheet's bytes/attachment,
/// and the attachment-id pointer — and a crash between the first two left
/// an <see cref="EvidenceStatus.Issued"/> record with no sheet. It is now
/// two: the sheet (bytes and metadata, one transaction, `WP 17.1B`)
/// attached before the issue record is ever written, and the issue record
/// itself — with the real attachment id already in it — as the one
/// remaining write. These facts stage the two faults a process kill could
/// land on and show neither can reproduce the old hazard, exactly as
/// <see cref="Tempest.Core.Tests.EngineeringDomain.TransactionalWriteFaultInjectionTests"/>
/// does for <c>Part</c>.
/// </summary>
public sealed class IssueTransactionFaultInjectionTests
{
    private static readonly byte[] SheetBytes = [1, 2, 3, 4, 5];

    private static (EvidenceService Service, EngineeringDomainContext Context, CommitFailingPersistenceStore Failing, InMemoryQueryablePersistenceStore Backing)
        NewService()
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

        return (service, context, failing, backing);
    }

    private static async Task<Tempest.Core.Evidence.Evidence> NewCheckedEvidenceAsync(EvidenceService service)
    {
        var created = await service.CreateAsync(null, "Bracket calculation", EvidenceClassification.Calculation);
        var checkResult = await service.RecordCheckAsync(
            created.Id, "J. Reviewer", "Client Co", "Reviewed and accepted.", CheckOutcome.Accepted);
        Assert.True(checkResult.Succeeded);
        return checkResult.Evidence!;
    }

    /// <summary>
    /// A fault while the sheet is being attached — the first of the two
    /// remaining writes — leaves the evidence exactly where it stood before
    /// <see cref="IEvidenceService.IssueAsync"/> was called: Checked, with
    /// no attachment and no issue record. Never Issued.
    /// </summary>
    [Fact]
    public async Task AFaultAttachingTheIssueSheet_LeavesEvidenceChecked_NeverIssued()
    {
        var (service, context, failing, backing) = NewService();
        var evidence = await NewCheckedEvidenceAsync(service);

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => service.IssueAsync(
            evidence.Id, "ISS-FAULT-01", "A", "Client Co",
            attachIssueSheetAsync: async (ev, issuedAtUtc, token) =>
            {
                failing.FailNextCommit = true;
                var attachment = await ev.AttachContentAsync("issue-sheet.pdf", "application/pdf", SheetBytes, token)
                    .ConfigureAwait(false);
                return attachment.Id;
            }));

        Assert.Equal(EvidenceStatus.Checked, evidence.Status);
        Assert.Null(evidence.Issue);
        Assert.Empty(await evidence.GetAttachmentsAsync());

        var state = await context.ObjectStateStore.FindAsync(evidence.Id);
        Assert.NotNull(state);
        Assert.Empty(state.Attachments);
        Assert.Equal(EvidenceStatus.Checked.ToString(), state.Type(nameof(Tempest.Core.Evidence.Evidence.Status)));
        Assert.Null(state.Type(nameof(Tempest.Core.Evidence.Evidence.Issue)));

        // Not even orphaned bytes: the attach transaction's own commit
        // failed, so nothing landed (`WP 17.1B`'s own guarantee).
        Assert.Empty(backing.CommittedKeys(AttachmentContentStore.ContentCollectionName));
    }

    /// <summary>
    /// A fault in the issue transaction itself — after the sheet already
    /// attached successfully — leaves the evidence Checked, not Issued, and
    /// with no issue record naming the attachment. At worst one harmless,
    /// unreferenced attachment is left behind — never the old hazard of an
    /// Issued record with no sheet.
    /// </summary>
    [Fact]
    public async Task AFaultInTheIssueTransaction_LeavesEvidenceChecked_WithOnlyAnOrphanedAttachment()
    {
        var (service, context, failing, backing) = NewService();
        var evidence = await NewCheckedEvidenceAsync(service);

        Guid? attachedId = null;

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => service.IssueAsync(
            evidence.Id, "ISS-FAULT-02", "A", "Client Co",
            attachIssueSheetAsync: async (ev, issuedAtUtc, token) =>
            {
                var attachment = await ev.AttachContentAsync("issue-sheet.pdf", "application/pdf", SheetBytes, token)
                    .ConfigureAwait(false);
                attachedId = attachment.Id;

                // The attach above already committed; only the issue
                // transaction that follows (back in IssueAsync) should fail.
                failing.FailNextCommit = true;
                return attachment.Id;
            }));

        Assert.NotNull(attachedId);
        Assert.Equal(EvidenceStatus.Checked, evidence.Status);
        Assert.Null(evidence.Issue);

        // The attachment is there (harmless, unreferenced) - the sheet was
        // never lost, it simply never got named by an issue record.
        Assert.Single(await evidence.GetAttachmentsAsync(), a => a.Id == attachedId);

        var state = await context.ObjectStateStore.FindAsync(evidence.Id);
        Assert.NotNull(state);
        Assert.Equal(EvidenceStatus.Checked.ToString(), state.Type(nameof(Tempest.Core.Evidence.Evidence.Status)));
        Assert.Null(state.Type(nameof(Tempest.Core.Evidence.Evidence.Issue)));
        Assert.Contains(state.Attachments, a => a.Id == attachedId);

        // The bytes really did land - the attach transaction was real and unaffected.
        Assert.Contains(attachedId!.Value.ToString("N"), backing.CommittedKeys(AttachmentContentStore.ContentCollectionName));
    }

    /// <summary>
    /// The contrast: when both writes succeed, the issue record and the
    /// attachment-id pointer are the very same write — there is no window
    /// in which the durable state shows Issued with a null pointer, because
    /// the <see cref="IssueRecord"/> is only ever constructed, and only
    /// ever committed, with the real id already in it.
    /// </summary>
    [Fact]
    public async Task IssueAsync_WithARenderer_CommitsTheSheetThenTheRecordAndThePointerTogether()
    {
        var (service, context, _, backing) = NewService();
        var evidence = await NewCheckedEvidenceAsync(service);

        var commitsBeforeIssue = backing.CommitCount;
        Guid? attachedId = null;

        var result = await service.IssueAsync(
            evidence.Id, "ISS-OK-01", "A", "Client Co",
            attachIssueSheetAsync: async (ev, issuedAtUtc, token) =>
            {
                var attachment = await ev.AttachContentAsync("issue-sheet.pdf", "application/pdf", SheetBytes, token)
                    .ConfigureAwait(false);
                attachedId = attachment.Id;
                return attachment.Id;
            });

        Assert.True(result.Succeeded);
        Assert.Equal(EvidenceStatus.Issued, result.Evidence!.Status);
        Assert.Equal(attachedId, result.Evidence.Issue!.IssueSheetAttachmentId);

        // Exactly two commits landed for this call: the attach, then the
        // issue record carrying the pointer - never a third for a
        // separate pointer patch.
        Assert.Equal(2, backing.CommitCount - commitsBeforeIssue);
        Assert.Equal(0, backing.RollbackCount);

        var state = await context.ObjectStateStore.FindAsync(evidence.Id);
        Assert.NotNull(state);
        Assert.Equal(EvidenceStatus.Issued.ToString(), state.Type(nameof(Tempest.Core.Evidence.Evidence.Status)));
        Assert.Contains(attachedId!.Value.ToString(), state.Type(nameof(Tempest.Core.Evidence.Evidence.Issue)), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Without a renderer, issuing is still exactly one commit — unaffected by `B2`'s reordering.</summary>
    [Fact]
    public async Task IssueAsync_WithNoRenderer_IsStillOneCommit_AndIssuesCleanly()
    {
        var (service, _, _, backing) = NewService();
        var evidence = await NewCheckedEvidenceAsync(service);

        var commitsBeforeIssue = backing.CommitCount;

        var result = await service.IssueAsync(evidence.Id, "ISS-OK-02", "A", "Client Co");

        Assert.True(result.Succeeded);
        Assert.Equal(EvidenceStatus.Issued, result.Evidence!.Status);
        Assert.Null(result.Evidence.Issue!.IssueSheetAttachmentId);
        Assert.Equal(1, backing.CommitCount - commitsBeforeIssue);
    }
}
