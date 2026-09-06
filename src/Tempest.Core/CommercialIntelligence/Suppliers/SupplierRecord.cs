using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Suppliers;

/// <summary>
/// How well established a supplier's claim to a capability is.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three different facts, and a commercial library that conflates them
/// will recommend a supplier for work they have never done.</b> A
/// supplier's website says they do five-axis machining. That is
/// <see cref="Offered"/>. Somebody visited and saw the machine, or read
/// the certificate. That is <see cref="Verified"/>. They have actually
/// delivered five-axis work to this organisation, on time and to
/// drawing. That is <see cref="Proven"/>.
/// </para>
/// <para>
/// The presence of a process in `P01`'s Manufacturing Process Library
/// says nothing about any of the three. `A7` describes what CNC milling
/// is; it does not confer the ability to do it on anybody.
/// </para>
/// </remarks>
public enum CapabilityAssurance
{
    /// <summary>Nobody has said anything either way.</summary>
    NotAssessed,

    /// <summary>The supplier says they can. Their claim, recorded as theirs.</summary>
    Offered,

    /// <summary>Somebody checked — an audit, a visit, a certificate, a sample.</summary>
    Verified,

    /// <summary>They have actually supplied this to the organisation.</summary>
    Proven,

    /// <summary>They were asked and said they cannot.</summary>
    Declined,

    /// <summary>They claimed it and it turned out not to hold.</summary>
    Disproven
}

/// <summary>Reasoning over <see cref="CapabilityAssurance"/>.</summary>
public static class CapabilityAssurances
{
    /// <summary>Every level, strongest first.</summary>
    public static IReadOnlyList<CapabilityAssurance> StrongestFirst { get; } =
    [
        CapabilityAssurance.Proven, CapabilityAssurance.Verified, CapabilityAssurance.Offered,
        CapabilityAssurance.NotAssessed, CapabilityAssurance.Declined, CapabilityAssurance.Disproven,
    ];

    /// <summary>Whether the supplier can be considered for work needing this capability.</summary>
    /// <remarks>
    /// <see cref="CapabilityAssurance.Offered"/> counts: a supplier's own
    /// claim is a reason to ask them, and excluding unverified suppliers
    /// from every comparison would leave the organisation only ever using
    /// firms it already uses. What it is not is a reason to award work
    /// without asking further.
    /// </remarks>
    public static bool IsConsiderable(CapabilityAssurance assurance) =>
        assurance is CapabilityAssurance.Proven or CapabilityAssurance.Verified or CapabilityAssurance.Offered;

    /// <summary>Whether somebody other than the supplier has established the capability.</summary>
    public static bool IsIndependentlyEstablished(CapabilityAssurance assurance) =>
        assurance is CapabilityAssurance.Proven or CapabilityAssurance.Verified;

    /// <summary>How strong the claim is, for ordering. Higher is stronger; refusals sort below unassessed.</summary>
    public static int Strength(CapabilityAssurance assurance) => assurance switch
    {
        CapabilityAssurance.Proven => 4,
        CapabilityAssurance.Verified => 3,
        CapabilityAssurance.Offered => 2,
        CapabilityAssurance.NotAssessed => 1,
        CapabilityAssurance.Declined => 0,
        CapabilityAssurance.Disproven => -1,
        _ => 1,
    };
}

