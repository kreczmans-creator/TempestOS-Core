using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Risk;

/// <summary>What kind of loss a policy is written against.</summary>
/// <remarks>
/// The kinds an engineering consultancy typically carries. The list is not
/// exhaustive and is not advice about what an organisation needs to hold:
/// TempestOS records what is held, and does not opine on what should be.
/// </remarks>
public enum InsuranceCoverageType
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Professional indemnity — claims arising from the organisation's advice or design.</summary>
    ProfessionalIndemnity,

    /// <summary>Public liability — injury or damage to third parties.</summary>
    PublicLiability,

    /// <summary>Employers' liability — injury to the organisation's own people.</summary>
    EmployersLiability,

    /// <summary>Product liability — harm caused by a product supplied.</summary>
    ProductLiability,

    /// <summary>Cyber — data breach, ransomware, business interruption from an IT cause.</summary>
    Cyber,

    /// <summary>Contents, equipment and premises.</summary>
    PropertyAndEquipment,

    /// <summary>Business interruption.</summary>
    BusinessInterruption,

    /// <summary>Directors' and officers' liability.</summary>
    DirectorsAndOfficers,

    /// <summary>Legal expenses.</summary>
    LegalExpenses,

    /// <summary>Something else, described in the policy record.</summary>
    Other
}

/// <summary>Where a policy stands.</summary>
public enum PolicyStatus
{
    /// <summary>Being arranged; not yet on cover.</summary>
    Proposed,

    /// <summary>On cover.</summary>
    Active,

    /// <summary>Its period has ended and it has not been renewed.</summary>
    Expired,

    /// <summary>Ended early.</summary>
    Cancelled,

    /// <summary>Replaced by a later policy, which the record names.</summary>
    Renewed
}

/// <summary>
/// One coverage section of a policy: what it insures, up to what limit,
/// and what it does not.
/// </summary>
/// <remarks>
/// <b>The limits and exclusions here are transcribed from the policy, not
/// interpreted.</b> Whether a given loss falls inside a limit or outside
/// an exclusion is a question for the insurer and, if it is contested, a
/// solicitor. What this record supports is the prior question, which the
/// organisation can and should answer for itself: is there a policy, what
/// does its schedule say, and where is the document.
/// </remarks>
/// <param name="Type">What kind of loss this section is written against.</param>
/// <param name="Description">What the schedule says it covers. Required.</param>
/// <param name="LimitOfIndemnity">The most the insurer will pay. <see langword="null"/> where the schedule states none and that is itself worth reporting.</param>
/// <param name="LimitBasis">Whether the limit is per claim, in the aggregate, or otherwise. <see langword="null"/> if the schedule does not say.</param>
/// <param name="Excess">What the organisation bears before the insurer pays. <see langword="null"/> where none is stated.</param>
/// <param name="Exclusions">What the schedule says is not covered, in its own words. Never <see langword="null"/>.</param>
/// <param name="Conditions">Conditions the organisation must satisfy for cover to respond. Never <see langword="null"/>.</param>
public sealed record InsuranceCoverage(
    InsuranceCoverageType Type,
    string Description,
    Money? LimitOfIndemnity = null,
    string? LimitBasis = null,
    Money? Excess = null,
    IReadOnlyList<string>? Exclusions = null,
    IReadOnlyList<string>? Conditions = null)
{
    /// <summary>What the schedule says it covers.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A coverage section must say what it covers, in the schedule's own words.", nameof(Description))
        : Description.Trim();

    /// <summary>What the schedule says is not covered.</summary>
    public IReadOnlyList<string> Exclusions { get; init; } = Exclusions ?? [];

    /// <summary>Conditions the organisation must satisfy for cover to respond.</summary>
    public IReadOnlyList<string> Conditions { get; init; } = Conditions ?? [];

    /// <summary>Whether the schedule states how much the insurer will pay.</summary>
    public bool HasStatedLimit => LimitOfIndemnity is not null;
}

