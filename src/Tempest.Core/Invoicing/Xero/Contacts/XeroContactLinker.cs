using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tempest.Core.Audit;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Persistence;
using Tempest.Core.Secrets;

namespace Tempest.Core.Invoicing.Xero.Contacts;

/// <summary>How <see cref="XeroContactLinker"/> behaves where the Product Owner has a choice.</summary>
public sealed record XeroContactLinkerOptions
{
    /// <summary>
    /// Q7 (build default): when the Product Owner links an existing Xero
    /// contact whose <c>ContactNumber</c> is empty, TempestOS writes its
    /// customer code there — an identifier, never billing details — and
    /// nothing else. A <c>ContactNumber</c> already set is never changed.
    /// <see langword="false"/> makes linking write nothing to Xero at all.
    /// </summary>
    public bool WriteContactNumberWhenEmpty { get; init; } = true;
}

/// <summary>
/// What a push handler (X3 quotes, X4 invoices, X5 purchase orders and
/// bills) needs about a document's customer or supplier: the linked
/// <c>ContactID</c>, or why the document is
/// <see cref="XeroPushOutcome.Blocked"/>. Every document TempestOS writes
/// names its contact by <c>ContactID</c> only — never by name (M21).
/// </summary>
/// <param name="ContactId">The linked contact's <c>ContactID</c>; <see langword="null"/> when not linked.</param>
/// <param name="BlockedReason">Why there is no usable link, for the Blocked badge; <see langword="null"/> when linked.</param>
/// <param name="Link">The link itself, when one exists.</param>
public sealed record XeroContactResolution(string? ContactId, string? BlockedReason, XeroLink? Link)
{
    /// <summary>Whether the organisation is linked and the document may be pushed.</summary>
    public bool IsLinked => ContactId is not null;

    /// <summary>The contact reference a Xero document carries: <c>ContactID</c> only.</summary>
    /// <exception cref="InvalidOperationException">The organisation is not linked (<see cref="IsLinked"/> is false).</exception>
    public XeroWireContactRef ToContactRef() =>
        ContactId is { } id ? new XeroWireContactRef(id) : throw new InvalidOperationException(BlockedReason ?? "Not linked to a Xero contact.");
}

/// <summary>
/// Links TempestOS customers and suppliers to Xero contacts by
/// <c>ContactID</c> (`v0.24.0` X2, design §5; fixes M21). Links are kept
/// per Xero organisation in <see cref="IXeroLinkStore"/> (kind
/// <see cref="XeroDocumentKind.Contact"/>, key
/// <see cref="Organisation.ReferenceKey"/>), so the Demo Company's links
/// are never used against the live organisation (D7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Never by name alone.</b> Candidates are only offered; a link is made
/// by the Product Owner's confirmation (<see cref="LinkExistingAsync"/>) or
/// an explicit create (<see cref="CreateAsync"/>). A document for an
/// unlinked organisation is Blocked (<see cref="ResolveForPushAsync"/>),
/// never sent with a contact name for Xero to match or invent.
/// </para>
/// <para>
/// <b>Never two contacts.</b> A create is issued only when the organisation
/// has no link; it first looks Xero up by <c>ContactNumber</c> (the
/// customer code), carries an <c>Idempotency-Key</c> that is reused only to
/// retry an attempt whose answer was uncertain (a fresh one after a definite
/// answer, so a refusal fixed in Xero is not replayed), and after a lost
/// or failed response looks again before reporting — a contact Xero made
/// is found and linked (<c>"reconciled"</c>), never created twice.
/// </para>
/// <para>
/// <b>Read, never pushed.</b> Billing address, VAT number and payment terms
/// are Xero's (D6): <see cref="ReadDetailsAsync"/> reads them. The only
/// writes to a contact are its create and, under Q7, filling an empty
/// <c>ContactNumber</c> with the customer code.
/// </para>
/// <para>
/// Every write goes through the Xero <see cref="HttpClient"/>'s
/// <see cref="XeroWriteSafetyHandler"/> (D7: the live organisation is
/// refused unless allowed). Never throws for anything Xero or the network
/// did (`ADR-0151`); a store defect still throws.
/// </para>
/// </remarks>
public sealed class XeroContactLinker : IXeroContactLinker
{
    /// <summary><see cref="XeroLink.LinkedBy"/> for a contact TempestOS created.</summary>
    public const string LinkedByCreated = "created";

    /// <summary><see cref="XeroLink.LinkedBy"/> for an existing contact the Product Owner confirmed.</summary>
    public const string LinkedByLinked = "linked";

    /// <summary><see cref="XeroLink.LinkedBy"/> for a contact found by its <c>ContactNumber</c> instead of created (a lost response, or a contact made earlier).</summary>
    public const string LinkedByReconciled = "reconciled";

    /// <summary>Audit action (§6.7): TempestOS created the Xero contact and linked it.</summary>
    public const string AuditLinkCreated = "xero.link.created";

    /// <summary>Audit action: the Product Owner linked an existing Xero contact.</summary>
    public const string AuditLinkLinked = "xero.link.linked";

    /// <summary>Audit action: a contact was found by its <c>ContactNumber</c> and linked instead of created.</summary>
    public const string AuditLinkReconciled = "xero.link.reconciled";

    /// <summary>Audit action: the Product Owner removed a contact link.</summary>
    public const string AuditLinkUnlinked = "xero.link.unlinked";

    /// <summary>The <see cref="IPersistenceStore"/> collection the linker keeps its write attempts in (one small record per create or <c>ContactNumber</c> write), so a retry after an uncertain answer reuses its <c>Idempotency-Key</c> and a retry after a definite one does not.</summary>
    public const string AttemptsCollection = "Xero.ContactWrites";

    /// <summary>The <see cref="ISecretStore"/> key the Xero OAuth authoriser keeps the connected organisation's tenant id under — read here, never written, so a link lookup needs no network.</summary>
    public const string TenantIdSecretKey = "Invoicing:Xero:TenantId";

