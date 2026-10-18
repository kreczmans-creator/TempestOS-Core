using System.Globalization;
using System.Text.Json;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Persistence;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>What one run of <see cref="XeroInvoiceLinkImporter"/> did.</summary>
/// <param name="Imported">Links written (<see cref="XeroLink.LinkedBy"/> <c>"imported"</c>).</param>
/// <param name="AlreadyLinked">Requests that already had a link to the same Xero invoice — left as they are.</param>
/// <param name="ConflictingRequestIds">Requests whose existing link names a different Xero record, or cannot be read — never overwritten; shown so a person can look.</param>
public sealed record XeroInvoiceLinkImportReport(int Imported, int AlreadyLinked, IReadOnlyList<Guid> ConflictingRequestIds)
{
    /// <summary>`v0.24.0` F1 (additive, M1): requests whose Xero invoice this organisation does not hold (Xero answered 404) — sent to another organisation; never imported here.</summary>
    public int NotInOrganisation { get; init; }

    /// <summary>`v0.24.0` F1 (additive, M1): requests sent through `v0.24.0`'s own sync (at or after <see cref="XeroInvoiceLinkImporter.FirstRunKey"/>) — their link is written by X4 in the organisation they were sent to; never imported.</summary>
    public int SentThroughSync { get; init; }

    /// <summary>`v0.24.0` F1 (additive, M1): whether every request was decided; <see langword="false"/> when Xero could not be asked about some (unreachable, rate-limited, needs re-authorising, or this run's budget spent) — the next run carries on.</summary>
    public bool Complete { get; init; } = true;
}

/// <summary>
/// Carries invoice requests sent to Xero before `v0.24.0` over to the link
/// store (`v0.24.0` B2, `ADR-0162` decision 6; design §5): every
/// <see cref="InvoiceRequest"/> whose <see cref="InvoiceRequest.Connector"/>
/// is <c>"Xero"</c> and which carries an
/// <see cref="InvoiceRequest.ExternalId"/> gains an <see cref="XeroLink"/>
/// with <see cref="XeroLink.LinkedBy"/> <c>"imported"</c>, so v0.19–v0.23
/// sends keep reconciling and are never created in Xero a second time.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing existing changes.</b> The request's own `ADR-0151` fields
/// (<c>ExternalId</c>, <c>ExternalInvoiceNumber</c>, <c>ExternalStatus</c>)
/// are read, never written; an existing link is never replaced — one that
/// names a different Xero record is reported, not overwritten.
/// </para>
/// <para>
/// <b>Idempotent, and remembered per organisation (F1, M1).</b> A second run
/// over the same requests writes nothing. <see cref="EnsureImportedAsync"/>
/// runs the import once per organisation: built with a state store, a
/// finished run is recorded (<see cref="RanKey"/>) and never repeated, also
/// after a restart; without one, once per process. <see cref="ImportAsync"/>
/// always scans.
/// </para>
/// <para>
/// <b>Only requests sent before `v0.24.0` (F1, M1).</b> Built with a state
/// store, the first run ever records its moment (<see cref="FirstRunKey"/>):
/// a request sent at or after it went through `v0.24.0`'s own sync, which
/// links it in the organisation it was sent to — it is never imported into
/// another (the Demo Company's invoice ids never become the live
/// organisation's links).
/// </para>
/// <para>
/// <b>Only invoices the organisation holds (F1, M1).</b> A request sent before
/// `v0.24.0` recorded no Xero organisation. Built with the Xero API, the
/// importer first asks Xero whether the organisation it imports into holds
/// the invoice (<c>GET Invoices/{InvoiceID}</c>, at most
/// <see cref="DefaultBudget"/> per run, so the drain keeps its share of the
/// minute limit): a 404 is recorded and never imported (it was sent to another
/// organisation); an invoice found is imported with Xero's own status and
/// number; when Xero cannot answer (unreachable, rate-limited, needs
/// re-authorising) the run stops and the next one carries on — nothing is
/// imported on a guess. Without the API (tests, a host with no Xero client)
/// it imports as before F1, and a link imported into the wrong organisation
/// reads back as <i>Deleted in Xero</i>; nothing is ever created from it.
/// </para>
/// <para>
/// <b>Which requests.</b> Exactly those whose <c>Connector</c> is
/// <c>"Xero"</c> (ordinal, as the connector records its own
/// <see cref="IInvoicingConnector.Name"/>) and which carry an
/// <c>ExternalId</c>.
/// </para>
/// </remarks>
public sealed class XeroInvoiceLinkImporter
{
    /// <summary>The <see cref="IInvoicingConnector.Name"/> of the Xero connector, as <see cref="InvoiceRequest.Connector"/> records it.</summary>
    public const string XeroConnectorName = "Xero";

