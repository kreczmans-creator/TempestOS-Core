using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Risk;

/// <summary>What kind of harm a business risk threatens.</summary>
public enum BusinessRiskCategory
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Loss of money, margin or cash.</summary>
    Financial,

    /// <summary>An inability to deliver — capacity, key person, supplier, tooling.</summary>
    Operational,

    /// <summary>A contractual, regulatory or statutory exposure.</summary>
    LegalAndContractual,

    /// <summary>Professional indemnity: the risk that the organisation's engineering advice is wrong.</summary>
    ProfessionalLiability,

    /// <summary>Harm to a person.</summary>
    HealthAndSafety,

    /// <summary>Loss, misuse or disclosure of information — the organisation's or a client's.</summary>
    InformationSecurity,

    /// <summary>Loss of intellectual property, or infringement of somebody else's.</summary>
    IntellectualProperty,

    /// <summary>Damage to reputation or client relationships.</summary>
    Reputational,

    /// <summary>A market, demand or competitive exposure.</summary>
    Commercial,

    /// <summary>Dependence on a supplier, subcontractor or third-party service.</summary>
    SupplyChain,

    /// <summary>Something else, described in the risk itself.</summary>
    Other
}

/// <summary>
/// How likely a risk is, on a stated scale.
/// </summary>
/// <remarks>
/// A band rather than a probability, and deliberately. An organisation
/// that has not run a hundred similar engagements cannot honestly say
/// "17 per cent", and a number invites arithmetic — likelihood times
/// impact — that produces a figure nobody can defend. Bands are what the
/// assessment actually knows.
/// </remarks>
public enum RiskLikelihood
{
    /// <summary>Not assessed.</summary>
    NotAssessed,

    /// <summary>Would be surprising.</summary>
    Rare,

    /// <summary>Could happen, but is not expected.</summary>
    Unlikely,

    /// <summary>Might well happen.</summary>
    Possible,

    /// <summary>Expected more often than not.</summary>
    Likely,

    /// <summary>Expected, or already happening.</summary>
    AlmostCertain
}

/// <summary>How badly the organisation would be hurt if the risk occurred.</summary>
public enum RiskImpact
{
    /// <summary>Not assessed.</summary>
    NotAssessed,

    /// <summary>Absorbed without material consequence.</summary>
    Negligible,

    /// <summary>Noticeable, and handled within normal operation.</summary>
    Minor,

    /// <summary>Materially disruptive; needs management attention.</summary>
    Moderate,

    /// <summary>Threatens a client relationship, a year's margin, or the ability to deliver.</summary>
    Major,

    /// <summary>Threatens the organisation's continued existence.</summary>
    Severe
}

/// <summary>
/// The combination of likelihood and impact, as a band.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a score, and not a number.</b> `P07` does not multiply a
/// likelihood by an impact: the product of two ordinal bands is not a
/// quantity, and treating it as one lets a "12" argue with a "9" as
/// though the difference meant something. What the matrix produces is
/// another band, and the band's only use is sorting a register so the
/// worst appears first.
/// </para>
/// <para>
/// <see cref="NotAssessed"/> is a real answer, returned whenever either
/// input is missing. A risk nobody has assessed must never appear as a low
/// one.
/// </para>
/// </remarks>
public enum RiskExposure
{
    /// <summary>Likelihood or impact — or both — have not been assessed.</summary>
    NotAssessed,

    /// <summary>Tolerable as it stands.</summary>
    Low,

    /// <summary>Worth managing.</summary>
    Medium,

    /// <summary>Needs action and an owner.</summary>
    High,

    /// <summary>Needs a decision at director level.</summary>
    Extreme
}

/// <summary>Turns a likelihood and an impact into an exposure band.</summary>
public static class RiskExposures
{
    /// <summary>Every exposure band, worst first — the order a risk register should present them in.</summary>
    public static IReadOnlyList<RiskExposure> WorstFirst { get; } =
    [
        RiskExposure.Extreme, RiskExposure.High, RiskExposure.NotAssessed, RiskExposure.Medium, RiskExposure.Low,
    ];

    /// <summary>
    /// The exposure band for <paramref name="likelihood"/> and
    /// <paramref name="impact"/>.
    /// </summary>
    /// <remarks>
    /// A published, deterministic matrix, so that the same assessment
    /// always yields the same band and two people reading the register see
    /// the same thing. If either input is unassessed, so is the output.
    /// </remarks>
    public static RiskExposure From(RiskLikelihood likelihood, RiskImpact impact)
    {
        if (likelihood == RiskLikelihood.NotAssessed || impact == RiskImpact.NotAssessed)
            return RiskExposure.NotAssessed;

        var l = (int)likelihood; // Rare 1 … AlmostCertain 5
        var i = (int)impact;     // Negligible 1 … Severe 5

        // A severe impact is never merely Low, however rare: an
        // organisation-ending event at low probability is still the kind of
        // risk a director has to see.
        if (impact == RiskImpact.Severe)
            return likelihood >= RiskLikelihood.Possible ? RiskExposure.Extreme : RiskExposure.High;

        return (l * i) switch
        {
            >= 16 => RiskExposure.Extreme,
            >= 9 => RiskExposure.High,
            >= 4 => RiskExposure.Medium,
            _ => RiskExposure.Low,
        };
    }

