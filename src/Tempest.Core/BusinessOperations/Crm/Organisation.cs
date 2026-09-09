using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.BusinessOperations.Crm;

/// <summary>Where a relationship with an organisation stands.</summary>
/// <remarks>
/// Deliberately about the <em>relationship</em>, not about the
/// organisation. A dormant customer is a fact about the last two years of
/// trading, not a judgement about the company.
/// </remarks>
public enum RelationshipStatus
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Known of, never approached.</summary>
    Identified,

    /// <summary>Approached, no trading yet.</summary>
    Engaged,

    /// <summary>Currently trading.</summary>
    Active,

    /// <summary>Traded before, nothing recent.</summary>
    Dormant,

    /// <summary>Deliberately not traded with, with a reason.</summary>
    Declined,

    /// <summary>No longer in business, or absorbed into another.</summary>
    Ceased
}

/// <summary>How the organisation was first come across.</summary>
public enum LeadOrigin
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Somebody recommended the organisation, or was recommended to it.</summary>
    Referral,

    /// <summary>They came to us.</summary>
    InboundEnquiry,

    /// <summary>We went to them.</summary>
    OutboundApproach,

    /// <summary>Met at an event.</summary>
    Event,

    /// <summary>Found through published sources.</summary>
    Research,

    /// <summary>An existing relationship that grew.</summary>
    ExistingRelationship,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>Where an organisation is, and how to reach it.</summary>
/// <remarks>
/// Deliberately thin. `P04` records enough to identify and contact an
/// organisation; it is not an address-validation service and holds no
/// geocoding, no postal-format rules and no country registry.
/// </remarks>
/// <param name="Line1">The first line. Required.</param>
/// <param name="Town">The town or city. <see langword="null"/> where unrecorded.</param>
/// <param name="Postcode">The postcode. <see langword="null"/> where unrecorded.</param>
/// <param name="CountryCode">An ISO 3166 alpha-2 code, unvalidated against any registry. <see langword="null"/> where unrecorded.</param>
/// <param name="Line2">The second line. <see langword="null"/> where there is none.</param>
/// <param name="Region">The county, state or region. <see langword="null"/> where unrecorded.</param>
public sealed record PostalAddress(
    string Line1,
    string? Town = null,
    string? Postcode = null,
    string? CountryCode = null,
    string? Line2 = null,
    string? Region = null)
{
    /// <summary>The first line.</summary>
    public string Line1 { get; } = string.IsNullOrWhiteSpace(Line1)
        ? throw new ArgumentException("An address must carry at least a first line.", nameof(Line1))
        : Line1.Trim();

    /// <summary>An ISO 3166 alpha-2 code, normalised to upper case.</summary>
    public string? CountryCode { get; } = string.IsNullOrWhiteSpace(CountryCode) ? null : CountryCode.Trim().ToUpperInvariant();

    /// <summary>Whether the address carries enough to post something.</summary>
    public bool IsPostable => !string.IsNullOrWhiteSpace(Postcode) || !string.IsNullOrWhiteSpace(Town);
}

/// <summary>
/// An organisation the business deals with.
/// </summary>
/// <remarks>
/// <para>
/// <b>The record `P07`'s opportunities currently do without.</b>
/// <c>Opportunity.OrganisationName</c> is free text and
/// <c>ExternalOrganisationId</c> is an unresolved hook; `WP04.1` gives it
/// something to point at, so "what else do we know about this customer?"
/// becomes answerable (`ADR-0142`).
/// </para>
/// <para>
/// <b>Not a supplier record.</b> Where an organisation is also somebody
/// the business buys from, <see cref="SupplierRecordId"/> links the `P03`
/// record rather than copying it. One organisation may be a customer and
/// a supplier at once, which is why <see cref="Roles"/> is a list.
/// </para>
/// <para>
/// <b>No personal data beyond business contact details.</b> `P07`'s
/// <c>C3</c> owns data protection, and `P04` holds only what a business
/// needs to trade: a name, a role, a work address, a work number.
/// </para>
/// </remarks>
public sealed record Organisation
{
    /// <summary>The reference the organisation is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>Its legal or trading name. Required.</summary>
    public required string Name { get; init; }

    /// <summary>What the organisation is to this business. Never <see langword="null"/>; empty where nobody said.</summary>
    public IReadOnlyList<PartyKind> Roles { get; init; } = [];

    /// <summary>Where the relationship stands.</summary>
    public RelationshipStatus Status { get; init; } = RelationshipStatus.Identified;

    /// <summary>Why, where the status is <see cref="RelationshipStatus.Declined"/>. <see langword="null"/> otherwise.</summary>
    public string? StatusReason { get; init; }

