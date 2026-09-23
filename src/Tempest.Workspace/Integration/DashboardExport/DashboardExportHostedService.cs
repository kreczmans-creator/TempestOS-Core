using Tempest.Core.BackgroundServices;
using Tempest.Core.BusinessGovernance.Contracts;
using Tempest.Core.BusinessGovernance.Quotations;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ExportImport;
using Tempest.Core.Logging;
using Tempest.Core.Navigation;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Periodically writes <c>engineering-status.json</c>/<c>programme.json</c>/
/// <c>contracts.json</c>/<c>quotes.json</c> to
/// <see cref="DashboardExportOptions.ExportDirectory"/>, on
/// <see cref="DashboardExportOptions.IntervalSeconds"/> — the Core-side half
/// of the Core→Dashboard integration contract (§3.4): Core has no outbound
/// network integration of its own, so this only ever writes a local file; a
/// separate push agent on the same machine (Tempest-Dashboard's own
/// <c>agents/tempest-core-agent.ps1</c>) reads it and POSTs it on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first timer-driven <see cref="IHostedService"/> in this
/// codebase</b> — no existing implementation to mirror
/// (<c>Tempest.Samples.NotificationSampleHostedService</c> fires once on
/// start/stop only; no <see cref="PeriodicTimer"/> usage existed anywhere
/// under <c>src/Tempest.Core</c> before this). <see cref="PeriodicTimer"/>
/// is BCL, consistent with <c>ADR-0005</c>'s "no
/// <c>Microsoft.Extensions.Hosting</c>" stance this interface's own doc
/// comment already cites.
/// </para>
/// <para>
/// No manual registration is needed — this concrete, non-abstract
/// <see cref="IHostedService"/> is found by
/// <see cref="HostedServiceDiscoveryService.DiscoverHostedServiceTypes()"/>'s
/// own reflection scan of every loaded assembly and registered as a DI
/// singleton by <see cref="HostedServiceCollectionExtensions.AddDiscoveredHostedServices"/> —
/// exactly the same, assembly-agnostic convention every other
/// <see cref="IHostedService"/> in this platform already relies on. That
/// registration covers only this type itself, not its own dependencies
/// (<c>Tempest.Core.DependencyInjection.TempestServiceProvider</c>'s own
/// constructor-injection is strict — every parameter type needs its own
/// registration, with no implicit auto-wiring for an unregistered concrete
/// type), so this constructor deliberately takes only ordinary,
/// already-platform-registered services and constructs
/// <see cref="EngineeringStatusExportAdapter"/>/<see cref="ProgrammeHierarchyExportAdapter"/>/
/// <see cref="ContractsExportAdapter"/>/<see cref="QuotesExportAdapter"/>
/// itself, composition-root style (`new`), exactly as it constructs the
/// Cockpit read models it reuses — rather than declaring them as
/// constructor parameters, which would need a registration this Work
/// Package has no natural place to add (Core's own central service
/// registration, <c>TempestHost.cs</c>, cannot reference a
/// <c>Tempest.Workspace</c> type at all).
/// </para>
/// <para>
/// Isolated by default (`ADR-0021`) — does not implement
/// <see cref="ICriticalBackgroundService"/>: an export failure (a bad
/// directory, a locked file, a transient read fault) must never be
/// Host-fatal, and is logged and retried on the next tick instead.
/// </para>
/// </remarks>
public sealed class DashboardExportHostedService : IHostedService
{
    private readonly EngineeringStatusExportAdapter _engineeringStatus;
    private readonly ProgrammeHierarchyExportAdapter _programme;
    private readonly ContractsExportAdapter _contracts;
    private readonly QuotesExportAdapter _quotes;
    private readonly IConfigurationProvider _configuration;
    private readonly ILogger? _logger;

    private readonly SemaphoreSlim _exportGate = new(1, 1);

    private PeriodicTimer? _timer;
    private Task? _loop;
    private CancellationTokenSource? _cts;

    /// <summary>Initialises a new instance of the <see cref="DashboardExportHostedService"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository — passed straight through to both freshly-constructed adapters.</param>
    /// <param name="requirementsService">The Requirements Framework's own service — passed straight through to <see cref="EngineeringStatusExportAdapter"/>.</param>
    /// <param name="requirementValidationService">The Requirements Framework's own validation service — passed straight through to <see cref="EngineeringStatusExportAdapter"/>.</param>
    /// <param name="navigationProvider">The Platform's own navigation provider — passed straight through to <see cref="EngineeringStatusExportAdapter"/>, whose headless <c>EngineeringCockpit</c> (schema v2) needs one to construct; never consulted by anything exported.</param>
    /// <param name="commandRegistry">The Platform's own command registry — likewise passed straight through to <see cref="EngineeringStatusExportAdapter"/> for its <c>EngineeringCockpit</c>.</param>
    /// <param name="contractCatalog">The issued-contract library — passed straight through to <see cref="ContractsExportAdapter"/> (`ADR-0154`).</param>
    /// <param name="quotationCatalog">The quotation library — passed straight through to <see cref="QuotesExportAdapter"/> (`ADR-0154`).</param>
    /// <param name="configuration">Read once per tick for <see cref="DashboardExportOptions.ExportDirectory"/> — a directory change takes effect on the very next export, no restart required.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public DashboardExportHostedService(
        EngineeringDomainContext domainContext,
        IRequirementsService requirementsService,
        IRequirementValidationService requirementValidationService,
        INavigationProvider navigationProvider,
        ICommandRegistry commandRegistry,
        IIssuedContractCatalog contractCatalog,
        IQuotationCatalog quotationCatalog,
        IConfigurationProvider configuration,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(requirementValidationService);
        ArgumentNullException.ThrowIfNull(navigationProvider);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(contractCatalog);
        ArgumentNullException.ThrowIfNull(quotationCatalog);
        ArgumentNullException.ThrowIfNull(configuration);

