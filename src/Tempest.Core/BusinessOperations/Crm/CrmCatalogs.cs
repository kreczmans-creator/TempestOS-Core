using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessOperations.Crm;

/// <summary>A deterministic filter over the organisation library.</summary>
public sealed record OrganisationQuery
{
    /// <summary>Matches any organisation whose reference or name contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches organisations holding any of these roles. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<PartyKind> Roles { get; init; } = [];

    /// <summary>Matches any of these relationship statuses. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<RelationshipStatus> Statuses { get; init; } = [];

    /// <summary>Matches only organisations that may be traded with. <see langword="null"/> to match any.</summary>
    public bool? IsTradeable { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The organisations the business deals with.</summary>
public interface IOrganisationCatalog : IReferenceDataCatalog<Organisation>
{
    /// <summary>Returns the organisation registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<Organisation>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered organisation matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<Organisation>>> SearchAsync(OrganisationQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IOrganisationCatalog"/> implementation.</summary>
public sealed class OrganisationCatalog : ReferenceDataCatalog<Organisation>, IOrganisationCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every organisation's own backing document carries.</summary>
    public const string OrganisationDocumentKind = "BusinessOrganisation";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string OrganisationLibraryName = "BusinessOrganisations";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>organisationId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessOrganisations.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each reference to the <c>organisationId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessOrganisations.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="OrganisationCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public OrganisationCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => OrganisationLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => OrganisationDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<Organisation>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(Organisation.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<Organisation>>> SearchAsync(
        OrganisationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(Organisation definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(Organisation definition) => $"Organisation reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<Organisation> record, OrganisationQuery query)
    {
        var organisation = record.Definition;

        if (query.TextContains is { } text
            && !organisation.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !organisation.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Roles.Count > 0 && !query.Roles.Any(organisation.Roles.Contains))
            return false;

        if (query.Statuses.Count > 0 && !query.Statuses.Contains(organisation.Status))
            return false;

        if (query.IsTradeable is { } tradeable && organisation.IsTradeable != tradeable)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The people at those organisations.</summary>
public interface IContactCatalog : IReferenceDataCatalog<Contact>
{
    /// <summary>Returns the contact registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<Contact>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>The contacts at <paramref name="organisationReference"/>, primary contact first. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="organisationReference"/> is null, empty, or whitespace.</exception>
    Task<IReadOnlyList<IReferenceRecord<Contact>>> FindForOrganisationAsync(string organisationReference, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IContactCatalog"/> implementation.</summary>
public sealed class ContactCatalog : ReferenceDataCatalog<Contact>, IContactCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every contact's own backing document carries.</summary>
    public const string ContactDocumentKind = "BusinessContact";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string ContactLibraryName = "BusinessContacts";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>contactId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessContacts.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each reference to the <c>contactId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessContacts.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="ContactCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public ContactCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => ContactLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => ContactDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<Contact>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(Contact.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<Contact>>> FindForOrganisationAsync(
        string organisationReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationReference);

        var organisation = organisationReference.Trim();

        var contacts = await FilterAsync(
            record => string.Equals(record.Definition.OrganisationReference, organisation, StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);

        return contacts
            .OrderByDescending(c => c.Definition.IsPrimaryContact)
            .ThenBy(c => c.Definition.IsInactive)
            .ThenBy(c => c.Definition.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(Contact definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(Contact definition) => $"Contact reference '{definition.Reference}'";
}

// WP 18.0C (D-028): IInteractionCatalog/InteractionCatalog and the
// interaction/contact half of CrmValidationRules moved to
// src/Frozen/Tempest.Core.BusinessOperations/Crm/CrmCatalogs.cs the same
// day, alongside ICrmValidationService/CrmValidationService.

/// <summary>The diagnostic codes WP04.1's validation reports (the kept, organisation half).</summary>
public static class CrmValidationRules
{
    /// <summary>The organisation states no role, so nothing says what it is to this business.</summary>
    public const string OrganisationHasNoRole = "TEMPEST-BOC-001";

    /// <summary>The organisation is identified only by its name.</summary>
    public const string OrganisationHasNoHardIdentifier = "TEMPEST-BOC-002";

    /// <summary>An organisation declined for a reason nobody stated.</summary>
    public const string DeclinedWithoutReason = "TEMPEST-BOC-003";

    /// <summary>The organisation names a `P03` supplier record the supplier database does not hold.</summary>
    public const string SupplierRecordMustResolve = "TEMPEST-BOC-004";

    /// <summary>The organisation names a parent the library does not hold.</summary>
    public const string ParentMustResolve = "TEMPEST-BOC-005";

    /// <summary>The organisation is its own parent, directly or through a chain.</summary>
    public const string OrganisationHierarchyCycle = "TEMPEST-BOC-006";

    /// <summary>A tradeable organisation has no contact anybody can reach.</summary>
    public const string NoReachableContact = "TEMPEST-BOC-007";

    /// <summary>Two contacts at one organisation are both marked primary.</summary>
    public const string MultiplePrimaryContacts = "TEMPEST-BOC-008";
}

/// <summary>Governance of the organisation library.</summary>
public interface IOrganisationValidationService : IReferenceValidationService<Organisation>
{
}

/// <summary>The concrete <see cref="IOrganisationValidationService"/> implementation.</summary>
public sealed class OrganisationValidationService : ReferenceValidationService<Organisation>, IOrganisationValidationService
{
    private readonly IOrganisationCatalog _organisations;
    private readonly IContactCatalog? _contacts;
    private readonly ISupplierCatalog? _suppliers;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="OrganisationValidationService"/> class.</summary>
    /// <param name="catalog">The organisation library whose records this service validates.</param>
    /// <param name="contacts">The contact library, for confirming somebody is reachable. Optional.</param>
    /// <param name="suppliers">The `P03` supplier database, for confirming a linked supplier exists. Optional.</param>
    /// <param name="timeProvider">The clock overdue checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public OrganisationValidationService(
        IOrganisationCatalog catalog,
        IContactCatalog? contacts = null,
        ISupplierCatalog? suppliers = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _organisations = catalog;
        _contacts = contacts;
        _suppliers = suppliers;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        Organisation definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Organisation '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.Roles.Count == 0)
            warnings.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.OrganisationHasNoRole,
                $"{subject} states no role, so nothing says what it is to this business."));

        if (!definition.HasHardIdentifier)
            warnings.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.OrganisationHasNoHardIdentifier,
                $"{subject} is identified only by its name. Two companies share a trading name more often than anybody "
                + "expects, and a registration number is the only thing that reliably tells them apart."));

        if (definition.Status == RelationshipStatus.Declined && string.IsNullOrWhiteSpace(definition.StatusReason))
            errors.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.DeclinedWithoutReason,
                $"{subject} is recorded as declined without a stated reason. Somebody will later need to know whether "
                + "the decision still stands."));

        if (string.Equals(definition.ParentOrganisationReference, definition.Reference, StringComparison.OrdinalIgnoreCase))
            errors.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.OrganisationHierarchyCycle,
                $"{subject} names itself as its own parent."));

