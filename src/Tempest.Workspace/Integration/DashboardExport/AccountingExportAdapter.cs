using System.Globalization;
using System.Text.Json;
using Tempest.Core.ExportImport;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Writes <c>accounting.json</c>, the ledger the dashboard shows
/// (Tempest-Dashboard <c>docs/DATA-CONTRACTS.md</c> §Accounting): Core holds
/// the one Xero connection, so the panel never opens its own. Rows, not
/// totals: receivable invoices and payable bills still outstanding, and
/// receivables paid in the last 30 days, each in the dashboard's
/// provider-neutral Invoice shape. Every figure the dashboard shows
/// (outstanding, due in 7/30 days, overdue, aging, FX) is derived there,
/// from these arrays, so they are never capped.
/// </summary>
/// <remarks>
/// <para>
/// The three reads are exactly the queries the dashboard's own Xero adapter
/// made, and each walks every page (<see cref="XeroAccountingApi.ListInvoicesAsync"/>).
/// </para>
/// <para>
/// <b>All or nothing.</b> Any read that fails throws
/// <see cref="AccountingExportUnavailableException"/> before anything is
/// written, so <see cref="DashboardExportHostedService"/> leaves the last good
/// <c>accounting.json</c> in place, where the dashboard flags it stale with
/// time, rather than replacing it with an empty or partial ledger that would
/// understate the money.
/// </para>
/// </remarks>
public sealed class AccountingExportAdapter : IExportable, IExportableKind
{
    /// <summary>The schema version of <c>accounting.json</c>.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>How far back <c>paid</c> reaches: the dashboard's "Received 30d".</summary>
    public const int PaidWindowDays = 30;

    private const string Source = "xero";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly XeroAccountingApi _xero;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="AccountingExportAdapter"/> class.</summary>
    /// <param name="xero">Core's Xero API — read only here.</param>
    /// <param name="timeProvider">The clock for <c>generatedAt</c> and the paid window.</param>
    public AccountingExportAdapter(XeroAccountingApi xero, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(xero);
        _xero = xero;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Kind => "dashboard.accounting";

    /// <inheritdoc />
    public int SchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    /// <exception cref="AccountingExportUnavailableException">A Xero read failed; nothing was written.</exception>
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var now = _time.GetUtcNow();
        var since = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-PaidWindowDays);
        var sinceExpression = string.Create(CultureInfo.InvariantCulture, $"DateTime({since.Year},{since.Month},{since.Day})");

        var receivable = await ReadAsync("""Type=="ACCREC" AND Status=="AUTHORISED" """.Trim(), "DueDate", "receivable invoices", cancellationToken).ConfigureAwait(false);
        var payable = await ReadAsync("""Type=="ACCPAY" AND Status=="AUTHORISED" """.Trim(), "DueDate", "bills", cancellationToken).ConfigureAwait(false);
        var paid = await ReadAsync($"""Type=="ACCREC" AND Status=="PAID" AND FullyPaidOnDate>={sinceExpression}""", "UpdatedDateUTC DESC", "paid invoices", cancellationToken).ConfigureAwait(false);

        var export = new AccountingExport(
            SchemaVersion,
            EngineeringStatusExportAdapter.FormatUtc(now),
            [.. receivable.Select(ToOutstanding)],
            [.. payable.Select(ToOutstanding)],
            [.. paid.Select(ToPaid)],
            Cash: null);

        await JsonSerializer.SerializeAsync(destination, export, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<XeroWireInvoice>> ReadAsync(string where, string order, string what, CancellationToken cancellationToken)
    {
        var result = await _xero.ListInvoicesAsync(where, order, cancellationToken).ConfigureAwait(false);
        return result.Outcome == ConnectorOutcome.Ok
            ? result.Value!
            : throw new AccountingExportUnavailableException($"Could not read the {what} from Xero ({result.Outcome}): {result.Reason}");
    }

    private static OutstandingRow ToOutstanding(XeroWireInvoice invoice) => new(
        invoice.InvoiceID!,
        invoice.InvoiceNumber ?? invoice.InvoiceID!,
        invoice.Contact?.Name ?? "Unknown",
        invoice.AmountDue ?? 0m,
        invoice.CurrencyCode ?? "GBP",
        Date(invoice.Date),
        Date(invoice.DueDate),
        Source,
        UrlOf(invoice));

    private static PaidRow ToPaid(XeroWireInvoice invoice) => new(
        invoice.InvoiceID!,
        invoice.InvoiceNumber ?? invoice.InvoiceID!,
        invoice.Contact?.Name ?? "Unknown",
        invoice.AmountPaid ?? invoice.Total ?? 0m,
        invoice.CurrencyCode ?? "GBP",
        Date(invoice.FullyPaidOnDate) ?? Date(invoice.UpdatedDateUTC),
        Source,
        UrlOf(invoice));

    private static string? Date(string? raw) =>
        XeroWire.ParseDate(raw)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string UrlOf(XeroWireInvoice invoice) =>
        $"https://go.xero.com/app/invoicing/edit/{Uri.EscapeDataString(invoice.InvoiceID!)}";

    private sealed record AccountingExport(
        int SchemaVersion, string GeneratedAt,
        IReadOnlyList<OutstandingRow> Invoices, IReadOnlyList<OutstandingRow> Bills, IReadOnlyList<PaidRow> Paid,
        object? Cash);

    private sealed record OutstandingRow(
        string Id, string Reference, string Client, decimal AmountDue, string Currency,
        string? IssueDate, string? DueDate, string Source, string Url);

    private sealed record PaidRow(
        string Id, string Reference, string Client, decimal AmountPaid, string Currency,
        string? PaidDate, string Source, string Url);
}

/// <summary>A Xero read behind <c>accounting.json</c> failed, so no ledger was written and the last good file stands.</summary>
public sealed class AccountingExportUnavailableException(string message) : Exception(message);
