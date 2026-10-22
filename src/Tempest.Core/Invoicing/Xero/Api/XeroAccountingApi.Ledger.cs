using System.Globalization;

namespace Tempest.Core.Invoicing.Xero.Api;

public sealed partial class XeroAccountingApi
{
    /// <summary>Xero's page size for <c>GET Invoices</c> with <c>page=</c>.</summary>
    public const int InvoicesPageSize = 100;

    /// <summary>
    /// A hard stop on <see cref="ListInvoicesAsync"/>'s paging, not an
    /// expectation: 100 pages is 10,000 invoices. Reaching it answers a
    /// failure rather than a partial list, because a ledger summed from a
    /// truncated list understates every figure built on it.
    /// </summary>
    public const int MaximumInvoicePages = 100;

    /// <summary>
    /// Every invoice or bill matching <paramref name="where"/>
    /// (<c>GET Invoices?where=…&amp;order=…&amp;page=n</c>), walking every page
    /// (the ledger export for the dashboard). Xero returns 100 at a time and
    /// gives no total count, so a full page is the only sign that more exist;
    /// a short page is the last. Read only.
    /// </summary>
    /// <param name="where">Xero's <c>where</c> filter, e.g. <c>Type=="ACCREC" AND Status=="AUTHORISED"</c>.</param>
    /// <param name="order">Xero's <c>order</c>, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    public async Task<XeroApiResult<IReadOnlyList<XeroWireInvoice>>> ListInvoicesAsync(
        string where, string? order = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(where);

        var invoices = new List<XeroWireInvoice>();
        XeroApiResult<XeroWireInvoicesEnvelope>? last = null;

        for (var page = 1; page <= MaximumInvoicePages; page++)
        {
            last = await GetAsync<XeroWireInvoicesEnvelope>(
                "Invoices",
                [new("where", where), new("order", order), new("page", page.ToString(CultureInfo.InvariantCulture))],
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (last.Outcome != ConnectorOutcome.Ok)
                return Retype<XeroWireInvoicesEnvelope, IReadOnlyList<XeroWireInvoice>>(last);

            var items = last.Value!.Invoices ?? [];
            invoices.AddRange(items.Where(i => !string.IsNullOrWhiteSpace(i.InvoiceID)));
            if (items.Count < InvoicesPageSize)
                return new XeroApiResult<IReadOnlyList<XeroWireInvoice>>(ConnectorOutcome.Ok, invoices, last.HttpStatus, null, []);
        }

        return Failure<IReadOnlyList<XeroWireInvoice>>(
            ConnectorOutcome.Unknown, last?.HttpStatus,
            $"Xero still had more invoices after {MaximumInvoicePages} pages ({invoices.Count} read); the ledger is larger than this read allows, so no partial list is given.");
    }
}
