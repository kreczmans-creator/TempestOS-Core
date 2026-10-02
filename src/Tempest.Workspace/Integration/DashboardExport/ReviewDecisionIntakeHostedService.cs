using System.Text.Json;
using Tempest.Core.BackgroundServices;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Requirements;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Verification;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Periodically reads pending review decisions from
/// <see cref="ReviewDecisionIntakeOptions.IntakeDirectory"/> and dispatches
/// each one to the real lifecycle command its target's own discipline
/// already uses (`ADR-0162`). The mirror of
/// <see cref="DashboardExportHostedService"/>, in the opposite direction:
/// that service writes local files a push agent reads and POSTs out; this
/// one reads local files the same agent wrote, pulled from Tempest-Dashboard
/// on its own existing poll. Neither service ever makes a network call —
/// Core still has no outbound network integration of its own, and gains no
/// inbound listener either.
/// </summary>
/// <remarks>
/// <para>
/// <b>Switched off by default, and off means off, not quiet.</b> When
/// <see cref="ReviewDecisionIntakeOptions.Enabled"/> is <see langword="false"/>
/// (`ADR-0162` Decision 3), <see cref="StartAsync"/> never starts the timer
/// loop at all — the intake directory is not created, not listed, not
/// read. Flipping the flag on takes effect from the next Host start, the
/// same deployment-time-fact convention <see cref="DashboardExportOptions"/>
/// already establishes for its own directory and interval.
/// </para>
/// <para>
/// <b>Four disciplines, one each, never a fifth.</b> The target Kind
/// decides which existing command runs — <see cref="SetDocumentStatusCommand"/>
/// for <see cref="ReviewQueueExportAdapter.LifecycleKinds"/>'s own Document
/// family, <see cref="SetCalculationStatusCommand"/> for its Calculation
/// entry, <see cref="SetVerificationActivityStatusCommand"/> for its
/// Verification Activity entry, <see cref="SetRequirementStatusCommand"/>
/// for <see cref="RequirementsService.RequirementDocumentKind"/>. Reusing
/// <see cref="ReviewQueueExportAdapter.LifecycleKinds"/> directly, rather
/// than a second, hand-maintained list, is deliberate: the set of Kinds
/// this service will act on can never drift from the set
/// <c>reviews.json</c> itself reports as awaiting review. Evidence is not
/// in that list and is refused by name, not silently ignored (`TD-189`).
/// </para>
/// <para>
/// Discovered by reflection exactly as <see cref="DashboardExportHostedService"/>
/// is (see that class's own remarks) — no manual registration. Isolated by
/// default (`ADR-0021`): a bad intent file, a locked directory, or a
/// transient read fault is logged and the next tick tries again; this
/// service never brings the Host down. One intent's own failure (a Kind
/// this service does not handle, a transition the target's own lifecycle
/// table refuses) never blocks the intents beside it in the same tick.
/// </para>
/// </remarks>
public sealed class ReviewDecisionIntakeHostedService : IHostedService
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IConfigurationProvider _configuration;
    private readonly ILogger? _logger;

    private readonly SemaphoreSlim _intakeGate = new(1, 1);

    private PeriodicTimer? _timer;
    private Task? _loop;
    private CancellationTokenSource? _cts;

    /// <summary>Maps each lifecycle-family Kind this service acts on to the command that dispatches a status change for it.</summary>
    private static readonly IReadOnlyDictionary<string, Func<Guid, string, LifecycleState, ICommand>> LifecycleCommandFactories =
        BuildLifecycleCommandFactories();

    /// <summary>Initialises a new instance of the <see cref="ReviewDecisionIntakeHostedService"/> class.</summary>
    /// <param name="domainContext">Unused directly by this service today — accepted for symmetry with <see cref="DashboardExportHostedService"/> and because a future Kind's own dispatch (beyond the four this service already handles) is very likely to need it.</param>
    /// <param name="commandDispatcher">Dispatches the one real command each intent resolves to.</param>
    /// <param name="configuration">Read once per tick for <see cref="ReviewDecisionIntakeOptions"/> — a directory or interval change takes effect on the very next tick, no restart required (the <see cref="Enabled"/> flag itself is the one exception, read once at <see cref="StartAsync"/>).</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public ReviewDecisionIntakeHostedService(
        EngineeringDomainContext domainContext,
        ICommandDispatcher commandDispatcher,
        IConfigurationProvider configuration,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(configuration);

        _domainContext = domainContext;
        _commandDispatcher = commandDispatcher;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>The most recent intake tick's own attempt time, for diagnostics — <see langword="null"/> before the first tick completes, and permanently <see langword="null"/> while the capability is switched off.</summary>
    public DateTimeOffset? LastIntakeAttemptedAt { get; private set; }

    /// <summary>How many intents the most recent tick processed (succeeded or failed) — <see langword="null"/> before the first tick completes.</summary>
    public int? LastIntakeProcessedCount { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Never blocks Host start. When the capability is off, this returns
    /// having started nothing at all — no timer, no loop, no directory
    /// touched.
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = ReviewDecisionIntakeOptions.FromConfiguration(_configuration);
        if (!options.Enabled)
            return Task.CompletedTask;

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(options.IntervalSeconds));
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        do
        {
            await IntakeOnceAsync(cancellationToken).ConfigureAwait(false);
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
    /// Processes every pending intent file once. <see cref="_intakeGate"/>
    /// serialises overlapping calls, mirroring
    /// <see cref="DashboardExportHostedService.ExportOnceAsync"/>'s own
    /// reasoning exactly — there is no manual "process now" command today,
    /// but a future one would call this same method, and the gate costs
    /// nothing to have in place already.
    /// </summary>
    public async Task IntakeOnceAsync(CancellationToken cancellationToken = default)
    {
        await _intakeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LastIntakeAttemptedAt = DateTimeOffset.UtcNow;

            var directory = ReviewDecisionIntakeOptions.FromConfiguration(_configuration).IntakeDirectory;
            if (!Directory.Exists(directory))
            {
                LastIntakeProcessedCount = 0;
                return;
            }

            var processed = 0;
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                if (path.EndsWith(".result.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                await ProcessIntentFileAsync(path, cancellationToken).ConfigureAwait(false);
                processed++;
            }

            LastIntakeProcessedCount = processed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Error("Review decision intake failed.", ex);
        }
        finally
        {
            _intakeGate.Release();
        }
    }

    private async Task ProcessIntentFileAsync(string path, CancellationToken cancellationToken)
    {
        ReviewDecisionIntent? intent;
        try
        {
            await using var file = File.OpenRead(path);
            intent = await JsonSerializer.DeserializeAsync<ReviewDecisionIntent>(file, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger?.Error($"Review decision intent '{path}' could not be read.", ex);
            return;
        }

        if (intent is null)
        {
            _logger?.Warning($"Review decision intent '{path}' deserialized to nothing — skipped.");
            return;
        }

        var outcome = await ApplyAsync(intent, cancellationToken).ConfigureAwait(false);
        await WriteOutcomeAsync(path, outcome, cancellationToken).ConfigureAwait(false);

        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger?.Warning($"Review decision intent '{path}' was processed but could not be deleted: {ex.Message}");
        }
    }

    private async Task<ReviewDecisionOutcome> ApplyAsync(ReviewDecisionIntent intent, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        if (intent.SchemaVersion != ReviewDecisionIntent.CurrentSchemaVersion)
            return new ReviewDecisionOutcome(intent.IntentId, false, $"Unsupported schema version {intent.SchemaVersion}.", now);

        LifecycleState targetStatus;
        if (string.Equals(intent.Decision, ReviewDecisionIntent.DecisionApprove, StringComparison.OrdinalIgnoreCase))
            targetStatus = LifecycleState.Approved;
        else if (string.Equals(intent.Decision, ReviewDecisionIntent.DecisionReject, StringComparison.OrdinalIgnoreCase))
            targetStatus = LifecycleState.Draft;
        else
            return new ReviewDecisionOutcome(intent.IntentId, false, $"'{intent.Decision}' is not 'approve' or 'reject'.", now);

        if (!LifecycleCommandFactories.TryGetValue(intent.Kind, out var makeCommand))
        {
            var reason = string.Equals(intent.Kind, "Evidence", StringComparison.OrdinalIgnoreCase)
                ? "Evidence decisions are not supported yet (TD-189) — approve it from Core's own desktop, as Issue."
                : $"'{intent.Kind}' is not a Kind this service dispatches a decision for.";
            return new ReviewDecisionOutcome(intent.IntentId, false, reason, now);
        }

        try
        {
            var command = makeCommand(intent.ReviewItemId, intent.Kind, targetStatus);
            var result = await DispatchAsync(command, cancellationToken).ConfigureAwait(false);
            return new ReviewDecisionOutcome(intent.IntentId, result.Succeeded, result.Message ?? "(no message)", now);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ReviewDecisionOutcome(intent.IntentId, false, ex.Message, now);
        }
    }

    /// <summary>
    /// <see cref="ICommandDispatcher.DispatchAsync{TCommand}"/> is generic
    /// over the command's own concrete type, which this method only knows
    /// at runtime (<see cref="LifecycleCommandFactories"/> returns
    /// <see cref="ICommand"/>) — resolved here, once, by pattern match over
    /// the four concrete types this service ever constructs, rather than
    /// reflection.
    /// </summary>
    private Task<CommandResult> DispatchAsync(ICommand command, CancellationToken cancellationToken) => command switch
    {
        SetDocumentStatusCommand c => _commandDispatcher.DispatchAsync(c, cancellationToken),
        SetCalculationStatusCommand c => _commandDispatcher.DispatchAsync(c, cancellationToken),
        SetVerificationActivityStatusCommand c => _commandDispatcher.DispatchAsync(c, cancellationToken),
        SetRequirementStatusCommand c => _commandDispatcher.DispatchAsync(c, cancellationToken),
        _ => throw new InvalidOperationException($"'{command.GetType()}' is not a command this service knows how to dispatch."),
    };

    private static async Task WriteOutcomeAsync(string intentPath, ReviewDecisionOutcome outcome, CancellationToken cancellationToken)
    {
        var resultPath = Path.ChangeExtension(intentPath, null) + ".result.json";
        var tempPath = resultPath + ".tmp";

        await using (var file = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(file, outcome, cancellationToken: cancellationToken).ConfigureAwait(false);

        File.Move(tempPath, resultPath, overwrite: true);
    }

    private static IReadOnlyDictionary<string, Func<Guid, string, LifecycleState, ICommand>> BuildLifecycleCommandFactories()
    {
        var map = new Dictionary<string, Func<Guid, string, LifecycleState, ICommand>>(StringComparer.Ordinal);

        foreach (var (kind, _) in ReviewQueueExportAdapter.LifecycleKinds)
        {
            map[kind] = kind switch
            {
                CalculationObjectFactoryRegistry.CalculationKind =>
                    (id, k, status) => new SetCalculationStatusCommand(id, k, status),
                VerificationActivityFactoryRegistry.SupportedKind =>
                    (id, k, status) => new SetVerificationActivityStatusCommand(id, k, status),
                _ => (id, k, status) => new SetDocumentStatusCommand(id, k, status),
            };
        }

        map[RequirementsService.RequirementDocumentKind] =
            (id, _, status) => new SetRequirementStatusCommand(id, RequirementStatusFor(status));

        return map;
    }

    /// <summary>
    /// <see cref="SetRequirementStatusCommand"/> takes <see cref="RequirementStatus"/>,
    /// not <see cref="LifecycleState"/> — Requirements never adopted the
    /// canonical vocabulary (`ADR-0157`'s own disclosure). The two words
    /// this service ever asks for both have a direct equivalent.
    /// </summary>
    private static RequirementStatus RequirementStatusFor(LifecycleState status) => status switch
    {
        LifecycleState.Approved => RequirementStatus.Approved,
        LifecycleState.Draft => RequirementStatus.Draft,
        _ => throw new InvalidOperationException($"'{status}' has no Requirement equivalent — this service only ever asks for Approved or Draft."),
    };

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        if (_loop is not null)
            await _loop.ConfigureAwait(false);

        _timer?.Dispose();
    }
}
