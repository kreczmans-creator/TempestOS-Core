using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Assets;

/// <summary>What kind of intellectual property an asset is.</summary>
public enum IPType
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Copyright — drawings, models, calculations, reports, source code.</summary>
    Copyright,

    /// <summary>A registered or unregistered design right.</summary>
    Design,

    /// <summary>A patent, or an application for one.</summary>
    Patent,

    /// <summary>A trade mark, registered or otherwise.</summary>
    TradeMark,

    /// <summary>Know-how or a trade secret — unregistered, and protected only by keeping it confidential.</summary>
    KnowHow,

    /// <summary>Database right.</summary>
    Database,

    /// <summary>Software, considered as an asset in its own right.</summary>
    Software,

    /// <summary>Something else, described in the record.</summary>
    Other
}

/// <summary>
/// Where an intellectual property asset came from, and therefore who is
/// likely to own it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The origin is a fact about history; ownership is a legal
/// conclusion.</b> They usually align and sometimes do not — an
/// engineering contract can assign foreground IP to the client, or leave
/// it with the consultant, or divide it by field of use. `P07` records the
/// origin because the organisation knows it, and records ownership
/// separately because it is determined by a contract somebody must read.
/// </para>
/// <para>
/// The background/foreground distinction is the one that matters most in
/// consultancy. Background IP existed before the engagement and is the
/// consultant's stock in trade; foreground IP was created during it and is
/// what the client usually believes they are paying for. Losing track of
/// which is which is how a firm signs away its own methods.
/// </para>
/// </remarks>
public enum IPOrigin
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Existed before the engagement, and was brought to it.</summary>
    Background,

    /// <summary>Created during the engagement.</summary>
    Foreground,

    /// <summary>Supplied by the client.</summary>
    ClientSupplied,

    /// <summary>Supplied by a third party under licence.</summary>
    ThirdPartyLicensed,

    /// <summary>Obtained under an open-source licence.</summary>
    OpenSource,

    /// <summary>Acquired outright from somebody else.</summary>
    Acquired
}

/// <summary>Who an asset's intellectual property belongs to.</summary>
/// <remarks>
/// <see cref="NotDetermined"/> is the default and the honest one.
/// Ownership follows from a contract, an employment relationship or a
/// licence, and TempestOS reads none of those. Holding an asset in the
/// system establishes nothing about owning it.
/// </remarks>
public enum IPOwnership
{
    /// <summary>Nobody has established who owns it.</summary>
    NotDetermined,

    /// <summary>The organisation owns it.</summary>
    Organisation,

    /// <summary>A client owns it.</summary>
    Client,

    /// <summary>A third party owns it, and the organisation uses it under licence.</summary>
    ThirdParty,

    /// <summary>Owned jointly, on terms the record describes.</summary>
    Joint,

    /// <summary>In the public domain.</summary>
    PublicDomain,

    /// <summary>Two sources give different answers.</summary>
    Disputed
}

/// <summary>What the organisation may do with an asset it does not own outright.</summary>
/// <param name="Licensor">Who granted the licence. Required.</param>
/// <param name="LicenceName">What the licence is called — a contract clause, an open-source licence identifier, a supplier's terms. Required.</param>
/// <param name="PermittedUse">What the licence allows, in its own terms. Required.</param>
/// <param name="Restrictions">What it forbids or conditions. Never <see langword="null"/>.</param>
/// <param name="Period">How long it runs for. <see langword="null"/> where the licence is perpetual or the term is not recorded.</param>
/// <param name="IsSublicensable">Whether the organisation may pass the rights on. Recorded from the licence, not inferred.</param>
/// <param name="IsExclusive">Whether the grant is exclusive.</param>
/// <param name="Evidence">The licence document or clause. Never <see langword="null"/>.</param>
public sealed record IPLicence(
    string Licensor,
    string LicenceName,
    string PermittedUse,
    IReadOnlyList<string>? Restrictions = null,
    EffectivePeriod? Period = null,
    bool IsSublicensable = false,
    bool IsExclusive = false,
    IReadOnlyList<BusinessEvidence>? Evidence = null)
{
    /// <summary>Who granted the licence.</summary>
    public string Licensor { get; } = string.IsNullOrWhiteSpace(Licensor)
        ? throw new ArgumentException("A licence must name who granted it.", nameof(Licensor))
        : Licensor.Trim();

    /// <summary>What the licence is called.</summary>
    public string LicenceName { get; } = string.IsNullOrWhiteSpace(LicenceName)
        ? throw new ArgumentException("A licence must be identifiable — a clause, a named licence, a supplier's terms.", nameof(LicenceName))
        : LicenceName.Trim();

    /// <summary>What the licence allows, in its own terms.</summary>
    public string PermittedUse { get; } = string.IsNullOrWhiteSpace(PermittedUse)
        ? throw new ArgumentException("A licence must say what it permits. A licence nobody can state the scope of is not usable.", nameof(PermittedUse))
        : PermittedUse.Trim();

    /// <summary>What the licence forbids or conditions.</summary>
    public IReadOnlyList<string> Restrictions { get; init; } = Restrictions ?? [];

    /// <summary>The licence document or clause.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = Evidence ?? [];

    /// <summary>Whether the licence has run out as at <paramref name="asAt"/>.</summary>
    public bool HasExpiredBy(DateOnly asAt) => Period?.HasExpiredBy(asAt) ?? false;

    /// <summary>Whether anything at all evidences the grant.</summary>
    public bool IsEvidenced => Evidence.Count > 0;
}

