using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>What one run of <see cref="XeroInvoiceLinkImporter"/> did.</summary>
/// <param name="Imported">Links written (<see cref="XeroLink.LinkedBy"/> <c>"imported"</c>).</param>
/// <param name="AlreadyLinked">Requests that already had a link to the same Xero invoice — left as they are.</param>
/// <param name="ConflictingRequestIds">Requests whose existing link names a different Xero record, or cannot be read — never overwritten; shown so a person can look.</param>
public sealed record XeroInvoiceLinkImportReport(int Imported, int AlreadyLinked, IReadOnlyList<Guid> ConflictingRequestIds);

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
/// <b>Idempotent.</b> A second run over the same requests writes nothing.
/// <see cref="EnsureImportedAsync"/> runs the import once per tenant per
/// process ("on first read"); <see cref="ImportAsync"/> always scans.
/// </para>
/// <para>
/// <b>Which tenant.</b> A request sent before `v0.24.0` recorded no Xero
/// organisation; its link is imported into the tenant the caller names — the
/// organisation connected when the engine first reads links. A link imported
/// into the wrong organisation names an invoice Xero does not hold there:
/// reading it back answers 404 (<i>Deleted in Xero</i>, offer Unlink), and
/// nothing is ever created from it.
/// </para>
/// </remarks>
public sealed class XeroInvoiceLinkImporter
{
    /// <summary>The <see cref="IInvoicingConnector.Name"/> of the Xero connector, as <see cref="InvoiceRequest.Connector"/> records it.</summary>
    public const string XeroConnectorName = "Xero";

    /// <summary>The <see cref="XeroLink.LinkedBy"/> of an imported link.</summary>
    public const string ImportedLinkedBy = "imported";

    private readonly Func<CancellationToken, Task<IReadOnlyList<LegacyInvoiceLink>>> _source;
    private readonly IXeroLinkStore _links;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _importedTenants = new(StringComparer.Ordinal);

    /// <summary>Initialises a new instance of the <see cref="XeroInvoiceLinkImporter"/> class.</summary>
    /// <param name="context">Where invoice requests are read from.</param>
    /// <param name="links">Where links are written.</param>
    public XeroInvoiceLinkImporter(EngineeringDomainContext context, IXeroLinkStore links)
        : this(ReaderOver(context), links, TimeProvider.System)
    {
    }

    /// <summary>Test seam (`ADR-0121`): the requests as plain facts, and a pinned clock.</summary>
    internal XeroInvoiceLinkImporter(
        Func<CancellationToken, Task<IReadOnlyList<LegacyInvoiceLink>>> source, IXeroLinkStore links, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _source = source;
        _links = links;
        _time = timeProvider;
    }

    /// <summary>
    /// Imports into <paramref name="tenantId"/> the first time it is called
    /// for that tenant in this process; afterwards does nothing. A run that
    /// fails or is cancelled is not remembered, so the next call tries again.
    /// </summary>
    /// <returns>The report of the run, or <see langword="null"/> when this tenant was already imported in this process.</returns>
    public async Task<XeroInvoiceLinkImportReport?> EnsureImportedAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_importedTenants.Contains(tenantId))
                return null;

            var report = await ImportCoreAsync(tenantId, cancellationToken).ConfigureAwait(false);
            _importedTenants.Add(tenantId);
            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Scans every invoice request and imports a link for each pre-`v0.24.0` Xero send that has none in <paramref name="tenantId"/>.</summary>
    public async Task<XeroInvoiceLinkImportReport> ImportAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ImportCoreAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<XeroInvoiceLinkImportReport> ImportCoreAsync(string tenantId, CancellationToken cancellationToken)
    {
        var candidates = await _source(cancellationToken).ConfigureAwait(false);
        var imported = 0;
        var alreadyLinked = 0;
        var conflicting = new List<Guid>();

        foreach (var candidate in candidates.OrderBy(c => c.RequestId))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(candidate.Connector, XeroConnectorName, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(candidate.ExternalId))
            {
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

                await _links.SaveAsync(
                    new XeroLink(
                        SchemaVersion: XeroLink.CurrentSchemaVersion,
                        TenantId: tenantId,
                        Document: document,
                        XeroId: externalId,
                        XeroNumber: candidate.ExternalInvoiceNumber,
                        LastPushedContentHash: null,
                        LastKnownXeroStatus: candidate.ExternalStatus,
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

        return new XeroInvoiceLinkImportReport(imported, alreadyLinked, conflicting);
    }

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
}
