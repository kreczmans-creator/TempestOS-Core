using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Risk;

/// <summary>The diagnostic codes C2's validation services report.</summary>
public static class RiskValidationRules
{
    /// <summary>Nobody has assessed the risk's likelihood or impact.</summary>
    public const string RiskIsUnassessed = "TEMPEST-BGR-001";

    /// <summary>The risk does not say what could bring it about.</summary>
    public const string RiskHasNoCause = "TEMPEST-BGR-002";

    /// <summary>The risk does not say what would follow if it occurred.</summary>
    public const string RiskHasNoConsequence = "TEMPEST-BGR-003";

    /// <summary>The residual assessment is better than the inherent one with nothing implemented to explain the difference.</summary>
    public const string ResidualExposureIsUnearned = "TEMPEST-BGR-004";

    /// <summary>A high or extreme risk is being carried with nobody's acceptance behind it.</summary>
    public const string RiskCarriedWithoutAcceptance = "TEMPEST-BGR-005";

    /// <summary>The risk's treatment is Accept, but no named person has accepted it.</summary>
    public const string AcceptTreatmentNeedsAcceptance = "TEMPEST-BGR-006";

    /// <summary>The risk's treatment is Transfer, but no insurance policy is named against it.</summary>
    public const string TransferTreatmentNeedsPolicy = "TEMPEST-BGR-007";

    /// <summary>A mitigation claims to be in place and shows nothing for it.</summary>
    public const string MitigationIsUnevidenced = "TEMPEST-BGR-008";

    /// <summary>A mitigation is past its own date and still not in place.</summary>
    public const string MitigationIsOverdue = "TEMPEST-BGR-009";

    /// <summary>The risk has no mitigations at all and is not merely being monitored.</summary>
    public const string RiskHasNoMitigations = "TEMPEST-BGR-010";

    /// <summary>The risk is closed but does not say why.</summary>
    public const string ClosedRiskNeedsReason = "TEMPEST-BGR-011";

    /// <summary>Two risks share one reference.</summary>
    public const string DuplicateRiskReference = "TEMPEST-BGR-012";

    /// <summary>The risk names an insurance policy the library does not hold.</summary>
    public const string PolicyReferenceMustResolve = "TEMPEST-BGR-013";

    /// <summary>The policy holds no coverage sections, so registering it records nothing about what it insures.</summary>
    public const string PolicyMustHaveCoverage = "TEMPEST-BGR-014";

    /// <summary>A coverage section states no limit of indemnity.</summary>
    public const string CoverageHasNoStatedLimit = "TEMPEST-BGR-015";

    /// <summary>The policy's period of cover has ended.</summary>
    public const string PolicyHasLapsed = "TEMPEST-BGR-016";

    /// <summary>The policy is recorded as active but its period has ended.</summary>
    public const string ActivePolicyHasExpiredPeriod = "TEMPEST-BGR-017";

    /// <summary>Cover ends soon and renewal has not been arranged.</summary>
    public const string PolicyRenewalNotArranged = "TEMPEST-BGR-018";

    /// <summary>No document or certificate proves the policy.</summary>
    public const string PolicyHasNoDocument = "TEMPEST-BGR-019";

    /// <summary>Two policies share one reference.</summary>
    public const string DuplicatePolicyReference = "TEMPEST-BGR-020";
}

/// <summary>Governance of the business risk register itself.</summary>
public interface IBusinessRiskValidationService : IReferenceValidationService<BusinessRisk>
{
}