    private readonly XeroAccountingApi _api;
    private readonly IXeroLinkStore _links;
    private readonly IOrganisationCatalog _organisations;
    private readonly ISecretStore _secretStore;
    private readonly IAuditRecorder? _audit;
    private readonly TimeProvider _time;
    private readonly XeroContactLinkerOptions _options;
    private readonly ILogger? _logger;
    private readonly IPersistenceStore? _attemptStore;
    private readonly Dictionary<string, string> _attemptsInMemory = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    /// <summary>Initialises a new instance of the <see cref="XeroContactLinker"/> class.</summary>
    /// <param name="api">The typed Xero client (its <see cref="HttpClient"/> holds the safety handler).</param>
    /// <param name="links">The link store (B2).</param>
    /// <param name="organisations">TempestOS's customers and suppliers.</param>
    /// <param name="secretStore">Where the connected Xero organisation's tenant id is kept (by the OAuth authoriser).</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="timeProvider">The clock links are stamped with; <see langword="null"/> for the system clock.</param>
    /// <param name="options">Behaviour choices (Q7); <see langword="null"/> for the build defaults.</param>
    /// <param name="logger">Diagnostics; <see langword="null"/> for none.</param>
    /// <param name="attemptStore">Where write attempts are kept (<see cref="AttemptsCollection"/>), so they survive a restart; <see langword="null"/> keeps them in memory only.</param>
    public XeroContactLinker(
        XeroAccountingApi api,
        IXeroLinkStore links,
        IOrganisationCatalog organisations,
        ISecretStore secretStore,
        IAuditRecorder? audit = null,
        TimeProvider? timeProvider = null,
        XeroContactLinkerOptions? options = null,
        ILogger? logger = null,
        IPersistenceStore? attemptStore = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(organisations);
        ArgumentNullException.ThrowIfNull(secretStore);

        _api = api;
        _links = links;
        _organisations = organisations;
        _secretStore = secretStore;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
        _options = options ?? new XeroContactLinkerOptions();
        _logger = logger;
        _attemptStore = attemptStore;
    }

    /// <summary>The link-store reference for <paramref name="organisationReference"/>: kind <see cref="XeroDocumentKind.Contact"/>, key <see cref="Organisation.ReferenceKeyFor"/>.</summary>
    /// <param name="organisationReference">The organisation's reference.</param>
    public static XeroDocumentRef DocumentFor(string organisationReference) =>
        new(XeroDocumentKind.Contact, Organisation.ReferenceKeyFor(organisationReference));

