using Tempest.Workspace.Projects;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations;
using Tempest.Core.Deliverables;
using Tempest.Core.Evidence;
using Tempest.Core.Projects;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Timesheets;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// The `WP 19.0A` acceptance journey (`ADR-0150`): create a project,
/// register an Organisation and a two-grade RateCard, release it, set the
/// commercial fields, pin the card (a Draft card refused first), record
/// time across both grades with rates frozen at record time (a later
/// revision of the card never reaches an already-recorded entry), refuse
/// an unpriced grade, amend an entry, mark one invoiced (once only),
/// complete a deliverable with one issued Evidence citation (once only,
/// the first shown on a second attempt), list the principal's week and
/// the project's unbilled entries, restart the host and read everything
/// back, with audit rows for each act.
/// </summary>
public sealed class ProjectCommercialJourneyTests
{
    [Fact]
    public async Task CommercialCore_TimeAndDeliverableCompletion_SurviveARestart_WithAuditRows()
    {
        using var temp = new TempDirectory();

        Guid projectId;
        Guid firstEntryId;
        Guid secondEntryId;
        Guid deliverableId;
        Guid completionId;
        Guid invoiceRequestId = Guid.NewGuid();
        const string organisationId = "COMMERCIAL-CLIENT";
        const string rateCardId = "COMMERCIAL-CARD";
        const string seniorGrade = "Senior";
        const string principalGrade = "Principal";
        var startDate = new DateOnly(2026, 3, 1);
        var targetDate = new DateOnly(2026, 9, 1);
        var week = new DateOnly(2026, 3, 2); // a Monday
        var deliverableCompletionDate = new DateOnly(2026, 3, 10);

        // ================================================================
        // FIRST HOST
        // ================================================================
        {
            var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);
            ProjectCommercialTestHost.SignIn(host);

            projectId = await ProjectCommercialTestHost.CreateProjectAsync(host);

            var commercial = ProjectCommercialTestHost.ProjectCommercial(host);
            var rateCards = ProjectCommercialTestHost.RateCards(host);
            var organisations = ProjectCommercialTestHost.Organisations(host);
            var timesheets = ProjectCommercialTestHost.Timesheets(host);
            var deliverables = ProjectCommercialTestHost.Deliverables(host);
            var evidenceService = (IEvidenceService)host.Services!.GetService(typeof(IEvidenceService));
            var domain = ProjectCommercialTestHost.Domain(host);

            // ---- Register the Organisation (client) ----
            await organisations.RegisterAsync(organisationId, OperationsFixtures.Organisation(organisationId), OperationsFixtures.Verified());

            // ---- Register a two-grade rate card, cost rates included ----
            var card = TwoGradeCard(rateCardId, seniorGrade, 150m, 90m, principalGrade, 220m, 130m);
            await rateCards.RegisterAsync(rateCardId, card, BusinessGovernanceFixtures.Verified());

            // ---- Pinning a Draft card is refused ----
            var draftPin = await commercial.PinRateCardAsync(projectId, rateCardId);
            Assert.False(draftPin.Succeeded);
            Assert.Equal(ProjectCommercialRefusal.RateCardNotReleased, draftPin.Refusal);

            // ---- Release it, then pin succeeds, pinned to the released revision ----
            var released = await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, rateCardId);
            var pin = await commercial.PinRateCardAsync(projectId, rateCardId);
            Assert.True(pin.Succeeded);
            Assert.Equal(released.RevisionNumber, pin.Project!.RateCardPin!.RevisionNumber);

            // ---- Client, PO, budget, dates, project manager ----
            Assert.True((await commercial.SetClientAsync(projectId, organisationId)).Succeeded);
            Assert.True((await commercial.SetPurchaseOrderAsync(projectId, "PO-COMMERCIAL-1")).Succeeded);
            Assert.True((await commercial.SetBudgetAsync(projectId, new Money(50_000m, CurrencyCode.Gbp))).Succeeded);
            Assert.True((await commercial.SetDatesAsync(projectId, startDate, targetDate)).Succeeded);
            Assert.True((await commercial.SetProjectManagerAsync(projectId, "pm-identity-1")).Succeeded);

            var afterCommercial = (Core.EngineeringDomain.Project)(await domain.Repository.FindAsync(projectId))!;
            Assert.Equal(organisationId, afterCommercial.ClientOrganisationId);
            Assert.Equal("PO-COMMERCIAL-1", afterCommercial.PurchaseOrderReference);
            Assert.Equal(new Money(50_000m, CurrencyCode.Gbp), afterCommercial.Budget);
            Assert.Equal(startDate, afterCommercial.StartDate);
            Assert.Equal(targetDate, afterCommercial.TargetDate);
            Assert.Equal("pm-identity-1", afterCommercial.ProjectManagerIdentityId);

