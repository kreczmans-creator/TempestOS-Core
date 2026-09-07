using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Risk;

/// <summary>
/// What the records support saying about whether a risk is insured.
/// </summary>
/// <remarks>
/// <b>Every value except <see cref="NotCovered"/> stops short of asserting
/// cover.</b> Whether a policy responds to a loss depends on wording,
/// disclosure and facts nobody has until a claim is made; that
/// determination belongs to the insurer. What TempestOS can say is what
/// its own records support, and the strongest of those is "a current
/// policy of the right type names a limit" — which is
/// <see cref="PolicySupportsClaim"/>, not "covered".
/// </remarks>
public enum CoverageAssessment
{
    /// <summary>No policy is named against the risk. Nothing is claimed either way.</summary>
    NoPolicyNamed,

    /// <summary>A policy is named but the library does not hold it.</summary>
    PolicyNotFound,

    /// <summary>A policy is held but its period has ended, so it cannot answer a loss occurring now.</summary>
    PolicyLapsed,

    /// <summary>A policy is held and current, but holds no section of a type that answers this kind of risk.</summary>
    NoRelevantCoverage,

    /// <summary>A current policy holds relevant cover, but its schedule states no limit here, so the extent is unknown.</summary>
    CoverageLimitUnknown,

    /// <summary>A current policy holds relevant cover with a stated limit, and no document proves it.</summary>
    UnevidencedPolicy,

    /// <summary>
    /// A current, evidenced policy holds relevant cover with a stated
    /// limit. The strongest statement the records support — and still not
    /// a statement that a claim would be paid.
    /// </summary>
    PolicySupportsClaim,

    /// <summary>The risk is of a kind the named policies expressly exclude.</summary>
    NotCovered
}

/// <summary>
/// What the records say about one risk's insurance position.
/// </summary>
/// <param name="RiskReference">The risk assessed.</param>
/// <param name="Assessment">What the records support.</param>
/// <param name="Reason">Why, in plain language. Required.</param>
/// <param name="PolicyReferences">The policies considered. Never <see langword="null"/>.</param>
/// <param name="StatedLimit">The limit the relevant coverage states, where one is stated. <see langword="null"/> otherwise.</param>
/// <param name="EstimatedExposure">The risk's own estimated financial exposure, for comparison. <see langword="null"/> where none is estimated.</param>
/// <param name="Pins">The exact policy record revisions read. Never <see langword="null"/>.</param>
public sealed record RiskCoveragePosition(
    string RiskReference,
    CoverageAssessment Assessment,
    string Reason,
    IReadOnlyList<string> PolicyReferences,
    Money? StatedLimit,
    Money? EstimatedExposure,
    IReadOnlyList<ReferencePin> Pins)
{
    /// <summary>Why the assessment concluded what it did.</summary>
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException("A coverage position must say why it concluded what it did.", nameof(Reason))
        : Reason.Trim();

    /// <summary>
    /// Whether the estimated exposure exceeds the stated limit.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> where either figure is missing or they are in
    /// different currencies — this comparison is refused rather than
    /// converted, for the same reason <see cref="Money"/> refuses to add
    /// across currencies.
    /// </remarks>
    public bool? ExposureExceedsLimit =>
        StatedLimit is { } limit && EstimatedExposure is { } exposure && limit.Currency == exposure.Currency
            ? exposure > limit
            : null;
}

/// <summary>
/// The organisation's overall risk and insurance position.
/// </summary>
/// <param name="AsAt">The date the position was taken at.</param>
/// <param name="OpenRisksByExposure">How many open risks sit in each residual exposure band.</param>
/// <param name="CarriedWithoutAcceptance">Serious risks nobody has accepted, by reference.</param>
/// <param name="UnassessedRisks">Risks nobody has assessed, by reference.</param>
/// <param name="RisksWithUnearnedResidual">Risks whose residual rating credits mitigations nobody implemented.</param>
/// <param name="LapsedPoliciesStillActive">Policies recorded as active whose period has ended.</param>
/// <param name="PoliciesExpiringWithin">Policies whose cover ends within the window asked for.</param>
/// <param name="OverdueMitigations">Mitigations past their own date, with the risk each belongs to.</param>
public sealed record RiskRegisterPosition(
    DateOnly AsAt,
    IReadOnlyDictionary<RiskExposure, int> OpenRisksByExposure,
    IReadOnlyList<string> CarriedWithoutAcceptance,
    IReadOnlyList<string> UnassessedRisks,
    IReadOnlyList<string> RisksWithUnearnedResidual,
    IReadOnlyList<string> LapsedPoliciesStillActive,
    IReadOnlyList<string> PoliciesExpiringWithin,
    IReadOnlyList<RiskMitigationEntry> OverdueMitigations)
{
    /// <summary>Whether anything at all needs somebody's attention.</summary>
    public bool HasFindings =>
        CarriedWithoutAcceptance.Count > 0
        || UnassessedRisks.Count > 0
        || RisksWithUnearnedResidual.Count > 0
        || LapsedPoliciesStillActive.Count > 0
        || PoliciesExpiringWithin.Count > 0
        || OverdueMitigations.Count > 0;
}

