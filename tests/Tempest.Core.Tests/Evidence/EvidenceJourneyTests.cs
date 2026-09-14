using Tempest.Workspace.Composition;
using Tempest.Core.Audit;
using Tempest.Core.Evidence;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Materials;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Evidence;

/// <summary>
/// The `WP 18.0A` acceptance journey: create, attach, cite (refused Draft,
/// accepted Released, pinned to the record's own revision), declare
/// figures, check (rule off), issue, revise, restart, read back — over a
/// real host and a real SQLite root, with audit rows for each act.
/// </summary>
public sealed class EvidenceJourneyTests
{
    [Fact]
    public async Task Evidence_RecordCiteDeclareCheckIssueRevise_SurvivesARestart_WithAuditRows()
    {
        using var temp = new TempDirectory();
        Guid evidenceId;
        Guid projectId;
        const string materialId = "FX-STEEL-EVIDENCE-JOURNEY";

        // ================================================================
        // FIRST HOST
        // ================================================================
        {
            var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);
            EvidenceTestHost.SignIn(host);

            projectId = await EvidenceTestHost.CreateProjectAsync(host);

            var service = EvidenceTestHost.Service(host);
            var materials = EvidenceTestHost.Materials(host);

            var evidence = await service.CreateAsync(projectId, "Bracket calculation", EvidenceClassification.Calculation);
            evidenceId = evidence.Id;

            Assert.Equal(EvidenceStatus.Draft, evidence.Status);
            Assert.Equal(projectId, evidence.ParentId);

            // ---- Attach 4 KB of bytes ----
            var bytes = new byte[4096];
            new Random(42).NextBytes(bytes);
            var attachment = await evidence.AttachContentAsync("bracket-calc.xlsx", "application/vnd.ms-excel", bytes);
            Assert.Equal(4096, attachment.SizeInBytes);

            // ---- Cite a Draft material: refused, named ----
            await materials.RegisterAsync(materialId, MaterialFixtures.Steel(materialId), MaterialFixtures.Verified());

            var draftCite = await service.CiteAsync(evidenceId, materials.LibraryName, materialId);
            Assert.False(draftCite.Succeeded);
            Assert.Equal(EvidenceRefusal.RecordNotReleased, draftCite.Refusal);
            Assert.Contains(materialId, draftCite.Reason, StringComparison.Ordinal);
            Assert.Empty(draftCite.Evidence!.Citations);

            // ---- Release, then cite: pin revision equals the record's ----
            var released = await MaterialFixtures.ReleaseAsync((MaterialCatalog)materials, materialId);

            var cite = await service.CiteAsync(evidenceId, materials.LibraryName, materialId);
            Assert.True(cite.Succeeded);
            Assert.Equal(released.RevisionNumber, cite.Citation!.Pin.RevisionNumber);
            Assert.Equal(materials.LibraryName, cite.Citation.Pin.Library);
            Assert.Equal(materialId, cite.Citation.Pin.RecordId);
            Assert.Null(cite.Citation.SourceCitationSnapshot); // the record was registered without a source

            // ---- A record with a source: the citation snapshots it (WP 18.0B meets WP 18.0A) ----
            const string citedId = "FX-STEEL-CITED";
            await materials.RegisterAsync(
                citedId, MaterialFixtures.Steel(citedId), MaterialFixtures.Verified(),
                new SourceCitation("BSI", "BS EN 10025-2", "2019", "Table 7", null, "S355"));
            await MaterialFixtures.ReleaseAsync((MaterialCatalog)materials, citedId);
            var citedWithSource = await service.CiteAsync(evidenceId, materials.LibraryName, citedId);
            Assert.True(citedWithSource.Succeeded);
            Assert.NotNull(citedWithSource.Citation!.SourceCitationSnapshot);
            Assert.Contains("BS EN 10025-2", citedWithSource.Citation.SourceCitationSnapshot, StringComparison.Ordinal);
            await service.RemoveCitationAsync(evidenceId, citedWithSource.Citation.Pin);

            // ---- Declare figures ----
            await service.DeclareFigureAsync(evidenceId, "Utilisation", DeclaredFigureRole.Result, "0.82 1");
            var afterFigures = await service.DeclareFigureAsync(evidenceId, "Max stress", DeclaredFigureRole.Result, "142 MPa");
            Assert.Equal(2, afterFigures.DeclaredFigures.Count);

            // An unknown unit is refused outright.
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.DeclareFigureAsync(evidenceId, "Nonsense", DeclaredFigureRole.Input, "5 furlongs"));

