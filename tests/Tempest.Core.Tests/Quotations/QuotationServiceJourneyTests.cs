using Tempest.Workspace.Projects;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Projects;
using Tempest.Core.Quotations;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Quotations;

/// <summary>
/// The `WP 19.5A` acceptance journey (`ADR-0152`): a project with a client
/// and a pinned rate card; a quotation with two hourly lines and one
/// fixed-price line; the right total; sent; accepted — one Deliverable and
/// one Requirement per line, the deliverables under a milestone named
/// after the quotation's own reference, the requirements associated to the
/// project; an audit row for every step; restart the host and everything
/// is still there; every documented refusal (a second Accept, a line added
/// after Send, sending an empty quote, accepting a Declined quote).
/// </summary>
public sealed class QuotationServiceJourneyTests
{
    private static readonly DateOnly QuoteDate = new(2026, 3, 2);

    [Fact]
    public async Task CoreJourney_ThreeLines_RightTotal_Send_Accept_DeliverablesAndRequirementsCreated_RestartAndEverythingIsStillThere()
    {
        using var temp = new TempDirectory();

        Guid projectId;
        Guid quotationId;
        string reference;

        // ================================================================
        // FIRST HOST
        // ================================================================
        {
            var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
            QuotationTestHost.SignIn(host);

            var (setUpProjectId, organisationId) = await SetUpBillableProjectAsync(host, "CORE");
            projectId = setUpProjectId;

            var domain = QuotationTestHost.Domain(host);
            var quotations = QuotationTestHost.Quotations(host);

            var created = await quotations.CreateAsync(projectId);
            Assert.True(created.Succeeded, created.Reason);
            var quotation = created.Quotation!;
            quotationId = quotation.Id;
            reference = quotation.Reference;

            Assert.Equal(organisationId, quotation.ClientOrganisationId);
            Assert.Equal(CurrencyCode.Gbp, quotation.Currency);
            Assert.Equal(30, quotation.ValidityDays);
            Assert.Equal(QuotationStatus.Draft, quotation.Status);
            Assert.Empty(quotation.Lines);

            var line1 = await quotations.AddLineAsync(quotationId, "Detailed design", 10m, new Money(150m, CurrencyCode.Gbp), null);
            Assert.True(line1.Succeeded, line1.Reason);
            var line2 = await quotations.AddLineAsync(quotationId, "Drafting", 5m, new Money(90m, CurrencyCode.Gbp), null);
            Assert.True(line2.Succeeded, line2.Reason);
            var line3 = await quotations.AddLineAsync(quotationId, "Prototype build", null, null, new Money(2_500m, CurrencyCode.Gbp));
            Assert.True(line3.Succeeded, line3.Reason);

            var withLines = line3.Quotation!;
            Assert.Equal(3, withLines.Lines.Count);
            Assert.Equal(new Money(10m * 150m + 5m * 90m + 2_500m, CurrencyCode.Gbp), withLines.Total);
            Assert.Equal(QuotationLineBasis.Hourly, withLines.Lines[0].Basis);
            Assert.Equal(QuotationLineBasis.Hourly, withLines.Lines[1].Basis);
            Assert.Equal(QuotationLineBasis.FixedPrice, withLines.Lines[2].Basis);

            // ---- A line added after Send is refused ----
            var sent = await quotations.SendAsync(quotationId);
            Assert.True(sent.Succeeded, sent.Reason);
            Assert.Equal(QuotationStatus.Sent, sent.Quotation!.Status);
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), sent.Quotation.SentOn);

            var lineAfterSend = await quotations.AddLineAsync(quotationId, "Too late", 1m, new Money(1m, CurrencyCode.Gbp), null);
            Assert.False(lineAfterSend.Succeeded);
            Assert.Equal(QuotationRefusal.QuotationNotDraft, lineAfterSend.Refusal);

            // ---- Sending an empty quote is refused ----
            var emptyCreated = await quotations.CreateAsync(projectId);
            Assert.True(emptyCreated.Succeeded);
            var emptySend = await quotations.SendAsync(emptyCreated.Quotation!.Id);
            Assert.False(emptySend.Succeeded);
            Assert.Equal(QuotationRefusal.NothingToSend, emptySend.Refusal);