/// <summary>One mitigation, named with the risk it belongs to.</summary>
/// <param name="RiskReference">The risk.</param>
/// <param name="Mitigation">The mitigation.</param>
public sealed record RiskMitigationEntry(string RiskReference, RiskMitigation Mitigation);

/// <summary>
/// Reports what the risk register and the insurance library together say
/// about the organisation's exposure.
/// </summary>
/// <remarks>
/// <b>Nothing here accepts a risk or asserts that one is insured.</b>
/// <see cref="RecordAcceptance"/> attaches an acceptance a named person
/// made; it does not decide that a risk is acceptable.
/// <see cref="AssessCoverageAsync"/> reports what the records support and
/// stops one step short of "covered", because that word belongs to an
/// insurer.
/// </remarks>
public interface IRiskAndInsuranceService
{
    /// <summary>Reports what the records say about one risk's insurance position.</summary>
    /// <param name="riskReference">The risk to assess.</param>
    /// <param name="asAt">The date cover is judged at.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <exception cref="ArgumentException"><paramref name="riskReference"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ReferenceRecordNotFoundException">No risk is registered under <paramref name="riskReference"/>.</exception>
    Task<RiskCoveragePosition> AssessCoverageAsync(
        string riskReference,
        DateOnly asAt,
        CancellationToken cancellationToken = default);

    /// <summary>Reports the organisation's overall risk and insurance position as at <paramref name="asAt"/>.</summary>
    /// <param name="asAt">The date to take the position at.</param>
    /// <param name="renewalWindowDays">How far ahead to look for policies whose cover is ending.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="renewalWindowDays"/> is negative.</exception>
    Task<RiskRegisterPosition> ReportPositionAsync(
        DateOnly asAt,
        int renewalWindowDays = 60,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches an acceptance a named person made to a risk.
    /// </summary>
    /// <remarks>
    /// The acceptance is supplied, never computed. This records it and
    /// changes nothing else — in particular it does not close the risk,
    /// alter its assessment, or reduce its exposure. An accepted risk is
    /// a risk the organisation is knowingly running.
    /// </remarks>
    /// <param name="risk">The risk being accepted.</param>
    /// <param name="acceptance">The acceptance a person made.</param>
    /// <returns>The risk with the acceptance recorded. The original is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="acceptance"/> is not a risk acceptance.</exception>
    /// <exception cref="InvalidOperationException">The risk already records an acceptance.</exception>
    BusinessRisk RecordAcceptance(BusinessRisk risk, BusinessAuthorisation acceptance);
}

/// <summary>The concrete <see cref="IRiskAndInsuranceService"/> implementation.</summary>
public sealed class RiskAndInsuranceService : IRiskAndInsuranceService
{
    private readonly IBusinessRiskCatalog _risks;
    private readonly IInsurancePolicyCatalog _policies;

    /// <summary>Initialises a new instance of the <see cref="RiskAndInsuranceService"/> class.</summary>
    /// <param name="risks">The organisation's risk register.</param>
    /// <param name="policies">The insurance library.</param>
    /// <remarks>
    /// Deliberately takes no <see cref="Tempest.Core.Identity.ICurrentPrincipalAccessor"/>.
    /// Nothing this service does is attributed to the caller: the reports
    /// are pure functions of the records, and the one act of authority it
    /// touches — accepting a risk — arrives already carrying the name of
    /// the person who made it. A service that could read the ambient
    /// principal could quietly attribute an acceptance to whoever happened
    /// to be logged in.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public RiskAndInsuranceService(IBusinessRiskCatalog risks, IInsurancePolicyCatalog policies)
    {
        ArgumentNullException.ThrowIfNull(risks);
        ArgumentNullException.ThrowIfNull(policies);

        _risks = risks;
        _policies = policies;
    }

