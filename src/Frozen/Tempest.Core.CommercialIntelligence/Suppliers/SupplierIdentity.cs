namespace Tempest.Core.CommercialIntelligence.Suppliers;

/// <summary>
/// How confident anybody is that two records describe the same supplier.
/// </summary>
/// <remarks>
/// <b>Suppliers are never merged silently.</b> Two records with the same
/// trading name may be one company, two subsidiaries, or a company and
/// the firm that bought its name out of administration. Identity
/// resolution therefore produces a confidence and a set of candidates
/// for a person to settle, never a merge.
/// </remarks>
public enum IdentityConfidence
{
    /// <summary>Nobody has assessed whether this is a distinct supplier.</summary>
    NotAssessed,

    /// <summary>The record may be a duplicate of another; nothing distinguishes them.</summary>
    Ambiguous,

    /// <summary>Probably distinct, on the balance of what is recorded.</summary>
    Probable,

    /// <summary>Distinct, evidenced by a registration number or equivalent hard identifier.</summary>
    Confirmed
}

/// <summary>Where a supplier stands with the organisation.</summary>
/// <remarks>
/// <b>None of these is an approval.</b> Approving a supplier is an act of
/// commercial authority under `P07`; these values describe the trading
/// relationship, and <see cref="Active"/> means "we deal with them", not
/// "somebody signed them off".
/// </remarks>
public enum SupplierStatus
{
    /// <summary>Identified, never traded with.</summary>
    Prospective,

    /// <summary>Traded with, and currently in use.</summary>
    Active,

    /// <summary>Traded with in the past; nothing current.</summary>
    Dormant,

    /// <summary>No longer traded with, by choice or because they stopped trading.</summary>
    Inactive,

    /// <summary>Deliberately not to be used. The reason is recorded on the record.</summary>
    Barred,

    /// <summary>Replaced by another supplier record, which this one names — an acquisition, a rename, or a resolved duplicate.</summary>
    Superseded
}

/// <summary>Reasoning over <see cref="SupplierStatus"/>.</summary>
public static class SupplierStatuses
{
    /// <summary>Every status, in the order a report should present them.</summary>
    public static IReadOnlyList<SupplierStatus> All { get; } =
    [
        SupplierStatus.Active, SupplierStatus.Prospective, SupplierStatus.Dormant,
        SupplierStatus.Inactive, SupplierStatus.Barred, SupplierStatus.Superseded,
    ];

    /// <summary>Whether the supplier may be considered for new work.</summary>
    /// <remarks>
    /// Dormant counts: a supplier nobody has used for two years is still a
    /// supplier, and excluding them from a comparison silently would hide
    /// a real option. Barred and Superseded do not.
    /// </remarks>
    public static bool IsConsiderable(SupplierStatus status) =>
        status is SupplierStatus.Active or SupplierStatus.Prospective or SupplierStatus.Dormant;

    /// <summary>Whether the organisation has actually traded with them.</summary>
    public static bool HasTraded(SupplierStatus status) =>
        status is SupplierStatus.Active or SupplierStatus.Dormant or SupplierStatus.Inactive;
}

/// <summary>
/// A name a supplier is or has been known by.
/// </summary>
/// <remarks>
/// Aliases are first-class because supplier names change constantly —
/// rebrands, acquisitions, trading names differing from registered names,
/// and the shorthand everybody in the office actually uses. A library
/// that stores one name per supplier fails the first time somebody
/// searches for the other one.
/// </remarks>
/// <param name="Name">The name. Required.</param>
/// <param name="Kind">What kind of name it is.</param>
/// <param name="UsedUntil">When the supplier stopped using it. <see langword="null"/> where it is current.</param>
/// <param name="Note">Anything worth recording about it — "after the 2024 acquisition". <see langword="null"/> if nothing.</param>
public sealed record SupplierAlias(string Name, SupplierNameKind Kind = SupplierNameKind.TradingName, DateOnly? UsedUntil = null, string? Note = null)
{
    /// <summary>The name.</summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("A supplier alias must have a name.", nameof(Name))
        : Name.Trim();

    /// <summary>Whether the supplier still uses this name.</summary>
    public bool IsCurrent => UsedUntil is null;

    /// <summary>The case-insensitive key the alias is matched on.</summary>
    public string MatchKey => Name.ToUpperInvariant();
}

/// <summary>What kind of name a supplier alias is.</summary>
public enum SupplierNameKind
{
    /// <summary>The name on the register of companies.</summary>
    RegisteredName,

    /// <summary>The name they trade under.</summary>
    TradingName,

    /// <summary>A name they used before a rename or acquisition.</summary>
    FormerName,

    /// <summary>What the organisation's own people call them.</summary>
    Shorthand
}

/// <summary>
/// A supplier's stable identity, held apart from everything that changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A supplier's name is not its identity.</b> Names collide, change
/// and are reused; a commercial library that keys on them will eventually
/// merge two companies or split one. The canonical reference is the
/// identity, and everything else — registered name, trading names,
/// former names — hangs off it as evidence of what the supplier has been
/// called.
/// </para>
/// <para>
/// <see cref="RegistrationNumber"/> is the only hard identifier the model
/// treats as conclusive, and it is optional, because sole traders and
/// overseas suppliers legitimately have none. Where it is absent,
/// <see cref="Confidence"/> can never reach
/// <see cref="IdentityConfidence.Confirmed"/> on registration evidence
/// alone.
/// </para>
/// </remarks>
public sealed record SupplierIdentity
{
    /// <summary>The reference the supplier is known by throughout TempestOS. Required, and stable for the life of the record.</summary>
    public required string Reference { get; init; }

    /// <summary>The supplier's registered or principal legal name. Required.</summary>
    public required string LegalName { get; init; }

    /// <summary>A company number or equivalent registration. <see langword="null"/> where the supplier has none or nobody recorded it.</summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>The country the registration is held in, as an ISO 3166-1 alpha-2 code. <see langword="null"/> where not recorded.</summary>
    public string? RegistrationCountry { get; init; }

    /// <summary>Every other name the supplier is or has been known by. Never <see langword="null"/>.</summary>
    public IReadOnlyList<SupplierAlias> Aliases { get; init; } = [];

    /// <summary>How confident anybody is that this is a distinct supplier.</summary>
    public IdentityConfidence Confidence { get; init; } = IdentityConfidence.NotAssessed;

    /// <summary>Other supplier references this one may be a duplicate of. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// Recorded rather than resolved. Two records that might be the same
    /// company stay two records, each naming the other, until somebody
    /// with the facts decides.
    /// </remarks>
    public IReadOnlyList<string> PossibleDuplicatesOf { get; init; } = [];

    /// <summary>Whether the identity rests on a hard identifier rather than a name.</summary>
    public bool HasHardIdentifier => !string.IsNullOrWhiteSpace(RegistrationNumber);

    /// <summary>Whether anybody has yet established that this is a distinct supplier.</summary>
    public bool IsResolved => Confidence is IdentityConfidence.Confirmed or IdentityConfidence.Probable;

    /// <summary>Every name the supplier answers to, current and former, including the legal name.</summary>
    public IEnumerable<string> AllNames => Aliases.Select(a => a.Name).Prepend(LegalName).Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the supplier is or has been known by <paramref name="name"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    public bool AnswersTo(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return AllNames.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);
    }

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
