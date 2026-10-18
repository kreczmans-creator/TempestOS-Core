using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// `v0.24.0` X7: <see cref="AccountsReadModel.ReadForwardCashAsync"/> over a
/// real host — the accepted-but-not-invoiced pipeline read from TempestOS's
/// own quotations, deliverables, milestones and completions (gross: each
/// line's net plus its VAT rate), and a deliverable dropping out of it the
/// moment its invoice is in the accounting package. The host's own connector
/// is the Fake one; the Xero journey is <see cref="AccountsForwardCashXeroJourneyTests"/>.
/// </summary>
public sealed class AccountsForwardCashReadModelTests
{
    [Fact]
    public async Task AcceptedQuoteLines_AreThePipeline_DatedByMilestone_Gross_UntilTheirInvoiceIsInTheAccountingPackage()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var domain = QuotationTestHost.Domain(host);
            var quotations = QuotationTestHost.Quotations(host);
            var projectId = await SetUpBillableProjectAsync(host);

            // A sent quote that is not accepted: never in the picture.
            var open = await quotations.CreateAsync(projectId);
            await quotations.AddLineAsync(open.Quotation!.Id, "Not yet accepted", null, null, Gbp(9_999m), vatRate: VatRate.Standard);
            await QuotationReviewTestSupport.ApproveAndSendAsync(quotations, QuotationTestHost.Principals(host), open.Quotation.Id);

            var created = await quotations.CreateAsync(projectId);
            var quoteId = created.Quotation!.Id;
            await quotations.AddLineAsync(quoteId, "Detailed design", null, null, Gbp(1_000m), vatRate: VatRate.Standard);
            await quotations.AddLineAsync(quoteId, "Site survey", null, null, Gbp(500m), vatRate: VatRate.Standard);
            await quotations.AddLineAsync(quoteId, "Site attendance", 10m, Gbp(100m), null, vatRate: VatRate.OutOfScope);
            await QuotationReviewTestSupport.ApproveAndSendAsync(quotations, QuotationTestHost.Principals(host), quoteId);
            var accepted = await quotations.AcceptAsync(quoteId);
            Assert.True(accepted.Succeeded, accepted.Reason);
            var quote = accepted.Quotation!;
            var milestoneTarget = quote.QuoteDate.AddDays(quote.ValidityDays);

            var store = new FileAccountsReadingStore(Path.Combine(temp.Path, "accounts"));
            var readModel = new AccountsReadModel(store, domain, refreshService: null, timeProvider: new FixedTimeProvider(today));

            // ---- accepted, nothing invoiced ----
            var picture = await readModel.ReadForwardCashAsync();
            var pipeline = PipelineItems(picture);
            Assert.Equal(2, pipeline.Count);
            var design = Assert.Single(pipeline, i => i.Description.EndsWith("Detailed design", StringComparison.Ordinal));
            Assert.Equal(Gbp(1_200m), design.Gross); // 1,000 net + 20% VAT
            Assert.Equal(milestoneTarget, design.Date);
            Assert.Contains("target date", design.Note, StringComparison.Ordinal);
            Assert.Equal(quoteId, design.ObjectId);
            Assert.Equal(ForwardCashSourceKind.TempestOS, design.Source.Kind);
            var survey = Assert.Single(pipeline, i => i.Description.EndsWith("Site survey", StringComparison.Ordinal));
            Assert.Equal(Gbp(600m), survey.Gross);
            Assert.DoesNotContain(pipeline, i => i.Description.Contains("Not yet accepted", StringComparison.Ordinal));

            // The hourly line is reported, not projected (billed from timesheets).
            var hourly = Assert.Single(picture.Exclusions);
            Assert.Equal(1, hourly.Count);
            Assert.Equal(Gbp(1_000m), hourly.Total);
            Assert.Contains("timesheets", hourly.Reason, StringComparison.Ordinal);

            // No accounts reading: the package's figures are unavailable, TempestOS's still show.
            Assert.Equal("No accounts reading yet.", picture.AccountsUnavailableReason);
            Assert.False(picture.Months[0].OpeningCash.IsAvailable);
            Assert.True(picture.Months[0].ExpectedMilestones.IsAvailable);

            // ---- the survey is completed: a draft request, not yet in the package ----
            var surveyDeliverable = quote.Lines.Single(l => l.Description == "Site survey").DeliverableId!.Value;
            var completion = await QuotationTestHost.Deliverables(host).CompleteAsync(surveyDeliverable, projectId, today, fixedPriceValue: Gbp(500m));
            Assert.True(completion.Succeeded, completion.Reason);
            var request = await QuotationTestHost.RequestRaisedByCompletionAsync(host, completion.Completion!.Id);
            Assert.Equal(InvoiceRequestStatus.Draft, request.Status);

            picture = await readModel.ReadForwardCashAsync();
            survey = Assert.Single(PipelineItems(picture), i => i.Description.EndsWith("Site survey", StringComparison.Ordinal));
            Assert.Equal(today, survey.Date);
            Assert.Contains("invoice drafted in TempestOS", survey.Note, StringComparison.Ordinal);
            Assert.Contains(survey, picture.Months[0].Items);
            Assert.DoesNotContain(picture.Months.SelectMany(m => m.Items), i => i.Kind == ForwardCashItemKind.InvoiceDue);