/// <summary>
/// One intellectual property asset the organisation holds, uses or
/// created.
/// </summary>
/// <remarks>
/// <para>
/// <b>Presence in TempestOS establishes nothing about ownership.</b> A
/// drawing in the system may belong to the client who commissioned it, to
/// a subcontractor who drew it, or to the organisation. Which of those is
/// true depends on a contract, and <see cref="Ownership"/> defaults to
/// <see cref="IPOwnership.NotDetermined"/> until somebody reads one and
/// records what it says.
/// </para>
/// <para>
/// The register exists to make two questions answerable: what may we use,
/// and on what terms; and what have we created that we should be
/// protecting. Both are unanswerable from a file store.
/// </para>
/// </remarks>
public sealed record IPAsset
{
    /// <summary>The reference the asset is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What the asset is. Required.</summary>
    public required string Name { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>What kind of intellectual property it is.</summary>
    public IPType Type { get; init; } = IPType.Unspecified;

    /// <summary>Where it came from.</summary>
    public IPOrigin Origin { get; init; } = IPOrigin.Unspecified;

    /// <summary>Who owns it, as somebody has determined from a contract.</summary>
    public IPOwnership Ownership { get; init; } = IPOwnership.NotDetermined;

    /// <summary>The owner's name, where ownership is determined and is not the organisation's. <see langword="null"/> otherwise.</summary>
    public string? OwnerName { get; init; }

    /// <summary>What establishes the ownership position. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// Ownership recorded as anything other than
    /// <see cref="IPOwnership.NotDetermined"/> with nothing here is an
    /// assertion, and validation reports it as one.
    /// </remarks>
    public IReadOnlyList<BusinessEvidence> OwnershipEvidence { get; init; } = [];

    /// <summary>The contract that determines ownership, where one does. <see langword="null"/> otherwise.</summary>
    public string? GoverningContractReference { get; init; }

    /// <summary>The terms the organisation uses it under, where it does not own it. <see langword="null"/> where it owns it outright.</summary>
    public IPLicence? Licence { get; init; }

    /// <summary>Restrictions on use beyond any licence — an export control, a client's field-of-use limit. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> UseRestrictions { get; init; } = [];

    /// <summary>Registration details, where the asset is registered. <see langword="null"/> where it is not.</summary>
    public string? RegistrationReference { get; init; }

    /// <summary>When registration must be renewed. <see langword="null"/> where nothing must be.</summary>
    public DateOnly? RegistrationRenewalDue { get; init; }

    /// <summary>When it was created. <see langword="null"/> if not recorded.</summary>
    public DateOnly? CreatedOn { get; init; }

    /// <summary>The TempestOS document or object holding the asset itself. <see langword="null"/> where it is held elsewhere.</summary>
    public Guid? AssetDocumentId { get; init; }

    /// <summary>Anything else about the asset. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether somebody has established who owns it.</summary>
    public bool IsOwnershipDetermined => Ownership != IPOwnership.NotDetermined && Ownership != IPOwnership.Disputed;

    /// <summary>Whether the recorded ownership rests on anything a reader could check.</summary>
    public bool IsOwnershipEvidenced => OwnershipEvidence.Count > 0;

    /// <summary>
    /// Whether ownership is asserted with nothing behind it — the finding
    /// an IP register exists to produce.
    /// </summary>
    public bool IsOwnershipAsserted => IsOwnershipDetermined && !IsOwnershipEvidenced;

    /// <summary>Whether the organisation needs a licence for this asset and does not have one recorded.</summary>
    public bool NeedsLicenceAndHasNone =>
        Ownership is IPOwnership.ThirdParty or IPOwnership.Client && Licence is null;

    /// <summary>Whether the licence the organisation relies on has run out as at <paramref name="asAt"/>.</summary>
    public bool LicenceHasExpiredBy(DateOnly asAt) => Licence?.HasExpiredBy(asAt) ?? false;

    /// <summary>Whether registration renewal is due within <paramref name="withinDays"/> of <paramref name="asAt"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="withinDays"/> is negative.</exception>
    public bool RenewalDueWithin(DateOnly asAt, int withinDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(withinDays);

        return RegistrationRenewalDue is { } due && due <= asAt.AddDays(withinDays);
    }

    /// <summary>Every reference-data revision the asset's ownership position rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        OwnershipEvidence.Select(e => e.Pin)
            .Concat((Licence?.Evidence ?? []).Select(e => e.Pin))
            .Concat(Governance.Evidence.Select(e => e.Pin))
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
