using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Assets;

/// <summary>What kind of information a data asset holds.</summary>
/// <remarks>
/// The categories that change how information must be handled. Personal
/// data is separated from special-category personal data because the two
/// carry different obligations, and separating them is a matter of
/// record-keeping rather than of legal interpretation.
/// </remarks>
public enum DataCategory
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>The organisation's own business information.</summary>
    BusinessData,

    /// <summary>Engineering data — models, calculations, test results, drawings.</summary>
    EngineeringData,

    /// <summary>Information belonging to, or about, a client.</summary>
    ClientData,

    /// <summary>Information identifying a living individual.</summary>
    PersonalData,

    /// <summary>Personal data of a kind attracting additional obligations — health, biometric, and the rest.</summary>
    SpecialCategoryPersonalData,

    /// <summary>Information about the organisation's own people, as employees.</summary>
    EmployeeData,

    /// <summary>Financial records.</summary>
    FinancialData,

    /// <summary>Information a supplier provided in confidence.</summary>
    SupplierData,

    /// <summary>Something else, described in the record.</summary>
    Other
}

/// <summary>How long information is kept, and what happens then.</summary>
/// <param name="Description">The rule, in the organisation's own words. Required.</param>
/// <param name="RetainForMonths">How long it is kept. <see langword="null"/> where retention is indefinite or driven by an event rather than a period.</param>
/// <param name="RetentionTrigger">What starts the clock — end of engagement, last contact, statutory limitation. <see langword="null"/> if not stated.</param>
/// <param name="DisposalMethod">What is done at the end — secure deletion, return to client, archival. <see langword="null"/> if not stated.</param>
/// <param name="Basis">Why this period: a statutory requirement, a contract term, or the organisation's own policy. Required.</param>
/// <param name="BasisState">How firmly the basis is established. A retention period somebody assumed is not one somebody determined.</param>
public sealed record RetentionRule(
    string Description,
    int? RetainForMonths = null,
    string? RetentionTrigger = null,
    string? DisposalMethod = null,
    string Basis = "Organisation policy",
    DeterminationState BasisState = DeterminationState.NotDetermined)
{
    /// <summary>The rule, in the organisation's own words.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A retention rule must say what it requires.", nameof(Description))
        : Description.Trim();

    /// <summary>Why this period.</summary>
    public string Basis { get; } = string.IsNullOrWhiteSpace(Basis)
        ? throw new ArgumentException("A retention rule must say what it rests on — a statute, a contract, or policy.", nameof(Basis))
        : Basis.Trim();

    /// <summary>How long the information is kept.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="RetainForMonths"/> is not positive.</exception>
    public int? RetainForMonths { get; } = RetainForMonths is { } months && months <= 0
        ? throw new ArgumentOutOfRangeException(nameof(RetainForMonths), months, "A retention period must be a positive number of months.")
        : RetainForMonths;

    /// <summary>Whether the rule says what happens to the information at the end.</summary>
    public bool StatesDisposal => !string.IsNullOrWhiteSpace(DisposalMethod);

    /// <summary>Whether the rule states a definite period rather than leaving retention open.</summary>
    public bool IsBounded => RetainForMonths is not null;
}

/// <summary>
/// A body of information the organisation holds, and the terms on which it
/// holds it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a data-protection framework, not a compliance
/// determination.</b> TempestOS records what data exists, why it is held,
/// who owns it, who may see it, how long it is kept and what evidence
/// supports each answer. It does not conclude that the organisation
/// complies with anything — that is a determination for somebody qualified
/// to make it, and <see cref="ComplianceReviewState"/> records whose it is
/// and whether it has happened.
/// </para>
/// <para>
/// It is also not a security system. Access is enforced by
/// <see cref="Tempest.Core.Identity.IPermissionEvaluator"/> and the
/// platform's own roles; what is recorded here is the handling
/// requirement, so a person or a future policy can act on it.
/// </para>
/// </remarks>
public sealed record DataAsset
{
    /// <summary>The reference the data asset is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What the information is. Required.</summary>
    public required string Name { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>What kind of information it is.</summary>
    public DataCategory Category { get; init; } = DataCategory.Unspecified;

    /// <summary>Why the organisation holds it. Required in substance — an asset with no stated purpose is one nobody can justify keeping.</summary>
    public string? ProcessingPurpose { get; init; }

    /// <summary>Whose information it is, where it is not the organisation's. <see langword="null"/> where it is.</summary>
    public string? DataOwnerName { get; init; }

    /// <summary>Where it is held — a system, a location, a service. <see langword="null"/> if not recorded.</summary>
    public string? Location { get; init; }

    /// <summary>How long it is kept. <see langword="null"/> where nobody has set a rule — itself the most common and most reportable state.</summary>
    public RetentionRule? Retention { get; init; }

    /// <summary>Who may see it, in the organisation's own terms. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// A statement of requirement, not an access-control list. The
    /// platform's Identity layer enforces access; this records what the
    /// enforcement is supposed to achieve.
    /// </remarks>
    public IReadOnlyList<string> AccessRequirements { get; init; } = [];

    /// <summary>Restrictions on moving it — outside the organisation, outside a jurisdiction, to a subcontractor. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> TransferRestrictions { get; init; } = [];

    /// <summary>Whether a qualified person has reviewed the organisation's position on this data.</summary>
    public DeterminationState ComplianceReviewState { get; init; } = DeterminationState.NotDetermined;

    /// <summary>Who the review belongs to, where it has not happened. <see langword="null"/> where nobody has been named.</summary>
    public string? ComplianceReviewOwner { get; init; }

    /// <summary>The contract that governs how this data is handled, where one does. <see langword="null"/> otherwise.</summary>
    public string? GoverningContractReference { get; init; }

    /// <summary>Anything else about the asset. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the asset holds information about identifiable people.</summary>
    public bool IsPersonalData => Category is DataCategory.PersonalData or DataCategory.SpecialCategoryPersonalData or DataCategory.EmployeeData;

    /// <summary>Whether the asset holds information attracting the heaviest handling obligations.</summary>
    public bool IsSpecialCategory => Category == DataCategory.SpecialCategoryPersonalData;

    /// <summary>Whether the organisation has stated why it holds this information.</summary>
    public bool HasStatedPurpose => !string.IsNullOrWhiteSpace(ProcessingPurpose);

    /// <summary>Whether anybody has set a retention rule.</summary>
    public bool HasRetentionRule => Retention is not null;

    /// <summary>Whether the asset is held indefinitely with nothing saying it should be.</summary>
    public bool IsRetainedIndefinitely => Retention is null || !Retention.IsBounded;

    /// <summary>Every reference-data revision the asset's position rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        Governance.Evidence.Select(e => e.Pin)
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
