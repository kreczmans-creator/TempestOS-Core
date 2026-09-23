using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Quotations;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.BusinessGovernance;

// A quotation's whole claim is that its status says what the client said
// and moves one way only: Draft, Submitted, then Accepted or Declined.
// Most of these tests exist to hold that (ADR-0154).
public class QuotationTests
{
    private static DateOnly Today => BusinessGovernanceFixtures.Today;

    private static Quotation Quote(string reference = "QUO-1", QuotationStatus status = QuotationStatus.Draft) =>
        BusinessGovernanceFixtures.Quote(reference, status);

    private static async Task<IValidationResult> ValidateAsync(Quotation quotation, QuotationCatalog? catalog = null)
    {
        var service = new QuotationValidationService(
            catalog ?? BusinessGovernanceFixtures.BuildQuotationCatalog(),
            BusinessGovernanceFixtures.Clock());

        return await service.ValidateDefinitionAsync(quotation, BusinessGovernanceFixtures.Verified());
    }

    [Theory]
    [InlineData(QuotationStatus.Draft, QuotationStatus.Submitted, true)]
    [InlineData(QuotationStatus.Submitted, QuotationStatus.Accepted, true)]
    [InlineData(QuotationStatus.Submitted, QuotationStatus.Declined, true)]
    [InlineData(QuotationStatus.Draft, QuotationStatus.Draft, true)]
    [InlineData(QuotationStatus.Accepted, QuotationStatus.Accepted, true)]
    [InlineData(QuotationStatus.Draft, QuotationStatus.Accepted, false)]
    [InlineData(QuotationStatus.Draft, QuotationStatus.Declined, false)]
    [InlineData(QuotationStatus.Submitted, QuotationStatus.Draft, false)]
    [InlineData(QuotationStatus.Accepted, QuotationStatus.Declined, false)]
    [InlineData(QuotationStatus.Declined, QuotationStatus.Accepted, false)]
    [InlineData(QuotationStatus.Accepted, QuotationStatus.Submitted, false)]
    [InlineData(QuotationStatus.Declined, QuotationStatus.Draft, false)]
    public void TheLifecycleMovesOneWay_DraftSubmittedThenAcceptedOrDeclined(QuotationStatus from, QuotationStatus to, bool permitted)
    {
        Assert.Equal(permitted, QuotationStatuses.CanMove(from, to));
    }

    [Fact]
    public void OnlyDraftAndSubmitted_AreOpen()
    {
        Assert.True(QuotationStatuses.IsOpen(QuotationStatus.Draft));
        Assert.True(QuotationStatuses.IsOpen(QuotationStatus.Submitted));
        Assert.False(QuotationStatuses.IsOpen(QuotationStatus.Accepted));
        Assert.False(QuotationStatuses.IsOpen(QuotationStatus.Declined));
        Assert.True(QuotationStatuses.IsDecided(QuotationStatus.Declined));
        Assert.False(QuotationStatuses.HasBeenSubmitted(QuotationStatus.Draft));
    }