            // ---- Record three entries across two grades ----
            var first = await timesheets.RecordAsync(projectId, week, 4m, billable: true, seniorGrade, "Design review");
            Assert.True(first.Succeeded);
            firstEntryId = first.Entry!.Id;
            Assert.Equal(150m, first.Entry.BillingRate.Amount);
            Assert.Equal(90m, first.Entry.CostRate!.Value.Amount);

            var second = await timesheets.RecordAsync(projectId, week, 3.5m, billable: true, seniorGrade, "Detailed design");
            Assert.True(second.Succeeded);
            secondEntryId = second.Entry!.Id;

            var third = await timesheets.RecordAsync(projectId, week, 2m, billable: false, principalGrade, "Governance review");
            Assert.True(third.Succeeded);
            Assert.Equal(220m, third.Entry!.BillingRate.Amount);

            // ---- Superseding the card afterwards (the only way a Released
            // record's own rates can change) never reaches an already-
            // recorded entry — nor even a later recording against the same,
            // now-superseded, pin: the pin names a revision, and a revision
            // is immutable regardless of the record's own current state. ----
            const string supersedingCardId = "COMMERCIAL-CARD-V2";
            var supersedingCard = TwoGradeCard(supersedingCardId, seniorGrade, 999m, 999m, principalGrade, 999m, 999m);
            await rateCards.RegisterAsync(supersedingCardId, supersedingCard, BusinessGovernanceFixtures.Verified());
            await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, supersedingCardId);
            await rateCards.SupersedeAsync(rateCardId, supersedingCardId, "Rates changed for new work; existing entries are unaffected.");

            var stillFrozen = (TimesheetEntry)(await domain.Repository.FindAsync(firstEntryId))!;
            Assert.Equal(150m, stillFrozen.BillingRate.Amount);
            Assert.Equal(90m, stillFrozen.CostRate!.Value.Amount);

            // A fourth recording, still against the project's own
            // (now-superseded) pin, still resolves the frozen, original rate.
            var afterSupersession = await timesheets.RecordAsync(projectId, week, 1m, billable: true, seniorGrade, "After supersession");
            Assert.True(afterSupersession.Succeeded);
            Assert.Equal(150m, afterSupersession.Entry!.BillingRate.Amount);
            await afterSupersession.Entry.DeleteAsync(); // not part of the counted set below

            // ---- A grade not on the card is refused ----
            var unpriced = await timesheets.RecordAsync(projectId, week, 1m, billable: true, "Not-A-Grade", "Should refuse");
            Assert.False(unpriced.Succeeded);
            Assert.Equal(TimesheetRefusal.GradeNotOnCard, unpriced.Refusal);

            // ---- Amend one entry ----
            var amended = await timesheets.AmendAsync(secondEntryId, 4.5m, "Detailed design — revised", billable: true);
            Assert.True(amended.Succeeded);
            Assert.Equal(4.5m, amended.Entry!.Hours);

            // ---- Mark one invoiced; amend/delete of it and a second mark are all refused ----
            var invoiced = await timesheets.MarkInvoicedAsync(firstEntryId, invoiceRequestId);
            Assert.True(invoiced.Succeeded);
            Assert.Equal(invoiceRequestId, invoiced.Entry!.InvoicedBy);

            var amendInvoiced = await timesheets.AmendAsync(firstEntryId, 1m, "Should refuse", billable: true);
            Assert.False(amendInvoiced.Succeeded);
            Assert.Equal(TimesheetRefusal.EntryInvoiced, amendInvoiced.Refusal);

            var deleteInvoiced = await timesheets.DeleteAsync(firstEntryId);
            Assert.False(deleteInvoiced.Succeeded);
            Assert.Equal(TimesheetRefusal.EntryInvoiced, deleteInvoiced.Refusal);

            var secondMarkInvoiced = await timesheets.MarkInvoicedAsync(firstEntryId, Guid.NewGuid());
            Assert.False(secondMarkInvoiced.Succeeded);
            Assert.Equal(TimesheetRefusal.EntryInvoiced, secondMarkInvoiced.Refusal);

            // ---- An issued piece of evidence, and a deliverable to complete against it ----
            var evidence = await evidenceService.CreateAsync(projectId, "Design calculation", EvidenceClassification.Calculation);
            await evidenceService.RecordCheckAsync(evidence.Id, "J. Reviewer", "Client Co", "Reviewed.", CheckOutcome.Accepted);
            var issued = await evidenceService.IssueAsync(evidence.Id, "ISS-1", "A", "Fictional Client Ltd");
            Assert.True(issued.Succeeded);

            var milestoneService = new ProjectMilestoneService(domain);
            var milestone = await milestoneService.CreateMilestoneAsync(projectId, "MS-1", "Milestone One", DateTimeOffset.UtcNow.AddDays(30));
            var deliverable = await milestoneService.CreateDeliverableAsync(projectId, milestone.Id, "DEL-1", "Deliverable One");
            deliverableId = deliverable.Id;

            var completion = await deliverables.CompleteAsync(deliverableId, projectId, deliverableCompletionDate, [evidence.Id]);
            Assert.True(completion.Succeeded);
            completionId = completion.Completion!.Id;
            Assert.Single(completion.Completion.IssuedEvidenceIds);

            // ---- A second completion is refused, with the first shown ----
            var secondCompletion = await deliverables.CompleteAsync(deliverableId, projectId, deliverableCompletionDate.AddDays(1));
            Assert.False(secondCompletion.Succeeded);
            Assert.Equal(DeliverableCompletionRefusal.AlreadyCompleted, secondCompletion.Refusal);
            Assert.Equal(completionId, secondCompletion.Completion!.Id);

            // ---- List the principal's week, and the project's unbilled entries ----
            var weekEntries = await timesheets.ListForPrincipalWeekAsync(ProjectCommercialTestHost.PrincipalId, week);
            Assert.Equal(3, weekEntries.Count);

            var unbilled = await timesheets.ListUnbilledForProjectAsync(projectId);
            var unbilledEntry = Assert.Single(unbilled);
            Assert.Equal(secondEntryId, unbilledEntry.Id);

            // ---- Audit rows for the acts above ----
            var auditQuery = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
            var projectAudit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: projectId));
            Assert.True(projectAudit.Count >= 6, $"Expected at least one audit row per commercial act; found {projectAudit.Count}.");

            var entryAudit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: firstEntryId));
            Assert.True(entryAudit.Count >= 2, "Expected an audit row for create and for MarkInvoiced.");

            var completionAudit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: completionId));
            Assert.NotEmpty(completionAudit);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        // ================================================================
        // SECOND HOST — restart, read everything back
        // ================================================================
        {
            var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);

            var rehydration = await Tempest.Workspace.Composition.EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            Assert.True(rehydration.IsComplete, "Expected a clean rehydration.");

            var domain = ProjectCommercialTestHost.Domain(host);

            var project = (Core.EngineeringDomain.Project)(await domain.Repository.FindAsync(projectId))!;
            Assert.Equal(organisationId, project.ClientOrganisationId);
            Assert.Equal("PO-COMMERCIAL-1", project.PurchaseOrderReference);
            Assert.Equal(new Money(50_000m, CurrencyCode.Gbp), project.Budget);
            Assert.Equal(rateCardId, project.RateCardPin!.RecordId);
            Assert.Equal(startDate, project.StartDate);
            Assert.Equal(targetDate, project.TargetDate);
            Assert.Equal("pm-identity-1", project.ProjectManagerIdentityId);

            var entry = (TimesheetEntry)(await domain.Repository.FindAsync(firstEntryId))!;
            Assert.Equal(150m, entry.BillingRate.Amount);
            Assert.Equal(90m, entry.CostRate!.Value.Amount);
            Assert.Equal(invoiceRequestId, entry.InvoicedBy);

            var completion = (DeliverableCompletion)(await domain.Repository.FindAsync(completionId))!;
            Assert.Equal(deliverableId, completion.DeliverableId);
            Assert.Equal(deliverableCompletionDate, completion.CompletedOn);
            Assert.Single(completion.IssuedEvidenceIds);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static RateCard TwoGradeCard(
        string code, string firstGrade, decimal firstBilling, decimal firstCost, string secondGrade, decimal secondBilling, decimal secondCost) => new()
    {
        Code = code,
        Name = "Commercial journey rate card",
        EffectivePeriod = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
        Currency = CurrencyCode.Gbp,
        TaxTreatment = "Exclusive of VAT.",
        Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
        Entries =
        [
            new RateCardEntry(
                "ENG-1", $"{firstGrade} engineering", PricingBasis.Hourly, new Money(firstBilling, CurrencyCode.Gbp), new Money(firstCost, CurrencyCode.Gbp),
                Grade: firstGrade),
            new RateCardEntry(
                "ENG-2", $"{secondGrade} engineering", PricingBasis.Hourly, new Money(secondBilling, CurrencyCode.Gbp), new Money(secondCost, CurrencyCode.Gbp),
                Grade: secondGrade),
        ],
    };
}