    /// <summary>The <see cref="XeroLink.LinkedBy"/> of an imported link.</summary>
    public const string ImportedLinkedBy = "imported";

    /// <summary>`v0.24.0` F1: the <see cref="IPersistenceStore"/> collection of the importer's own state.</summary>
    public const string StateCollection = "Xero.InvoiceLinkImport";

    /// <summary>`v0.24.0` F1: the key of the moment the import first ran in this workspace (round-trip ISO-8601) — requests sent at or after it went through `v0.24.0`'s sync.</summary>
    public const string FirstRunKey = "first-run";

    /// <summary>`v0.24.0` F1: how many invoices one run asks Xero about at most.</summary>
    public const int DefaultBudget = 10;

    private readonly Func<CancellationToken, Task<IReadOnlyList<LegacyInvoiceLink>>> _source;
    private readonly IXeroLinkStore _links;
    private readonly IPersistenceStore? _state;
    private readonly Func<string, string, CancellationToken, Task<InvoiceCheck>>? _verify;
    private readonly int _budget;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _importedTenants = new(StringComparer.Ordinal);

    /// <summary>Initialises a new instance of the <see cref="XeroInvoiceLinkImporter"/> class.</summary>
    /// <param name="context">Where invoice requests are read from.</param>
    /// <param name="links">Where links are written.</param>
    /// <param name="state">`v0.24.0` F1 (additive): where the importer remembers its first run and each organisation's finished run; <see langword="null"/> remembers per process only and imports every request, as before F1.</param>
    /// <param name="api">`v0.24.0` F1 (additive): the Xero client, to check an invoice exists in the organisation before its link is imported; <see langword="null"/> imports without checking, as before F1.</param>
    public XeroInvoiceLinkImporter(EngineeringDomainContext context, IXeroLinkStore links, IPersistenceStore? state = null, XeroAccountingApi? api = null)
        : this(ReaderOver(context), links, TimeProvider.System, state, api is null ? null : CheckWith(api))
    {
    }

