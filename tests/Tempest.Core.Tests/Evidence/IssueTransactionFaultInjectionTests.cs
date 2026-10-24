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
        // `TD-95` (`WP 20.1C1`): bytes live at their content hash, and the
        // attachment's own id maps to that hash — resolve through the mapping
        // rather than expecting the id as the content key.
        var hashKey = System.Text.Encoding.ASCII.GetString(
            backing.CommittedBytes(AttachmentContentStore.HashByAttachmentCollectionName, attachedId!.Value.ToString("N"))!);
        Assert.Contains(hashKey, backing.CommittedKeys(AttachmentContentStore.ContentCollectionName));
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

/// <summary>
/// `v0.26.0` (ADR-0165): <see cref="IEvidenceService.IssueInOneTransactionAsync"/>
/// commits the sheet bytes, the attachment metadata, the issue record, the
/// Issued status and any extra write together. A fault at commit leaves
/// nothing behind: no orphaned attachment, no half-issued evidence.
/// </summary>
public sealed class IssueInOneTransactionFaultInjectionTests
{
    private static readonly byte[] SheetBytes = [9, 8, 7, 6];

    private static (EvidenceService Service, EngineeringDomainContext Context, CommitFailingPersistenceStore Failing, InMemoryQueryablePersistenceStore Backing) NewService()
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
        var created = await service.CreateAsync(null, "Calc sheet", EvidenceClassification.Calculation);
        var checkResult = await service.RecordCheckAsync(created.Id, "J. Reviewer", "Client Co", "Reviewed.", CheckOutcome.Accepted);
        Assert.True(checkResult.Succeeded);
        return checkResult.Evidence!;
    }

    [Fact]
    public async Task AFaultAtCommit_LeavesNothing_NoAttachment_NoIssue_NoExtraWrite()
    {
        var (service, context, failing, backing) = NewService();
        var evidence = await NewCheckedEvidenceAsync(service);
        var rendered = false;

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => service.IssueInOneTransactionAsync(
            evidence.Id, "ISS-ONE-01", "A", "Client Co",
            renderSheetAsync: (_, _, _) =>
            {
                rendered = true;
                return Task.FromResult<IssueSheetContent?>(new IssueSheetContent("sheet.pdf", "application/pdf", SheetBytes));
            },
            alsoWrite: async (tx, token) =>
            {
                await tx.WriteAsync("Calc.Instances", "x/1", "{}", token);
                failing.FailNextCommit = true;
            }));

        Assert.True(rendered);
        Assert.Equal(EvidenceStatus.Checked, evidence.Status);
        Assert.Null(evidence.Issue);
        Assert.Empty(await evidence.GetAttachmentsAsync());

        var state = await context.ObjectStateStore.FindAsync(evidence.Id);
        Assert.NotNull(state);
        Assert.Empty(state.Attachments);
        Assert.Equal(EvidenceStatus.Checked.ToString(), state.Type(nameof(Tempest.Core.Evidence.Evidence.Status)));
        Assert.Empty(backing.CommittedKeys(AttachmentContentStore.ContentCollectionName));
        Assert.Empty(backing.CommittedKeys("Calc.Instances"));
    }

    [Fact]
    public async Task Success_CommitsSheetRecordStatusAndExtraWrite_InOneCommit()
    {
        var (service, context, _, backing) = NewService();
        var evidence = await NewCheckedEvidenceAsync(service);
        var commitsBefore = backing.CommitCount;

        var result = await service.IssueInOneTransactionAsync(
            evidence.Id, "ISS-ONE-02", "A", "Client Co",
            renderSheetAsync: (_, _, _) => Task.FromResult<IssueSheetContent?>(new IssueSheetContent("sheet.pdf", "application/pdf", SheetBytes)),
            alsoWrite: (tx, token) => tx.WriteAsync("Calc.Instances", "x/2", "{\"issued\":true}", token));

        Assert.True(result.Succeeded);
        Assert.Equal(1, backing.CommitCount - commitsBefore);
        Assert.Equal(EvidenceStatus.Issued, evidence.Status);
        Assert.NotNull(evidence.Issue);
        Assert.NotNull(evidence.Issue!.IssueSheetAttachmentId);
        var attachment = Assert.Single(await evidence.GetAttachmentsAsync());
        Assert.Equal(evidence.Issue.IssueSheetAttachmentId, attachment.Id);
        Assert.Equal("sheet.pdf", attachment.FileName);

        var content = await evidence.ReadAttachmentContentAsync(attachment.Id);
        Assert.True(content.IsAvailable);
        Assert.Equal(SheetBytes, content.Bytes);

        var state = await context.ObjectStateStore.FindAsync(evidence.Id);
        Assert.Equal(EvidenceStatus.Issued.ToString(), state!.Type(nameof(Tempest.Core.Evidence.Evidence.Status)));
        Assert.Contains("x/2", backing.CommittedKeys("Calc.Instances"));
    }

    [Fact]
    public async Task WithoutASheet_IssuesTheRecordAlone()
    {
        var (service, _, _, _) = NewService();
        var evidence = await NewCheckedEvidenceAsync(service);
        var result = await service.IssueInOneTransactionAsync(evidence.Id, "ISS-ONE-03", "A", "Client Co");
        Assert.True(result.Succeeded);
        Assert.Equal(EvidenceStatus.Issued, evidence.Status);
        Assert.Null(evidence.Issue!.IssueSheetAttachmentId);
        Assert.Empty(await evidence.GetAttachmentsAsync());
    }

    [Fact]
    public async Task FromDraft_IsRefused_WithoutWriting()
    {
        var (service, _, _, backing) = NewService();
        var created = await service.CreateAsync(null, "Draft", EvidenceClassification.Calculation);
        var before = backing.CommitCount;
        var result = await service.IssueInOneTransactionAsync(created.Id, "ISS-ONE-04", "A", "Client Co");
        Assert.Equal(EvidenceRefusal.TransitionNotPermitted, result.Refusal);
        Assert.Equal(before, backing.CommitCount);
    }
}
