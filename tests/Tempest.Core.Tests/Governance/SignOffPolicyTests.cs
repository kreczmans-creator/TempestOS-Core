using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.Evidence;
using Tempest.Core.Governance;
using Tempest.Core.Persistence;
using Tempest.Core.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Evidence;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;
using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Mechanical;

namespace Tempest.Core.Tests.Governance;

/// <summary>
/// `ADR-0161` (Product Owner decision 2026-10-01: "This software is
/// initially for a single-user consultancy, so EVERYTHING needing a second
/// person to verify/approve cannot be the case. Add into the settings a
/// switch to flick second-person sign-off on/off globally."): the one
/// global switch — off by default, durable across a restart, audited when
/// it changes — and every separation-of-duty rule read in both positions.
/// </summary>
public sealed class SignOffPolicyTests
{
    private const string AuthorId = QuotationTestHost.PrincipalId;

    [Fact]
    public async Task IsOffByDefault_ForAOnePersonConsultancy()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        try
        {
            Assert.False(await SignOffTestSupport.Policy(host).IsSecondPersonRequiredAsync());
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConfigurationMayMakeItOnByDefault()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(MechanicalWorkspaceExplorerModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, temp.Path),
                new KeyValuePair<string, string>(SignOffPolicy.ConfigurationKey, "true"),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);
        await manager.StartAsync();
        try
        {
            Assert.True(await SignOffTestSupport.Policy(host).IsSecondPersonRequiredAsync());
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task TheSwitch_SurvivesARestart_AndEveryChangeIsAudited_WhoWhenOldAndNew()
    {
        using var temp = new TempDirectory();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        {
            var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
            QuotationTestHost.SignIn(host, AuthorId);
            var policy = SignOffTestSupport.Policy(host);

            Assert.True(await policy.SetSecondPersonRequiredAsync(true));
            Assert.True(await policy.IsSecondPersonRequiredAsync());

            // Setting the value it already has changes nothing and records nothing.
            Assert.False(await policy.SetSecondPersonRequiredAsync(true));

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        {
            var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
            QuotationTestHost.SignIn(host, "colleague-c");
            var policy = SignOffTestSupport.Policy(host);

            // Durable: the same answer after a restart, for every module.
            Assert.True(await policy.IsSecondPersonRequiredAsync());

            Assert.True(await policy.SetSecondPersonRequiredAsync(false));
            Assert.False(await policy.IsSecondPersonRequiredAsync());

            var audit = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
            var rows = (await audit.QueryAsync(new AuditQueryCriteria(action: SignOffPolicy.ChangedActionName)))
                .OrderBy(r => r.OccurredAt)
                .ToList();

            Assert.Equal(2, rows.Count);

            Assert.Equal(AuthorId, rows[0].ActorId);
            Assert.Equal("Off", rows[0].Detail["OldValue"]);
            Assert.Equal("On", rows[0].Detail["NewValue"]);
            Assert.Equal(SignOffPolicy.SettingKey, rows[0].Detail["Subject"]);
            Assert.True(rows[0].OccurredAt >= before);

            Assert.Equal("colleague-c", rows[1].ActorId);
            Assert.Equal("On", rows[1].Detail["OldValue"]);
            Assert.Equal("Off", rows[1].Detail["NewValue"]);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Off_TheAuthorMaySubmitAndApproveTheirOwnQuote_AndTheRevisionSaysItWasASelfApproval()
    {
        using var temp = new TempDirectory();
        Guid quoteId;

        {
            var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
            QuotationTestHost.SignIn(host, AuthorId);
            var quotations = QuotationTestHost.Quotations(host);

            var projectId = await QuotationTestHost.CreateProjectAsync(host, "SIGNOFF-Q1");
            quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
            Assert.True((await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m))).Succeeded);
            Assert.True((await quotations.SubmitForReviewAsync(quoteId)).Succeeded);

            var approved = await quotations.ApproveAsync(quoteId);
            Assert.True(approved.Succeeded, approved.Reason);
            Assert.Equal("R1", approved.Quotation!.RevisionLabel);
            var revision = Assert.Single(approved.Quotation.Review.Revisions);
            Assert.Equal(AuthorId, revision.ApprovedBy);
            Assert.Equal(AuthorId, revision.SubmittedBy);
            Assert.True(revision.SelfApproved);

            // The audit row is honest about it.
            var audit = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
            var rows = await audit.QueryAsync(new AuditQueryCriteria(objectId: quoteId));
            Assert.Contains(rows, r => r.ActorId == AuthorId && r.Detail.Values.Any(v =>
                v.Contains("Approved as R1", StringComparison.Ordinal)
                && v.Contains(SignOffPolicy.SelfApprovalNote, StringComparison.Ordinal)));

            // And an approved revision may be sent, exactly as a second person's approval may.
            Assert.True((await quotations.SendAsync(quoteId)).Succeeded);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        {
            var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
            await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            var reloaded = (Quotation)(await QuotationTestHost.Domain(host).Repository.FindAsync(quoteId))!;
            Assert.True(Assert.Single(reloaded.Review.Revisions).SelfApproved);
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Off_StillNeedsSomebodySignedIn_AndASecondPersonsApprovalIsNotMarkedSelfApproved()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        try
        {
            QuotationTestHost.SignIn(host, AuthorId);
            var quotations = QuotationTestHost.Quotations(host);
            var principals = QuotationTestHost.Principals(host);

            var projectId = await QuotationTestHost.CreateProjectAsync(host, "SIGNOFF-Q2");
            var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
            await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m));
            Assert.True((await quotations.SubmitForReviewAsync(quoteId)).Succeeded);

            var nobody = await QuotationReviewTestSupport.AsAsync<QuotationResult>(principals, "placeholder", async () =>
            {
                ((Tempest.Core.Identity.CurrentPrincipalAccessor)principals).SetCurrent(null);
                return await quotations.ApproveAsync(quoteId);
            });
            Assert.Equal(QuotationRefusal.NoPrincipalSignedIn, nobody.Refusal);
            Assert.Equal(QuotationStatus.InReview, nobody.Quotation!.Status);

            var bySecond = await QuotationReviewTestSupport.AsAsync(principals, "reviewer-b", () => quotations.ApproveAsync(quoteId));
            Assert.True(bySecond.Succeeded, bySecond.Reason);
            Assert.False(Assert.Single(bySecond.Quotation!.Review.Revisions).SelfApproved);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Off_AnUnattributedSubmissionMayBeApproved_AndIsRecordedAsASelfApproval()
    {
        // On, this is `QuotationRefusal.AuthorUnknown` — nobody can be shown
        // to differ. Off, nobody needs to be, and the record says so.
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        try
        {
            QuotationTestHost.SignIn(host, AuthorId);
            var quotations = QuotationTestHost.Quotations(host);
            var domain = QuotationTestHost.Domain(host);

            var projectId = await QuotationTestHost.CreateProjectAsync(host, "SIGNOFF-Q3");
            var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
            await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m));
            var quote = (Quotation)(await domain.Repository.FindAsync(quoteId))!;
            await quote.MarkInReviewAsync(EngineeringDocumentStore.UnknownAuthorPrincipalId, DateTimeOffset.UtcNow);

            var approved = await quotations.ApproveAsync(quoteId);
            Assert.True(approved.Succeeded, approved.Reason);
            Assert.True(Assert.Single(approved.Quotation!.Review.Revisions).SelfApproved);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task FlickingTheSwitch_ChangesTheQuoteRuleAtOnce_OnRefusesTheAuthor_OffLetsThemApprove()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        try
        {
            QuotationTestHost.SignIn(host, AuthorId);
            var quotations = QuotationTestHost.Quotations(host);
            var policy = SignOffTestSupport.Policy(host);

            var projectId = await QuotationTestHost.CreateProjectAsync(host, "SIGNOFF-Q4");
            var quoteId = (await quotations.CreateAsync(projectId)).Quotation!.Id;
            await quotations.AddLineAsync(quoteId, "Survey", null, null, Gbp(500m));
            Assert.True((await quotations.SubmitForReviewAsync(quoteId)).Succeeded);

            await policy.SetSecondPersonRequiredAsync(true);
            var refused = await quotations.ApproveAsync(quoteId);
            Assert.Equal(QuotationRefusal.ReviewerMustDifferFromAuthor, refused.Refusal);
            Assert.Contains("second person", refused.Reason, StringComparison.Ordinal);
            Assert.Equal(QuotationStatus.InReview, refused.Quotation!.Status);

            await policy.SetSecondPersonRequiredAsync(false);
            var approved = await quotations.ApproveAsync(quoteId);
            Assert.True(approved.Succeeded, approved.Reason);
            Assert.True(Assert.Single(approved.Quotation!.Review.Revisions).SelfApproved);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Evidence_Off_TheAuthorMayCheckTheirOwnEvidence_AndTheCheckSaysSo()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path, independentCheck: true);
        try
        {
            EvidenceTestHost.SignIn(host, EvidenceTestHost.PrincipalId);
            Assert.False(await SignOffTestSupport.Policy(host).IsSecondPersonRequiredAsync());

            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);
            var evidence = await service.CreateAsync(projectId, "Self-checked evidence", EvidenceClassification.Report);

            var checkedByAuthor = await service.RecordCheckAsync(evidence.Id, "Author", "Org", "Own review.", CheckOutcome.Accepted);
            Assert.True(checkedByAuthor.Succeeded, checkedByAuthor.Reason);
            var check = checkedByAuthor.Evidence!.Check!;
            Assert.Equal(EvidenceTestHost.PrincipalId, check.CheckerIdentityId);
            Assert.Equal(EvidenceTestHost.PrincipalId, check.RecordedByIdentityId);
            Assert.True(check.SelfCheck);

            var audit = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
            var rows = await audit.QueryAsync(new AuditQueryCriteria(objectId: evidence.Id));
            Assert.Contains(rows, r => r.Detail.Values.Any(v => v.Contains(SignOffPolicy.SelfApprovalNote, StringComparison.Ordinal)));
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Evidence_Off_StillNeedsSomebodySignedIn_On_RefusesTheAuthor()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path, independentCheck: true);
        try
        {
            EvidenceTestHost.SignIn(host, EvidenceTestHost.PrincipalId);
            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);
            var evidence = await service.CreateAsync(projectId, "Signed-in evidence", EvidenceClassification.Report);

            ((Tempest.Core.Identity.CurrentPrincipalAccessor)EvidenceTestHost.Principals(host)).SetCurrent(null);
            var nobody = await service.RecordCheckAsync(evidence.Id, "Nobody", "Org", "No one.", CheckOutcome.Accepted);
            Assert.Equal(EvidenceRefusal.NoPrincipalSignedIn, nobody.Refusal);

            EvidenceTestHost.SignIn(host, EvidenceTestHost.PrincipalId);
            await SignOffTestSupport.RequireSecondPersonAsync(host);
            var refused = await service.RecordCheckAsync(evidence.Id, "Author", "Org", "Own review.", CheckOutcome.Accepted);
            Assert.Equal(EvidenceRefusal.CheckerMustDifferFromAuthor, refused.Refusal);

            EvidenceTestHost.SignIn(host, EvidenceTestHost.SecondPrincipalId);
            var accepted = await service.RecordCheckAsync(evidence.Id, "Second", "Org", "Independent review.", CheckOutcome.Accepted);
            Assert.True(accepted.Succeeded, accepted.Reason);
            Assert.False(accepted.Evidence!.Check!.SelfCheck);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static Money Gbp(decimal amount) => new(amount, CurrencyCode.Gbp);
}
