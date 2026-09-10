namespace Tempest.Core.Invoicing;

/// <summary>One call this connector recorded, in call order — for a test to assert against directly.</summary>
/// <param name="Member">Which <see cref="IInvoicingConnector"/> member was called.</param>
/// <param name="Argument">That call's own key argument (the idempotency key, the external id, or the reference), or <see langword="null"/> for a call that takes none.</param>
public sealed record FakeConnectorCall(string Member, string? Argument);

/// <summary>
/// The only <see cref="IInvoicingConnector"/> implementation this Work
/// Package ships — records every call, and can be scripted to answer
/// however a test needs (`WP 19.1A`, `ADR-0151`). Bound as
/// <see cref="Tempest.Core.Runtime.TempestHost"/>'s own default connector
/// (<c>Invoicing:Connector</c> configuration key, unset or <c>"Fake"</c>);
/// `WP 19.1A` parts 2 and 3 add real bindings alongside it, never replacing
/// this seam.
/// </summary>
/// <remarks>
/// <para>
/// <b>What "lost" means here.</b> Scripting <see cref="ConnectorOutcome.Unknown"/>
/// for <see cref="CreateDraftInvoiceAsync"/> still records the invoice
/// under its own idempotency key — a real timeout after the accounting
/// system already committed the write looks exactly like this: the
/// request was received, the invoice exists, only the response back to
/// this process never arrived. <see cref="FindByReferenceAsync"/> then
/// finds it, exactly as <see cref="InvoicingService.ReconcileAsync"/>'s own
/// <see cref="InvoiceRequestStatus.Unknown"/> branch expects.
/// </para>
/// <para>
/// <b>Scripting is consumed once.</b> <see cref="ScriptNextCreate"/> answers
/// exactly one future <see cref="CreateDraftInvoiceAsync"/> call (whichever
/// request reaches it next) and is then cleared, so a test scripting a
/// failure and then sending again does not need to un-script anything.
/// <see cref="ScriptCreateFor"/> pins an outcome to one specific request's
/// own idempotency key instead, for a test juggling more than one request
/// at a time; a per-key script is checked first and is never consumed —
/// the same request retried keeps hitting it, matching a real accounting
/// system's own idempotent behaviour for a repeated key.
/// </para>
/// </remarks>
public sealed class FakeInvoicingConnector : IInvoicingConnector
{
    private readonly object _gate = new();
    private readonly List<FakeConnectorCall> _calls = [];
    private readonly Dictionary<string, CreatedInvoice> _invoicesByReference = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InvoiceStatusReading> _statusByExternalId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (ConnectorOutcome Outcome, string? Reason)> _scriptedByKey = new(StringComparer.Ordinal);
    private readonly List<ConnectorContact> _contacts = [];

    private (ConnectorOutcome Outcome, string? Reason)? _nextCreateScript;
    private ConnectorAuthorisationState _authorisationState = new(ConnectorAuthorisation.Authorised);
    private int _invoiceNumberSequence;

    /// <inheritdoc />
    public string Name => "Fake";

    /// <summary>Every call this connector has recorded, in call order.</summary>
    public IReadOnlyList<FakeConnectorCall> Calls
    {
        get { lock (_gate) return [.. _calls]; }
    }

    /// <summary>Scripts the next <see cref="CreateDraftInvoiceAsync"/> call, whichever request reaches it, to answer with <paramref name="outcome"/>. Consumed once.</summary>
    public void ScriptNextCreate(ConnectorOutcome outcome, string? reason = null)
    {
        lock (_gate)
            _nextCreateScript = (outcome, reason);
    }

    /// <summary>Scripts every <see cref="CreateDraftInvoiceAsync"/> call carrying <paramref name="idempotencyKey"/> to answer with <paramref name="outcome"/> — never consumed, so a retry of the same request keeps hitting it.</summary>
    /// <exception cref="ArgumentException"><paramref name="idempotencyKey"/> is null, empty, or whitespace.</exception>
    public void ScriptCreateFor(string idempotencyKey, ConnectorOutcome outcome, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        lock (_gate)
            _scriptedByKey[idempotencyKey] = (outcome, reason);
    }

    /// <summary>Scripts what <see cref="ReadStatusAsync"/> answers for <paramref name="externalId"/> — otherwise it answers a plain, unremarkable <c>"SUBMITTED"</c> reading with no dates.</summary>
    /// <exception cref="ArgumentException"><paramref name="externalId"/> is null, empty, or whitespace.</exception>
    public void ScriptStatus(string externalId, InvoiceStatusReading reading)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentNullException.ThrowIfNull(reading);