            // ---- the request is sent to the accounting package: out of the pipeline, in as an invoice due ----
            var sent = await QuotationTestHost.Invoicing(host).SendAsync(request.Id);
            Assert.True(sent.Succeeded, sent.Reason);

            picture = await readModel.ReadForwardCashAsync();
            Assert.DoesNotContain(PipelineItems(picture), i => i.Description.EndsWith("Site survey", StringComparison.Ordinal));
            Assert.Single(PipelineItems(picture));
            var invoice = Assert.Single(picture.Months.SelectMany(m => m.Items).Concat(picture.Later), i => i.Kind == ForwardCashItemKind.InvoiceDue);
            Assert.Equal(request.Id, invoice.ObjectId);
            Assert.Equal(sent.Request!.GrossTotal, invoice.Gross);
            Assert.Equal(sent.Request.DueOn, invoice.Date);
            Assert.Contains("not yet approved", invoice.Note, StringComparison.Ordinal);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task ADeletedDeliverable_IsNoLongerPipeline_AndAnUnknownMilestoneIsUndated()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var domain = QuotationTestHost.Domain(host);
            var quotations = QuotationTestHost.Quotations(host);
            var projectId = await SetUpBillableProjectAsync(host);

            var created = await quotations.CreateAsync(projectId);
            var quoteId = created.Quotation!.Id;
            await quotations.AddLineAsync(quoteId, "Kept", null, null, Gbp(100m), vatRate: VatRate.OutOfScope);
            await quotations.AddLineAsync(quoteId, "Dropped", null, null, Gbp(200m), vatRate: VatRate.OutOfScope);
            await QuotationReviewTestSupport.ApproveAndSendAsync(quotations, QuotationTestHost.Principals(host), quoteId);
            var quote = (await quotations.AcceptAsync(quoteId)).Quotation!;

            var dropped = (IDeletable)(await domain.Repository.FindAsync(quote.Lines.Single(l => l.Description == "Dropped").DeliverableId!.Value))!;
            await dropped.DeleteAsync();

            var keptDeliverable = (Deliverable)(await domain.Repository.FindAsync(quote.Lines.Single(l => l.Description == "Kept").DeliverableId!.Value))!;
            // Its milestone is deleted (the deliverable moved out from under it first — a
            // milestone with a live child cannot be deleted): the deliverable has no date.
            await ((IHasParent)keptDeliverable).MoveAsync(projectId);
            var milestone = (IDeletable)(await domain.Repository.FindAsync(keptDeliverable.MilestoneId))!;
            await milestone.DeleteAsync();

            var readModel = new AccountsReadModel(new FileAccountsReadingStore(Path.Combine(temp.Path, "accounts")), domain, timeProvider: new FixedTimeProvider(today));
            var picture = await readModel.ReadForwardCashAsync();

            var undated = Assert.Single(picture.Undated);
            Assert.EndsWith("Kept", undated.Description, StringComparison.Ordinal);
            Assert.Contains("undated", undated.Note, StringComparison.Ordinal);
            Assert.Equal(Gbp(100m), picture.UndatedTotal);
            Assert.Empty(picture.Months.SelectMany(m => m.Items));
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    internal static IReadOnlyList<ForwardCashItem> PipelineItems(ForwardCashPicture picture) =>
        [.. picture.Months.SelectMany(m => m.Items).Concat(picture.Later).Concat(picture.Undated).Where(i => i.Kind == ForwardCashItemKind.ExpectedMilestone)];

    private static Money Gbp(decimal amount) => new(amount, CurrencyCode.Gbp);

    private static async Task<Guid> SetUpBillableProjectAsync(ITempestHost host)
    {
        const string organisationId = "FC-CLIENT";
        const string rateCardId = "FC-CARD";

        await QuotationTestHost.Organisations(host).RegisterAsync(organisationId, OperationsFixtures.Organisation(organisationId), OperationsFixtures.Verified());

        var rateCards = QuotationTestHost.RateCards(host);
        await rateCards.RegisterAsync(rateCardId, new RateCard
        {
            Code = rateCardId,
            Name = "Forward cash test rate card",
            EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
            Currency = CurrencyCode.Gbp,
            Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
            Entries = [new RateCardEntry("ENG-1", "Senior engineering", PricingBasis.Hourly, Gbp(100m), Gbp(60m), Grade: "Senior")],
        }, BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, rateCardId);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "FC-PRJ");
        var commercial = QuotationTestHost.ProjectCommercial(host);
        Assert.True((await commercial.PinRateCardAsync(projectId, rateCardId)).Succeeded);
        Assert.True((await commercial.SetClientAsync(projectId, organisationId)).Succeeded);
        return projectId;
    }

    private sealed class FixedTimeProvider(DateOnly date) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(date.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
    }
}