/// <summary>The concrete <see cref="IBusinessRiskValidationService"/> implementation.</summary>
/// <remarks>
/// The findings that matter are the ones a risk register conceals: a
/// residual rating that credits mitigations nobody implemented, a serious
/// risk nobody accepted, a transfer with no policy behind it. None of
/// these is visible from a colour-coded matrix, and every one is visible
/// from the record.
/// </remarks>
public sealed class BusinessRiskValidationService
    : ReferenceValidationService<BusinessRisk>, IBusinessRiskValidationService
{
    private readonly IInsurancePolicyCatalog? _policies;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="BusinessRiskValidationService"/> class.</summary>
    /// <param name="catalog">The risk register whose records this service validates.</param>
    /// <param name="policies">The insurance library, for confirming that a named policy exists. Optional: a register must be usable before any policy is recorded.</param>
    /// <param name="timeProvider">The clock overdue checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public BusinessRiskValidationService(
        IBusinessRiskCatalog catalog,
        IInsurancePolicyCatalog? policies = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _policies = policies;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        BusinessRisk definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Risk '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings, expectEvidence: false);

        if (definition.IsClosed)
        {
            if (string.IsNullOrWhiteSpace(definition.ClosureReason))
                warnings.Add(Diagnostic(
                    RiskValidationRules.ClosedRiskNeedsReason,
                    $"{subject} is closed but does not say why. A risk that reappears needs to be readable against how it was seen off."));

            return;
        }

        if (!definition.IsAssessed)
            warnings.Add(Diagnostic(
                RiskValidationRules.RiskIsUnassessed,
                $"{subject} has no assessed likelihood or impact, so it appears in the register without a position. "
                + "An unassessed risk is not a low one."));

        if (string.IsNullOrWhiteSpace(definition.Cause))
            warnings.Add(Diagnostic(
                RiskValidationRules.RiskHasNoCause,
                $"{subject} does not say what could bring it about, so nothing can be done to make it less likely."));

        if (string.IsNullOrWhiteSpace(definition.Consequence))
            warnings.Add(Diagnostic(
                RiskValidationRules.RiskHasNoConsequence,
                $"{subject} does not say what would follow if it occurred, so its impact rating rests on nothing stated."));

        if (definition.ResidualIsUnearned)
            errors.Add(Diagnostic(
                RiskValidationRules.ResidualExposureIsUnearned,
                $"{subject} is assessed as {definition.ResidualExposure} residual against {definition.InherentExposure} inherent, "
                + "with no mitigation actually implemented. That is an assessment of the plan, not of the position the "
                + "organisation is in."));

        if (definition.IsCarriedWithoutAcceptance)
            warnings.Add(Diagnostic(
                RiskValidationRules.RiskCarriedWithoutAcceptance,
                $"{subject} is carried at {definition.ResidualExposure} residual exposure and nobody has accepted it. "
                + "The organisation is running the risk; no one has said it will."));

        if (definition.Treatment == RiskTreatment.Accept && !definition.IsAccepted)
            errors.Add(Diagnostic(
                RiskValidationRules.AcceptTreatmentNeedsAcceptance,
                $"{subject} is treated as Accept, but no named person has accepted it. Choosing to accept a risk is a decision "
                + "somebody takes, not a field somebody sets."));

        if (definition.Treatment == RiskTreatment.Transfer && definition.RelatedPolicyReferences.Count == 0)
            warnings.Add(Diagnostic(
                RiskValidationRules.TransferTreatmentNeedsPolicy,
                $"{subject} is treated as Transfer but names no insurance policy, so what it is transferred to is unrecorded."));

        if (definition.Mitigations.Count == 0 && definition.Treatment is not (RiskTreatment.Monitor or RiskTreatment.Accept))
            warnings.Add(Diagnostic(
                RiskValidationRules.RiskHasNoMitigations,
                $"{subject} is treated as {definition.Treatment} but records nothing being done about it."));

        foreach (var mitigation in definition.Mitigations.Where(m => m.IsUnevidenced))
            warnings.Add(Diagnostic(
                RiskValidationRules.MitigationIsUnevidenced,
                $"Mitigation '{mitigation.Reference}' on {subject} is recorded as in place with no evidence, so the residual "
                + "assessment rests on somebody's word."));

        foreach (var mitigation in definition.OverdueMitigations(today))
            warnings.Add(Diagnostic(
                RiskValidationRules.MitigationIsOverdue,
                $"Mitigation '{mitigation.Reference}' on {subject} was due on {mitigation.DueBy:O}, is owned by "
                + $"'{mitigation.OwnerPrincipalId}', and is not in place."));

        await EvaluatePoliciesAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private async Task EvaluatePoliciesAsync(
        BusinessRisk definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_policies is null)
            return;

        foreach (var reference in definition.RelatedPolicyReferences)
        {
            if (await _policies.FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false) is null)
                warnings.Add(Diagnostic(
                    RiskValidationRules.PolicyReferenceMustResolve,
                    $"{subject} names insurance policy '{reference}', which the insurance library does not hold. The risk is "
                    + "recorded as transferred to a policy nobody can produce."));
        }
    }
}