    /// <summary>
    /// Which coverage types are treated as answering which risk category.
    /// </summary>
    /// <remarks>
    /// A mapping the organisation can disagree with, published so that it
    /// can. It is used only to say "no policy of a relevant type is held";
    /// it never concludes that a policy of a relevant type does respond.
    /// </remarks>
    public static IReadOnlyDictionary<BusinessRiskCategory, IReadOnlyList<InsuranceCoverageType>> RelevantCoverage { get; } =
        new Dictionary<BusinessRiskCategory, IReadOnlyList<InsuranceCoverageType>>
        {
            [BusinessRiskCategory.ProfessionalLiability] = [InsuranceCoverageType.ProfessionalIndemnity],
            [BusinessRiskCategory.HealthAndSafety] = [InsuranceCoverageType.EmployersLiability, InsuranceCoverageType.PublicLiability],
            [BusinessRiskCategory.InformationSecurity] = [InsuranceCoverageType.Cyber],
            [BusinessRiskCategory.LegalAndContractual] = [InsuranceCoverageType.ProfessionalIndemnity, InsuranceCoverageType.LegalExpenses],
            [BusinessRiskCategory.Operational] = [InsuranceCoverageType.BusinessInterruption, InsuranceCoverageType.PropertyAndEquipment],
            [BusinessRiskCategory.IntellectualProperty] = [InsuranceCoverageType.ProfessionalIndemnity, InsuranceCoverageType.LegalExpenses],
        };

    /// <inheritdoc />
    public async Task<RiskCoveragePosition> AssessCoverageAsync(
        string riskReference,
        DateOnly asAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(riskReference);

        var record = await _risks.FindByReferenceAsync(riskReference, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(_risks.LibraryName, riskReference);

        var risk = record.Definition;

        if (risk.RelatedPolicyReferences.Count == 0)
            return Position(risk, CoverageAssessment.NoPolicyNamed,
                "No insurance policy is named against this risk, so the records say nothing about whether it is insured.",
                null, []);

        var pins = new List<ReferencePin>();
        var relevantTypes = RelevantCoverage.TryGetValue(risk.Category, out var types) ? types : [];
        var anyFound = false;
        var anyCurrent = false;
        InsuranceCoverage? best = null;
        var bestIsEvidenced = false;

        foreach (var reference in risk.RelatedPolicyReferences)
        {
            var policyRecord = await _policies.FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false);

            if (policyRecord is null)
                continue;

            anyFound = true;
            pins.Add(ReferencePin.For(_policies.LibraryName, policyRecord));

            var policy = policyRecord.Definition;

            if (!policy.IsOnCoverOn(asAt))
                continue;

            anyCurrent = true;

            var coverage = relevantTypes.Count == 0
                ? policy.Coverages.FirstOrDefault()
                : relevantTypes.Select(policy.FindCoverage).FirstOrDefault(c => c is not null);

            if (coverage is null)
                continue;

            if (best is null || (coverage.HasStatedLimit && !best.HasStatedLimit))
            {
                best = coverage;
                bestIsEvidenced = policy.HasPolicyDocument;
            }
        }

        if (!anyFound)
            return Position(risk, CoverageAssessment.PolicyNotFound,
                $"The risk names {risk.RelatedPolicyReferences.Count} polic"
                + $"{(risk.RelatedPolicyReferences.Count == 1 ? "y" : "ies")}, and the insurance library holds none of them.",
                null, pins);

        if (!anyCurrent)
            return Position(risk, CoverageAssessment.PolicyLapsed,
                $"Every policy named against this risk was off cover on {asAt:O}, so none of them answers a loss occurring now.",
                null, pins);

        if (best is null)
            return Position(risk, CoverageAssessment.NoRelevantCoverage,
                $"A current policy is held, but none of the named policies holds a section of a type that answers a "
                + $"{risk.Category} risk.",
                null, pins);

        if (!best.HasStatedLimit)
            return Position(risk, CoverageAssessment.CoverageLimitUnknown,
                $"A current policy holds {best.Type} cover, but its schedule states no limit of indemnity here, so the extent of "
                + "any protection is unknown.",
                null, pins);

        if (!bestIsEvidenced)
            return Position(risk, CoverageAssessment.UnevidencedPolicy,
                $"A current policy holds {best.Type} cover to {best.LimitOfIndemnity}, and no policy document is held, so the "
                + "organisation could not demonstrate it.",
                best.LimitOfIndemnity, pins);

        return Position(risk, CoverageAssessment.PolicySupportsClaim,
            $"A current, evidenced policy holds {best.Type} cover to {best.LimitOfIndemnity}. Whether it would respond to a "
            + "particular loss is the insurer's determination, not this record's.",
            best.LimitOfIndemnity, pins);
    }

