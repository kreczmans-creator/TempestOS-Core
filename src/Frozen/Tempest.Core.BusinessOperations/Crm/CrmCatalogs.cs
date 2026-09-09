using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessOperations.Crm;

// WP 18.0C (D-028): split out of the live
// src/Tempest.Core/BusinessOperations/Crm/CrmCatalogs.cs the same day
// OrganisationCatalog/ContactCatalog/OrganisationValidationService stayed
// live. IOrganisationCatalog/IContactCatalog are referenced here only
// through Organisation.cs/CrmCatalogs.cs's live counterparts' own
// interfaces, which is fine — frozen code compiles against nothing.

/// <summary>What has passed between the business and those organisations.</summary>
public interface IInteractionCatalog : IReferenceDataCatalog<Interaction>
{
    /// <summary>Returns the interaction registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<Interaction>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>The interactions with <paramref name="organisationReference"/>, most recent first. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="organisationReference"/> is null, empty, or whitespace.</exception>
    Task<IReadOnlyList<IReferenceRecord<Interaction>>> FindForOrganisationAsync(string organisationReference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every agreed action nobody has done, most overdue first.
    /// </summary>
    /// <remarks>
    /// The list a business actually needs from a CRM. Agreed actions live
    /// inside the interaction that produced them, which is where they
    /// belong and the last place anybody looks.
    /// </remarks>
    Task<IReadOnlyList<IReferenceRecord<Interaction>>> FindOutstandingActionsAsync(DateOnly asAt, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IInteractionCatalog"/> implementation.</summary>
public sealed class InteractionCatalog : ReferenceDataCatalog<Interaction>, IInteractionCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every interaction's own backing document carries.</summary>
    public const string InteractionDocumentKind = "BusinessInteraction";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string InteractionLibraryName = "BusinessInteractions";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>interactionId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessInteractions.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each reference to the <c>interactionId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessInteractions.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="InteractionCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public InteractionCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => InteractionLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => InteractionDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<Interaction>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(Interaction.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<Interaction>>> FindForOrganisationAsync(
        string organisationReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationReference);

        var organisation = organisationReference.Trim();

        var interactions = await FilterAsync(
            record => string.Equals(record.Definition.OrganisationReference, organisation, StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);

        return interactions
            .OrderByDescending(i => i.Definition.OccurredOn)
            .ThenBy(i => i.Definition.Reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<Interaction>>> FindOutstandingActionsAsync(
        DateOnly asAt,
        CancellationToken cancellationToken = default)
    {
        var outstanding = await FilterAsync(
            record => record.Definition.HasOutstandingAction,
            cancellationToken).ConfigureAwait(false);

        return outstanding
            .OrderBy(i => i.Definition.NextActionDue ?? DateOnly.MaxValue)
            .ThenBy(i => i.Definition.Reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(Interaction definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(Interaction definition) => $"Interaction reference '{definition.Reference}'";
}

/// <summary>The diagnostic codes WP04.1's validation reports (the archived, contact/interaction half).</summary>
public static class CrmValidationRules
{
    /// <summary>A contact names an organisation the library does not hold.</summary>
    public const string ContactOrganisationMustResolve = "TEMPEST-BOC-009";

    /// <summary>A contact is marked inactive without a stated reason.</summary>
    public const string InactiveContactHasNoReason = "TEMPEST-BOC-010";

    /// <summary>An interaction names a contact the library does not hold.</summary>
    public const string InteractionContactMustResolve = "TEMPEST-BOC-011";

    /// <summary>An interaction agreed an action with no date by which it is due.</summary>
    public const string AgreedActionHasNoDate = "TEMPEST-BOC-012";

    /// <summary>An agreed action is past its date and not done.</summary>
    public const string AgreedActionIsOverdue = "TEMPEST-BOC-013";

    /// <summary>An interaction is dated in the future.</summary>
    public const string InteractionIsInTheFuture = "TEMPEST-BOC-014";
}

/// <summary>Governance of the contact and interaction libraries.</summary>
public interface ICrmValidationService
{
    /// <summary>Validates a contact against the organisation it names.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="contact"/> is <see langword="null"/>.</exception>
    Task<IValidationResult> ValidateContactAsync(Contact contact, CancellationToken cancellationToken = default);

    /// <summary>Validates an interaction against the organisation and contacts it names.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="interaction"/> is <see langword="null"/>.</exception>
    Task<IValidationResult> ValidateInteractionAsync(Interaction interaction, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ICrmValidationService"/> implementation.</summary>
/// <remarks>
/// Contacts and interactions are validated by one service rather than
/// two, because every meaningful check on either needs the other: a
/// contact is only sound relative to its organisation, and an interaction
/// only relative to the contacts it names.
/// </remarks>
public sealed class CrmValidationService : ICrmValidationService
{
    private readonly IOrganisationCatalog _organisations;
    private readonly IContactCatalog _contacts;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="CrmValidationService"/> class.</summary>
    /// <param name="organisations">The organisation library.</param>
    /// <param name="contacts">The contact library.</param>
    /// <param name="timeProvider">The clock date checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="organisations"/> or <paramref name="contacts"/> is <see langword="null"/>.</exception>
    public CrmValidationService(IOrganisationCatalog organisations, IContactCatalog contacts, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(organisations);
        ArgumentNullException.ThrowIfNull(contacts);

        _organisations = organisations;
        _contacts = contacts;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<IValidationResult> ValidateContactAsync(Contact contact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);

        var errors = new List<IValidationDiagnostic>();
        var warnings = new List<IValidationDiagnostic>();
        var subject = $"Contact '{contact.Reference}'";

        var organisation = await _organisations
            .FindByReferenceAsync(contact.OrganisationReference, cancellationToken)
            .ConfigureAwait(false);

        if (organisation is null)
            warnings.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.ContactOrganisationMustResolve,
                $"{subject} is at '{contact.OrganisationReference}', which the organisation library does not hold."));

        if (contact.IsInactive && string.IsNullOrWhiteSpace(contact.InactiveReason))
            warnings.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.InactiveContactHasNoReason,
                $"{subject} is marked inactive without a stated reason."));

        return new ValidationResult(errors, warnings);
    }

    /// <inheritdoc />
    public async Task<IValidationResult> ValidateInteractionAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var errors = new List<IValidationDiagnostic>();
        var warnings = new List<IValidationDiagnostic>();
        var subject = $"Interaction '{interaction.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        var organisation = await _organisations
            .FindByReferenceAsync(interaction.OrganisationReference, cancellationToken)
            .ConfigureAwait(false);

        if (organisation is null)
            warnings.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.ContactOrganisationMustResolve,
                $"{subject} is with '{interaction.OrganisationReference}', which the organisation library does not hold."));

        foreach (var reference in interaction.ContactReferences)
        {
            var contact = await _contacts.FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false);

            if (contact is null)
                warnings.Add(OperationalValidation.Diagnostic(
                    CrmValidationRules.InteractionContactMustResolve,
                    $"{subject} names contact '{reference}', which the library does not hold."));
        }

        if (interaction.OccurredOn > today)
            errors.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.InteractionIsInTheFuture,
                $"{subject} is dated {interaction.OccurredOn:O}, which has not happened yet."));

        if (interaction.HasOutstandingAction && interaction.NextActionDue is null)
            warnings.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.AgreedActionHasNoDate,
                $"{subject} agreed an action with no date by which it is due."));

        if (interaction.IsActionOverdueAt(today))
            warnings.Add(OperationalValidation.Diagnostic(
                CrmValidationRules.AgreedActionIsOverdue,
                $"{subject} agreed an action due on {interaction.NextActionDue:O} that nobody has recorded as done."));

        return new ValidationResult(errors, warnings);
    }
}
