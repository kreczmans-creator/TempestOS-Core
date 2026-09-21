using System.Text.Json;
using Tempest.Core.BusinessGovernance.Quotations;
using Tempest.Core.ExportImport;
using Tempest.Core.ReferenceData;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Exports the quotation library in Tempest-Dashboard's own <c>Quote</c>
/// shape — <c>quotes.json</c>, read by the dashboard's <c>quotes</c>
/// connector (<c>server/connectors/tempestos.js</c>) as
/// <c>{ quotes: [ … ] }</c>. Implements <see cref="IExportable"/>/
/// <see cref="IExportableKind"/> exactly as
/// <see cref="EngineeringStatusExportAdapter"/> does — see that class's
/// own remarks for why <see cref="DashboardExportHostedService"/> calls
/// <see cref="ExportAsync"/> directly rather than through
/// <see cref="IExportService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shape.</b> Each entry is the dashboard's documented <c>Quote</c>
/// (<c>docs/DATA-CONTRACTS.md</c>, "Quote"): <c>id, reference, client,
/// amount, currency, status, submittedDate, followUpDate?, source</c>.
/// <c>amount</c> is a plain number in <c>currency</c>'s minor units as
/// <see cref="Core.BusinessGovernance.Money"/> states it; <c>currency</c>
/// is the ISO code. Dates are <c>yyyy-MM-dd</c>. <c>daysToFollowUp</c> is
/// the dashboard's own derivation and is deliberately not written here.
/// </para>
/// <para>
/// <b>Status.</b> <see cref="QuotationStatus"/> was modelled on the
/// dashboard's own four-valued vocabulary (<c>draft | submitted |
/// accepted | declined</c>), so the mapping is the enum name in lower
/// case, with nothing lost either way. <c>submittedDate</c> is
/// <see langword="null"/> for a draft, which is the honest value: the
/// dashboard's <c>daysUntil</c> returns <see langword="null"/> for it
/// rather than a number.
/// </para>
/// <para>
/// <b>Which records.</b> As <see cref="ContractsExportAdapter"/>: the
/// current revision of every registered quotation whose record is not
/// itself <see cref="ReferenceValidationState.Superseded"/>.
/// </para>
/// </remarks>
public sealed class QuotesExportAdapter : IExportable, IExportableKind
{
    /// <summary>The schema version this adapter's own payload shape uses.</summary>
    public const int CurrentSchemaVersion = 1;

    private readonly IQuotationCatalog _quotations;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="QuotesExportAdapter"/> class.</summary>
    /// <param name="quotations">The quotation library this adapter reads.</param>
    /// <param name="timeProvider"><see langword="null"/> — the default — uses <see cref="TimeProvider.System"/>, mirroring <see cref="EngineeringStatusExportAdapter"/>'s own convention.</param>
    public QuotesExportAdapter(IQuotationCatalog quotations, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(quotations);

        _quotations = quotations;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Kind => "dashboard.quotes";

    /// <inheritdoc />
    public int SchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var records = await _quotations.ListAsync(cancellationToken).ConfigureAwait(false);

        var entries = records
            .Where(r => r.ValidationState != ReferenceValidationState.Superseded)
            .Select(ToEntry)
            .OrderBy(e => e.Reference, StringComparer.Ordinal)
            .ToList();

        var export = new QuotesExport(SchemaVersion, EngineeringStatusExportAdapter.FormatUtc(_time.GetUtcNow()), entries);

        await JsonSerializer.SerializeAsync(destination, export, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Maps <see cref="QuotationStatus"/> onto the dashboard's <c>Quote.status</c> vocabulary: the same four words, in lower case.</summary>
    public static string MapStatus(QuotationStatus status) => status switch
    {
        QuotationStatus.Draft => "draft",
        QuotationStatus.Submitted => "submitted",
        QuotationStatus.Accepted => "accepted",
        QuotationStatus.Declined => "declined",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Every QuotationStatus must map onto a dashboard status."),
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static QuoteEntry ToEntry(IReferenceRecord<Quotation> record)
    {
        var quotation = record.Definition;

        return new QuoteEntry(
            record.Id,
            quotation.Reference,
            quotation.Client.LegalName,
            quotation.Amount.Amount,
            quotation.Amount.Currency.ToString(),
            MapStatus(quotation.Status),
            ContractsExportAdapter.FormatDate(quotation.SubmittedOn),
            ContractsExportAdapter.FormatDate(quotation.FollowUpOn),
            ContractsExportAdapter.Source);
    }

    private sealed record QuotesExport(int SchemaVersion, string GeneratedAt, IReadOnlyList<QuoteEntry> Quotes);

    private sealed record QuoteEntry(
        string Id,
        string Reference,
        string Client,
        decimal Amount,
        string Currency,
        string Status,
        string? SubmittedDate,
        string? FollowUpDate,
        string Source);
}