    /// <inheritdoc />
    public async Task<RiskRegisterPosition> ReportPositionAsync(
        DateOnly asAt,
        int renewalWindowDays = 60,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(renewalWindowDays);

        var risks = await _risks.ListAsync(cancellationToken).ConfigureAwait(false);
        var policies = await _policies.ListAsync(cancellationToken).ConfigureAwait(false);

        var byExposure = new Dictionary<RiskExposure, int>();
        var unaccepted = new List<string>();
        var unassessed = new List<string>();
        var unearned = new List<string>();
        var overdue = new List<RiskMitigationEntry>();

        foreach (var record in risks.Where(r => r.ValidationState != ReferenceValidationState.Superseded))
        {
            var risk = record.Definition;

            if (risk.IsClosed)
                continue;

            byExposure[risk.ResidualExposure] = byExposure.GetValueOrDefault(risk.ResidualExposure) + 1;

            if (risk.IsCarriedWithoutAcceptance)
                unaccepted.Add(risk.Reference);

            if (!risk.IsAssessed)
                unassessed.Add(risk.Reference);

            if (risk.ResidualIsUnearned)
                unearned.Add(risk.Reference);

            overdue.AddRange(risk.OverdueMitigations(asAt).Select(m => new RiskMitigationEntry(risk.Reference, m)));
        }

        var lapsed = new List<string>();
        var expiring = new List<string>();

        foreach (var record in policies.Where(p => p.ValidationState != ReferenceValidationState.Superseded))
        {
            var policy = record.Definition;

            if (policy.Status == PolicyStatus.Active && policy.HasLapsedBy(asAt))
                lapsed.Add(policy.Reference);
            else if (policy.ExpiresWithin(asAt, renewalWindowDays))
                expiring.Add(policy.Reference);
        }

        return new RiskRegisterPosition(
            asAt,
            byExposure,
            Sorted(unaccepted),
            Sorted(unassessed),
            Sorted(unearned),
            Sorted(lapsed),
            Sorted(expiring),
            overdue.OrderBy(e => e.Mitigation.DueBy).ThenBy(e => e.RiskReference, StringComparer.Ordinal).ToList());
    }

    /// <inheritdoc />
    public BusinessRisk RecordAcceptance(BusinessRisk risk, BusinessAuthorisation acceptance)
    {
        ArgumentNullException.ThrowIfNull(risk);
        ArgumentNullException.ThrowIfNull(acceptance);

        if (acceptance.Kind != BusinessAuthorityKind.RiskAcceptance)
            throw new ArgumentException(
                $"Accepting a risk requires an act of {BusinessAuthorityKind.RiskAcceptance}, not {acceptance.Kind}.",
                nameof(acceptance));

        if (risk.IsAccepted)
            throw new InvalidOperationException(
                $"Risk '{risk.Reference}' already records an acceptance by '{risk.Acceptance!.PrincipalId}'. An acceptance is "
                + "not overwritten: re-assess the risk and accept it again, so both acts stay on the record.");

        // The acceptance is added and nothing else changes. In particular
        // the exposure is untouched: accepting a risk is a decision to
        // carry it, not a reason to think it smaller.
        return risk with
        {
            Governance = risk.Governance with
            {
                Authorisations = [.. risk.Governance.Authorisations, acceptance],
                OutstandingAuthorities = risk.Governance.OutstandingAuthorities
                    .Where(r => r.Kind != BusinessAuthorityKind.RiskAcceptance)
                    .ToList(),
            },
        };
    }

    private static IReadOnlyList<string> Sorted(List<string> values) =>
        values.OrderBy(v => v, StringComparer.Ordinal).ToList();

    private static RiskCoveragePosition Position(
        BusinessRisk risk,
        CoverageAssessment assessment,
        string reason,
        Money? limit,
        IReadOnlyList<ReferencePin> pins) =>
        new(risk.Reference, assessment, reason, risk.RelatedPolicyReferences, limit, risk.EstimatedFinancialExposure, pins);
}