            // ---- Check, rule off, same principal succeeds ----
            var check = await service.RecordCheckAsync(evidenceId, "J. Reviewer", "Client Co", "Reviewed and accepted.", CheckOutcome.Accepted);
            Assert.True(check.Succeeded);
            Assert.Equal(EvidenceStatus.Checked, check.Evidence!.Status);
            Assert.Null(check.Evidence.Check!.CheckerIdentityId);
            Assert.Equal(EvidenceTestHost.PrincipalId, check.Evidence.Check.RecordedByIdentityId);
            Assert.Equal("J. Reviewer", check.Evidence.Check.CheckerName);

            // A second check while already Checked is refused.
            var reChecked = await service.RecordCheckAsync(evidenceId, "Someone Else", "Org", "Again.", CheckOutcome.Accepted);
            Assert.False(reChecked.Succeeded);
            Assert.Equal(EvidenceRefusal.TransitionNotPermitted, reChecked.Refusal);

            // ---- Issue ----
            var issue = await service.IssueAsync(evidenceId, "ISS-001", "A", "Client Co");
            Assert.True(issue.Succeeded);
            Assert.Equal(EvidenceStatus.Issued, issue.Evidence!.Status);
            Assert.Equal("ISS-001", issue.Evidence.Issue!.IssueReference);

            // ---- Revise: Issued -> new Draft revision; issued content stays readable ----
            var revise = await service.ReviseAsync(evidenceId);
            Assert.True(revise.Succeeded);
            Assert.Equal(EvidenceStatus.Draft, revise.Evidence!.Status);
            Assert.NotNull(revise.Evidence.Issue); // historical record kept, not cleared
            Assert.Equal(2, revise.Evidence.CurrentRevisionNumber);

            var history = await revise.Evidence.GetRevisionHistoryAsync();
            Assert.True(history.Count >= 2, "The issued revision's own content must still be readable via revision history.");