    /// <summary>How serious a band is, for ordering. Higher is worse; <see cref="RiskExposure.NotAssessed"/> sorts high on purpose.</summary>
    public static int Rank(RiskExposure exposure) => exposure switch
    {
        RiskExposure.Low => 0,
        RiskExposure.Medium => 1,
        RiskExposure.NotAssessed => 2,
        RiskExposure.High => 3,
        RiskExposure.Extreme => 4,
        _ => 2,
    };
}

/// <summary>How a risk is being handled.</summary>
public enum RiskTreatment
{
    /// <summary>Not decided.</summary>
    NotDecided,

    /// <summary>Stop doing the thing that creates the risk.</summary>
    Avoid,

    /// <summary>Reduce the likelihood, the impact, or both.</summary>
    Reduce,

    /// <summary>Move the financial consequence to somebody else — usually an insurer.</summary>
    /// <remarks>
    /// <b>Transfer is not elimination.</b> An insured risk still occurs;
    /// what changes is who pays. The register keeps the risk open and
    /// records the policy against it.
    /// </remarks>
    Transfer,

    /// <summary>Carry it knowingly. Requires a named person's acceptance, not a status change.</summary>
    Accept,

    /// <summary>Watch it, and decide when something changes.</summary>
    Monitor
}

/// <summary>
/// Something being done about a risk.
/// </summary>
/// <param name="Reference">The mitigation's own identifier. Required.</param>
/// <param name="Description">What is being done. Required.</param>
/// <param name="OwnerPrincipalId">Who is doing it. Required — a mitigation nobody owns is a hope.</param>
/// <param name="DueBy">When it is to be in place by. <see langword="null"/> where it is a standing control rather than a task.</param>
/// <param name="IsImplemented">Whether it is actually in place, as distinct from planned.</param>
/// <param name="Evidence">What shows it is in place. Never <see langword="null"/>.</param>
public sealed record RiskMitigation(
    string Reference,
    string Description,
    string OwnerPrincipalId,
    DateOnly? DueBy = null,
    bool IsImplemented = false,
    IReadOnlyList<BusinessEvidence>? Evidence = null)
{
    /// <summary>The mitigation's own identifier.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A mitigation must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What is being done.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A mitigation must say what is being done.", nameof(Description))
        : Description.Trim();

    /// <summary>Who is doing it.</summary>
    public string OwnerPrincipalId { get; } = string.IsNullOrWhiteSpace(OwnerPrincipalId)
        ? throw new ArgumentException("A mitigation must name who is carrying it out. One nobody owns is a hope, not a mitigation.", nameof(OwnerPrincipalId))
        : OwnerPrincipalId.Trim();

    /// <summary>What shows it is in place.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = Evidence ?? [];

    /// <summary>Whether the mitigation claims to be in place but shows nothing for it.</summary>
    public bool IsUnevidenced => IsImplemented && Evidence.Count == 0;

    /// <summary>Whether the mitigation is past its own date and still not in place.</summary>
    public bool IsOverdueAt(DateOnly asAt) => !IsImplemented && DueBy is { } due && due < asAt;
}

