using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Contracts;

/// <summary>Governance of issued contracts themselves.</summary>
public interface IIssuedContractValidationService : IReferenceValidationService<IssuedContract>
{
}

/// <summary>The concrete <see cref="IIssuedContractValidationService"/> implementation.</summary>
/// <remarks>
/// <para>
/// The checks that matter here are the ones a business misses: a contract
/// recorded as executed that nobody was authorised to sign, a term that
/// quietly ended, a departure from a clause the template said needed a
/// solicitor, a deliverable with nothing to accept it against.
/// </para>
/// <para>
/// None of them is a legal opinion. Every one is a question about whether
/// the record is complete and internally consistent.
/// </para>
/// </remarks>
public sealed class IssuedContractValidationService
    : ReferenceValidationService<IssuedContract>, IIssuedContractValidationService
{
    private readonly IContractTemplateCatalog? _templates;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="IssuedContractValidationService"/> class.</summary>
    /// <param name="catalog">The contract library whose records this service validates.</param>
    /// <param name="templates">The template library, for resolving a contract's own template pin and its mandatory clauses. Optional: a contract may legitimately be bespoke.</param>
    /// <param name="timeProvider">The clock overdue and expiry checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public IssuedContractValidationService(
        IIssuedContractCatalog catalog,
        IContractTemplateCatalog? templates = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _templates = templates;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        IssuedContract definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Contract '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings);
        BusinessGovernanceValidator.EvaluatePeriod(subject, definition.Term, today, warnings);

        if (definition.Parties.Count < 2)
            errors.Add(Diagnostic(
                ContractValidationRules.ContractNeedsTwoParties,
                $"{subject} names {definition.Parties.Count} part{(definition.Parties.Count == 1 ? "y" : "ies")}. "
                + "A contract binds at least two."));

        EvaluateExecution(definition, subject, today, errors, warnings);
        EvaluateCommercialTerms(definition, subject, warnings);

        foreach (var deliverable in definition.Deliverables.Where(d => !d.HasAcceptanceCriteria))
            warnings.Add(Diagnostic(
                ContractValidationRules.DeliverableHasNoAcceptanceCriteria,
                $"Deliverable '{deliverable.Reference}' in {subject} states nothing that would make it acceptable, so whether it "
                + "has been met is a matter of opinion."));

        foreach (var obligation in definition.OverdueObligations(today))
            warnings.Add(Diagnostic(
                ContractValidationRules.ObligationIsOverdue,
                $"Obligation '{obligation.Reference}' in {subject} was due on {obligation.DueBy:O} and is owed by "
                + $"{obligation.OwedBy}: {obligation.Description}"));

        await EvaluateAgainstTemplateAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private void EvaluateExecution(
        IssuedContract definition,
        string subject,
        DateOnly today,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (!ContractStatuses.HasBeenExecuted(definition.Status))
            return;

        if (!definition.Governance.HasAuthority(BusinessAuthorityKind.CommercialCommitment))
            errors.Add(Diagnostic(
                ContractValidationRules.ExecutedContractNeedsCommitmentAuthority,
                $"{subject} is recorded as {definition.Status}, but no commercial commitment by a named person is recorded. "
                + "A contract binds the organisation; who bound it must be on the record."));

        if (definition.ExecutedOn is null)
            errors.Add(Diagnostic(
                ContractValidationRules.ExecutedContractNeedsDate,
                $"{subject} is recorded as {definition.Status} but carries no execution date, so when its obligations started "
                + "cannot be established."));

        if (definition.ExecutedDocumentId is null)
            warnings.Add(Diagnostic(
                ContractValidationRules.ExecutedContractShouldHaveDocument,
                $"{subject} is recorded as {definition.Status} but no signed document is held. What was actually agreed rests on "
                + "this record rather than on the instrument."));

        if (definition.Status == ContractStatus.Executed && definition.TermHasEndedBy(today))
            warnings.Add(Diagnostic(
                ContractValidationRules.ContractTermHasEnded,
                $"{subject} is still recorded as Executed, but its term ended on {definition.Term!.To:O}. Either the status is "
                + "stale or the contract was extended and the record does not say so."));
    }

    private void EvaluateCommercialTerms(IssuedContract definition, string subject, List<IValidationDiagnostic> warnings)
    {
        if (definition.CommercialTerms is not { } terms)
            return;

        if (!terms.HasLiabilityCap)
            warnings.Add(Diagnostic(
                ContractValidationRules.ContractHasNoLiabilityCap,
                $"{subject} states no limit of liability. Uncapped liability may be a deliberate commercial position; it is "
                + "reported because it is rarely an intended one."));

        if (!terms.HasChangeControl)
            warnings.Add(Diagnostic(
                ContractValidationRules.ContractHasNoChangeControl,
                $"{subject} states no mechanism for changing scope or price after signature."));

        if (terms.PaymentPercentageTotal is { } total && total != 100m)
            warnings.Add(Diagnostic(
                ContractValidationRules.PaymentTermsDoNotTotal,
                $"{subject}'s payment terms account for {total} per cent of the contract value, not 100."));

        foreach (var term in terms.PaymentTerms.Where(t => t.DaysToPay is null && t.Trigger != PaymentTrigger.InAdvance))
            warnings.Add(Diagnostic(
                ContractValidationRules.PaymentTermHasNoPeriod,
                $"A payment term in {subject} does not say how long the payer has: {term.Description}"));
    }

    private async Task EvaluateAgainstTemplateAsync(
        IssuedContract definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (definition.TemplatePin is not { } pin)
        {
            warnings.Add(Diagnostic(
                ContractValidationRules.ContractIsBespoke,
                $"{subject} was not drawn from a controlled template. That is legitimate — the other party's paper often is — "
                + "and it means the contract carries none of the review the template library holds."));

            return;
        }

        if (_templates is null)
            return;

        IReferenceRecord<ContractTemplate>? template = null;

        try
        {
            template = await _templates.GetRevisionAsync(pin.RecordId, pin.RevisionNumber, cancellationToken).ConfigureAwait(false);
        }
        catch (ReferenceDataException)
        {
            warnings.Add(Diagnostic(
                ContractValidationRules.TemplatePinMustResolve,
                $"{subject} cites template revision {pin}, which the template library does not hold. The contract cannot be read "
                + "against the template it claims to follow."));

            return;
        }

        foreach (var departure in definition.Departures)
        {
            var clause = template.Definition.FindClause(departure.ClauseReference);

            if (clause is { RequiresLegalReview: true } && departure.LegalReviewState != DeterminationState.Recorded)
                warnings.Add(Diagnostic(
                    ContractValidationRules.DepartureNeedsLegalReview,
                    $"{subject} departs from clause '{departure.ClauseReference}', which template '{template.Definition.Code}' "
                    + $"marks as needing legal review, and none is recorded ({departure.LegalReviewState})."));
        }

        var departedFrom = definition.Departures
            .Select(d => d.ClauseReference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mandatory in template.Definition.MandatoryClauses.Where(c => departedFrom.Contains(c.Reference)))
            warnings.Add(Diagnostic(
                ContractValidationRules.MandatoryClauseNotAddressed,
                $"{subject} departs from clause '{mandatory.Reference}' ({mandatory.Heading}), which template "
                + $"'{template.Definition.Code}' marks mandatory."));
    }
}