        lock (_gate)
            _statusByExternalId[externalId] = reading;
    }

    /// <summary>Scripts what <see cref="AuthorisationStateAsync"/> answers. Defaults to <see cref="ConnectorAuthorisation.Authorised"/> until scripted otherwise.</summary>
    public void ScriptAuthorisationState(ConnectorAuthorisationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_gate)
            _authorisationState = state;
    }

    /// <summary>Adds <paramref name="contact"/> to what <see cref="ListContactsAsync"/> answers.</summary>
    public void AddContact(ConnectorContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        lock (_gate)
            _contacts.Add(contact);
    }

    /// <summary>
    /// Forgets whatever invoice was recorded under <paramref name="reference"/>
    /// — simulates a response genuinely lost with no trace at the provider
    /// either, distinct from the ordinary <see cref="ConnectorOutcome.Unknown"/>
    /// case (this class's own remarks), so a test can drive
    /// <see cref="InvoicingService.ReconcileAsync"/>'s own "not found"
    /// branch deliberately. A no-op if nothing is recorded under it.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public void ForgetReference(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        lock (_gate)
            _invoicesByReference.Remove(reference);
    }

    /// <inheritdoc />
    public Task<ConnectorResult<CreatedInvoice>> CreateDraftInvoiceAsync(
        InvoiceRequestSnapshot request, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        lock (_gate)
        {
            _calls.Add(new FakeConnectorCall(nameof(CreateDraftInvoiceAsync), idempotencyKey));

            var (outcome, reason) = ResolveCreateScript(idempotencyKey);

            switch (outcome)
            {
                case ConnectorOutcome.Rejected:
                    return Task.FromResult(ConnectorResult<CreatedInvoice>.Rejected(reason ?? "Rejected by the fake connector."));

                case ConnectorOutcome.Reauthorise:
                    return Task.FromResult(ConnectorResult<CreatedInvoice>.Reauthorise(reason));

                case ConnectorOutcome.Unavailable:
                    return Task.FromResult(ConnectorResult<CreatedInvoice>.Unavailable(reason));

                case ConnectorOutcome.Unknown:
                    // Received and committed at "the provider" — only the
                    // response back to this caller is lost. See this
                    // class's own remarks.
                    RecordInvoice(idempotencyKey);
                    return Task.FromResult(ConnectorResult<CreatedInvoice>.Unknown(reason));

                default:
                    var created = RecordInvoice(idempotencyKey);
                    return Task.FromResult(ConnectorResult<CreatedInvoice>.Ok(created));
            }
        }
    }

    /// <inheritdoc />
    public Task<ConnectorResult<InvoiceStatusReading>> ReadStatusAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        lock (_gate)
        {
            _calls.Add(new FakeConnectorCall(nameof(ReadStatusAsync), externalId));

            var reading = _statusByExternalId.TryGetValue(externalId, out var scripted)
                ? scripted
                : new InvoiceStatusReading("SUBMITTED", null, null, null);

            return Task.FromResult(ConnectorResult<InvoiceStatusReading>.Ok(reading));
        }
    }

    /// <inheritdoc />
    public Task<ConnectorResult<CreatedInvoice?>> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        lock (_gate)
        {
            _calls.Add(new FakeConnectorCall(nameof(FindByReferenceAsync), reference));

            return Task.FromResult(
                _invoicesByReference.TryGetValue(reference, out var invoice)
                    ? ConnectorResult<CreatedInvoice?>.Ok(invoice)
                    : ConnectorResult<CreatedInvoice?>.Ok(null));
        }
    }

    /// <inheritdoc />
    public Task<ConnectorResult<IReadOnlyList<ConnectorContact>>> ListContactsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _calls.Add(new FakeConnectorCall(nameof(ListContactsAsync), Argument: null));

            return Task.FromResult(ConnectorResult<IReadOnlyList<ConnectorContact>>.Ok([.. _contacts]));
        }
    }

    /// <inheritdoc />
    public Task<ConnectorAuthorisationState> AuthorisationStateAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _calls.Add(new FakeConnectorCall(nameof(AuthorisationStateAsync), Argument: null));

            return Task.FromResult(_authorisationState);
        }
    }

    private CreatedInvoice RecordInvoice(string idempotencyKey)
    {
        var externalId = $"fake-inv-{Guid.NewGuid():N}";
        var invoiceNumber = $"INV-{++_invoiceNumberSequence:0000}";
        var invoice = new CreatedInvoice(externalId, invoiceNumber, idempotencyKey);

        _invoicesByReference[idempotencyKey] = invoice;

        return invoice;
    }

    private (ConnectorOutcome Outcome, string? Reason) ResolveCreateScript(string idempotencyKey)
    {
        if (_scriptedByKey.TryGetValue(idempotencyKey, out var perKey))
            return perKey;

        if (_nextCreateScript is { } next)
        {
            _nextCreateScript = null;
            return next;
        }

        return (ConnectorOutcome.Ok, null);
    }
}