/// <summary>
/// A risk the organisation carries, as recorded in its risk register.
/// </summary>
/// <remarks>
/// <para>
/// <b>Assessment and acceptance are different acts, and this type keeps
/// them apart.</b> An assessment says how likely something is and how much
/// it would hurt. An acceptance says the organisation will carry it
/// anyway. The first is analysis; the second binds, and needs a named
/// person exercising
/// <see cref="BusinessAuthorityKind.RiskAcceptance"/>. Nothing in `P07`
/// converts one into the other, and a risk assessed as Low is still an
/// unaccepted risk.
/// </para>
/// <para>
/// This is the organisation's risk register, not a project's.
/// <see cref="Tempest.Core.EngineeringDomain.IRisk"/> already models a
/// risk on an engineering project, with the workspace's own lifecycle;
/// `P07` does not replace it. Where a project risk is serious enough to
/// belong to the business, <see cref="EscalatedFromProjectRiskId"/> names
/// the project risk it came from and the two stay linked rather than
/// duplicated.
/// </para>
/// </remarks>
public sealed record BusinessRisk
{
    /// <summary>The reference the risk is known by in the register. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What the risk is, in one line. Required.</summary>
    public required string Title { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>What kind of harm it threatens.</summary>
    public BusinessRiskCategory Category { get; init; } = BusinessRiskCategory.Unspecified;

    /// <summary>What could bring it about. <see langword="null"/> where nobody has worked that out — itself worth reporting.</summary>
    public string? Cause { get; init; }

    /// <summary>What would follow if it did. <see langword="null"/> where nobody has worked that out.</summary>
    public string? Consequence { get; init; }

    /// <summary>How likely it is before anything is done about it.</summary>
    public RiskLikelihood InherentLikelihood { get; init; } = RiskLikelihood.NotAssessed;

    /// <summary>How badly it would hurt before anything is done about it.</summary>
    public RiskImpact InherentImpact { get; init; } = RiskImpact.NotAssessed;

    /// <summary>How likely it is with the implemented mitigations in place.</summary>
    public RiskLikelihood ResidualLikelihood { get; init; } = RiskLikelihood.NotAssessed;

    /// <summary>How badly it would hurt with the implemented mitigations in place.</summary>
    public RiskImpact ResidualImpact { get; init; } = RiskImpact.NotAssessed;

    /// <summary>An estimate of what it would cost if it occurred. <see langword="null"/> where nobody has estimated one.</summary>
    /// <remarks>Recorded as an estimate, never as a determined liability. It is a planning figure, not an accounting one.</remarks>
    public Money? EstimatedFinancialExposure { get; init; }

    /// <summary>How the risk is being handled.</summary>
    public RiskTreatment Treatment { get; init; } = RiskTreatment.NotDecided;

    /// <summary>What is being done about it. Never <see langword="null"/>.</summary>
    public IReadOnlyList<RiskMitigation> Mitigations { get; init; } = [];

    /// <summary>Insurance policies the organisation holds against this risk, by policy reference. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// A reference to a policy is not proof of cover. Whether the policy
    /// actually answers this risk is assessed by
    /// <see cref="IRiskAndInsuranceService"/>, which reports what the
    /// evidence supports and never asserts coverage on its own.
    /// </remarks>
    public IReadOnlyList<string> RelatedPolicyReferences { get; init; } = [];

    /// <summary>The project risk this was escalated from, where it was. <see langword="null"/> where the risk arose at business level.</summary>
    public Guid? EscalatedFromProjectRiskId { get; init; }

    /// <summary>What would tell the organisation the risk is materialising. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> EarlyWarnings { get; init; } = [];

    /// <summary>Whether the risk is still live.</summary>
    public bool IsClosed { get; init; }

    /// <summary>Why it was closed. <see langword="null"/> where it is still live.</summary>
    public string? ClosureReason { get; init; }

    /// <summary>Anything else about the risk. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The exposure before mitigation.</summary>
    public RiskExposure InherentExposure => RiskExposures.From(InherentLikelihood, InherentImpact);

    /// <summary>The exposure the organisation is actually left carrying.</summary>
    public RiskExposure ResidualExposure => RiskExposures.From(ResidualLikelihood, ResidualImpact);

    /// <summary>Whether anybody has assessed the risk at all.</summary>
    public bool IsAssessed => InherentExposure != RiskExposure.NotAssessed;

    /// <summary>
    /// Whether a named person has accepted the risk.
    /// </summary>
    /// <remarks>
    /// The one question the register must never answer by inference. A
    /// risk is accepted when somebody with the authority accepted it, and
    /// not because its band is low, its mitigations are done, or its
    /// treatment says Accept.
    /// </remarks>
    public bool IsAccepted => Governance.HasAuthority(BusinessAuthorityKind.RiskAcceptance);

    /// <summary>Who accepted it, where anybody has.</summary>
    public BusinessAuthorisation? Acceptance => Governance.FindAuthority(BusinessAuthorityKind.RiskAcceptance);

    /// <summary>
    /// Whether the risk is being carried without anybody having accepted
    /// it — the finding a risk register exists to produce.
    /// </summary>
    public bool IsCarriedWithoutAcceptance =>
        !IsClosed && !IsAccepted && ResidualExposure is RiskExposure.High or RiskExposure.Extreme;

    /// <summary>Mitigations that are actually in place.</summary>
    public IReadOnlyList<RiskMitigation> ImplementedMitigations => Mitigations.Where(m => m.IsImplemented).ToList();

    /// <summary>Mitigations past their own date and still not in place.</summary>
    public IReadOnlyList<RiskMitigation> OverdueMitigations(DateOnly asAt) =>
        Mitigations.Where(m => m.IsOverdueAt(asAt)).ToList();

    /// <summary>
    /// Whether the residual assessment credits mitigations that are not
    /// actually in place.
    /// </summary>
    /// <remarks>
    /// A residual exposure lower than the inherent one, with nothing
    /// implemented to explain the difference, is an assessment of a plan
    /// rather than of the position. It is one of the most common and most
    /// dangerous errors in a risk register.
    /// </remarks>
    public bool ResidualIsUnearned =>
        IsAssessed
        && ResidualExposure != RiskExposure.NotAssessed
        && RiskExposures.Rank(ResidualExposure) < RiskExposures.Rank(InherentExposure)
        && ImplementedMitigations.Count == 0;

    /// <summary>Every reference-data revision the risk rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        Governance.Evidence.Select(e => e.Pin)
            .Concat(Mitigations.SelectMany(m => m.Evidence).Select(e => e.Pin))
            .OfType<ReferencePin>()
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }
}