    /// <summary>Its company registration number. <see langword="null"/> where unrecorded.</summary>
    public string? RegistrationNumber { get; init; }

    /// <summary>Where it is registered. <see langword="null"/> where unrecorded.</summary>
    public string? RegistrationCountry { get; init; }

    /// <summary>Its VAT or equivalent tax registration. <see langword="null"/> where unrecorded.</summary>
    /// <remarks>
    /// Recorded because an invoice needs it. `P04` applies no tax rules
    /// and computes no tax: that is accounting, and the platform does not
    /// do accounting.
    /// </remarks>
    public string? TaxRegistration { get; init; }

    /// <summary>Its main address. <see langword="null"/> where unrecorded.</summary>
    public PostalAddress? Address { get; init; }

    /// <summary>Its website. <see langword="null"/> where unrecorded.</summary>
    public string? Website { get; init; }

    /// <summary>What it does, in the organisation's own words. <see langword="null"/> where nothing was written.</summary>
    public string? Sector { get; init; }

    /// <summary>How it was first come across.</summary>
    public LeadOrigin Origin { get; init; } = LeadOrigin.Unspecified;

    /// <summary>The `P03` supplier record, where this organisation is also a supplier. <see langword="null"/> otherwise.</summary>
    public string? SupplierRecordId { get; init; }

    /// <summary>The organisation this one is part of, by reference. <see langword="null"/> where it stands alone.</summary>
    public string? ParentOrganisationReference { get; init; }

    /// <summary>The currency it is normally invoiced in. <see langword="null"/> where unrecorded.</summary>
    public CurrencyCode? TradingCurrency { get; init; }

    /// <summary>Who owns the relationship, and where it stands operationally.</summary>
    public OperationalFacts Facts { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the organisation may be traded with.</summary>
    public bool IsTradeable => Status is RelationshipStatus.Engaged or RelationshipStatus.Active or RelationshipStatus.Dormant;

    /// <summary>Whether the business buys from this organisation as well as selling to it.</summary>
    public bool IsAlsoASupplier => SupplierRecordId is not null || Roles.Contains(PartyKind.Supplier);

    /// <summary>Whether anything identifies the organisation beyond its name.</summary>
    /// <remarks>
    /// Two companies share a trading name more often than anybody expects,
    /// and a registration number is the only thing that reliably tells
    /// them apart — the same reasoning `ADR-0131` applies to suppliers.
    /// </remarks>
    public bool HasHardIdentifier => !string.IsNullOrWhiteSpace(RegistrationNumber);

    /// <summary>A party reference pointing at this organisation.</summary>
    public PartyReference AsParty(PartyKind kind = PartyKind.Customer) =>
        PartyReference.Organisation(kind, Name, Reference);

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

/// <summary>A person at an organisation.</summary>
/// <remarks>
/// Business contact details only — a name, a role, a work address. `P07`'s
/// `C3` owns data protection and retention; `P04` holds the minimum a
/// business needs to trade and nothing more (`ADR-0142`).
/// </remarks>
public sealed record Contact
{
    /// <summary>The reference the contact is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>The organisation they are at, by reference. Required.</summary>
    public required string OrganisationReference { get; init; }

    /// <summary>Their name. Required.</summary>
    public required string Name { get; init; }

    /// <summary>Their job title. <see langword="null"/> where unrecorded.</summary>
    public string? JobTitle { get; init; }

    /// <summary>Their work e-mail. <see langword="null"/> where unrecorded.</summary>
    public string? EmailAddress { get; init; }

    /// <summary>Their work telephone number. <see langword="null"/> where unrecorded.</summary>
    public string? TelephoneNumber { get; init; }

    /// <summary>What they decide or influence, in the organisation's own words. <see langword="null"/> where nothing was written.</summary>
    public string? Role { get; init; }

    /// <summary>Whether this is the person to go to by default.</summary>
    public bool IsPrimaryContact { get; init; }

    /// <summary>Whether they have left, or the contact is otherwise no longer good.</summary>
    public bool IsInactive { get; init; }

    /// <summary>Why, where <see cref="IsInactive"/>. <see langword="null"/> otherwise.</summary>
    public string? InactiveReason { get; init; }

    /// <summary>Who owns the relationship, and where it stands.</summary>
    public OperationalFacts Facts { get; init; } = new();

    /// <summary>Anything else about them. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether there is any way to reach them.</summary>
    public bool IsReachable =>
        !IsInactive && (!string.IsNullOrWhiteSpace(EmailAddress) || !string.IsNullOrWhiteSpace(TelephoneNumber));

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

// WP 18.0C (D-028): InteractionChannel and Interaction moved to
// src/Frozen/Tempest.Core.BusinessOperations/Crm/Organisation.cs the same
// day the InteractionCatalog they back did.