/// <summary>
/// Something a supplier can do, and how well that is established.
/// </summary>
/// <remarks>
/// References `P01` by record Id rather than describing the process or
/// material itself. What CNC milling is belongs to the Manufacturing
/// Process Library; whether this supplier does it belongs here.
/// </remarks>
/// <param name="Reference">The capability's own identifier within the supplier record. Required.</param>
/// <param name="Description">What the supplier can do, in plain terms. Required.</param>
/// <param name="Assurance">How well established the claim is.</param>
/// <param name="ProcessRecordId">The `A7` process this capability is for. <see langword="null"/> where it is not tied to a catalogued process.</param>
/// <param name="MaterialRecordIds">The `A1` materials they can work in. Never <see langword="null"/>; empty where unrestricted or unrecorded.</param>
/// <param name="Limits">What they cannot do within it — size, tolerance, batch. Never <see langword="null"/>.</param>
/// <param name="AssessedOn">When the assurance level was last established. <see langword="null"/> where nobody has.</param>
/// <param name="Evidence">What establishes it. Never <see langword="null"/>.</param>
public sealed record SupplierCapability(
    string Reference,
    string Description,
    CapabilityAssurance Assurance = CapabilityAssurance.NotAssessed,
    string? ProcessRecordId = null,
    IReadOnlyList<string>? MaterialRecordIds = null,
    IReadOnlyList<string>? Limits = null,
    DateOnly? AssessedOn = null,
    IReadOnlyList<BusinessEvidence>? Evidence = null)
{
    /// <summary>The capability's own identifier within the supplier record.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A supplier capability must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What the supplier can do.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A supplier capability must say what the supplier can do.", nameof(Description))
        : Description.Trim();

    /// <summary>The `A1` materials they can work in.</summary>
    public IReadOnlyList<string> MaterialRecordIds { get; init; } = MaterialRecordIds ?? [];

    /// <summary>What they cannot do within it.</summary>
    public IReadOnlyList<string> Limits { get; init; } = Limits ?? [];

    /// <summary>What establishes the assurance level.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = Evidence ?? [];

    /// <summary>
    /// Whether the capability claims to be independently established and
    /// shows nothing for it.
    /// </summary>
    /// <remarks>
    /// The finding a supplier database exists to produce. A capability
    /// marked Verified with no certificate, audit note or sample behind
    /// it is somebody's recollection wearing a stronger label.
    /// </remarks>
    public bool IsUnevidenced => CapabilityAssurances.IsIndependentlyEstablished(Assurance) && Evidence.Count == 0;

    /// <summary>Whether the capability covers <paramref name="materialRecordId"/>.</summary>
    /// <remarks>
    /// An empty material list means unrestricted <i>or</i> unrecorded, and
    /// this returns <see langword="true"/> for it. The two are separated
    /// by validation, which reports an empty list on a process capability
    /// as a gap, rather than by silently excluding the supplier here.
    /// </remarks>
    public bool CoversMaterial(string materialRecordId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialRecordId);

        return MaterialRecordIds.Count == 0
               || MaterialRecordIds.Contains(materialRecordId, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// A certification or accreditation a supplier holds.
/// </summary>
/// <remarks>
/// Recorded as the supplier's claim plus whatever evidences it. TempestOS
/// does not verify certificates, and a recorded certification is never a
/// statement that the certificate is genuine or current with its issuing
/// body — only that the organisation holds something that says so.
/// </remarks>
/// <param name="Reference">The certification's own identifier within the supplier record. Required.</param>
/// <param name="Standard">The standard certified against, resolved against `A2` where it is a registered standard. Required.</param>
/// <param name="CertificateNumber">The certificate's own number. <see langword="null"/> where not recorded — itself worth reporting.</param>
/// <param name="Issuer">The certification body. <see langword="null"/> where not recorded.</param>
/// <param name="Validity">When the certificate runs from and to. <see langword="null"/> where not recorded.</param>
/// <param name="Scope">What the certificate actually covers, in its own words. <see langword="null"/> where not recorded.</param>
/// <param name="Evidence">The certificate itself. Never <see langword="null"/>.</param>
public sealed record SupplierCertification(
    string Reference,
    StandardReference Standard,
    string? CertificateNumber = null,
    string? Issuer = null,
    EffectivePeriod? Validity = null,
    string? Scope = null,
    IReadOnlyList<BusinessEvidence>? Evidence = null)
{
    /// <summary>The certification's own identifier within the supplier record.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A supplier certification must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>The standard certified against.</summary>
    public StandardReference Standard { get; } = Standard ?? throw new ArgumentNullException(nameof(Standard));

    /// <summary>The certificate itself.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = Evidence ?? [];

    /// <summary>Whether the certificate has run out as at <paramref name="asAt"/>.</summary>
    public bool HasExpiredBy(DateOnly asAt) => Validity?.HasExpiredBy(asAt) ?? false;

    /// <summary>Whether the certificate is recorded as current on <paramref name="asAt"/>.</summary>
    /// <remarks>
    /// A certificate with no recorded validity returns
    /// <see langword="false"/>: an undated certificate cannot be shown to
    /// be current, and treating it as current would let a lapsed one
    /// qualify a supplier.
    /// </remarks>
    public bool IsCurrentOn(DateOnly asAt) => Validity?.Contains(asAt) ?? false;

    /// <summary>Whether anything at all evidences the certification.</summary>
    public bool IsEvidenced => Evidence.Count > 0;
}

/// <summary>
/// One of a supplier's places of business.
/// </summary>
/// <remarks>
/// Sites are modelled because capability and lead time are properties of
/// a site rather than of a company: a supplier with a factory in
/// Birmingham and a sales office in Aberdeen does not offer the same lead
/// time from both.
/// </remarks>
/// <param name="Reference">The site's own identifier within the supplier record. Required.</param>
/// <param name="Name">What the site is called. Required.</param>
/// <param name="Geography">Where it is.</param>
/// <param name="IsManufacturing">Whether work is actually done here, as distinct from being sold from here.</param>
/// <param name="CapabilityReferences">The supplier capabilities this site provides. Never <see langword="null"/>; empty means unrecorded.</param>
public sealed record SupplierSite(
    string Reference,
    string Name,
    GeographicScope? Geography = null,
    bool IsManufacturing = false,
    IReadOnlyList<string>? CapabilityReferences = null)
{
    /// <summary>The site's own identifier within the supplier record.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A supplier site must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What the site is called.</summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("A supplier site must have a name.", nameof(Name))
        : Name.Trim();

    /// <summary>Where it is.</summary>
    public GeographicScope Geography { get; init; } = Geography ?? GeographicScope.Unstated;

    /// <summary>The supplier capabilities this site provides.</summary>
    public IReadOnlyList<string> CapabilityReferences { get; init; } = CapabilityReferences ?? [];
}

/// <summary>
/// A supplier, as the organisation knows them.
/// </summary>
/// <remarks>
/// <para>
/// A governed reference-data record on the shared lifecycle
/// (`ADR-0131`), so a supplier record is authored, sourced, checked,
/// released, revisioned and superseded exactly as a material is —
/// which is what lets a two-year-old estimate still resolve the supplier
/// it was priced against.
/// </para>
/// <para>
/// <b>The record describes; it does not qualify.</b> Nothing here
/// approves a supplier: that is an act of commercial authority under
/// `P07`, and a supplier record marked Active and fully certified is
/// still a supplier nobody has signed off.
/// </para>
/// </remarks>
public sealed record SupplierRecord
{
    /// <summary>Who the supplier is. Required.</summary>
    public required SupplierIdentity Identity { get; init; }

    /// <summary>Where the supplier stands with the organisation.</summary>
    public SupplierStatus Status { get; init; } = SupplierStatus.Prospective;

    /// <summary>Why, where the status needs one — a bar, a supersession, a decision to stop using them. <see langword="null"/> otherwise.</summary>
    public string? StatusReason { get; init; }

    /// <summary>The supplier record that replaces this one. <see langword="null"/> unless <see cref="Status"/> is <see cref="SupplierStatus.Superseded"/>.</summary>
    public string? SupersededByReference { get; init; }

    /// <summary>What the supplier can do. Never <see langword="null"/>.</summary>
    public IReadOnlyList<SupplierCapability> Capabilities { get; init; } = [];

    /// <summary>What they are certified for. Never <see langword="null"/>.</summary>
    public IReadOnlyList<SupplierCertification> Certifications { get; init; } = [];

    /// <summary>Their places of business. Never <see langword="null"/>.</summary>
    public IReadOnlyList<SupplierSite> Sites { get; init; } = [];

    /// <summary>How the organisation categorises them — "machining subcontractor", "material stockist". <see langword="null"/> where uncategorised.</summary>
    public string? Category { get; init; }

    /// <summary>The currency they invoice in. <see langword="null"/> where not recorded.</summary>
    public CurrencyCode? TradingCurrency { get; init; }

    /// <summary>Their payment terms, in the supplier's own words. <see langword="null"/> where not recorded.</summary>
    /// <remarks>
    /// Recorded as commercial context, not as a governed term. Terms that
    /// bind either party live in a `P07` contract, and this is what the
    /// supplier says they expect.
    /// </remarks>
    public string? StatedPaymentTerms { get; init; }

    /// <summary>The smallest order they will accept, where they state one. <see langword="null"/> otherwise.</summary>
    public Money? MinimumOrderValue { get; init; }

    /// <summary>The `P07` contract governing the relationship, where one exists. <see langword="null"/> otherwise.</summary>
    /// <remarks>
    /// A reference, not a copy. The contract itself, its terms and its
    /// governance belong to `P07`'s contract library; `P03` records only
    /// that one exists and which it is.
    /// </remarks>
    public string? GoverningContractReference { get; init; }

    /// <summary>Where the supplier information came from.</summary>
    public CommercialSource Source { get; init; } = CommercialSource.Unrecorded;

    /// <summary>Anything else about the supplier. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The reference the supplier is known by.</summary>
    public string Reference => Identity.Reference;

    /// <summary>Whether the supplier may be considered for new work.</summary>
    public bool IsConsiderable => SupplierStatuses.IsConsiderable(Status);

    /// <summary>Capabilities claiming independent establishment with nothing behind them.</summary>
    public IReadOnlyList<SupplierCapability> UnevidencedCapabilities =>
        Capabilities.Where(c => c.IsUnevidenced).ToList();

    /// <summary>Certifications that have run out as at <paramref name="asAt"/>.</summary>
    public IReadOnlyList<SupplierCertification> ExpiredCertifications(DateOnly asAt) =>
        Certifications.Where(c => c.HasExpiredBy(asAt)).ToList();

    /// <summary>Returns the capability for <paramref name="processRecordId"/>, or <see langword="null"/> where the supplier records none.</summary>
    /// <remarks>
    /// Where several capabilities name the same process, the
    /// best-established one is returned: a supplier who has proven a
    /// process on one line and merely offers it on another has proven it.
    /// </remarks>
    public SupplierCapability? FindCapabilityForProcess(string processRecordId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processRecordId);

        return Capabilities
            .Where(c => string.Equals(c.ProcessRecordId, processRecordId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => CapabilityAssurances.Strength(c.Assurance))
            .FirstOrDefault();
    }

    /// <summary>Whether the supplier holds a current certification against <paramref name="designation"/> on <paramref name="asAt"/>.</summary>
    public bool HoldsCurrentCertification(string designation, DateOnly asAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(designation);

        return Certifications.Any(c =>
            string.Equals(c.Standard.Designation, designation, StringComparison.OrdinalIgnoreCase)
            && c.IsCurrentOn(asAt));
    }

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => Identity.ReferenceKey;
}