/// <summary>Governance of insurance policy records themselves.</summary>
public interface IInsurancePolicyValidationService : IReferenceValidationService<InsurancePolicy>
{
}

/// <summary>The concrete <see cref="IInsurancePolicyValidationService"/> implementation.</summary>
/// <remarks>
/// Nothing here interprets policy wording. What it checks is whether the
/// organisation could actually demonstrate the cover it believes it has:
/// is there a document, is the period current, does the schedule state a
/// limit, and has renewal been arranged before cover runs out.
/// </remarks>
public sealed class InsurancePolicyValidationService
    : ReferenceValidationService<InsurancePolicy>, IInsurancePolicyValidationService
{
    /// <summary>How far ahead a renewal is expected to have been arranged.</summary>
    public const int RenewalWarningDays = 60;

    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="InsurancePolicyValidationService"/> class.</summary>
    /// <param name="catalog">The insurance library whose records this service validates.</param>
    /// <param name="timeProvider">The clock expiry checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public InsurancePolicyValidationService(IInsurancePolicyCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        InsurancePolicy definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Policy '{definition.Reference}' ({definition.PolicyNumber})";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings);

        if (definition.Coverages.Count == 0)
            errors.Add(Diagnostic(
                RiskValidationRules.PolicyMustHaveCoverage,
                $"{subject} records no coverage, so registering it says nothing about what the organisation is insured for."));

        foreach (var coverage in definition.Coverages.Where(c => !c.HasStatedLimit))
            warnings.Add(Diagnostic(
                RiskValidationRules.CoverageHasNoStatedLimit,
                $"{subject} records {coverage.Type} cover with no limit of indemnity. Either the schedule states none, or it was "
                + "not transcribed — and the two are very different positions."));

        if (definition.HasLapsedBy(today))
            warnings.Add(Diagnostic(
                RiskValidationRules.PolicyHasLapsed,
                $"{subject} ran to {definition.PeriodOfCover.To:O} and has lapsed."));

        if (definition.Status == PolicyStatus.Active && definition.HasLapsedBy(today))
            errors.Add(Diagnostic(
                RiskValidationRules.ActivePolicyHasExpiredPeriod,
                $"{subject} is recorded as Active but its period ended on {definition.PeriodOfCover.To:O}. Any risk relying on "
                + "this policy is currently relying on a record nobody updated."));

        if (definition.ExpiresWithin(today, RenewalWarningDays) && definition.RenewalState != DeterminationState.Recorded)
            warnings.Add(Diagnostic(
                RiskValidationRules.PolicyRenewalNotArranged,
                $"{subject} ends on {definition.PeriodOfCover.To:O} and renewal is {definition.RenewalState}."));

        if (!definition.HasPolicyDocument)
            warnings.Add(Diagnostic(
                RiskValidationRules.PolicyHasNoDocument,
                $"{subject} has no policy document or certificate held against it, so the organisation cannot demonstrate the "
                + "cover it believes it has."));

        return Task.CompletedTask;
    }
}
