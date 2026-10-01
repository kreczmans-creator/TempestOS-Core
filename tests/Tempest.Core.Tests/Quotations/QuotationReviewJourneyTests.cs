using System.Reflection;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Quotations;

/// <summary>
/// Runbook C3 (PO: "should have the ability to save the quote without
/// exporting or sending, save it as a draft and then review by second
/// person, then export becomes R1 … make the hourly rate a drop down from
/// whatever rate card is applied to the project or 'fixed'"): Save draft;
/// Draft → In review → Approved (R1) by a second person → Sent; the same
/// person refused; Return to draft with a comment; an edit after approval
/// starting a new draft that approves as R2; rate-card-priced lines; and a
/// quotation persisted before any of this still loading sensibly.
/// </summary>
public sealed class QuotationReviewJourneyTests
{
    private const string AuthorId = QuotationTestHost.PrincipalId;

    [Fact]
    public async Task SaveDraft_SubmitApproveAsR1BySecondPerson_ThenSend_WithAnAuditRowForEveryStep()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = QuotationTestHost.Principals(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-1");
        var created = await quotations.CreateAsync(projectId);
        var quoteId = created.Quotation!.Id;
        Assert.Equal(AuthorId, created.Quotation.AuthorIdentityId);
        Assert.Null(created.Quotation.RevisionLabel);
        Assert.Null(created.Quotation.Review.DraftSavedAt);

        // ---- A line change saves the draft; Save draft makes it explicit ----
        var line = await quotations.AddLineAsync(quoteId, "Concept design", null, null, Gbp(1_200m));
        Assert.True(line.Succeeded, line.Reason);
        Assert.NotNull(line.Quotation!.Review.DraftSavedAt);

        var saved = await quotations.SaveDraftAsync(quoteId);
        Assert.True(saved.Succeeded, saved.Reason);
        Assert.Equal(QuotationStatus.Draft, saved.Quotation!.Status);
        Assert.Null(saved.Quotation.SentOn);

        // ---- A draft cannot be sent ----
        var draftSend = await quotations.SendAsync(quoteId);
        Assert.False(draftSend.Succeeded);
        Assert.Equal(QuotationRefusal.TransitionNotPermitted, draftSend.Refusal);
        Assert.Contains("approved", draftSend.Reason, StringComparison.Ordinal);

        // ---- Submit: Draft → In review, lines fixed, still not sendable ----
        var submitted = await quotations.SubmitForReviewAsync(quoteId);
        Assert.True(submitted.Succeeded, submitted.Reason);
        Assert.Equal(QuotationStatus.InReview, submitted.Quotation!.Status);
        Assert.Equal(AuthorId, submitted.Quotation.Review.SubmittedBy);
        Assert.Equal(QuotationRefusal.QuotationNotDraft, (await quotations.AddLineAsync(quoteId, "Too late", null, null, Gbp(1m))).Refusal);
        Assert.False((await quotations.SendAsync(quoteId)).Succeeded);
        Assert.False((await quotations.SaveDraftAsync(quoteId)).Succeeded);

        // ---- Approve as a second person: R1 ----
        var approved = await QuotationReviewTestSupport.AsAsync(principals, "reviewer-b", () => quotations.ApproveAsync(quoteId));
        Assert.True(approved.Succeeded, approved.Reason);
        Assert.Equal(QuotationStatus.Approved, approved.Quotation!.Status);
        Assert.Equal(1, approved.Quotation.RevisionNumber);
        Assert.Equal("R1", approved.Quotation.RevisionLabel);
        var revision = Assert.Single(approved.Quotation.Review.Revisions);
        Assert.Equal("reviewer-b", revision.ApprovedBy);
        Assert.Equal(AuthorId, revision.SubmittedBy);

        // ---- Send, Accept: the existing flow, unchanged after approval ----
        var sent = await quotations.SendAsync(quoteId);
        Assert.True(sent.Succeeded, sent.Reason);
        Assert.Equal(QuotationStatus.Sent, sent.Quotation!.Status);
        Assert.Equal("R1", sent.Quotation.RevisionLabel);
        var accepted = await quotations.AcceptAsync(quoteId);
        Assert.True(accepted.Succeeded, accepted.Reason);
        Assert.Equal("R1", accepted.Quotation!.RevisionLabel);

        // ---- An audit row for every step, naming who did it ----
        var auditQuery = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
        var audit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: quoteId));
        var details = audit.Select(a => string.Join(" | ", a.Detail.Values)).ToList();
        Assert.Contains(details, d => d.Contains("Draft saved", StringComparison.Ordinal));
        Assert.Contains(details, d => d.Contains($"Submitted for review by '{AuthorId}'", StringComparison.Ordinal));
        Assert.Contains(details, d => d.Contains("Approved as R1 by 'reviewer-b'", StringComparison.Ordinal));
        Assert.Contains(audit, a => a.ActorId == "reviewer-b");

        // ---- Restart: the review state is still there ----
        await manager.ShutdownAsync();
        await host.DisposeAsync();

        var (second, secondManager) = await QuotationTestHost.StartAsync(temp.Path);
        await Tempest.Workspace.Composition.EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(second);
        var reloaded = (Quotation)(await QuotationTestHost.Domain(second).Repository.FindAsync(quoteId))!;
        Assert.Equal("R1", reloaded.RevisionLabel);
        Assert.Equal(AuthorId, reloaded.AuthorIdentityId);
        Assert.Equal("reviewer-b", Assert.Single(reloaded.Review.Revisions).ApprovedBy);
        await secondManager.ShutdownAsync();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task Approve_IsRefused_ForTheAuthor_ForTheSubmitter_AndWithNobodySignedIn()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = QuotationTestHost.Principals(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-2");
        var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
        await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m));
        Assert.True((await quotations.SubmitForReviewAsync(quoteId)).Succeeded);

        // The author approving their own quote.
        var selfApproved = await quotations.ApproveAsync(quoteId);
        Assert.False(selfApproved.Succeeded);
        Assert.Equal(QuotationRefusal.ReviewerMustDifferFromAuthor, selfApproved.Refusal);
        Assert.Contains("second person", selfApproved.Reason, StringComparison.Ordinal);
        Assert.Contains("switch person", selfApproved.Reason, StringComparison.Ordinal);
        Assert.Equal(QuotationStatus.InReview, selfApproved.Quotation!.Status);

        // Someone else who submitted it (after a return) cannot approve it either.
        await QuotationReviewTestSupport.AsAsync(principals, "colleague-c", async () =>
        {
            Assert.True((await quotations.ReturnToDraftAsync(quoteId, "Check the survey scope.")).Succeeded);
            Assert.True((await quotations.SubmitForReviewAsync(quoteId)).Succeeded);
            var submitterApproved = await quotations.ApproveAsync(quoteId);
            Assert.Equal(QuotationRefusal.ReviewerMustDifferFromAuthor, submitterApproved.Refusal);
            return submitterApproved;
        });

        // Nobody signed in.
        var nobody = await QuotationReviewTestSupport.AsAsync<QuotationResult>(principals, "placeholder", async () =>
        {
            ((Tempest.Core.Identity.CurrentPrincipalAccessor)principals).SetCurrent(null);
            return await quotations.ApproveAsync(quoteId);
        });
        Assert.Equal(QuotationRefusal.NoPrincipalSignedIn, nobody.Refusal);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task ReturnToDraft_NeedsAComment_KeepsIt_AndTheNextSubmissionClearsIt()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = QuotationTestHost.Principals(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-3");
        var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
        await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m));

        // Return is only for a quote in review.
        Assert.Equal(QuotationRefusal.TransitionNotPermitted, (await quotations.ReturnToDraftAsync(quoteId, "Not yet")).Refusal);
        Assert.True((await quotations.SubmitForReviewAsync(quoteId)).Succeeded);

        var blank = await QuotationReviewTestSupport.AsAsync(principals, "reviewer-b", () => quotations.ReturnToDraftAsync(quoteId, "  "));
        Assert.Equal(QuotationRefusal.CommentRequired, blank.Refusal);

        var returned = await QuotationReviewTestSupport.AsAsync(principals, "reviewer-b", () => quotations.ReturnToDraftAsync(quoteId, "Add the travel allowance."));
        Assert.True(returned.Succeeded, returned.Reason);
        Assert.Equal(QuotationStatus.Draft, returned.Quotation!.Status);
        Assert.Equal("reviewer-b", returned.Quotation.Review.ReturnedBy);
        Assert.Equal("Add the travel allowance.", returned.Quotation.Review.ReturnComment);
        Assert.Equal(0, returned.Quotation.RevisionNumber);

        // Editable again; the next submission clears the comment.
        Assert.True((await quotations.AddLineAsync(quoteId, "Travel", null, null, Gbp(80m))).Succeeded);
        var resubmitted = await quotations.SubmitForReviewAsync(quoteId);
        Assert.True(resubmitted.Succeeded, resubmitted.Reason);
        Assert.Null(resubmitted.Quotation!.Review.ReturnComment);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task AnEditAfterApproval_StartsANewDraft_ThatApprovesAsR2()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = QuotationTestHost.Principals(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-4");
        var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
        var added = await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m));
        await QuotationReviewTestSupport.SubmitAndApproveAsync(quotations, principals, quoteId);

        var edited = await quotations.UpdateLineAsync(quoteId, added.Quotation!.Lines[0].Id, "Survey and report", null, null, Gbp(650m));
        Assert.True(edited.Succeeded, edited.Reason);
        Assert.Equal(QuotationStatus.Draft, edited.Quotation!.Status);
        Assert.Equal(1, edited.Quotation.RevisionNumber);
        Assert.Null(edited.Quotation.RevisionLabel);
        Assert.False((await quotations.SendAsync(quoteId)).Succeeded);

        var r2 = await QuotationReviewTestSupport.SubmitAndApproveAsync(quotations, principals, quoteId);
        Assert.Equal("R2", r2.Quotation!.RevisionLabel);
        Assert.Equal(["R1", "R2"], r2.Quotation.Review.Revisions.Select(r => r.Label));
        Assert.True((await quotations.SendAsync(quoteId)).Succeeded);

        var auditQuery = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
        var audit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: quoteId));
        Assert.Contains(audit, a => string.Join(" | ", a.Detail.Values).Contains("Approved R1 reopened as a new draft", StringComparison.Ordinal));

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task ALinePricedFromThePinnedRateCard_TakesTheCardRate_AndRefusesAnythingElse()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-5");
        var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;

        // No card pinned: there is no rate to take.
        var unpinned = await quotations.AddLineAsync(quoteId, "Design", 10m, null, null, rateCardServiceCode: "ENG-1");
        Assert.Equal(QuotationRefusal.RateCardEntryNotFound, unpinned.Refusal);
        Assert.Contains("pin a rate card", unpinned.Reason, StringComparison.Ordinal);

        var rateCards = QuotationTestHost.RateCards(host);
        await rateCards.RegisterAsync("QUO-REV-CARD", Card(), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, "QUO-REV-CARD");
        Assert.True((await QuotationTestHost.ProjectCommercial(host).PinRateCardAsync(projectId, "QUO-REV-CARD")).Succeeded);
        var pinnedQuoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;

        var priced = await quotations.AddLineAsync(pinnedQuoteId, "Design", 10m, null, null, rateCardServiceCode: "ENG-1");
        Assert.True(priced.Succeeded, priced.Reason);
        var line = Assert.Single(priced.Quotation!.Lines);
        Assert.Equal(Gbp(150m), line.Rate);
        Assert.Equal(Gbp(1_500m), line.Amount);
        Assert.Equal("ENG-1", line.RateCardServiceCode);

        Assert.Equal(QuotationRefusal.RateCardEntryNotFound, (await quotations.AddLineAsync(pinnedQuoteId, "X", 1m, null, null, rateCardServiceCode: "NOPE")).Refusal);
        Assert.Equal(QuotationRefusal.RateCardEntryNotFound, (await quotations.AddLineAsync(pinnedQuoteId, "X", 1m, null, null, rateCardServiceCode: "FIX-1")).Refusal);
        Assert.Equal(QuotationRefusal.InvalidLine, (await quotations.AddLineAsync(pinnedQuoteId, "X", 1m, Gbp(99m), null, rateCardServiceCode: "ENG-1")).Refusal);
        Assert.Equal(QuotationRefusal.InvalidLine, (await quotations.AddLineAsync(pinnedQuoteId, "X", null, null, null, rateCardServiceCode: "ENG-1")).Refusal);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Theory]
    [InlineData(QuotationStatus.Draft, 0, null)]
    [InlineData(QuotationStatus.Sent, 1, "R1")]
    [InlineData(QuotationStatus.Accepted, 1, "R1")]
    [InlineData(QuotationStatus.Declined, 1, "R1")]
    public async Task AQuotationPersistedBeforeRunbookC3_StillLoads_DraftUnreviewed_IssuedOnesAsR1(QuotationStatus legacyStatus, int expectedRevision, string? expectedLabel)
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, $"QUO-REV-L{(int)legacyStatus}");
        var quote = (await quotations.CreateAsync(projectId)).Quotation!;
        await quotations.AddLineAsync(quote.Id, "Legacy line", 2m, Gbp(100m), null);

        // The stored shape before runbook C3: no Review, no AuthorIdentityId,
        // and a line JSON with no RateCardServiceCode.
        var state = quote.CaptureState();
        var legacyTypeState = state.TypeState
            .Where(kv => kv.Key is not (nameof(Quotation.Review) or nameof(Quotation.AuthorIdentityId)))
            .ToDictionary(kv => kv.Key, kv => kv.Key == nameof(Quotation.Lines) ? kv.Value!.Replace(",\"RateCardServiceCode\":null", string.Empty, StringComparison.Ordinal) : kv.Value);
        legacyTypeState[nameof(Quotation.Status)] = legacyStatus.ToString();
        Assert.DoesNotContain("RateCardServiceCode", legacyTypeState[nameof(Quotation.Lines)], StringComparison.Ordinal);

        var legacy = RehydrateQuotation(quote, state with { TypeState = legacyTypeState });

        Assert.Equal(legacyStatus, legacy.Status);
        Assert.Null(legacy.AuthorIdentityId);
        Assert.Equal(expectedRevision, legacy.RevisionNumber);
        Assert.Equal(expectedLabel, legacy.RevisionLabel);
        Assert.Empty(legacy.Review.Revisions);
        var line = Assert.Single(legacy.Lines);
        Assert.Null(line.RateCardServiceCode);
        Assert.Equal(Gbp(200m), line.Amount);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task Approve_IsRefused_ForTheAuthor_EvenWhenAColleagueEditedAndSubmitted_AndAThirdPersonMayApprove()
    {
        // Colour review board B8: the author never touches a line or the
        // submission here, so only the author clause can refuse them.
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = QuotationTestHost.Principals(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-B8");
        var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
        await QuotationReviewTestSupport.AsAsync(principals, "colleague-c", async () =>
        {
            Assert.True((await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m))).Succeeded);
            var submitted = await quotations.SubmitForReviewAsync(quoteId);
            Assert.True(submitted.Succeeded, submitted.Reason);
            return submitted;
        });

        var byAuthor = await quotations.ApproveAsync(quoteId);
        Assert.Equal(QuotationRefusal.ReviewerMustDifferFromAuthor, byAuthor.Refusal);
        Assert.Equal(QuotationStatus.InReview, byAuthor.Quotation!.Status);

        var byThird = await QuotationReviewTestSupport.AsAsync(principals, "third-d", () => quotations.ApproveAsync(quoteId));
        Assert.True(byThird.Succeeded, byThird.Reason);
        Assert.Equal("R1", byThird.Quotation!.RevisionLabel);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task Create_EditAndSubmit_AreRefused_WithNobodySignedIn()
    {
        // Colour review board B1 (b).
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = (Tempest.Core.Identity.CurrentPrincipalAccessor)QuotationTestHost.Principals(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-B1B");
        var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
        var lineId = (await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m))).Quotation!.Lines[0].Id;

        principals.SetCurrent(null);
        Assert.Equal(QuotationRefusal.NoPrincipalSignedIn, (await quotations.CreateAsync(projectId)).Refusal);
        Assert.Equal(QuotationRefusal.NoPrincipalSignedIn, (await quotations.AddLineAsync(quoteId, "More", null, null, Gbp(1m))).Refusal);
        Assert.Equal(QuotationRefusal.NoPrincipalSignedIn, (await quotations.UpdateLineAsync(quoteId, lineId, "Survey", null, null, Gbp(2m))).Refusal);
        Assert.Equal(QuotationRefusal.NoPrincipalSignedIn, (await quotations.RemoveLineAsync(quoteId, lineId)).Refusal);
        var submitted = await quotations.SubmitForReviewAsync(quoteId);
        Assert.Equal(QuotationRefusal.NoPrincipalSignedIn, submitted.Refusal);
        Assert.Equal(QuotationStatus.Draft, submitted.Quotation!.Status);

        // The store's own "unknown" id is nobody too.
        principals.SetCurrent(new Tempest.Core.Identity.PlatformPrincipal(
            new Tempest.Core.Identity.PlatformIdentity(EngineeringDocumentStore.UnknownAuthorPrincipalId, "Unknown"),
            Tempest.Core.Identity.ApplicationPermissions.LocalSession));
        Assert.Equal(QuotationRefusal.NoPrincipalSignedIn, (await quotations.SubmitForReviewAsync(quoteId)).Refusal);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task Approve_IsRefused_WhenTheSubmitterIsNotOnRecord()
    {
        // Colour review board B1 (b): a review submitted before the submit
        // guard existed, with the store's "unknown" id or none at all.
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = QuotationTestHost.Principals(host);
        var domain = QuotationTestHost.Domain(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-B1S");
        var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
        await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m));

        var quote = (Quotation)(await domain.Repository.FindAsync(quoteId))!;
        await quote.MarkInReviewAsync(EngineeringDocumentStore.UnknownAuthorPrincipalId, DateTimeOffset.UtcNow);

        var refused = await QuotationReviewTestSupport.AsAsync(principals, "reviewer-b", () => quotations.ApproveAsync(quoteId));
        Assert.Equal(QuotationRefusal.AuthorUnknown, refused.Refusal);
        Assert.Equal(QuotationStatus.InReview, refused.Quotation!.Status);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task Approve_IsRefused_ForAnyoneWhoChangedALineSinceTheLastApproval()
    {
        // Colour review board B1 (a): after R1, colleague-e edits a line and
        // the author resubmits; colleague-e cannot approve R2.
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = QuotationTestHost.Principals(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-B1A");
        var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
        var lineId = (await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m))).Quotation!.Lines[0].Id;
        var r1 = await QuotationReviewTestSupport.SubmitAndApproveAsync(quotations, principals, quoteId);
        Assert.Empty(r1.Quotation!.Review.LineEditors);

        var edited = await QuotationReviewTestSupport.AsAsync(principals, "colleague-e", () =>
            quotations.UpdateLineAsync(quoteId, lineId, "Survey and report", null, null, Gbp(650m)));
        Assert.True(edited.Succeeded, edited.Reason);
        Assert.Equal(["colleague-e"], edited.Quotation!.Review.LineEditors);
        Assert.True((await quotations.SubmitForReviewAsync(quoteId)).Succeeded);

        var byEditor = await QuotationReviewTestSupport.AsAsync(principals, "colleague-e", () => quotations.ApproveAsync(quoteId));
        Assert.Equal(QuotationRefusal.ReviewerMustDifferFromAuthor, byEditor.Refusal);
        Assert.Contains("changed", byEditor.Reason, StringComparison.Ordinal);

        // The R1 approver did not change a line this time, so may approve R2;
        // the editors are cleared by it.
        var r2 = await QuotationReviewTestSupport.AsAsync(principals, QuotationReviewTestSupport.ReviewerId, () => quotations.ApproveAsync(quoteId));
        Assert.True(r2.Succeeded, r2.Reason);
        Assert.Equal("R2", r2.Quotation!.RevisionLabel);
        Assert.Empty(r2.Quotation.Review.LineEditors);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task ALegacyQuotationWithNoAuthor_TakesItsAuthorFromItsFirstRevision_OrRefusesApprovalWhenThatIsNobody()
    {
        // Colour review board B1 (c).
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = QuotationTestHost.Principals(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-B1C");

        // Opened by the author before AuthorIdentityId was recorded.
        var attributed = await CreateLegacyQuotationAsync(host, projectId, "QUO-LEGACY-1");
        Assert.Null(attributed.AuthorIdentityId);
        await SubmitAsColleagueAsync(quotations, principals, attributed.Id);
        var byAuthor = await quotations.ApproveAsync(attributed.Id);
        Assert.Equal(QuotationRefusal.ReviewerMustDifferFromAuthor, byAuthor.Refusal);
        var byThird = await QuotationReviewTestSupport.AsAsync(principals, "third-d", () => quotations.ApproveAsync(attributed.Id));
        Assert.True(byThird.Succeeded, byThird.Reason);

        // Opened with nobody signed in: nobody can be shown not to be its author.
        var anonymous = await QuotationReviewTestSupport.AsAsync(principals, "placeholder", async () =>
        {
            ((Tempest.Core.Identity.CurrentPrincipalAccessor)principals).SetCurrent(null);
            return await CreateLegacyQuotationAsync(host, projectId, "QUO-LEGACY-2");
        });
        await SubmitAsColleagueAsync(quotations, principals, anonymous.Id);
        var refused = await QuotationReviewTestSupport.AsAsync(principals, "third-d", () => quotations.ApproveAsync(anonymous.Id));
        Assert.Equal(QuotationRefusal.AuthorUnknown, refused.Refusal);
        Assert.Equal(QuotationStatus.InReview, refused.Quotation!.Status);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task AnApprovedRevision_RecordsItsLinesHash_AndAStoredRevisionWithoutOneStillLoads()
    {
        // Colour review board M18.
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host, AuthorId);
        var quotations = QuotationTestHost.Quotations(host);
        var principals = QuotationTestHost.Principals(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "QUO-REV-M18");
        var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
        var lineId = (await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m))).Quotation!.Lines[0].Id;
        var r1 = (await QuotationReviewTestSupport.SubmitAndApproveAsync(quotations, principals, quoteId)).Quotation!;
        var revision = Assert.Single(r1.Review.Revisions);
        Assert.StartsWith("sha256:", revision.LinesHash, StringComparison.Ordinal);
        Assert.Equal(r1.LinesHash, revision.LinesHash);

        var edited = (await quotations.UpdateLineAsync(quoteId, lineId, "Survey", null, null, Gbp(650m))).Quotation!;
        Assert.NotEqual(revision.LinesHash, edited.LinesHash);

        // The stored shape before M18/B1: no LinesHash, no LineEditors.
        var state = edited.CaptureState();
        var reviewJson = state.TypeState[nameof(Quotation.Review)]!;
        Assert.Contains("\"LinesHash\"", reviewJson, StringComparison.Ordinal);
        var legacyReviewJson = System.Text.RegularExpressions.Regex.Replace(reviewJson, ",\"LinesHash\":\"[^\"]*\"", string.Empty);
        legacyReviewJson = System.Text.RegularExpressions.Regex.Replace(legacyReviewJson, ",\"LineEditors\":\\[[^\\]]*\\]", string.Empty);
        Assert.DoesNotContain("LinesHash", legacyReviewJson, StringComparison.Ordinal);
        Assert.DoesNotContain("LineEditors", legacyReviewJson, StringComparison.Ordinal);
        var legacyTypeState = new Dictionary<string, string?>(state.TypeState) { [nameof(Quotation.Review)] = legacyReviewJson };

        var legacy = RehydrateQuotation(edited, state with { TypeState = legacyTypeState });
        var legacyRevision = Assert.Single(legacy.Review.Revisions);
        Assert.Null(legacyRevision.LinesHash);
        Assert.Equal("R1", legacyRevision.Label);
        Assert.Empty(legacy.Review.LineEditors);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    private static async Task<Quotation> CreateLegacyQuotationAsync(ITempestHost host, Guid projectId, string reference)
    {
        var domain = QuotationTestHost.Domain(host);
        var created = await new EngineeringObjectFactory<Quotation>(
            Quotation.CanonicalKind,
            domain,
            (doc, rev) => new Quotation(
                doc, rev, domain, identifier: null, $"Quotation — {reference}", EngineeringObjectMetadata.Empty, reference,
                new DateOnly(2026, 1, 5), clientOrganisationId: null, CurrencyCode.Gbp, validityDays: 30, terms: null, lines: [],
                authorIdentityId: null))
            .CreateAsync($"Legacy quotation '{reference}'.");
        await ((IHasParent)created).MoveAsync(projectId);
        return (Quotation)(await domain.Repository.FindAsync(created.Id))!;
    }

    private static Task<QuotationResult> SubmitAsColleagueAsync(IQuotationService quotations, Tempest.Core.Identity.ICurrentPrincipalAccessor principals, Guid quoteId) =>
        QuotationReviewTestSupport.AsAsync(principals, "colleague-c", async () =>
        {
            var added = await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m));
            Assert.True(added.Succeeded, added.Reason);
            var submitted = await quotations.SubmitForReviewAsync(quoteId);
            Assert.True(submitted.Succeeded, submitted.Reason);
            return submitted;
        });

    private static Quotation RehydrateQuotation(Quotation template, EngineeringObjectState state)
    {
        static object Protected(Quotation q, string name) =>
            typeof(EngineeringObjectBase).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(q)!;

        return Rehydrate<Quotation>(
            (IEngineeringDocument)Protected(template, "Document"),
            (Tempest.Core.EngineeringData.IDocumentRevision)Protected(template, "CurrentRevision"),
            QuotationContext(template),
            state);
    }

    private static EngineeringDomainContext QuotationContext(Quotation q) =>
        (EngineeringDomainContext)typeof(EngineeringObjectBase)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(f => f.FieldType == typeof(EngineeringDomainContext))
            .GetValue(q)!;

    private static T Rehydrate<T>(IEngineeringDocument document, Tempest.Core.EngineeringData.IDocumentRevision revision, EngineeringDomainContext context, EngineeringObjectState state)
        where T : EngineeringObjectBase, IRehydratable<T> =>
        T.Rehydrate(document, revision, context, state);

    private static Money Gbp(decimal amount) => new(amount, CurrencyCode.Gbp);

    private static RateCard Card() => new()
    {
        Code = "QUO-REV-CARD",
        Name = "Review journey rate card",
        EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
        Currency = CurrencyCode.Gbp,
        Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
        Entries =
        [
            new RateCardEntry("ENG-1", "Senior engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), Grade: "Senior"),
            new RateCardEntry("FIX-1", "Site visit", PricingBasis.FixedPrice, new Money(400m, CurrencyCode.Gbp)),
        ],
    };
}