    /// <summary>Test seam (`ADR-0121`): the requests as plain facts, and a pinned clock.</summary>
    internal XeroInvoiceLinkImporter(
        Func<CancellationToken, Task<IReadOnlyList<LegacyInvoiceLink>>> source, IXeroLinkStore links, TimeProvider timeProvider,
        IPersistenceStore? state = null, Func<string, string, CancellationToken, Task<InvoiceCheck>>? verify = null, int budget = DefaultBudget)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budget);

        _source = source;
        _links = links;
        _time = timeProvider;
        _state = state;
        _verify = verify;
        _budget = budget;
    }

    /// <summary>`v0.24.0` F1: the key recording that the import finished for <paramref name="tenantId"/>.</summary>
    /// <param name="tenantId">The organisation.</param>
    public static string RanKey(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return $"ran/{tenantId}";
    }

    /// <summary>
    /// Imports into <paramref name="tenantId"/> until the import has finished
    /// for that organisation (<see cref="XeroInvoiceLinkImportReport.Complete"/>);
    /// afterwards does nothing — remembered across restarts when built with a
    /// state store. A run that fails, is cancelled, or could not ask Xero about
    /// every request is not remembered, so the next call carries on.
    /// </summary>
    /// <returns>The report of the run, or <see langword="null"/> when the import had already finished for this organisation.</returns>
    public async Task<XeroInvoiceLinkImportReport?> EnsureImportedAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_importedTenants.Contains(tenantId))
                return null;

            if (_state is not null && await _state.ReadAsync(StateCollection, RanKey(tenantId), cancellationToken).ConfigureAwait(false) is not null)
            {
                _importedTenants.Add(tenantId);
                return null;
            }

            var report = await ImportCoreAsync(tenantId, _budget, cancellationToken).ConfigureAwait(false);
            if (report.Complete)
                _importedTenants.Add(tenantId);

            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Scans every invoice request and imports a link for each pre-`v0.24.0` Xero send that has none in <paramref name="tenantId"/> (asking Xero about every one, when built with the API).</summary>
    public async Task<XeroInvoiceLinkImportReport> ImportAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ImportCoreAsync(tenantId, int.MaxValue, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<XeroInvoiceLinkImportReport> ImportCoreAsync(string tenantId, int budget, CancellationToken cancellationToken)
    {
        var firstRun = await FirstRunAsync(cancellationToken).ConfigureAwait(false);
        var candidates = await _source(cancellationToken).ConfigureAwait(false);
        var imported = 0;
        var alreadyLinked = 0;
        var notInOrganisation = 0;
        var sentThroughSync = 0;
        var complete = true;
        var conflicting = new List<Guid>();

        foreach (var candidate in candidates.OrderBy(c => c.RequestId))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(candidate.Connector, XeroConnectorName, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(candidate.ExternalId))
            {
                continue;
            }

            // Sent through v0.24.0's own sync: X4 linked it in the organisation it went to.
            if (firstRun is { } since && candidate.SentAtUtc is { } sent && sent >= since)
            {
                sentThroughSync++;
                continue;
            }

            var externalId = candidate.ExternalId.Trim();
            var document = XeroDocumentRef.For(XeroDocumentKind.Invoice, candidate.RequestId);

            try
            {
                if (await _links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false) is { } existing)
                {
                    if (string.Equals(existing.XeroId, externalId, StringComparison.OrdinalIgnoreCase))
                        alreadyLinked++;
                    else
                        conflicting.Add(candidate.RequestId);

                    continue;
                }

                var number = candidate.ExternalInvoiceNumber;
                var status = candidate.ExternalStatus;
                if (_verify is not null)
                {
                    if (_state is not null && await _state.ReadAsync(StateCollection, AbsentKey(tenantId, candidate.RequestId), cancellationToken).ConfigureAwait(false) is not null)
                    {
                        notInOrganisation++;
                        continue;
                    }

                    if (budget <= 0)
                    {
                        complete = false;
                        break;
                    }

                    budget--;
                    var check = await _verify(tenantId, externalId, cancellationToken).ConfigureAwait(false);
                    if (check.Exists is null)
                    {
                        // Xero could not say: nothing is imported on a guess; the next run asks again.
                        complete = false;
                        break;
                    }

                    if (check.Exists == false)
                    {
                        if (_state is not null)
                            await _state.WriteAsync(StateCollection, AbsentKey(tenantId, candidate.RequestId), externalId, cancellationToken).ConfigureAwait(false);

                        notInOrganisation++;
                        continue;
                    }

                    number = check.Number ?? number;
                    status = check.Status ?? status;
                }

                await _links.SaveAsync(
                    new XeroLink(
                        SchemaVersion: XeroLink.CurrentSchemaVersion,
                        TenantId: tenantId,
                        Document: document,
                        XeroId: externalId,
                        XeroNumber: number,
                        LastPushedContentHash: null,
                        LastKnownXeroStatus: status,
                        AttachmentFileName: null,
                        AttachmentContentHash: null,
                        LinkedAtUtc: candidate.SentAtUtc ?? _time.GetUtcNow(),
                        LastReadAtUtc: null,
                        LinkedBy: ImportedLinkedBy),
                    cancellationToken).ConfigureAwait(false);

                imported++;
            }
            catch (Exception ex) when (ex is PersistenceException or InvalidOperationException)
            {
                // An unreadable link, or one written meanwhile naming another
                // record: never overwritten, reported for a person to look at.
                conflicting.Add(candidate.RequestId);
            }
        }

        if (complete && _state is not null)
        {
            await _state.WriteAsync(
                StateCollection, RanKey(tenantId),
                JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["ranAtUtc"] = _time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
                    ["imported"] = imported.ToString(CultureInfo.InvariantCulture),
                    ["notInOrganisation"] = notInOrganisation.ToString(CultureInfo.InvariantCulture),
                    ["sentThroughSync"] = sentThroughSync.ToString(CultureInfo.InvariantCulture),
                }),
                cancellationToken).ConfigureAwait(false);
        }

        return new XeroInvoiceLinkImportReport(imported, alreadyLinked, conflicting)
        {
            NotInOrganisation = notInOrganisation,
            SentThroughSync = sentThroughSync,
            Complete = complete,
        };
    }

    /// <summary>The moment the import first ran in this workspace, recorded the first time (needs a state store; <see langword="null"/> without one).</summary>
    private async Task<DateTimeOffset?> FirstRunAsync(CancellationToken cancellationToken)
    {
        if (_state is null)
            return null;

        var stored = await _state.ReadAsync(StateCollection, FirstRunKey, cancellationToken).ConfigureAwait(false);
        if (stored is not null && DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var recorded))
            return recorded;

        var now = _time.GetUtcNow();
        await _state.WriteAsync(StateCollection, FirstRunKey, now.ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        return now;
    }

    private static string AbsentKey(string tenantId, Guid requestId) => $"absent/{tenantId}/{requestId:D}";

    /// <summary>Asks Xero, through the connected organisation, whether it holds the invoice: found, a 404 (or another refusal of the id), or no answer.</summary>
    internal static Func<string, string, CancellationToken, Task<InvoiceCheck>> CheckWith(XeroAccountingApi api) =>
        async (_, invoiceId, cancellationToken) =>
        {
            var result = await api.GetInvoiceAsync(invoiceId, cancellationToken).ConfigureAwait(false);
            return result.Outcome switch
            {
                ConnectorOutcome.Ok when result.Value is { } invoice => new InvoiceCheck(true, invoice.Status, invoice.InvoiceNumber),
                ConnectorOutcome.Rejected => new InvoiceCheck(false, null, null),
                _ => new InvoiceCheck(null, null, null),
            };
        };

    private static Func<CancellationToken, Task<IReadOnlyList<LegacyInvoiceLink>>> ReaderOver(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return async cancellationToken =>
        {
            var entries = await context.Repository.ListByKindAsync(InvoiceRequest.CanonicalKind, cancellationToken).ConfigureAwait(false);
            var requests = await context.Repository.MaterialiseAsync<InvoiceRequest>(entries, cancellationToken).ConfigureAwait(false);

            return requests
                .Select(r => new LegacyInvoiceLink(r.Id, r.Connector, r.ExternalId, r.ExternalInvoiceNumber, r.ExternalStatus, r.SentAtUtc))
                .ToList();
        };
    }

    /// <summary>The facts of one <see cref="InvoiceRequest"/> the import reads.</summary>
    internal sealed record LegacyInvoiceLink(
        Guid RequestId, string? Connector, string? ExternalId, string? ExternalInvoiceNumber, string? ExternalStatus, DateTimeOffset? SentAtUtc);

    /// <summary>What Xero said of one invoice id in the organisation: <paramref name="Exists"/> <see langword="true"/> (with its status and number), <see langword="false"/> (not held there), or <see langword="null"/> (no answer).</summary>
    internal sealed record InvoiceCheck(bool? Exists, string? Status, string? Number);
}