    /// <summary>The connected Xero organisation's tenant id, from the secret store — no network; <see langword="null"/> when none is connected.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<string?> ReadTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = await _secretStore.GetAsync(TenantIdSecretKey, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
    }

    /// <inheritdoc />
    public async Task<XeroLink?> FindLinkAsync(string organisationReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationReference);

        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        return tenantId is null ? null : await FindLinkAsync(tenantId, organisationReference, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The link for <paramref name="organisationReference"/> in the Xero organisation <paramref name="tenantId"/>, or <see langword="null"/> — no network call.</summary>
    /// <param name="tenantId">The Xero organisation.</param>
    /// <param name="organisationReference">The organisation's reference.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public Task<XeroLink?> FindLinkAsync(string tenantId, string organisationReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationReference);

        return _links.FindAsync(tenantId, DocumentFor(organisationReference), cancellationToken);
    }

    /// <summary>
    /// For a push handler (X3–X5): the <c>ContactID</c> a document for
    /// <paramref name="organisationReference"/> must carry in
    /// <paramref name="tenantId"/>, or the reason it is Blocked (not
    /// linked, or linked by a newer TempestOS). No network call; never
    /// links or creates anything.
    /// </summary>
    /// <param name="tenantId">The Xero organisation being pushed to.</param>
    /// <param name="organisationReference">The document's customer or supplier; <see langword="null"/> or blank when the document names none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<XeroContactResolution> ResolveForPushAsync(string tenantId, string? organisationReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (string.IsNullOrWhiteSpace(organisationReference))
            return new XeroContactResolution(null, "The document names no customer or supplier, so it has no Xero contact.", null);

        var link = await FindLinkAsync(tenantId, organisationReference, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return new XeroContactResolution(
                null,
                $"'{organisationReference.Trim()}' is not linked to a Xero contact yet; link or create it under Customers & suppliers.",
                null);
        }

        if (PersistenceXeroLinkStore.IsFromNewerVersion(link) || string.IsNullOrWhiteSpace(link.XeroId))
        {
            return new XeroContactResolution(
                null,
                $"The Xero contact link for '{organisationReference.Trim()}' was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not use it.",
                link);
        }

        if (link.LastKnownXeroStatus is { } status && !string.Equals(status, XeroContactMatcher.ActiveStatus, StringComparison.OrdinalIgnoreCase))
        {
            return new XeroContactResolution(
                null,
                $"The Xero contact linked to '{organisationReference.Trim()}' was {status.ToLowerInvariant()} in Xero when last read, and Xero refuses documents for it; restore it in Xero (then refresh its details), or unlink it and link or create another.",
                link);
        }

        return new XeroContactResolution(link.XeroId, null, link);
    }

    /// <inheritdoc />
    public async Task<ConnectorResult<IReadOnlyList<XeroContactCandidate>>> FindCandidatesAsync(string organisationReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationReference);

        var organisation = await FindOrganisationAsync(organisationReference, cancellationToken).ConfigureAwait(false);
        if (organisation is null)
            return ConnectorResult<IReadOnlyList<XeroContactCandidate>>.Rejected(NoSuchOrganisation(organisationReference));

        var found = new List<XeroWireContact>();

        // 1. VAT number — the strongest identifier. Xero's where= is an exact
        // comparison, so the number is asked for as recorded and, when that
        // differs, in its plain form ("GB 123 4567 89" → "GB123456789").
        foreach (var vat in VatSpellings(organisation.TaxRegistration))
        {
            var byVat = await _api.FindContactsByTaxNumberAsync(vat, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (byVat.Outcome != ConnectorOutcome.Ok)
                return Fail<IReadOnlyList<XeroWireContact>, IReadOnlyList<XeroContactCandidate>>(byVat);
            found.AddRange(byVat.Value!);
        }

        // 2. ContactNumber = the customer code (a contact TempestOS created
        // or numbered before).
        if (XeroContactMatcher.ContactNumberFor(organisation) is { } contactNumber)
        {
            var byNumber = await _api.FindContactsByContactNumberAsync(contactNumber, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (byNumber.Outcome != ConnectorOutcome.Ok)
                return Fail<IReadOnlyList<XeroWireContact>, IReadOnlyList<XeroContactCandidate>>(byNumber);
            found.AddRange(byNumber.Value!);
        }

        // 3. Name: Xero's searchTerm (a part of the name), widened to the
        // name's most distinctive word only when no alike name came back.
        var byName = await _api.SearchContactsAsync(XeroContactMatcher.SearchTermFor(organisation.Name), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (byName.Outcome != ConnectorOutcome.Ok)
            return Fail<IReadOnlyList<XeroWireContact>, IReadOnlyList<XeroContactCandidate>>(byName);
        found.AddRange(byName.Value!);

        if (!byName.Value!.Any(c => XeroContactMatcher.SameName(organisation.Name, c.Name) || XeroContactMatcher.SimilarName(organisation.Name, c.Name))
            && XeroContactMatcher.WiderSearchTermFor(organisation.Name) is { } wider)
        {
            var byWord = await _api.SearchContactsAsync(wider, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (byWord.Outcome != ConnectorOutcome.Ok)
                return Fail<IReadOnlyList<XeroWireContact>, IReadOnlyList<XeroContactCandidate>>(byWord);
            found.AddRange(byWord.Value!);
        }

        return ConnectorResult<IReadOnlyList<XeroContactCandidate>>.Ok(XeroContactMatcher.Rank(organisation, found));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Idempotent: linking an organisation to the contact it is already
    /// linked to answers that link. Refused when the organisation is linked
    /// to another contact (unlink first), when the contact is not
    /// <c>ACTIVE</c>, or when another organisation is already linked to it.
    /// Under Q7 (<see cref="XeroContactLinkerOptions.WriteContactNumberWhenEmpty"/>)
    /// an empty <c>ContactNumber</c> is filled with the customer code — unless
    /// another Xero contact already carries that number — and a failure to
    /// write it never undoes the link.
    /// </remarks>
    public async Task<ConnectorResult<XeroLink>> LinkExistingAsync(string organisationReference, string contactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);

        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return ConnectorResult<XeroLink>.Reauthorise(NoTenant);

        var organisation = await FindOrganisationAsync(organisationReference, cancellationToken).ConfigureAwait(false);
        if (organisation is null)
            return ConnectorResult<XeroLink>.Rejected(NoSuchOrganisation(organisationReference));

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = DocumentFor(organisation.Reference);
            var existing = await _links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return string.Equals(existing.XeroId, contactId.Trim(), StringComparison.OrdinalIgnoreCase) && !PersistenceXeroLinkStore.IsFromNewerVersion(existing)
                    ? ConnectorResult<XeroLink>.Ok(existing)
                    : ConnectorResult<XeroLink>.Rejected(AlreadyLinked(organisation, existing));
            }

            var read = await _api.GetContactAsync(contactId, cancellationToken).ConfigureAwait(false);
            if (read.Outcome != ConnectorOutcome.Ok)
            {
                return read.NotFound
                    ? ConnectorResult<XeroLink>.Rejected($"Xero has no contact {contactId.Trim()} (it may have been deleted in Xero).")
                    : Fail<XeroWireContact, XeroLink>(read);
            }

            var contact = read.Value!;
            if (!XeroContactMatcher.IsActive(contact))
                return ConnectorResult<XeroLink>.Rejected($"The Xero contact '{contact.Name}' is {contact.ContactStatus}; only an active contact can be linked. Restore it in Xero first.");

            if (await LinkedElsewhereAsync(tenantId, document, contact.ContactID!, cancellationToken).ConfigureAwait(false) is { } other)
                return ConnectorResult<XeroLink>.Rejected(UsedByAnother(contact, other));

            var link = NewLink(tenantId, document, contact, LinkedByLinked);
            await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);

            var (numberedLink, numberNote) = await FillEmptyContactNumberAsync(tenantId, organisation, contact, link, cancellationToken).ConfigureAwait(false);

            await AuditAsync(AuditLinkLinked, tenantId, organisation, numberedLink, numberNote, cancellationToken).ConfigureAwait(false);
            return ConnectorResult<XeroLink>.Ok(numberedLink);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Answers the existing link when the organisation is already linked
    /// (no network). Otherwise looks Xero up by <c>ContactNumber</c> (the
    /// customer code, archived contacts included): one active contact
    /// carrying it is linked (<see cref="LinkedByReconciled"/>) instead of
    /// creating another; an archived one, or more than one, is refused with
    /// the reason. Only then <c>PUT Contacts</c> (<c>Name</c>,
    /// <c>ContactNumber</c>, VAT and company number, email), with the
    /// <c>Idempotency-Key</c> and body of the last attempt when its answer was
    /// uncertain (even if the organisation was edited since), else a fresh
    /// one; a lost or failed response is followed by one more lookup, so a
    /// contact Xero did create is linked, not repeated. A look-up that finds
    /// the number settles an uncertain attempt; a create is settled only
    /// after its link is saved.
    /// </remarks>
    public async Task<ConnectorResult<XeroLink>> CreateAsync(string organisationReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationReference);

        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return ConnectorResult<XeroLink>.Reauthorise(NoTenant);

        var organisation = await FindOrganisationAsync(organisationReference, cancellationToken).ConfigureAwait(false);
        if (organisation is null)
            return ConnectorResult<XeroLink>.Rejected(NoSuchOrganisation(organisationReference));

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = DocumentFor(organisation.Reference);
            var existing = await _links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return PersistenceXeroLinkStore.IsFromNewerVersion(existing)
                    ? ConnectorResult<XeroLink>.Rejected(AlreadyLinked(organisation, existing))
                    : ConnectorResult<XeroLink>.Ok(existing);
            }

            var contactNumber = XeroContactMatcher.ContactNumberFor(organisation);

            // Natural-key lookup first (§6.4): a contact made by an earlier
            // attempt whose answer was lost is linked, never duplicated.
            if (contactNumber is not null)
            {
                var before = await ReconcileByContactNumberAsync(tenantId, document, organisation, contactNumber, cancellationToken).ConfigureAwait(false);
                if (before is not null)
                    return before;
            }

            var body = new XeroWireContactCreate(
                organisation.Name.Trim(),
                contactNumber,
                FitOrNull(organisation.TaxRegistration),
                FitOrNull(organisation.RegistrationNumber),
                string.IsNullOrWhiteSpace(organisation.EmailAddress) ? null : organisation.EmailAddress.Trim());

            // While an earlier attempt's answer is still uncertain, its body
            // (and so its key) is sent again unchanged, even if the
            // organisation was edited since: Xero then replays that attempt
            // rather than making a second contact.
            var attempt = await BeginAttemptAsync(CreateOperation, tenantId, document.TempestKey, JsonSerializer.Serialize(body, XeroWire.JsonOptions), cancellationToken).ConfigureAwait(false);
            var sent = ReadCreateBody(attempt);

            // A retry resends the earlier body, whose ContactNumber may differ
            // from today's customer code: look that number up too, so the
            // resend is checked against its own natural key (§6.4.3) and does
            // not rely on Xero still holding the Idempotency-Key.
            var sentNumber = string.IsNullOrWhiteSpace(sent.ContactNumber) ? null : sent.ContactNumber.Trim();
            if (sentNumber is not null && !string.Equals(sentNumber, contactNumber, StringComparison.OrdinalIgnoreCase))
            {
                var resent = await ReconcileByContactNumberAsync(tenantId, document, organisation, sentNumber, cancellationToken).ConfigureAwait(false);
                if (resent is not null)
                    return resent;
            }

            var created = await _api.CreateContactAsync(sent, CreateKey(tenantId, document, attempt.Payload!, attempt.Occurrence), cancellationToken).ConfigureAwait(false);

            if (created.Outcome == ConnectorOutcome.Ok)
            {
                var link = NewLink(tenantId, document, created.Value!, LinkedByCreated);
                await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);

                // Settled only once the link is kept: a crash or a failed save
                // before this replays the same key, not a new contact.
                await EndAttemptAsync(attempt, created.Outcome, cancellationToken).ConfigureAwait(false);
                await AuditAsync(AuditLinkCreated, tenantId, organisation, link, null, cancellationToken).ConfigureAwait(false);
                return ConnectorResult<XeroLink>.Ok(link);
            }

            await EndAttemptAsync(attempt, created.Outcome, cancellationToken).ConfigureAwait(false);

            // The answer was lost, or Xero could not be reached: Xero may
            // still have made the contact. Look once more before reporting.
            // The look-up is by the number in the body actually sent.
            var lookupNumber = sentNumber ?? contactNumber;
            if (created.Outcome is ConnectorOutcome.Unknown or ConnectorOutcome.Unavailable && lookupNumber is not null)
            {
                var after = await ReconcileByContactNumberAsync(tenantId, document, organisation, lookupNumber, cancellationToken).ConfigureAwait(false);
                if (after is { Outcome: ConnectorOutcome.Ok or ConnectorOutcome.Rejected })
                    return after;
            }

            if (created.Outcome == ConnectorOutcome.Rejected && created.ValidationErrors.Any(e => e.Contains("already", StringComparison.OrdinalIgnoreCase)))
            {
                return ConnectorResult<XeroLink>.Rejected(
                    $"{created.Reason} Xero already has a contact like '{organisation.Name.Trim()}'; link that contact instead of creating one.");
            }

            return Fail<XeroWireContact, XeroLink>(created);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Read only: nothing is written to Xero. The link's
    /// <see cref="XeroLink.LastReadAtUtc"/>, <see cref="XeroLink.XeroNumber"/>
    /// (the <c>ContactNumber</c> as read) and
    /// <see cref="XeroLink.LastKnownXeroStatus"/> are updated. A contact
    /// deleted in Xero is Rejected with that reason; the link stays until the
    /// Product Owner unlinks it (<see cref="UnlinkAsync"/>).
    /// </remarks>
    public async Task<ConnectorResult<XeroContactDetails>> ReadDetailsAsync(string organisationReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationReference);

        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return ConnectorResult<XeroContactDetails>.Reauthorise(NoTenant);

        var link = await FindLinkAsync(tenantId, organisationReference, cancellationToken).ConfigureAwait(false);
        if (link is null || string.IsNullOrWhiteSpace(link.XeroId))
            return ConnectorResult<XeroContactDetails>.Rejected($"'{organisationReference.Trim()}' is not linked to a Xero contact.");

        var read = await _api.GetContactAsync(link.XeroId, cancellationToken).ConfigureAwait(false);
        if (read.Outcome != ConnectorOutcome.Ok)
        {
            return read.NotFound
                ? ConnectorResult<XeroContactDetails>.Rejected($"The linked Xero contact {link.XeroId} no longer exists (deleted in Xero); unlink it and link or create another.")
                : Fail<XeroWireContact, XeroContactDetails>(read);
        }

        var now = _time.GetUtcNow();
        var details = ToDetails(read.Value!, link.XeroId, now);

        // The GET ran outside the write gate: the link may have been
        // unlinked, or relinked to another contact, meanwhile. Stamp the
        // refresh onto the link as it is now, and only when it still points
        // at the contact just read; otherwise save nothing (an unlink must
        // not be undone, and the store refuses a changed Xero id).
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await FindLinkAsync(tenantId, organisationReference, cancellationToken).ConfigureAwait(false);
            if (current is not null
                && string.Equals(current.XeroId, link.XeroId, StringComparison.OrdinalIgnoreCase)
                && !PersistenceXeroLinkStore.IsFromNewerVersion(current))
            {
                var refreshed = current with
                {
                    XeroNumber = Blank(read.Value!.ContactNumber),
                    LastKnownXeroStatus = StatusOf(read.Value!),
                    LastReadAtUtc = now,
                };
                await _links.SaveAsync(refreshed, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _writeGate.Release();
        }

        return ConnectorResult<XeroContactDetails>.Ok(details);
    }

    /// <summary>
    /// The Product Owner's explicit Unlink (a contact deleted or merged in
    /// Xero, or linked by mistake): removes the link in the connected
    /// organisation and audits it. Writes nothing to Xero.
    /// </summary>
    /// <param name="organisationReference">The organisation's reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when a link was removed; <see langword="false"/> when there was none, or no organisation is connected.</returns>
    public async Task<bool> UnlinkAsync(string organisationReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationReference);

        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return false;

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = DocumentFor(organisationReference);
            var existing = await _links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false);
            if (existing is null)
                return false;

            await _links.UnlinkAsync(tenantId, document, cancellationToken).ConfigureAwait(false);

            if (_audit is not null)
            {
                await _audit.RecordAsync(AuditLinkUnlinked, new Dictionary<string, string>
                {
                    ["tenantId"] = tenantId,
                    ["organisation"] = organisationReference.Trim(),
                    ["contactId"] = existing.XeroId,
                }, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Maps a Xero contact to the read-only details TempestOS shows: the
    /// <c>POBOX</c> (billing) address, else <c>STREET</c>, as its non-blank
    /// lines, city, region, postcode and country; the sales payment terms,
    /// as days only for Xero's <c>DAYSAFTERBILLDATE</c> type.
    /// </summary>
    /// <param name="contact">The contact as read.</param>
    /// <param name="contactId">Its <c>ContactID</c> (the link's, when Xero's answer omits it).</param>
    /// <param name="readAtUtc">When it was read.</param>
    public static XeroContactDetails ToDetails(XeroWireContact contact, string contactId, DateTimeOffset readAtUtc)
    {
        ArgumentNullException.ThrowIfNull(contact);

        var addresses = contact.Addresses ?? [];
        var billing = BillingLines(addresses.FirstOrDefault(a => IsType(a, "POBOX") && BillingLines(a).Count > 0))
            is { Count: > 0 } poBox
            ? poBox
            : BillingLines(addresses.FirstOrDefault(a => IsType(a, "STREET")));

        var sales = contact.PaymentTerms?.Sales;
        var termsType = string.IsNullOrWhiteSpace(sales?.Type) ? null : sales.Type.Trim().ToUpperInvariant();
        int? termsDays = termsType == "DAYSAFTERBILLDATE" && sales?.Day is >= 0 and var day ? day : null;

        return new XeroContactDetails(
            Blank(contact.ContactID) ?? contactId,
            contact.Name?.Trim() ?? string.Empty,
            Blank(contact.TaxNumber),
            billing,
            Blank(contact.EmailAddress),
            termsDays,
            termsType,
            readAtUtc);

        static bool IsType(XeroWireContactAddress address, string type) =>
            string.Equals(address.AddressType?.Trim(), type, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> BillingLines(XeroWireContactAddress? address) => address is null
        ? []
        : [.. new[] { address.AddressLine1, address.AddressLine2, address.AddressLine3, address.AddressLine4, address.City, address.Region, address.PostalCode, address.Country }
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line!.Trim())];

    /// <summary>
    /// Looks Xero up by <c>ContactNumber</c>, archived contacts included:
    /// one active contact → linked as <see cref="LinkedByReconciled"/>; an
    /// archived one only, or several → Rejected with the reason; none →
    /// <see langword="null"/> (go on and create); the lookup failing → its
    /// failure.
    /// </summary>
    private async Task<ConnectorResult<XeroLink>?> ReconcileByContactNumberAsync(
        string tenantId, XeroDocumentRef document, Organisation organisation, string contactNumber, CancellationToken cancellationToken)
    {
        var lookup = await _api.FindContactsByContactNumberAsync(contactNumber, includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (lookup.Outcome != ConnectorOutcome.Ok)
            return Fail<IReadOnlyList<XeroWireContact>, XeroLink>(lookup);

        var holders = lookup.Value!.Where(c => string.Equals(c.ContactNumber?.Trim(), contactNumber, StringComparison.OrdinalIgnoreCase)).ToList();
        if (holders.Count == 0)
            return null;

        // From here Xero's answer for this organisation is definite (a
        // contact carrying its number exists): an earlier create left
        // uncertain is settled, so a later create takes a fresh key instead
        // of replaying a stale cached reply for a contact since re-purposed.
        var createAttempt = AttemptStoreKey(CreateOperation, tenantId, document.TempestKey);

        var active = holders.Where(XeroContactMatcher.IsActive).ToList();
        if (active.Count > 1)
        {
            await SettleOpenAttemptAsync(createAttempt, cancellationToken).ConfigureAwait(false);
            return ConnectorResult<XeroLink>.Rejected(
                $"{active.Count} Xero contacts carry ContactNumber '{contactNumber}' ({string.Join(", ", active.Select(c => $"'{c.Name}'"))}); choose the right one and link it.");
        }

        if (active.Count == 0)
        {
            await SettleOpenAttemptAsync(createAttempt, cancellationToken).ConfigureAwait(false);
            var archived = holders[0];
            return ConnectorResult<XeroLink>.Rejected(
                $"Xero holds the {archived.ContactStatus?.ToLowerInvariant() ?? "inactive"} contact '{archived.Name}' with ContactNumber '{contactNumber}'; restore it in Xero and link it, rather than creating a second contact.");
        }

        var contact = active[0];
        if (await LinkedElsewhereAsync(tenantId, document, contact.ContactID!, cancellationToken).ConfigureAwait(false) is { } other)
        {
            await SettleOpenAttemptAsync(createAttempt, cancellationToken).ConfigureAwait(false);
            return ConnectorResult<XeroLink>.Rejected(UsedByAnother(contact, other));
        }

        var link = NewLink(tenantId, document, contact, LinkedByReconciled);
        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);
        await SettleOpenAttemptAsync(createAttempt, cancellationToken).ConfigureAwait(false);
        await AuditAsync(AuditLinkReconciled, tenantId, organisation, link, null, cancellationToken).ConfigureAwait(false);
        return ConnectorResult<XeroLink>.Ok(link);
    }

    /// <summary>Q7: fills an empty <c>ContactNumber</c> with the customer code, unless switched off, already set, or held by another contact. Never fails the link.</summary>
    private async Task<(XeroLink Link, string? Note)> FillEmptyContactNumberAsync(
        string tenantId, Organisation organisation, XeroWireContact contact, XeroLink link, CancellationToken cancellationToken)
    {
        if (!_options.WriteContactNumberWhenEmpty || !string.IsNullOrWhiteSpace(contact.ContactNumber))
            return (link, null);

        if (XeroContactMatcher.ContactNumberFor(organisation) is not { } number)
            return (link, "no customer code fits Xero's ContactNumber");

        var holders = await _api.FindContactsByContactNumberAsync(number, includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (holders.Outcome != ConnectorOutcome.Ok)
            return (link, $"ContactNumber not written: {holders.Reason}");

        if (holders.Value!.Any(c => !string.Equals(c.ContactID, contact.ContactID, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.ContactNumber?.Trim(), number, StringComparison.OrdinalIgnoreCase)))
        {
            return (link, $"ContactNumber not written: another Xero contact already carries '{number}'");
        }

        var attempt = await BeginAttemptAsync(SetContactNumberOperation, tenantId, contact.ContactID!.Trim(), payload: null, cancellationToken).ConfigureAwait(false);
        var key = WithOccurrence(Key(SetContactNumberOperation, tenantId, contact.ContactID!, number), attempt.Occurrence);
        var written = await _api.SetContactNumberAsync(contact.ContactID!, number, key, cancellationToken).ConfigureAwait(false);
        var writtenNumber = written.Outcome == ConnectorOutcome.Ok ? Blank(written.Value!.ContactNumber) ?? number : null;

        // The answer was lost: Xero may have written it all the same. Read
        // the contact once, so the link records what Xero holds.
        if (written.Outcome is ConnectorOutcome.Unknown or ConnectorOutcome.Unavailable)
        {
            var reread = await _api.GetContactAsync(contact.ContactID!, cancellationToken).ConfigureAwait(false);
            if (reread.Outcome == ConnectorOutcome.Ok && string.Equals(reread.Value!.ContactNumber?.Trim(), number, StringComparison.OrdinalIgnoreCase))
                writtenNumber = reread.Value!.ContactNumber!.Trim();
        }

        if (writtenNumber is null)
        {
            await EndAttemptAsync(attempt, written.Outcome, cancellationToken).ConfigureAwait(false);
            _logger?.LogWarning("Linked {Organisation} to Xero contact {ContactId}, but its ContactNumber was not written: {Reason}", organisation.Reference, contact.ContactID, written.Reason);
            return (link, $"ContactNumber not written: {written.Reason}");
        }

        await EndAttemptAsync(attempt, ConnectorOutcome.Ok, cancellationToken).ConfigureAwait(false);
        var numbered = link with { XeroNumber = writtenNumber };
        await _links.SaveAsync(numbered, cancellationToken).ConfigureAwait(false);
        return (numbered, $"ContactNumber set to '{number}'");
    }

    /// <summary>Another organisation's link (in <paramref name="tenantId"/>) to <paramref name="contactId"/>, or <see langword="null"/>.</summary>
    private async Task<XeroLink?> LinkedElsewhereAsync(string tenantId, XeroDocumentRef document, string contactId, CancellationToken cancellationToken)
    {
        var links = await _links.ListAsync(tenantId, XeroDocumentKind.Contact, cancellationToken).ConfigureAwait(false);
        return links.FirstOrDefault(l =>
            string.Equals(l.XeroId, contactId, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(l.Document.TempestKey, document.TempestKey, StringComparison.Ordinal));
    }

    private async Task<Organisation?> FindOrganisationAsync(string organisationReference, CancellationToken cancellationToken)
    {
        var record = await _organisations.FindByReferenceAsync(organisationReference.Trim(), cancellationToken).ConfigureAwait(false);
        return record?.Definition;
    }

    private XeroLink NewLink(string tenantId, XeroDocumentRef document, XeroWireContact contact, string linkedBy)
    {
        var now = _time.GetUtcNow();
        return new XeroLink(
            XeroLink.CurrentSchemaVersion,
            tenantId,
            document,
            contact.ContactID!.Trim(),
            Blank(contact.ContactNumber),
            LastPushedContentHash: null,
            LastKnownXeroStatus: StatusOf(contact),
            AttachmentFileName: null,
            AttachmentContentHash: null,
            LinkedAtUtc: now,
            LastReadAtUtc: now,
            LinkedBy: linkedBy);
    }

    private async Task AuditAsync(string action, string tenantId, Organisation organisation, XeroLink link, string? note, CancellationToken cancellationToken)
    {
        if (_audit is null)
            return;

        var detail = new Dictionary<string, string>
        {
            ["tenantId"] = tenantId,
            ["organisation"] = organisation.Reference,
            ["contactId"] = link.XeroId,
            ["linkedBy"] = link.LinkedBy,
        };
        if (link.XeroNumber is { } number)
            detail["contactNumber"] = number;
        if (note is not null)
            detail["note"] = note;

        await _audit.RecordAsync(action, detail, cancellationToken).ConfigureAwait(false);
    }

    private const string CreateOperation = "CreateContact";
    private const string SetContactNumberOperation = "SetContactNumber";

    /// <summary>
    /// The <c>Idempotency-Key</c> of a create: the organisation, tenant, body
    /// and attempt <paramref name="occurrence"/>. A retry of an attempt whose
    /// answer was uncertain keeps the occurrence, so Xero replays its cached
    /// response (a lost create is never repeated); a retry after a definite
    /// answer (made, or refused) takes the next one, so a refusal the Product
    /// Owner has since fixed in Xero is not replayed from Xero's cache (S7).
    /// </summary>
    private static string CreateKey(string tenantId, XeroDocumentRef document, string serialisedBody, int occurrence) =>
        WithOccurrence(Key(CreateOperation, tenantId, document.TempestKey, serialisedBody), occurrence);

    /// <summary>The create body the attempt carries (<see cref="WriteAttempt.Payload"/>): this call's, or the uncertain earlier attempt's.</summary>
    private static XeroWireContactCreate ReadCreateBody(WriteAttempt attempt)
    {
        try
        {
            return JsonSerializer.Deserialize<XeroWireContactCreate>(attempt.Payload!, XeroWire.JsonOptions) is { Name: { Length: > 0 } } body
                ? body
                : throw new PersistenceException($"The Xero contact write attempt '{attempt.StoreKey}' in '{AttemptsCollection}' cannot be read.");
        }
        catch (JsonException ex)
        {
            throw new PersistenceException($"The Xero contact write attempt '{attempt.StoreKey}' in '{AttemptsCollection}' cannot be read.", ex);
        }
    }

    private static string Key(string operation, params string[] parts) =>
        $"{XeroIdempotencyKey.Prefix}{nameof(XeroDocumentKind.Contact)}:{operation}:{Hash(parts)}";

    /// <summary>The first attempt keeps the plain key; a later one hashes the occurrence in, as B2's <see cref="XeroIdempotencyKey.Create"/> does — at most 101 characters either way.</summary>
    private static string WithOccurrence(string key, int occurrence)
    {
        if (occurrence == 0)
            return key;

        var cut = key.LastIndexOf(':');
        return string.Concat(key.AsSpan(0, cut + 1), Hash(key[(cut + 1)..], occurrence.ToString(CultureInfo.InvariantCulture)));
    }

    private static string Hash(params string[] parts) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', parts))));

    /// <summary>One write's attempt: where its record is kept, the occurrence its key carries, and what it sends (<see langword="null"/> when the operation keeps none).</summary>
    private sealed record WriteAttempt(string StoreKey, int Occurrence, string? Payload);

    /// <summary>The persisted state of a write: the occurrence last used, whether Xero's answer to it was definite, and what it sent (a create's body) — sent again unchanged while the answer is uncertain.</summary>
    private sealed record AttemptRecord(int Occurrence, bool Settled, string? Payload = null);

    private static string AttemptStoreKey(string operation, string tenantId, string subject) => $"{tenantId}/{operation}/{subject}";

    /// <summary>
    /// Starts a write (under <see cref="_writeGate"/>): the occurrence of the
    /// last attempt again when its answer was uncertain, the next one when it
    /// was definite, <c>0</c> for the first. Recorded as unsettled before the
    /// request goes, so a crash mid-request also reuses the key.
    /// </summary>
    private async Task<WriteAttempt> BeginAttemptAsync(string operation, string tenantId, string subject, string? payload, CancellationToken cancellationToken)
    {
        var storeKey = AttemptStoreKey(operation, tenantId, subject);
        var previous = await ReadAttemptAsync(storeKey, cancellationToken).ConfigureAwait(false);
        var retrying = previous is { Settled: false };
        var occurrence = previous is null ? 0 : retrying ? previous.Occurrence : previous.Occurrence + 1;
        var sent = retrying && previous!.Payload is { } earlier ? earlier : payload;

        await WriteAttemptAsync(storeKey, new AttemptRecord(occurrence, Settled: false, sent), cancellationToken).ConfigureAwait(false);
        return new WriteAttempt(storeKey, occurrence, sent);
    }

    /// <summary>Settles the attempt unless Xero's answer was uncertain (<see cref="ConnectorOutcome.Unknown"/> or <see cref="ConnectorOutcome.Unavailable"/>), when the next try must replay the same key.</summary>
    private Task EndAttemptAsync(WriteAttempt attempt, ConnectorOutcome outcome, CancellationToken cancellationToken) =>
        outcome is ConnectorOutcome.Unknown or ConnectorOutcome.Unavailable
            ? Task.CompletedTask
            : WriteAttemptAsync(attempt.StoreKey, new AttemptRecord(attempt.Occurrence, Settled: true, attempt.Payload), cancellationToken);

    /// <summary>Settles an attempt left uncertain once a look-up has given a definite answer for its subject; nothing to do when there is none or it is settled.</summary>
    private async Task SettleOpenAttemptAsync(string storeKey, CancellationToken cancellationToken)
    {
        if (await ReadAttemptAsync(storeKey, cancellationToken).ConfigureAwait(false) is { Settled: false } open)
            await WriteAttemptAsync(storeKey, open with { Settled = true }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AttemptRecord?> ReadAttemptAsync(string storeKey, CancellationToken cancellationToken)
    {
        var json = _attemptStore is null
            ? _attemptsInMemory.GetValueOrDefault(storeKey)
            : await _attemptStore.ReadAsync(AttemptsCollection, storeKey, cancellationToken).ConfigureAwait(false);
        if (json is null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<AttemptRecord>(json, AttemptJson) is { Occurrence: >= 0 } record
                ? record
                : throw new PersistenceException($"The Xero contact write attempt '{storeKey}' in '{AttemptsCollection}' cannot be read.");
        }
        catch (JsonException ex)
        {
            throw new PersistenceException($"The Xero contact write attempt '{storeKey}' in '{AttemptsCollection}' cannot be read.", ex);
        }
    }

    private async Task WriteAttemptAsync(string storeKey, AttemptRecord record, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(record, AttemptJson);
        if (_attemptStore is null)
            _attemptsInMemory[storeKey] = json;
        else
            await _attemptStore.WriteAsync(AttemptsCollection, storeKey, json, cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions AttemptJson = new(JsonSerializerDefaults.Web);

    private static IEnumerable<string> VatSpellings(string? vat)
    {
        if (string.IsNullOrWhiteSpace(vat))
            yield break;

        var recorded = vat.Trim();
        if (recorded.Length <= XeroAccountingApi.MaximumContactNumberLength)
            yield return recorded;

        if (XeroContactMatcher.NormaliseVatNumber(recorded) is not { } plain)
            yield break;

        if (!string.Equals(plain, recorded, StringComparison.Ordinal))
            yield return plain;

        // Xero's where= is exact, so the other form a person may have typed
        // is asked for too: without the two-letter country prefix
        // ("GB123456789" → "123456789"), or with "GB" when none was recorded
        // ("123456789" → "GB123456789") — the forms SameVatNumber ranks alike.
        var other = HasCountryPrefix(plain) ? plain[2..] : plain.Any(char.IsAsciiDigit) ? "GB" + plain : null;
        if (other is { Length: > 0 and <= XeroAccountingApi.MaximumContactNumberLength })
            yield return other;

        static bool HasCountryPrefix(string vat) => vat.Length > 2 && char.IsAsciiLetter(vat[0]) && char.IsAsciiLetter(vat[1]);
    }

    private static ConnectorResult<TTo> Fail<TFrom, TTo>(XeroApiResult<TFrom> result) => result.Outcome switch
    {
        ConnectorOutcome.Rejected => ConnectorResult<TTo>.Rejected(string.IsNullOrWhiteSpace(result.Reason) ? "Xero rejected the request." : result.Reason),
        ConnectorOutcome.Reauthorise => ConnectorResult<TTo>.Reauthorise(result.Reason),
        ConnectorOutcome.Unavailable => ConnectorResult<TTo>.Unavailable(result.Reason),
        _ => ConnectorResult<TTo>.Unknown(result.Reason),
    };

    private static string? StatusOf(XeroWireContact contact) =>
        string.IsNullOrWhiteSpace(contact.ContactStatus) ? XeroContactMatcher.ActiveStatus : contact.ContactStatus.Trim().ToUpperInvariant();

    private static string? FitOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > XeroAccountingApi.MaximumContactNumberLength ? null : value.Trim();

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NoSuchOrganisation(string reference) =>
        string.Create(CultureInfo.InvariantCulture, $"TempestOS has no customer or supplier '{reference.Trim()}'.");

    private static string AlreadyLinked(Organisation organisation, XeroLink existing) =>
        PersistenceXeroLinkStore.IsFromNewerVersion(existing)
            ? $"The Xero contact link for '{organisation.Reference}' was {PersistenceXeroLinkStore.NewerVersionNote}; it is left untouched."
            : $"'{organisation.Reference}' is already linked to Xero contact {existing.XeroId}; unlink it first to link another.";

    private static string UsedByAnother(XeroWireContact contact, XeroLink other) =>
        $"The Xero contact '{contact.Name}' is already linked to '{other.Document.TempestKey}'; one Xero contact serves one TempestOS organisation.";

    private const string NoTenant = "No Xero organisation is connected; authorise Xero first.";
}

/// <summary>
/// Answers <see cref="IXeroContactLinker"/> with the container's one
/// <see cref="XeroContactLinker"/> — the container has no factory
/// registrations, so this forwarder is how one instance serves both the
/// interface and the concrete type.
/// </summary>
/// <param name="inner">The one linker.</param>
internal sealed class XeroContactLinkerForwarder(XeroContactLinker inner) : IXeroContactLinker
{
    /// <inheritdoc />
    public Task<XeroLink?> FindLinkAsync(string organisationReference, CancellationToken cancellationToken = default) =>
        inner.FindLinkAsync(organisationReference, cancellationToken);

    /// <inheritdoc />
    public Task<ConnectorResult<IReadOnlyList<XeroContactCandidate>>> FindCandidatesAsync(string organisationReference, CancellationToken cancellationToken = default) =>
        inner.FindCandidatesAsync(organisationReference, cancellationToken);

    /// <inheritdoc />
    public Task<ConnectorResult<XeroLink>> LinkExistingAsync(string organisationReference, string contactId, CancellationToken cancellationToken = default) =>
        inner.LinkExistingAsync(organisationReference, contactId, cancellationToken);

    /// <inheritdoc />
    public Task<ConnectorResult<XeroLink>> CreateAsync(string organisationReference, CancellationToken cancellationToken = default) =>
        inner.CreateAsync(organisationReference, cancellationToken);

    /// <inheritdoc />
    public Task<ConnectorResult<XeroContactDetails>> ReadDetailsAsync(string organisationReference, CancellationToken cancellationToken = default) =>
        inner.ReadDetailsAsync(organisationReference, cancellationToken);
}