    [Fact]
    public async Task ADraftQuotation_IsValid()
    {
        var result = await ValidateAsync(Quote());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ARegisteredDraft_OfferedAsAccepted_IsAnError()
    {
        // The lifecycle rule. A client cannot accept what was never sent,
        // and a record that skips Submitted is reported, not rewritten.
        var catalog = BusinessGovernanceFixtures.BuildQuotationCatalog();
        await catalog.RegisterAsync("quo-1", Quote(), BusinessGovernanceFixtures.Verified());

        var result = await ValidateAsync(Quote(status: QuotationStatus.Accepted), catalog);

        Assert.Contains(QuotationValidationRules.StatusMoveNotPermitted, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task ARegisteredSubmitted_OfferedAsAccepted_IsPermitted()
    {
        var catalog = BusinessGovernanceFixtures.BuildQuotationCatalog();
        await catalog.RegisterAsync("quo-1", Quote(status: QuotationStatus.Submitted), BusinessGovernanceFixtures.Verified());

        var result = await ValidateAsync(Quote(status: QuotationStatus.Accepted), catalog);

        Assert.DoesNotContain(QuotationValidationRules.StatusMoveNotPermitted, result.Errors.Select(d => d.Code));
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ARegisteredDecision_OfferedBackAsSubmitted_IsAnError()
    {
        var catalog = BusinessGovernanceFixtures.BuildQuotationCatalog();
        await catalog.RegisterAsync("quo-1", Quote(status: QuotationStatus.Declined), BusinessGovernanceFixtures.Verified());

        var result = await ValidateAsync(Quote(status: QuotationStatus.Submitted), catalog);

        Assert.Contains(QuotationValidationRules.StatusMoveNotPermitted, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task TheStatusMoveIsCheckedByReference_IgnoringCase()
    {
        var catalog = BusinessGovernanceFixtures.BuildQuotationCatalog();
        await catalog.RegisterAsync("quo-1", Quote("QUO-1"), BusinessGovernanceFixtures.Verified());

        var result = await ValidateAsync(Quote("quo-1", QuotationStatus.Declined), catalog);

        Assert.Contains(QuotationValidationRules.StatusMoveNotPermitted, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task ASubmittedQuotationWithNoSubmissionDate_IsAnError()
    {
        var result = await ValidateAsync(Quote(status: QuotationStatus.Submitted) with { SubmittedOn = null });

        Assert.Contains(QuotationValidationRules.SubmittedQuotationNeedsDate, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AnAcceptedQuotationWithNoDecisionDate_IsAnError()
    {
        var result = await ValidateAsync(Quote(status: QuotationStatus.Accepted) with { DecidedOn = null });

        Assert.Contains(QuotationValidationRules.DecidedQuotationNeedsDate, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task ADraftCarryingASubmissionDate_IsAnError()
    {
        // Either it was sent and the status is stale, or the date is wrong.
        var result = await ValidateAsync(Quote() with { SubmittedOn = Today });

        Assert.Contains(QuotationValidationRules.DraftQuotationCarriesLaterDates, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task ADecisionBeforeSubmission_IsAnError()
    {
        var result = await ValidateAsync(Quote(status: QuotationStatus.Declined) with
        {
            SubmittedOn = Today,
            DecidedOn = Today.AddDays(-1),
        });

        Assert.Contains(QuotationValidationRules.DecisionPrecedesSubmission, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AFollowUpBeforeSubmission_IsAnError()
    {
        var result = await ValidateAsync(Quote(status: QuotationStatus.Submitted) with
        {
            SubmittedOn = Today,
            FollowUpOn = Today.AddDays(-1),
        });

        Assert.Contains(QuotationValidationRules.FollowUpPrecedesSubmission, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AQuotationNamingAContractItWasNeverAcceptedInto_IsAnError()
    {
        var result = await ValidateAsync(Quote(status: QuotationStatus.Submitted) with { IssuedContractReference = "CON-1" });

        Assert.Contains(QuotationValidationRules.UnacceptedQuotationNamesContract, result.Errors.Select(d => d.Code));
        Assert.True(Quote(status: QuotationStatus.Accepted).BecameContract == false);
    }

    [Fact]
    public async Task AnAcceptedQuotationThatBecameAContract_IsValid()
    {
        var result = await ValidateAsync(Quote(status: QuotationStatus.Accepted) with { IssuedContractReference = "CON-1" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AZeroAmount_IsAnError()
    {
        var result = await ValidateAsync(Quote() with { Amount = BusinessGovernanceFixtures.Gbp_(0m) });

        Assert.Contains(QuotationValidationRules.AmountMustBePositive, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AnOverdueFollowUp_IsReported_NotRejected()
    {
        // The fixture's Submitted quotation was due a follow-up yesterday.
        var result = await ValidateAsync(Quote(status: QuotationStatus.Submitted));

        Assert.Contains(QuotationValidationRules.FollowUpIsOverdue, result.Warnings.Select(d => d.Code));
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ASubmittedQuotationNobodyIsDueToChase_IsReported()
    {
        var result = await ValidateAsync(Quote(status: QuotationStatus.Submitted) with { FollowUpOn = null });

        Assert.Contains(QuotationValidationRules.SubmittedQuotationHasNoFollowUp, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task TheCatalogue_FindsAQuotationByReference_IgnoringCase()
    {
        var catalog = BusinessGovernanceFixtures.BuildQuotationCatalog();
        await catalog.RegisterAsync("quo-1", Quote("QUO-1"), BusinessGovernanceFixtures.Verified());

        var found = await catalog.FindByReferenceAsync("quo-1");

        Assert.Equal("QUO-1", found!.Definition.Reference);
        Assert.Equal(QuotationCatalog.QuotationDocumentKind, catalog.DocumentKind);
    }

    [Fact]
    public async Task TwoQuotationsSharingOneReference_AreRefused()
    {
        var catalog = BusinessGovernanceFixtures.BuildQuotationCatalog();
        await catalog.RegisterAsync("quo-1", Quote("QUO-1"), BusinessGovernanceFixtures.Verified());

        await Assert.ThrowsAsync<DuplicateReferenceKeyException>(
            () => catalog.RegisterAsync("quo-2", Quote("quo-1"), BusinessGovernanceFixtures.Verified()));
    }

    [Fact]
    public async Task TheCatalogue_SearchesByStatusAndByFollowUpDue()
    {
        var catalog = BusinessGovernanceFixtures.BuildQuotationCatalog();
        await catalog.RegisterAsync("quo-1", Quote("QUO-1"), BusinessGovernanceFixtures.Verified());
        await catalog.RegisterAsync("quo-2", Quote("QUO-2", QuotationStatus.Submitted), BusinessGovernanceFixtures.Verified());
        await catalog.RegisterAsync("quo-3", Quote("QUO-3", QuotationStatus.Accepted), BusinessGovernanceFixtures.Verified());

        var open = await catalog.SearchAsync(new QuotationQuery { Statuses = [QuotationStatus.Draft, QuotationStatus.Submitted] });
        var due = await catalog.SearchAsync(new QuotationQuery { FollowUpDueBy = Today });
        var client = await catalog.SearchAsync(new QuotationQuery { ClientNameContains = "fictional" });

        Assert.Equal(["QUO-1", "QUO-2"], open.Select(r => r.Definition.Reference));
        Assert.Equal("QUO-2", Assert.Single(due).Definition.Reference);
        Assert.Equal(3, client.Count);
    }

    [Fact]
    public async Task AReleasedQuotation_CannotBeEditedInPlace()
    {
        var catalog = BusinessGovernanceFixtures.BuildQuotationCatalog();
        await catalog.RegisterAsync("quo-1", Quote(), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(catalog, "quo-1");

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => catalog.ReviseAsync("quo-1", Quote() with { Title = "Something else." }, BusinessGovernanceFixtures.Verified(), "Attempted in-place edit."));
    }

    [Fact]
    public void AQuotationPricedFromARateCard_PinsTheRevisionItRead()
    {
        var pin = new ReferencePin("BusinessRateCards", "rc-1", 2);
        var quotation = Quote() with { RateCardPin = pin };

        Assert.True(quotation.IsFromRateCard);
        Assert.Contains(pin, quotation.AllPins);
        Assert.False(Quote().IsFromRateCard);
    }
}