        _engineeringStatus = new EngineeringStatusExportAdapter(domainContext, requirementsService, requirementValidationService, navigationProvider, commandRegistry);
        _programme = new ProgrammeHierarchyExportAdapter(domainContext, requirementsService, requirementValidationService, navigationProvider, commandRegistry);
        _contracts = new ContractsExportAdapter(contractCatalog);
        _quotes = new QuotesExportAdapter(quotationCatalog);
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>The most recent export's own outcome, for diagnostics — <see langword="null"/> before the first tick completes.</summary>
    public DateTimeOffset? LastExportAttemptedAt { get; private set; }

    /// <summary>Whether the most recent export attempt succeeded. <see langword="null"/> before the first tick completes.</summary>
    public bool? LastExportSucceeded { get; private set; }

    /// <summary>The exception the most recent export attempt failed with, or <see langword="null"/> if it succeeded (or none has run yet).</summary>
    public Exception? LastExportException { get; private set; }

    /// <inheritdoc />
    /// <remarks>Never blocks Host start on the first export — the loop's first tick runs immediately, but asynchronously, on its own <see cref="Task"/>.</remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var intervalSeconds = DashboardExportOptions.FromConfiguration(_configuration).IntervalSeconds;

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        do
        {
            await ExportOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        while (await WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _timer!.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes every export file once, immediately — the manual "export now"
    /// path (<see cref="ExportDashboardDataNowCommandHandler"/>) and the
    /// timer loop's own per-tick body both call this, so there is exactly
    /// one place that decides what an export attempt does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writes each file to a <c>.tmp</c> sibling first, then
    /// <see cref="File.Move(string, string, bool)"/>s it over the final
    /// name — an atomic rename (on the same volume, which a single fixed
    /// export directory always is) so the push agent reading the final
    /// name never observes a half-written file. A failure is caught,
    /// logged, and reported through <see cref="LastExportSucceeded"/>
    /// rather than thrown — see this class's own remarks on isolation.
    /// </para>
    /// <para>
    /// <see cref="_exportGate"/> serialises overlapping calls — a manual
    /// "export now" landing while a scheduled tick is already mid-write, or
    /// vice versa, both target the identical <c>.tmp</c> path, and a second
    /// writer moving a file the first writer already renamed away throws
    /// <see cref="FileNotFoundException"/> (found directly, by this
    /// exact race, under concurrent test load). A concurrent caller simply
    /// waits for the in-flight attempt to finish — its own request is
    /// satisfied by the export that was already running, not by running a
    /// redundant second one.
    /// </para>
    /// </remarks>
    public async Task ExportOnceAsync(CancellationToken cancellationToken = default)
    {
        await _exportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LastExportAttemptedAt = DateTimeOffset.UtcNow;

            var directory = DashboardExportOptions.FromConfiguration(_configuration).ExportDirectory;
            Directory.CreateDirectory(directory);

            await WriteAsync(_engineeringStatus, Path.Combine(directory, "engineering-status.json"), cancellationToken).ConfigureAwait(false);
            await WriteAsync(_programme, Path.Combine(directory, "programme.json"), cancellationToken).ConfigureAwait(false);
            await WriteAsync(_contracts, Path.Combine(directory, "contracts.json"), cancellationToken).ConfigureAwait(false);
            await WriteAsync(_quotes, Path.Combine(directory, "quotes.json"), cancellationToken).ConfigureAwait(false);

            LastExportSucceeded = true;
            LastExportException = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastExportSucceeded = false;
            LastExportException = ex;
            _logger?.Error("Dashboard export failed.", ex);
        }
        finally
        {
            _exportGate.Release();
        }
    }

    private static async Task WriteAsync(IExportable exportable, string finalPath, CancellationToken cancellationToken)
    {
        var tempPath = finalPath + ".tmp";

        await using (var file = File.Create(tempPath))
            await exportable.ExportAsync(file, cancellationToken).ConfigureAwait(false);

        File.Move(tempPath, finalPath, overwrite: true);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        if (_loop is not null)
            await _loop.ConfigureAwait(false);

        _timer?.Dispose();
    }
}