/// <summary>
/// An insurance policy the organisation holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recording a policy is not proof of cover, and reading a policy is
/// not advice.</b> This record says what the organisation believes it
/// holds and where the document is. Whether the policy responds to a
/// particular loss depends on wording, disclosure, conditions precedent
/// and facts nobody has yet — and is the insurer's determination, not
/// this platform's.
/// </para>
/// <para>
/// No policy data ships with TempestOS. Every value in a registered policy
/// comes from a document the organisation holds, and the record's own
/// evidence points at it.
/// </para>
/// </remarks>
public sealed record InsurancePolicy
{
    /// <summary>The reference the policy is known by internally. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>The insurer's own policy number. Required — a policy nobody can quote to an insurer is not usable.</summary>
    public required string PolicyNumber { get; init; }

    /// <summary>Who wrote the policy. Required.</summary>
    public required string Insurer { get; init; }

    /// <summary>Who is insured, as the schedule names them. Required.</summary>
    public required string InsuredParty { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>The period of cover. Required — a policy without one covers nothing identifiable.</summary>
    public required EffectivePeriod PeriodOfCover { get; init; }

    /// <summary>Where the policy stands.</summary>
    public PolicyStatus Status { get; init; } = PolicyStatus.Proposed;

    /// <summary>What the policy covers, section by section. Never <see langword="null"/>.</summary>
    public IReadOnlyList<InsuranceCoverage> Coverages { get; init; } = [];

    /// <summary>The broker who placed it. <see langword="null"/> where it was placed directly.</summary>
    public string? Broker { get; init; }

    /// <summary>The premium. <see langword="null"/> where it is not recorded here.</summary>
    public Money? Premium { get; init; }

    /// <summary>The policy document or certificate. <see langword="null"/> where it is not held in TempestOS.</summary>
    /// <remarks>
    /// A policy with no document is a policy the organisation cannot prove
    /// it has. That is a reportable state, not an error.
    /// </remarks>
    public Guid? PolicyDocumentId { get; init; }

    /// <summary>The policy that replaced this one. <see langword="null"/> where none has.</summary>
    public string? RenewedByPolicyReference { get; init; }

    /// <summary>Whether renewal has been arranged.</summary>
    public DeterminationState RenewalState { get; init; } = DeterminationState.NotDetermined;

    /// <summary>Anything else about the policy. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the policy is recorded as on cover.</summary>
    public bool IsActive => Status == PolicyStatus.Active;

    /// <summary>Whether the policy is on cover on <paramref name="date"/> according to its own recorded period and status.</summary>
    /// <remarks>
    /// Both conditions, deliberately: a policy whose period covers today
    /// but whose status says Cancelled is not cover, and a policy marked
    /// Active whose period ended is a record nobody updated.
    /// </remarks>
    public bool IsOnCoverOn(DateOnly date) => IsActive && PeriodOfCover.Contains(date);

    /// <summary>Whether the period of cover has ended by <paramref name="asAt"/>.</summary>
    public bool HasLapsedBy(DateOnly asAt) => PeriodOfCover.HasExpiredBy(asAt);

    /// <summary>Whether cover ends within <paramref name="withinDays"/> of <paramref name="asAt"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="withinDays"/> is negative.</exception>
    public bool ExpiresWithin(DateOnly asAt, int withinDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(withinDays);

        return PeriodOfCover.To is { } end && end >= asAt && end <= asAt.AddDays(withinDays);
    }

    /// <summary>Whether the policy holds a section of <paramref name="type"/>.</summary>
    public bool Covers(InsuranceCoverageType type) => Coverages.Any(c => c.Type == type);

    /// <summary>Returns the section of <paramref name="type"/>, or <see langword="null"/> if the policy has none.</summary>
    public InsuranceCoverage? FindCoverage(InsuranceCoverageType type) => Coverages.FirstOrDefault(c => c.Type == type);

    /// <summary>Whether the organisation holds a document proving this policy.</summary>
    public bool HasPolicyDocument => PolicyDocumentId is not null;

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
