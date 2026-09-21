using System.Globalization;
using System.Text.Json;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Contracts;
using Tempest.Core.ExportImport;
using Tempest.Core.ReferenceData;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Exports the issued-contract library in Tempest-Dashboard's own
/// <c>Contract</c> shape — <c>contracts.json</c>, read by the dashboard's
/// <c>contracts</c> connector (<c>server/connectors/tempestos.js</c>) as
/// <c>{ contracts: [ … ] }</c>. Implements <see cref="IExportable"/>/
/// <see cref="IExportableKind"/> exactly as
/// <see cref="EngineeringStatusExportAdapter"/> does — see that class's
/// own remarks for why <see cref="DashboardExportHostedService"/> calls
/// <see cref="ExportAsync"/> directly rather than through
/// <see cref="IExportService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shape.</b> Each entry is the dashboard's documented <c>Contract</c>
/// (<c>docs/DATA-CONTRACTS.md</c>, "Contract"): <c>id, reference, client,
/// title, status, sentDate?, startDate?, endDate?, renewalDate?, source,
/// url</c>. Dates are <c>yyyy-MM-dd</c>, which the dashboard's own
/// <c>daysUntil</c> parses as a local calendar date. <c>daysToRenewal</c>,
/// <c>daysToEnd</c> and <c>state</c> are the dashboard's own derivations
/// and are deliberately not written here.
/// </para>
/// <para>
/// <b>Status mapping.</b> Core's eight-valued <see cref="ContractStatus"/>
/// keeps commercial position apart from record state (`ADR-0129`); the
/// dashboard has five buckets. <see cref="MapStatus"/> is the one
/// statement of the mapping:
/// </para>
/// <list type="table">
/// <item><term><see cref="ContractStatus.Draft"/></term><description><c>draft</c></description></item>
/// <item><term><see cref="ContractStatus.InNegotiation"/></term><description><c>awaiting-signature</c> — sent and unsigned; the dashboard's bucket for "out with the other party", which is what negotiation is from a chasing dashboard's point of view.</description></item>
/// <item><term><see cref="ContractStatus.AwaitingSignature"/></term><description><c>awaiting-signature</c></description></item>
/// <item><term><see cref="ContractStatus.Executed"/></term><description><c>active</c> — the dashboard itself derives <c>expiring</c> from <c>endDate</c>, so a contract still recorded as Executed past its term reads as active-and-expired, exactly the stale record <c>IssuedContractValidationService</c> already reports.</description></item>
/// <item><term><see cref="ContractStatus.Expired"/>, <see cref="ContractStatus.Terminated"/>, <see cref="ContractStatus.Lapsed"/>, <see cref="ContractStatus.Superseded"/></term><description><c>expired</c> — the dashboard's only bucket for a contract that is over and will not become active; the successor of a Superseded contract is exported in its own right.</description></item>
/// </list>
/// <para>
/// No Core status maps to <c>on-hold</c>: Core has no paused contract
/// state, and inventing one here would be a fabrication.
/// </para>
/// <para>
/// <b>Fields Core does not hold</b> are written as <see langword="null"/>
/// rather than guessed: <c>sentDate</c> (Core records when a contract was
/// executed, not when it was sent), <c>renewalDate</c> (no renewal concept
/// on <see cref="IssuedContract"/>) and <c>url</c> (Core has no web
/// surface). <c>startDate</c>/<c>endDate</c> are the contract's own
/// <see cref="IssuedContract.Term"/>. <c>client</c> is the party whose
/// <see cref="ContractParty.Role"/> is "Client", ignoring case, else the
/// first party named.
/// </para>
/// <para>
/// <b>Which records.</b> The current revision of every registered contract
/// whose record is not itself <see cref="ReferenceValidationState.Superseded"/>
/// — a superseded record is history, and reporting it alongside the record
/// that replaced it would double-count, exactly as
/// <see cref="ContractService.ReportObligationsAsync"/> reasons. The
/// record's own validation state is not a filter: a Draft contract in a
/// Draft record is precisely what a dashboard should show as a draft.
/// </para>
/// </remarks>
public sealed class ContractsExportAdapter : IExportable, IExportableKind
{
    /// <summary>The schema version this adapter's own payload shape uses.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The <c>source</c> every exported entry carries, naming where the dashboard got it from.</summary>
    public const string Source = "tempestos";

    private readonly IIssuedContractCatalog _contracts;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ContractsExportAdapter"/> class.</summary>
    /// <param name="contracts">The issued-contract library this adapter reads.</param>
    /// <param name="timeProvider"><see langword="null"/> — the default — uses <see cref="TimeProvider.System"/>, mirroring <see cref="EngineeringStatusExportAdapter"/>'s own convention.</param>
    public ContractsExportAdapter(IIssuedContractCatalog contracts, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(contracts);

        _contracts = contracts;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Kind => "dashboard.contracts";

    /// <inheritdoc />
    public int SchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var records = await _contracts.ListAsync(cancellationToken).ConfigureAwait(false);

        var entries = records
            .Where(r => r.ValidationState != ReferenceValidationState.Superseded)
            .Select(ToEntry)
            .OrderBy(e => e.Reference, StringComparer.Ordinal)
            .ToList();

        var export = new ContractsExport(SchemaVersion, EngineeringStatusExportAdapter.FormatUtc(_time.GetUtcNow()), entries);

        await JsonSerializer.SerializeAsync(destination, export, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Maps Core's <see cref="ContractStatus"/> onto the dashboard's <c>Contract.status</c> vocabulary — see this class's own remarks for the reasoning behind each row.</summary>
    public static string MapStatus(ContractStatus status) => status switch
    {
        ContractStatus.Draft => "draft",
        ContractStatus.InNegotiation => "awaiting-signature",
        ContractStatus.AwaitingSignature => "awaiting-signature",
        ContractStatus.Executed => "active",
        ContractStatus.Expired => "expired",
        ContractStatus.Terminated => "expired",
        ContractStatus.Lapsed => "expired",
        ContractStatus.Superseded => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Every ContractStatus must map onto a dashboard status."),
    };

    /// <summary>The party a dashboard calls the client: the one contracting as "Client", else the first party named.</summary>
    public static string ClientOf(IssuedContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var client = contract.Parties.FirstOrDefault(p => string.Equals(p.Role, "Client", StringComparison.OrdinalIgnoreCase))
                     ?? contract.Parties.FirstOrDefault();

        return client?.LegalName ?? string.Empty;
    }

    internal static string? FormatDate(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static ContractEntry ToEntry(IReferenceRecord<IssuedContract> record)
    {
        var contract = record.Definition;

        return new ContractEntry(
            record.Id,
            contract.Reference,
            ClientOf(contract),
            contract.Title,
            MapStatus(contract.Status),
            SentDate: null,
            FormatDate(contract.Term?.From),
            FormatDate(contract.Term?.To),
            RenewalDate: null,
            Source,
            Url: null);
    }

    private sealed record ContractsExport(int SchemaVersion, string GeneratedAt, IReadOnlyList<ContractEntry> Contracts);

    private sealed record ContractEntry(
        string Id,
        string Reference,
        string Client,
        string Title,
        string Status,
        string? SentDate,
        string? StartDate,
        string? EndDate,
        string? RenewalDate,
        string Source,
        string? Url);
}
