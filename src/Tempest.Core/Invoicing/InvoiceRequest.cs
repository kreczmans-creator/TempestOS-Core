using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Invoicing;

/// <summary>
/// A canonical Kind recording one outbound invoice raised against a
/// client, from a project's own unbilled timesheet entries and completed,
/// fixed-price deliverables, through to whatever an accounting connector
/// reports back (`WP 19.1A`, `ADR-0151`). Follows
/// <c>Tempest.Core.Evidence.Evidence</c>'s own shape exactly: an
/// <c>EngineeringObjectBase</c> subtype carrying its own state through
/// <c>CaptureTypeState</c>/<c>ApplyTypeState</c>/<see cref="IRehydratable{InvoiceRequest}.Rehydrate"/>,
/// parented to the project it bills.
/// </summary>
/// <remarks>
/// <para>
/// <b>This request's own id is the idempotency key.</b>
/// <see cref="InvoicingService.SendAsync"/> passes <see cref="IEngineeringObject.Id"/>
/// itself to <see cref="IInvoicingConnector.CreateDraftInvoiceAsync"/> —
/// never a value minted per attempt — so a retried send after a lost
/// response is still the same key, resolvable by
/// <see cref="IInvoicingConnector.FindByReferenceAsync"/> before any
/// second call is ever made.
/// </para>
/// <para>
/// <b><see cref="PaidDate"/> is read from the connector only.</b> Nothing
/// in TempestOS computes it, sets it directly, or infers it from any other
/// fact this platform holds — the Product Owner guard `WP 19.1A`'s own row
/// states in as many words: "Nothing in TempestOS marks an invoice paid."
/// </para>
/// <para>
/// <b>Whether an act is permitted is decided by <see cref="InvoicingService"/>,
/// before any mutator below ever runs</b> — this class only ever persists
/// what it is told to, exactly as <c>Evidence</c>'s own mutators do for
/// <c>EvidenceService</c>.
/// </para>
/// </remarks>
public sealed class InvoiceRequest : EngineeringObjectBase, IRehydratable<InvoiceRequest>
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> every invoice request's own backing document carries (`ADR-0105`).</summary>
    public const string CanonicalKind = "InvoiceRequest";

    private readonly string _clientOrganisationId;
    private readonly string? _purchaseOrderReference;
    private readonly CurrencyCode _currency;
    private readonly List<InvoiceRequestLine> _lines;
    private readonly Money _total;
    private InvoiceRequestStatus _status;
    private string? _externalId;
    private string? _externalInvoiceNumber;
    private string? _externalStatus;
    private DateOnly? _issuedDate;
    private DateOnly? _paidDate;
    private string? _lastError;
    private string? _connector;
    private DateTimeOffset? _sentAtUtc;

    /// <summary>Initialises a new instance of the <see cref="InvoiceRequest"/> class.</summary>
    public InvoiceRequest(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        string clientOrganisationId, string? purchaseOrderReference, CurrencyCode currency,
        IReadOnlyList<InvoiceRequestLine> lines, Money total,
        InvoiceRequestStatus status = InvoiceRequestStatus.Draft,
        string? externalId = null, string? externalInvoiceNumber = null, string? externalStatus = null,
        DateOnly? issuedDate = null, DateOnly? paidDate = null, string? lastError = null,
        string? connector = null, DateTimeOffset? sentAtUtc = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientOrganisationId);
        ArgumentNullException.ThrowIfNull(lines);

        _clientOrganisationId = clientOrganisationId;
        _purchaseOrderReference = purchaseOrderReference;
        _currency = currency;
        _lines = [.. lines];
        _total = total;
        _status = status;
        _externalId = externalId;
        _externalInvoiceNumber = externalInvoiceNumber;
        _externalStatus = externalStatus;
        _issuedDate = issuedDate;
        _paidDate = paidDate;
        _lastError = lastError;
        _connector = connector;
        _sentAtUtc = sentAtUtc;
    }

    /// <summary>The client this invoice is raised against — an Organisation-catalogue id, never validated as a real record by this class.</summary>
    public string ClientOrganisationId => _clientOrganisationId;

    /// <summary>The project's own purchase-order reference, where one is recorded. <see langword="null"/> otherwise.</summary>
    public string? PurchaseOrderReference => _purchaseOrderReference;

    /// <summary>The currency every line and <see cref="Total"/> are stated in — resolved from the project's own pinned rate card at the moment this request was raised.</summary>
    public CurrencyCode Currency => _currency;

    /// <summary>This request's own lines — read-only once raised; a line never mutates independently of the request it belongs to.</summary>
    public IReadOnlyList<InvoiceRequestLine> Lines => _lines;

    /// <summary>The sum of every line's own <c>Amount</c>.</summary>
    public Money Total => _total;

    /// <inheritdoc cref="IHasLifecycle.Status" />
    /// <remarks>Hides <c>IHasLifecycle.Status</c> (the eight-value canonical <see cref="LifecycleState"/>) with this Kind's own status vocabulary (`ADR-0151`), exactly as <c>Tempest.Core.Evidence.Evidence.Status</c> does.</remarks>
    public new InvoiceRequestStatus Status => _status;

    /// <summary>The accounting system's own identity for the created invoice, once known. <see langword="null"/> before then.</summary>
    public string? ExternalId => _externalId;

    /// <summary>The accounting system's own invoice number, once assigned. <see langword="null"/> until then.</summary>
    public string? ExternalInvoiceNumber => _externalInvoiceNumber;

    /// <summary>The accounting system's own status word, verbatim, as last read. <see langword="null"/> until the first read.</summary>
    public string? ExternalStatus => _externalStatus;

    /// <summary>When the invoice was issued to the client, as the accounting system records it. <see langword="null"/> until it reports one.</summary>
    public DateOnly? IssuedDate => _issuedDate;

    /// <summary>
    /// When the invoice was paid, as the accounting system records it.
    /// <see langword="null"/> until it reports one. <b>Read from the
    /// connector only — this class's own mutators never set it from any
    /// other source.</b>
    /// </summary>
    public DateOnly? PaidDate => _paidDate;

    /// <summary>The accounting system's own last-reported refusal or failure reason, for the engineer to read. <see langword="null"/> once nothing is wrong.</summary>
    public string? LastError => _lastError;

    /// <summary>The connector this request was sent through — <see cref="IInvoicingConnector.Name"/> — once it has been. <see langword="null"/> while still <see cref="InvoiceRequestStatus.Draft"/>.</summary>
    public string? Connector => _connector;

    /// <summary>When this request was last handed to <see cref="IInvoicingConnector.CreateDraftInvoiceAsync"/>. <see langword="null"/> until the first send.</summary>
    public DateTimeOffset? SentAtUtc => _sentAtUtc;

    /// <summary>Moves this request to <see cref="InvoiceRequestStatus.Sending"/> and records which connector the attempt is through. <see cref="InvoicingService.SendAsync"/> checks <see cref="InvoiceRequestStatusTransitions"/> before this ever runs.</summary>
    internal Task MoveToSendingAsync(string connectorName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectorName);

        return MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(Status)] = InvoiceRequestStatus.Sending.ToString(),
                [nameof(Connector)] = connectorName,
                [nameof(LastError)] = null,
            },
            () =>
            {
                _status = InvoiceRequestStatus.Sending;
                _connector = connectorName;
                _lastError = null;
            },
            $"Sending via '{connectorName}'.",
            cancellationToken);
    }

    /// <summary>Records a successful send: moves to <see cref="InvoiceRequestStatus.Sent"/> with the connector's own external id known.</summary>
    internal Task MarkSentAsync(string externalId, string? externalInvoiceNumber, DateTimeOffset sentAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(Status)] = InvoiceRequestStatus.Sent.ToString(),
                [nameof(ExternalId)] = externalId,
                [nameof(ExternalInvoiceNumber)] = externalInvoiceNumber,
                [nameof(LastError)] = null,
                [nameof(SentAtUtc)] = sentAtUtc.ToString("O", CultureInfo.InvariantCulture),
            },
            () =>
            {
                _status = InvoiceRequestStatus.Sent;
                _externalId = externalId;
                _externalInvoiceNumber = externalInvoiceNumber;
                _lastError = null;
                _sentAtUtc = sentAtUtc;
            },
            $"Sent — external id '{externalId}'.",
            cancellationToken);
    }

    /// <summary>Records a rejection: moves to <see cref="InvoiceRequestStatus.Rejected"/> with the accounting system's own reason.</summary>
    internal Task MarkRejectedAsync(string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return SetStatusWithErrorAsync(InvoiceRequestStatus.Rejected, reason, $"Rejected: {reason}", cancellationToken);
    }

    /// <summary>Records that the connector's own stored token needs re-authorising: moves to <see cref="InvoiceRequestStatus.Reauthorise"/>.</summary>
    internal Task MarkReauthoriseAsync(string? reason, CancellationToken cancellationToken = default) =>
        SetStatusWithErrorAsync(InvoiceRequestStatus.Reauthorise, reason, "Connector needs re-authorising.", cancellationToken);

    /// <summary>
    /// Reverts this request to <see cref="InvoiceRequestStatus.Draft"/> —
    /// the connector was unreachable at send time
    /// (<see cref="InvoicingService.SendAsync"/>'s own remarks), or
    /// reconciliation found nothing by reference
    /// (<see cref="InvoicingService.ReconcileAsync"/>).
    /// </summary>
    internal Task RevertToDraftAsync(string? reason, CancellationToken cancellationToken = default) =>
        SetStatusWithErrorAsync(InvoiceRequestStatus.Draft, reason, "Reverted to Draft.", cancellationToken);

    /// <summary>Records that the send's own response was lost: moves to <see cref="InvoiceRequestStatus.Unknown"/>.</summary>
    internal Task MarkUnknownAsync(string? reason, CancellationToken cancellationToken = default) =>
        SetStatusWithErrorAsync(InvoiceRequestStatus.Unknown, reason, "Response lost; status unknown.", cancellationToken);

    /// <summary>Records that reconciliation found this request's own invoice by reference: moves to <see cref="InvoiceRequestStatus.Sent"/> with the external id now known.</summary>
    internal Task ReconcileFoundAsync(string externalId, string? externalInvoiceNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(Status)] = InvoiceRequestStatus.Sent.ToString(),
                [nameof(ExternalId)] = externalId,
                [nameof(ExternalInvoiceNumber)] = externalInvoiceNumber ?? _externalInvoiceNumber,
                [nameof(LastError)] = null,
            },
            () =>
            {
                _status = InvoiceRequestStatus.Sent;
                _externalId = externalId;
                _externalInvoiceNumber = externalInvoiceNumber ?? _externalInvoiceNumber;
                _lastError = null;
            },
            $"Found by reference — external id '{externalId}'.",
            cancellationToken);
    }

    /// <summary>Records a status reading from the connector — <paramref name="status"/> may equal <see cref="Status"/> already (a refresh with nothing new to report).</summary>
    internal Task RecordStatusReadingAsync(
        InvoiceRequestStatus status, string externalStatus, string? externalInvoiceNumber, DateOnly? issuedDate, DateOnly? paidDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalStatus);

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(Status)] = status.ToString(),
                    [nameof(ExternalStatus)] = externalStatus,
                    [nameof(ExternalInvoiceNumber)] = externalInvoiceNumber ?? _externalInvoiceNumber,
                };
                WriteJson(state, nameof(IssuedDate), issuedDate ?? _issuedDate);
                WriteJson(state, nameof(PaidDate), paidDate ?? _paidDate);
                return state;
            },
            () =>
            {
                _status = status;
                _externalStatus = externalStatus;
                _externalInvoiceNumber = externalInvoiceNumber ?? _externalInvoiceNumber;
                _issuedDate = issuedDate ?? _issuedDate;
                _paidDate = paidDate ?? _paidDate;
            },
            $"Status read: '{externalStatus}' -> {status}.",
            cancellationToken);
    }

    /// <summary>Voids this request locally — only ever called for <see cref="InvoiceRequestStatus.Draft"/> or <see cref="InvoiceRequestStatus.Rejected"/> (<see cref="InvoicingService.VoidAsync"/>'s own remarks); a request that reached the provider is voided there and read back through <see cref="RecordStatusReadingAsync"/> instead.</summary>
    internal Task VoidLocallyAsync(CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Status)] = InvoiceRequestStatus.Voided.ToString() },
            () => _status = InvoiceRequestStatus.Voided,
            "Voided.",
            cancellationToken);

    private Task SetStatusWithErrorAsync(InvoiceRequestStatus status, string? reason, string auditDetail, CancellationToken cancellationToken) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(Status)] = status.ToString(),
                [nameof(LastError)] = reason,
            },
            () =>
            {
                _status = status;
                _lastError = reason;
            },
            auditDetail,
            cancellationToken);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(ClientOrganisationId)] = _clientOrganisationId;
        state[nameof(PurchaseOrderReference)] = _purchaseOrderReference;
        WriteJson(state, nameof(Currency), _currency);
        WriteJson(state, nameof(Lines), _lines);
        WriteJson(state, nameof(Total), _total);
        state[nameof(Status)] = _status.ToString();
        state[nameof(ExternalId)] = _externalId;
        state[nameof(ExternalInvoiceNumber)] = _externalInvoiceNumber;
        state[nameof(ExternalStatus)] = _externalStatus;
        WriteJson(state, nameof(IssuedDate), _issuedDate);
        WriteJson(state, nameof(PaidDate), _paidDate);
        state[nameof(LastError)] = _lastError;
        state[nameof(Connector)] = _connector;
        state[nameof(SentAtUtc)] = _sentAtUtc?.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _status = ReadStatus(state);
        _externalId = state.Type(nameof(ExternalId));
        _externalInvoiceNumber = state.Type(nameof(ExternalInvoiceNumber));
        _externalStatus = state.Type(nameof(ExternalStatus));
        _issuedDate = state.TypeJson<DateOnly?>(nameof(IssuedDate));
        _paidDate = state.TypeJson<DateOnly?>(nameof(PaidDate));
        _lastError = state.Type(nameof(LastError));
        _connector = state.Type(nameof(Connector));
        _sentAtUtc = ParseSentAtUtc(state);
    }

    private static InvoiceRequestStatus ReadStatus(EngineeringObjectState state) =>
        Enum.TryParse<InvoiceRequestStatus>(state.Type(nameof(Status)), out var value) ? value : InvoiceRequestStatus.Draft;

    private static DateTimeOffset? ParseSentAtUtc(EngineeringObjectState state) =>
        DateTimeOffset.TryParse(state.Type(nameof(SentAtUtc)), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : null;

    private static List<InvoiceRequestLine> ReadLines(EngineeringObjectState state) =>
        state.TypeJson<List<InvoiceRequestLine>>(nameof(Lines)) ?? [];

    static InvoiceRequest IRehydratable<InvoiceRequest>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.Type(nameof(ClientOrganisationId)) ?? string.Empty,
            state.Type(nameof(PurchaseOrderReference)),
            state.TypeJson<CurrencyCode>(nameof(Currency)),
            ReadLines(state),
            state.TypeJson<Money>(nameof(Total)),
            ReadStatus(state),
            state.Type(nameof(ExternalId)),
            state.Type(nameof(ExternalInvoiceNumber)),
            state.Type(nameof(ExternalStatus)),
            state.TypeJson<DateOnly?>(nameof(IssuedDate)),
            state.TypeJson<DateOnly?>(nameof(PaidDate)),
            state.Type(nameof(LastError)),
            state.Type(nameof(Connector)),
            ParseSentAtUtc(state));
}
