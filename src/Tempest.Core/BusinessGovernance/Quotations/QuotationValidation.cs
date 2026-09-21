using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Quotations;

/// <summary>The diagnostic codes the quotation validation service reports.</summary>
/// <remarks>
/// A range of its own (<c>TEMPEST-BGQ-</c>) rather than a continuation of
/// <see cref="Contracts.ContractValidationRules"/>'s <c>TEMPEST-BGC-</c>,
/// because a quotation is not a contract and the two libraries validate
/// independently.
/// </remarks>
public static class QuotationValidationRules
{
    /// <summary>The status offered is not one the registered quotation may move to from where it stands.</summary>
    public const string StatusMoveNotPermitted = "TEMPEST-BGQ-001";

    /// <summary>The quotation is recorded as sent, accepted or declined but carries no submission date.</summary>
    public const string SubmittedQuotationNeedsDate = "TEMPEST-BGQ-002";

    /// <summary>The quotation is recorded as accepted or declined but carries no decision date.</summary>
    public const string DecidedQuotationNeedsDate = "TEMPEST-BGQ-003";

    /// <summary>The quotation is a draft but carries a submission or decision date.</summary>
    public const string DraftQuotationCarriesLaterDates = "TEMPEST-BGQ-004";

    /// <summary>The decision date precedes the submission date.</summary>
    public const string DecisionPrecedesSubmission = "TEMPEST-BGQ-005";

    /// <summary>The follow-up date precedes the submission date.</summary>
    public const string FollowUpPrecedesSubmission = "TEMPEST-BGQ-006";

    /// <summary>The quotation names a contract it became, but the client did not accept it.</summary>
    public const string UnacceptedQuotationNamesContract = "TEMPEST-BGQ-007";

    /// <summary>The quoted amount is negative or zero.</summary>
    public const string AmountMustBePositive = "TEMPEST-BGQ-008";

    /// <summary>The quotation is open, its follow-up date has passed, and nobody has recorded what happened.</summary>
    public const string FollowUpIsOverdue = "TEMPEST-BGQ-009";

    /// <summary>The quotation has been sent but no follow-up date is planned.</summary>
    public const string SubmittedQuotationHasNoFollowUp = "TEMPEST-BGQ-010";

    /// <summary>Two quotations share one reference.</summary>
    public const string DuplicateQuotationReference = "TEMPEST-BGQ-011";
}

/// <summary>Governance of quotations themselves.</summary>
public interface IQuotationValidationService : IReferenceValidationService<Quotation>
{
}