        OperationalValidation.Evaluate(definition.Facts, subject, today, errors, warnings, requireDueDate: false);

        await EvaluateLinksAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private async Task EvaluateLinksAsync(
        Organisation definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_suppliers is not null && definition.SupplierRecordId is { } supplierId)
        {
            var supplier = await _suppliers.FindAsync(supplierId, cancellationToken).ConfigureAwait(false);

            if (supplier is null)
                warnings.Add(OperationalValidation.Diagnostic(
                    CrmValidationRules.SupplierRecordMustResolve,
                    $"{subject} links supplier record '{supplierId}', which the supplier database does not hold."));
        }

        if (definition.ParentOrganisationReference is { } parent
            && !string.Equals(parent, definition.Reference, StringComparison.OrdinalIgnoreCase))
        {
            var found = await _organisations.FindByReferenceAsync(parent, cancellationToken).ConfigureAwait(false);

            if (found is null)
                warnings.Add(OperationalValidation.Diagnostic(
                    CrmValidationRules.ParentMustResolve,
                    $"{subject} is part of '{parent}', which the library does not hold."));
        }

        if (_contacts is null || !definition.IsTradeable)
            return;

        var contacts = await _contacts.FindForOrganisationAsync(definition.Reference, cancellationToken).ConfigureAwait(false);

        if (!contacts.Any(c => c.Definition.IsReachable))
            warnings.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.NoReachableContact,
                $"{subject} may be traded with and has no contact anybody can reach."));

        if (contacts.Count(c => c.Definition.IsPrimaryContact && !c.Definition.IsInactive) > 1)
            warnings.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.MultiplePrimaryContacts,
                $"{subject} has more than one active contact marked primary."));
    }
}
