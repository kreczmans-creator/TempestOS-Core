using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Contracts;

/// <summary>The diagnostic codes C1's validation services report.</summary>
public static class ContractValidationRules
{
    /// <summary>The template contains no clauses, so registering it records nothing about what it says.</summary>
    public const string TemplateMustHaveClauses = "TEMPEST-BGC-001";

    /// <summary>Two clauses in one template share a reference, so nothing can cite one of them unambiguously.</summary>
    public const string DuplicateClauseReference = "TEMPEST-BGC-002";

    /// <summary>A clause does not say what subject it deals with.</summary>
    public const string ClauseCategoryShouldBeStated = "TEMPEST-BGC-003";

    /// <summary>The template says nothing about a subject a commercial contract normally addresses.</summary>
    public const string TemplateOmitsExpectedCategory = "TEMPEST-BGC-004";

    /// <summary>No solicitor has reviewed this template revision.</summary>
    public const string TemplateHasNoLegalReview = "TEMPEST-BGC-005";

    /// <summary>Two templates share one code.</summary>
    public const string DuplicateTemplateCode = "TEMPEST-BGC-006";

    /// <summary>The contract names fewer than two parties.</summary>
    public const string ContractNeedsTwoParties = "TEMPEST-BGC-007";

    /// <summary>The contract is recorded as executed but nobody exercised the authority to commit the organisation.</summary>
    public const string ExecutedContractNeedsCommitmentAuthority = "TEMPEST-BGC-008";

    /// <summary>The contract is recorded as executed but carries no execution date.</summary>
    public const string ExecutedContractNeedsDate = "TEMPEST-BGC-009";

    /// <summary>The contract is recorded as executed but no signed document is held.</summary>
    public const string ExecutedContractShouldHaveDocument = "TEMPEST-BGC-010";

    /// <summary>The contract's own term ended before the date it is being validated against, and its status still says otherwise.</summary>
    public const string ContractTermHasEnded = "TEMPEST-BGC-011";

    /// <summary>The contract was not drawn from a controlled template.</summary>
    public const string ContractIsBespoke = "TEMPEST-BGC-012";

    /// <summary>The contract departs from a template clause that needs a solicitor, and no legal review is recorded.</summary>
    public const string DepartureNeedsLegalReview = "TEMPEST-BGC-013";

    /// <summary>The contract omits a clause its template marks mandatory.</summary>
    public const string MandatoryClauseNotAddressed = "TEMPEST-BGC-014";

    /// <summary>A deliverable states nothing that would make it acceptable.</summary>
    public const string DeliverableHasNoAcceptanceCriteria = "TEMPEST-BGC-015";

    /// <summary>The contract states no limit of liability.</summary>
    public const string ContractHasNoLiabilityCap = "TEMPEST-BGC-016";

    /// <summary>The contract states no mechanism for changing scope or price after signature.</summary>
    public const string ContractHasNoChangeControl = "TEMPEST-BGC-017";

    /// <summary>The payment terms' percentages do not add up to the whole contract value.</summary>
    public const string PaymentTermsDoNotTotal = "TEMPEST-BGC-018";

    /// <summary>A payment term does not say how long the payer has.</summary>
    public const string PaymentTermHasNoPeriod = "TEMPEST-BGC-019";

    /// <summary>An obligation is past its own due date.</summary>
    public const string ObligationIsOverdue = "TEMPEST-BGC-020";

    /// <summary>Two contracts share one reference.</summary>
    public const string DuplicateContractReference = "TEMPEST-BGC-021";

    /// <summary>The contract's template pin names a template revision the library does not hold.</summary>
    public const string TemplatePinMustResolve = "TEMPEST-BGC-022";
}

/// <summary>Governance of contract templates themselves.</summary>
public interface IContractTemplateValidationService : IReferenceValidationService<ContractTemplate>
{
}

/// <summary>The concrete <see cref="IContractTemplateValidationService"/> implementation.</summary>
/// <remarks>
/// <b>These checks are about completeness, never legality.</b> Nothing
/// here reads clause wording and forms a view on it. What it can tell is
/// whether the template says anything at all about liability, whether a
/// solicitor has looked at this revision, and whether two clauses share a
/// reference — questions of record-keeping, which are this platform's
/// business.
/// </remarks>
public sealed class ContractTemplateValidationService
    : ReferenceValidationService<ContractTemplate>, IContractTemplateValidationService
{
    /// <summary>
    /// The clause categories a commercial engineering contract normally
    /// addresses, and whose absence is therefore worth reporting.
    /// </summary>
    /// <remarks>
    /// A reporting heuristic the organisation can disagree with, not a
    /// legal requirement. A template that omits one of these may be
    /// perfectly correct for its purpose; what it should not be is
    /// omitting one by accident.
    /// </remarks>
    public static IReadOnlyList<ClauseCategory> ExpectedCategories { get; } =
    [
        ClauseCategory.Parties, ClauseCategory.Scope, ClauseCategory.Price, ClauseCategory.Payment,
        ClauseCategory.IntellectualProperty, ClauseCategory.Confidentiality, ClauseCategory.Liability,
        ClauseCategory.Termination,
    ];

    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ContractTemplateValidationService"/> class.</summary>
    /// <param name="catalog">The template library whose records this service validates.</param>
    /// <param name="timeProvider">The clock overdue checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public ContractTemplateValidationService(IContractTemplateCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        ContractTemplate definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Contract template '{definition.Code}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings, expectEvidence: false);

        if (definition.Clauses.Count == 0)
            errors.Add(Diagnostic(
                ContractValidationRules.TemplateMustHaveClauses,
                $"{subject} records no clauses, so registering it says nothing about what the template contains."));

        foreach (var duplicate in definition.Clauses
                     .GroupBy(c => c.ReferenceKey, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.First().Reference))
            errors.Add(Diagnostic(
                ContractValidationRules.DuplicateClauseReference,
                $"{subject} declares clause '{duplicate}' more than once, so nothing can cite one of them unambiguously."));

        foreach (var clause in definition.Clauses.Where(c => c.Category == ClauseCategory.Unspecified))
            warnings.Add(Diagnostic(
                ContractValidationRules.ClauseCategoryShouldBeStated,
                $"Clause '{clause.Reference}' in {subject} does not say what subject it deals with, so it cannot be found by "
                + "anybody asking what the template says about liability, IP or payment."));

        foreach (var category in ExpectedCategories.Where(c => !definition.Covers(c)))
            warnings.Add(Diagnostic(
                ContractValidationRules.TemplateOmitsExpectedCategory,
                $"{subject} says nothing about {category}. That may be deliberate; it is reported because a commercial contract "
                + "usually addresses it and an accidental omission looks identical to a deliberate one."));

        if (definition.LegalReviewState != DeterminationState.Recorded)
            warnings.Add(Diagnostic(
                ContractValidationRules.TemplateHasNoLegalReview,
                $"{subject} has no recorded legal review ({definition.LegalReviewState}). TempestOS cannot tell whether the "
                + "template is fit for use; it can tell that nobody qualified has said so."));

        return Task.CompletedTask;
    }
}