/// <summary>The concrete <see cref="IQuotationValidationService"/> implementation.</summary>
/// <remarks>
/// <para>
/// The checks that matter here are the ones a business misses: a
/// quotation that jumped from draft to accepted without ever being sent,
/// one accepted before it was submitted, one recorded as having become a
/// contract that the client never accepted, and one sitting open past its
/// own follow-up date.
/// </para>
/// <para>
/// <b>The lifecycle rule is enforced here, not in the catalogue.</b> Where
/// a quotation is already registered under the same reference, the status
/// offered must be one <see cref="QuotationStatuses.CanMove"/> permits
/// from the registered one — Draft to Submitted, Submitted to Accepted or
/// Declined, or no move at all. That is the shape
/// <see cref="Contracts.IssuedContractValidationService"/> uses for a
/// contract recorded as executed without authority: the record is
/// reported, not rewritten.
/// </para>
/// </remarks>
public sealed class QuotationValidationService
    : ReferenceValidationService<Quotation>, IQuotationValidationService
{
    private readonly IQuotationCatalog _quotations;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="QuotationValidationService"/> class.</summary>
    /// <param name="catalog">The quotation library whose records this service validates.</param>
    /// <param name="timeProvider">The clock overdue checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public QuotationValidationService(IQuotationCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _quotations = catalog;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        Quotation definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Quotation '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings);

        if (definition.Amount.Amount <= 0m)
            errors.Add(Diagnostic(
                QuotationValidationRules.AmountMustBePositive,
                $"{subject} quotes {definition.Amount}. A quotation states a price; nothing is offered for nothing."));

        EvaluateDates(definition, subject, today, errors, warnings);

        if (definition.BecameContract && definition.Status != QuotationStatus.Accepted)
            errors.Add(Diagnostic(
                QuotationValidationRules.UnacceptedQuotationNamesContract,
                $"{subject} names contract '{definition.IssuedContractReference}' as the contract it became, but is recorded as "
                + $"{definition.Status}. A contract follows an accepted quotation, not a {definition.Status.ToString().ToLowerInvariant()} one."));

        await EvaluateStatusMoveAsync(definition, subject, errors, cancellationToken).ConfigureAwait(false);
    }

    private static void EvaluateDates(
        Quotation definition,
        string subject,
        DateOnly today,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (definition.Status == QuotationStatus.Draft)
        {
            if (definition.SubmittedOn is not null || definition.DecidedOn is not null)
                errors.Add(Diagnostic(
                    QuotationValidationRules.DraftQuotationCarriesLaterDates,
                    $"{subject} is recorded as a draft but carries a submission or decision date. Either it was sent and the "
                    + "status is stale, or the dates belong to another quotation."));

            return;
        }

        if (definition.SubmittedOn is null)
            errors.Add(Diagnostic(
                QuotationValidationRules.SubmittedQuotationNeedsDate,
                $"{subject} is recorded as {definition.Status} but carries no submission date, so how long the client has had "
                + "it cannot be established."));

        if (QuotationStatuses.IsDecided(definition.Status))
        {
            if (definition.DecidedOn is null)
                errors.Add(Diagnostic(
                    QuotationValidationRules.DecidedQuotationNeedsDate,
                    $"{subject} is recorded as {definition.Status} but carries no decision date."));
            else if (definition.SubmittedOn is { } submitted && definition.DecidedOn < submitted)
                errors.Add(Diagnostic(
                    QuotationValidationRules.DecisionPrecedesSubmission,
                    $"{subject} was decided on {definition.DecidedOn:O}, before it was submitted on {submitted:O}. A client cannot "
                    + "answer what they have not yet been sent."));
        }

        if (definition.FollowUpOn is { } followUp && definition.SubmittedOn is { } sent && followUp < sent)
            errors.Add(Diagnostic(
                QuotationValidationRules.FollowUpPrecedesSubmission,
                $"{subject} plans a follow-up on {followUp:O}, before it was submitted on {sent:O}."));

        if (definition.Status != QuotationStatus.Submitted)
            return;

        if (definition.FollowUpOn is null)
            warnings.Add(Diagnostic(
                QuotationValidationRules.SubmittedQuotationHasNoFollowUp,
                $"{subject} has been sent and nobody is due to chase it."));
        else if (definition.IsFollowUpOverdueAt(today))
            warnings.Add(Diagnostic(
                QuotationValidationRules.FollowUpIsOverdue,
                $"{subject} was due a follow-up on {definition.FollowUpOn:O} and is still open. Either the client has not "
                + "answered, or they have and the record does not say so."));
    }

    private async Task EvaluateStatusMoveAsync(
        Quotation definition,
        string subject,
        List<IValidationDiagnostic> errors,
        CancellationToken cancellationToken)
    {
        var registered = await _quotations.FindByReferenceAsync(definition.Reference, cancellationToken).ConfigureAwait(false);

        if (registered is null)
            return;

        var from = registered.Definition.Status;

        if (!QuotationStatuses.CanMove(from, definition.Status))
            errors.Add(Diagnostic(
                QuotationValidationRules.StatusMoveNotPermitted,
                $"{subject} is registered as {from} and is offered as {definition.Status}. A quotation moves only Draft to "
                + "Submitted, then Submitted to Accepted or Declined; it does not go back, and it is not accepted or declined "
                + "before it is sent."));
    }
}