            // ---- Audit rows exist for the acts above ----
            var auditQuery = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
            var auditRows = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: evidenceId));
            Assert.NotEmpty(auditRows);
            Assert.True(auditRows.Count >= 6, $"Expected at least one audit row per act (create, attach, cite, 2 declares, check, issue, revise); found {auditRows.Count}.");

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        // ================================================================
        // SECOND HOST — restart, read everything back
        // ================================================================
        {
            var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);

            var result = await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            Assert.True(result.IsComplete, "Expected a clean rehydration of the Evidence record.");

            var domain = EvidenceTestHost.Domain(host);
            var recovered = await domain.Repository.FindAsync(evidenceId) as Core.Evidence.Evidence;

            Assert.NotNull(recovered);
            Assert.Equal(EvidenceStatus.Draft, recovered!.Status);
            Assert.Equal(projectId, recovered.ParentId);
            Assert.Single(recovered.Citations);
            Assert.Equal(materialId, recovered.Citations[0].Pin.RecordId);
            Assert.Equal(2, recovered.DeclaredFigures.Count);
            Assert.NotNull(recovered.Check);
            Assert.NotNull(recovered.Issue);
            Assert.Equal("ISS-001", recovered.Issue!.IssueReference);

            var attachments = await recovered.GetAttachmentsAsync();
            Assert.Single(attachments);
            Assert.Equal("bracket-calc.xlsx", attachments[0].FileName);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task IndependentCheck_WhenOn_RefusesTheAuthorAndAcceptsASecondPrincipal()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path, independentCheck: true);

        try
        {
            EvidenceTestHost.SignIn(host, EvidenceTestHost.PrincipalId);

            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);
            var evidence = await service.CreateAsync(projectId, "Independent check evidence", EvidenceClassification.Report);

            // The author attempts to check their own evidence: refused.
            var refused = await service.RecordCheckAsync(evidence.Id, "Author Checking Self", "Org", "Self review.", CheckOutcome.Accepted);
            Assert.False(refused.Succeeded);
            Assert.Equal(EvidenceRefusal.CheckerMustDifferFromAuthor, refused.Refusal);
            Assert.Equal(EvidenceStatus.Draft, refused.Evidence!.Status);

            // A second, different principal succeeds.
            EvidenceTestHost.SignIn(host, EvidenceTestHost.SecondPrincipalId);
            var accepted = await service.RecordCheckAsync(evidence.Id, "Second Reviewer", "Org", "Independent review.", CheckOutcome.Accepted);
            Assert.True(accepted.Succeeded);
            Assert.Equal(EvidenceTestHost.SecondPrincipalId, accepted.Evidence!.Check!.CheckerIdentityId);
            Assert.Equal(EvidenceTestHost.SecondPrincipalId, accepted.Evidence.Check.RecordedByIdentityId);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task IndependentCheck_WhenOff_AnyPrincipalRecordsTheCheck_CheckerIdentityStaysNull()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path, independentCheck: false);

        try
        {
            EvidenceTestHost.SignIn(host);

            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);
            var evidence = await service.CreateAsync(projectId, "Rule-off evidence", EvidenceClassification.Test);

            var result = await service.RecordCheckAsync(evidence.Id, "Client Reviewer", "Client Co", "Entered by hand.", CheckOutcome.AcceptedWithComments);

            Assert.True(result.Succeeded);
            Assert.Null(result.Evidence!.Check!.CheckerIdentityId);
            Assert.Equal(EvidenceTestHost.PrincipalId, result.Evidence.Check.RecordedByIdentityId);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>`WP 18.2B`, §1: the statement (and outcome) are stored exactly as given — leading/trailing whitespace and an embedded quotation mark survive untouched.</summary>
    [Fact]
    public async Task RecordCheckAsync_StoresTheStatementVerbatim_LeadingTrailingWhitespaceAndAQuotationMark()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);

        try
        {
            EvidenceTestHost.SignIn(host);

            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);
            var evidence = await service.CreateAsync(projectId, "Verbatim statement evidence", EvidenceClassification.Report);

            const string statement = "  Reviewed against \"BS EN 10025-2\" and accepted.  ";

            var result = await service.RecordCheckAsync(evidence.Id, "J. Reviewer", "Client Co", statement, CheckOutcome.AcceptedWithComments);

            Assert.True(result.Succeeded);
            Assert.Equal(statement, result.Evidence!.Check!.Statement);
            Assert.Equal(CheckOutcome.AcceptedWithComments, result.Evidence.Check.Outcome);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>`WP 18.2B`, closing a `WP 18.2A` gap: a subject can be set and cleared while Draft, and is refused once Issued.</summary>
    [Fact]
    public async Task SetSubjectAsync_SetsAndClearsWhileDraft_RefusedOnceIssued()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);

        try
        {
            EvidenceTestHost.SignIn(host);

            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var partId = Guid.NewGuid(); // SubjectId is never dereferenced or validated as a real object (`D-028`) — an arbitrary id proves the point.
            var service = EvidenceTestHost.Service(host);
            var evidence = await service.CreateAsync(projectId, "Subject retag evidence", EvidenceClassification.Calculation);
            Assert.Null(evidence.SubjectId);

            var tagged = await service.SetSubjectAsync(evidence.Id, partId);
            Assert.True(tagged.Succeeded);
            Assert.Equal(partId, tagged.Evidence!.SubjectId);

            var cleared = await service.SetSubjectAsync(evidence.Id, null);
            Assert.True(cleared.Succeeded);
            Assert.Null(cleared.Evidence!.SubjectId);

            await service.SetSubjectAsync(evidence.Id, partId);
            await service.RecordCheckAsync(evidence.Id, "J. Reviewer", "Client Co", "Accepted.", CheckOutcome.Accepted);
            var issued = await service.IssueAsync(evidence.Id, "ISS-SUBJ-01", "A", "Client Co");
            Assert.True(issued.Succeeded);

            var refused = await service.SetSubjectAsync(evidence.Id, null);
            Assert.False(refused.Succeeded);
            Assert.Equal(EvidenceRefusal.SubjectLockedAfterIssue, refused.Refusal);
            Assert.Equal(partId, refused.Evidence!.SubjectId); // unchanged by the refusal
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>`WP 18.2B`, §2: the issue-sheet-attachment plumbing — <c>RecordIssueSheetAsync</c> points <c>Issue.IssueSheetAttachmentId</c> at an already-stored attachment, and refuses (by throwing, being plumbing rather than a governed act) when there is no issue record yet.</summary>
    [Fact]
    public async Task RecordIssueSheetAsync_PointsTheIssueRecordAtTheAttachment_AndRequiresAnIssueRecordFirst()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);

        try
        {
            EvidenceTestHost.SignIn(host);

            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);
            var evidence = await service.CreateAsync(projectId, "Issue sheet plumbing evidence", EvidenceClassification.Calculation);

            // No issue record yet: refused by throwing (plumbing, not a governed act with its own refusal result).
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RecordIssueSheetAsync(evidence.Id, Guid.NewGuid()));

            await service.RecordCheckAsync(evidence.Id, "J. Reviewer", "Client Co", "Accepted.", CheckOutcome.Accepted);
            var issued = await service.IssueAsync(evidence.Id, "ISS-PLUMB-01", "A", "Client Co");
            Assert.True(issued.Succeeded);
            Assert.Null(issued.Evidence!.Issue!.IssueSheetAttachmentId);

            var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
            var attachment = await issued.Evidence.AttachContentAsync("issue-sheet.pdf", "application/pdf", bytes);

            var afterAttach = await service.RecordIssueSheetAsync(evidence.Id, attachment.Id);
            Assert.Equal(attachment.Id, afterAttach.Issue!.IssueSheetAttachmentId);

            // Every other field of the issue record is untouched.
            Assert.Equal("ISS-PLUMB-01", afterAttach.Issue.IssueReference);
            Assert.Equal("A", afterAttach.Issue.Revision);
            Assert.Equal("Client Co", afterAttach.Issue.Client);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