            // ---- Accept: one Deliverable and one Requirement per line ----
            var accepted = await quotations.AcceptAsync(quotationId);
            Assert.True(accepted.Succeeded, accepted.Reason);
            Assert.Equal(QuotationStatus.Accepted, accepted.Quotation!.Status);
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), accepted.Quotation.DecidedOn);
            Assert.All(accepted.Quotation.Lines, l => Assert.NotNull(l.DeliverableId));
            Assert.All(accepted.Quotation.Lines, l => Assert.NotNull(l.RequirementId));

            // ---- Three deliverables exist under a milestone named after the reference ----
            var milestoneEntries = await domain.Repository.ListChildrenAsync(projectId);
            var milestones = await domain.Repository.MaterialiseAsync<Milestone>(milestoneEntries);
            var milestone = Assert.Single(milestones, m => m.DisplayName == reference);
            var deliverableEntries = await domain.Repository.ListChildrenAsync(milestone.Id);
            var deliverables = await domain.Repository.MaterialiseAsync<Deliverable>(deliverableEntries);
            Assert.Equal(3, deliverables.Count);
            Assert.Equal(accepted.Quotation.QuoteDate.AddDays(30), DateOnly.FromDateTime(milestone.TargetDate.UtcDateTime));

            // ---- Three requirements are associated to the project ----
            var requirementsService = QuotationTestHost.Requirements(host);
            var register = new ProjectRequirementRegister(requirementsService, domain);
            var projectRequirements = await register.ListAsync(projectId);
            Assert.Equal(3, projectRequirements.Count);
            var titles = projectRequirements.Select(r => r.Statement).ToHashSet(StringComparer.Ordinal);
            Assert.Contains("Detailed design", titles);
            Assert.Contains("Drafting", titles);
            Assert.Contains("Prototype build", titles);

            // ---- Every step has an audit row ----
            var auditQuery = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
            var quotationAudit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: quotationId));
            Assert.True(quotationAudit.Count >= 6, $"Expected an audit row for create, three line-adds, send and accept; found {quotationAudit.Count}.");

            foreach (var line in accepted.Quotation.Lines)
            {
                var deliverableAudit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: line.DeliverableId!.Value));
                Assert.NotEmpty(deliverableAudit);
            }

            // ---- A second Accept is refused as already accepted ----
            var secondAccept = await quotations.AcceptAsync(quotationId);
            Assert.False(secondAccept.Succeeded);
            Assert.Equal(QuotationRefusal.TransitionNotPermitted, secondAccept.Refusal);
            Assert.Equal(3, secondAccept.Quotation!.Lines.Count(l => l.DeliverableId is not null));

            // No second set of deliverables was created by the refused retry.
            var deliverableEntriesAfterSecondAccept = await domain.Repository.ListChildrenAsync(milestone.Id);
            var deliverablesAfterSecondAccept = await domain.Repository.MaterialiseAsync<Deliverable>(deliverableEntriesAfterSecondAccept);
            Assert.Equal(3, deliverablesAfterSecondAccept.Count);

            // ---- Accepting a Declined quotation is refused ----
            var declineCreated = await quotations.CreateAsync(projectId);
            await quotations.AddLineAsync(declineCreated.Quotation!.Id, "Never happened", 1m, new Money(100m, CurrencyCode.Gbp), null);
            await quotations.SendAsync(declineCreated.Quotation.Id);
            var declined = await quotations.DeclineAsync(declineCreated.Quotation.Id);
            Assert.True(declined.Succeeded, declined.Reason);
            Assert.Equal(QuotationStatus.Declined, declined.Quotation!.Status);

            var acceptDeclined = await quotations.AcceptAsync(declineCreated.Quotation.Id);
            Assert.False(acceptDeclined.Succeeded);
            Assert.Equal(QuotationRefusal.TransitionNotPermitted, acceptDeclined.Refusal);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        // ================================================================
        // SECOND HOST — restart, read everything back
        // ================================================================
        {
            var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);

            var rehydration = await Tempest.Workspace.Composition.EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            Assert.True(rehydration.IsComplete, "Expected a clean rehydration.");

            var domain = QuotationTestHost.Domain(host);

            var quotation = (Quotation)(await domain.Repository.FindAsync(quotationId))!;
            Assert.Equal(reference, quotation.Reference);
            Assert.Equal(QuotationStatus.Accepted, quotation.Status);
            Assert.Equal(3, quotation.Lines.Count);
            Assert.Equal(new Money(10m * 150m + 5m * 90m + 2_500m, CurrencyCode.Gbp), quotation.Total);
            Assert.All(quotation.Lines, l => Assert.NotNull(l.DeliverableId));
            Assert.All(quotation.Lines, l => Assert.NotNull(l.RequirementId));
            Assert.NotNull(quotation.SentOn);
            Assert.NotNull(quotation.DecidedOn);

            var deliverable = (Deliverable)(await domain.Repository.FindAsync(quotation.Lines[0].DeliverableId!.Value))!;
            Assert.NotNull(deliverable);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task ReferenceGeneration_TwoQuotesSameYear_GetSequentialReferences_AGivenReferenceIsKept()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var (projectId, _) = await SetUpBillableProjectAsync(host, "REF");
        var quotations = QuotationTestHost.Quotations(host);

        var first = await quotations.CreateAsync(projectId);
        Assert.True(first.Succeeded);
        Assert.StartsWith("Q-", first.Quotation!.Reference, StringComparison.Ordinal);

        var second = await quotations.CreateAsync(projectId);
        Assert.True(second.Succeeded);

        var year = first.Quotation.QuoteDate.Year;
        Assert.Equal($"Q-{year}-001", first.Quotation.Reference);
        Assert.Equal($"Q-{year}-002", second.Quotation!.Reference);

        var given = await quotations.CreateAsync(projectId, reference: "Q-CUSTOM-1");
        Assert.True(given.Succeeded);
        Assert.Equal("Q-CUSTOM-1", given.Quotation!.Reference);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task AddDeliverableAsync_CreatesUnderDefaultMilestone_CompletesAndRaisesInvoiceRequest_ExactlyAsAQuotedOneDoes()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var (projectId, _) = await SetUpBillableProjectAsync(host, "ADD");
        var domain = QuotationTestHost.Domain(host);
        var deliverables = QuotationTestHost.Deliverables(host);

        var deliverable = await deliverables.AddDeliverableAsync(projectId, "Directly added deliverable");

        var milestone = (Milestone)(await domain.Repository.FindAsync(deliverable.MilestoneId))!;
        Assert.Equal(DeliverableService.UnquotedMilestoneTitle, milestone.DisplayName);
        Assert.Equal(projectId, milestone.ParentId);

        // A second directly-added deliverable reuses the same default milestone.
        var secondDeliverable = await deliverables.AddDeliverableAsync(projectId, "A second directly-added deliverable");
        Assert.Equal(deliverable.MilestoneId, secondDeliverable.MilestoneId);

        // Completes and raises an invoice request exactly as a quoted one does.
        var completion = await deliverables.CompleteAsync(
            deliverable.Id, projectId, QuoteDate, fixedPriceValue: new Money(750m, CurrencyCode.Gbp));
        Assert.True(completion.Succeeded, completion.Reason);

        var request = await QuotationTestHost.RequestRaisedByCompletionAsync(host, completion.Completion!.Id);
        Assert.Equal(InvoiceRequestStatus.Draft, request.Status);
        Assert.Contains(request.Lines, l => l.SourceId == completion.Completion.Id);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    private static async Task<(Guid ProjectId, string OrganisationId)> SetUpBillableProjectAsync(ITempestHost host, string suffix)
    {
        var organisationId = $"QUO-CLIENT-{suffix}";
        var rateCardId = $"QUO-CARD-{suffix}";

        var organisations = QuotationTestHost.Organisations(host);
        var rateCards = QuotationTestHost.RateCards(host);
        var commercial = QuotationTestHost.ProjectCommercial(host);

        await organisations.RegisterAsync(organisationId, OperationsFixtures.Organisation(organisationId), OperationsFixtures.Verified());

        var card = OneGradeCard(rateCardId, "Senior", 150m, 90m);
        await rateCards.RegisterAsync(rateCardId, card, BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, rateCardId);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, $"QUO-PRJ-{suffix}");

        Assert.True((await commercial.PinRateCardAsync(projectId, rateCardId)).Succeeded);
        Assert.True((await commercial.SetClientAsync(projectId, organisationId)).Succeeded);

        return (projectId, organisationId);
    }

    private static RateCard OneGradeCard(string code, string grade, decimal billing, decimal cost) => new()
    {
        Code = code,
        Name = "Quotation journey rate card",
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
