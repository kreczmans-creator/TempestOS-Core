using System.Text.Json;
using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Samples;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Makes every registered <see cref="ICalculationDefinition{TInput, TResult}"/>
/// ("Calculation Template") dispatchable through one non-generic Workspace
/// command (<see cref="ExecuteCalculationCommand"/>/<see cref="RecalculateCalculationCommand"/>),
/// and browsable in the Project Explorer/Property Inspector, without the
/// Workspace layer ever needing to know each Template's own
/// <c>TInput</c>/<c>TResult</c> signature statically.
/// </summary>
/// <remarks>
/// <para>
/// A thin, additive, `Tempest.App`-only type-erasure adapter, one layer
/// above <see cref="ICalculationEngine"/>'s own already-type-erased
/// <c>RegisterDefinition</c>/<c>ExecuteAsync</c> dispatch (`ADR-0056`) — it
/// introduces no new Domain contract and no change to the Calculation
/// Framework itself, mirroring <see cref="Mechanical.MechanicalObjectFactoryRegistry"/>'s
/// own disclosed "never a Domain-layer registry" precedent (`WP 9.0A`).
/// </para>
/// <para>
/// Each Template's own input/output are marshalled as JSON at this
/// boundary — the Workspace layer is Kind-generic by construction (one
/// <see cref="IWorkspaceCommand"/>/<see cref="ICommandHandler{TCommand}"/>
/// pair must serve every registered Template), so a compile-time
/// <c>TInput</c>/<c>TResult</c> is never available here; JSON is the same
/// boundary <see cref="IEngineeringDocumentStore"/>'s own
/// <see cref="IDocumentRevision.Content"/> already uses.
/// </para>
/// <para>
/// A successful execution also records the one, additive integration
/// point this Work Package's own Digital Thread scope needs: the target
/// Domain object (a <c>"Calculation"</c> or any other
/// <see cref="IHasRelationships"/>-composing object) is linked to the
/// resulting <see cref="CalculationRecord{TResult}"/>'s own document Id
/// via the existing <c>"calculatedBy"</c> relationship kind — already
/// mapped to <see cref="RelationshipCategory.Calculation"/> by
/// <c>RelationshipKindCategoryMap</c> (`WP 8.2A`/`WP 8.2B`), never a new
/// relationship kind.
/// </para>
/// </remarks>
public sealed class CalculationTemplateRegistry
{
    /// <summary>The relationship kind a successful execution links its own target Domain object to the resulting <see cref="CalculationRecord{TResult}"/> document Id under.</summary>
    public const string CalculatedByRelationshipKind = "calculatedBy";

    private readonly Dictionary<string, ICalculationTemplateAdapter> _adaptersByCalculationId = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ICalculationTemplateAdapter> _adaptersByNodeId = new();
    private readonly ICalculationEngine _calculationEngine;
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="CalculationTemplateRegistry"/> class.</summary>
    public CalculationTemplateRegistry(ICalculationEngine calculationEngine, EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(calculationEngine);
        ArgumentNullException.ThrowIfNull(context);

        _calculationEngine = calculationEngine;
        _context = context;
    }

    /// <summary>
    /// Registers a Template — the calculation must already be registered
    /// with <see cref="ICalculationEngine"/> under the same
    /// <paramref name="calculationId"/> (typically by the module that owns
    /// the underlying <see cref="ICalculationDefinition{TInput, TResult}"/>,
    /// e.g. a sample module, mirroring <see cref="CalculationSampleModule"/>'s
    /// own registration).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="calculationId"/> is already registered here.</exception>
    public void Register<TInput, TResult>(string calculationId, CalculationMetadata metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(calculationId);
        ArgumentNullException.ThrowIfNull(metadata);

        if (_adaptersByCalculationId.ContainsKey(calculationId))
            throw new ArgumentException($"'{calculationId}' is already registered as a Calculation Template.", nameof(calculationId));

        var adapter = new CalculationTemplateAdapter<TInput, TResult>(calculationId, metadata);
        _adaptersByCalculationId[calculationId] = adapter;
        _adaptersByNodeId[adapter.NodeId] = adapter;
    }

    /// <summary>Every registered Template, in registration order.</summary>
    public IReadOnlyList<CalculationTemplateDescriptor> Templates =>
        _adaptersByCalculationId.Values.Select(a => new CalculationTemplateDescriptor(a.NodeId, a.CalculationId, a.Metadata)).ToList();

    /// <summary>Returns the Template registered under <paramref name="calculationId"/>, or <see langword="null"/> if none is.</summary>
    public CalculationTemplateDescriptor? FindByCalculationId(string calculationId) =>
        _adaptersByCalculationId.TryGetValue(calculationId, out var adapter) ? new(adapter.NodeId, adapter.CalculationId, adapter.Metadata) : null;

    /// <summary>Returns the Template addressed by its own Explorer/Property Inspector <paramref name="nodeId"/>, or <see langword="null"/> if none is registered under it.</summary>
    public CalculationTemplateDescriptor? FindByNodeId(Guid nodeId) =>
        _adaptersByNodeId.TryGetValue(nodeId, out var adapter) ? new(adapter.NodeId, adapter.CalculationId, adapter.Metadata) : null;

    /// <summary>
    /// Executes the Template registered under <paramref name="calculationId"/>
    /// against <paramref name="inputJson"/>, then links
    /// <paramref name="targetObjectId"/> to the resulting record via
    /// <see cref="CalculatedByRelationshipKind"/> if the target object
    /// itself composes <see cref="IHasRelationships"/> (every
    /// <c>EngineeringObjectBase</c>-derived object does).
    /// </summary>
    /// <exception cref="CalculationDefinitionNotFoundException"><paramref name="calculationId"/> is not a registered Template.</exception>
    /// <exception cref="ArgumentException"><paramref name="targetObjectId"/> does not identify a known Domain object.</exception>
    public async Task<CalculationExecutionSummary> ExecuteAsync(
        string calculationId, Guid targetObjectId, string inputJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(calculationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputJson);

        if (!_adaptersByCalculationId.TryGetValue(calculationId, out var adapter))
            throw new CalculationDefinitionNotFoundException(calculationId);

        var target = await _context.Repository.FindAsync(targetObjectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"'{targetObjectId}' is not a known Domain object.", nameof(targetObjectId));

        var summary = await adapter.ExecuteAsync(_calculationEngine, inputJson, cancellationToken).ConfigureAwait(false);

        if (target is IHasRelationships hasRelationships)
            await hasRelationships.LinkAsync(summary.RecordId, CalculatedByRelationshipKind, cancellationToken).ConfigureAwait(false);

        return summary;
    }

    /// <summary>Type-erased execution surface one <see cref="CalculationTemplateAdapter{TInput, TResult}"/> per registered Template implements.</summary>
    private interface ICalculationTemplateAdapter
    {
        Guid NodeId { get; }
        string CalculationId { get; }
        CalculationMetadata Metadata { get; }
        Task<CalculationExecutionSummary> ExecuteAsync(ICalculationEngine engine, string inputJson, CancellationToken cancellationToken);
    }

    /// <summary>The one, per-Template concrete adapter — the sole place <c>TInput</c>/<c>TResult</c> are statically known.</summary>
    private sealed class CalculationTemplateAdapter<TInput, TResult> : ICalculationTemplateAdapter
    {
        public Guid NodeId { get; } = Guid.NewGuid();
        public string CalculationId { get; }
        public CalculationMetadata Metadata { get; }

        public CalculationTemplateAdapter(string calculationId, CalculationMetadata metadata)
        {
            CalculationId = calculationId;
            Metadata = metadata;
        }

        public async Task<CalculationExecutionSummary> ExecuteAsync(ICalculationEngine engine, string inputJson, CancellationToken cancellationToken)
        {
            TInput input;
            try
            {
                input = JsonSerializer.Deserialize<TInput>(inputJson)
                    ?? throw new CalculationInputInvalidException($"Input JSON for '{CalculationId}' deserialized to null.");
            }
            catch (JsonException ex)
            {
                throw new CalculationInputInvalidException($"Input JSON for '{CalculationId}' could not be parsed: {ex.Message}");
            }

            var record = await engine.ExecuteAsync<TInput, TResult>(CalculationId, input, cancellationToken).ConfigureAwait(false);

            return new CalculationExecutionSummary(
                record.Id, CalculationId, JsonSerializer.Serialize(record.Result), record.Validation.Outcome,
                record.ExecutedAt, record.ExecutedByPrincipalId);
        }
    }
}

/// <summary>One registered Calculation Template's own display identity — Explorer/Property Inspector node Id, Calculation Id, and metadata.</summary>
public sealed record CalculationTemplateDescriptor(Guid NodeId, string CalculationId, CalculationMetadata Metadata);

/// <summary>
/// The result of one <see cref="CalculationTemplateRegistry.ExecuteAsync"/>
/// call — a JSON-erased summary a Workspace caller can display without
/// knowing the executed Template's own <c>TResult</c> type.
/// </summary>
/// <param name="RecordId">The resulting <see cref="CalculationRecord{TResult}"/>'s own Id (also its backing document's own Id).</param>
/// <param name="CalculationId">The Template that was executed.</param>
/// <param name="ResultJson">The result, JSON-serialized.</param>
/// <param name="Outcome">The execution's own validation outcome.</param>
/// <param name="ExecutedAt">When the calculation was executed.</param>
/// <param name="ExecutedByPrincipalId">Who executed it.</param>
public sealed record CalculationExecutionSummary(
    Guid RecordId, string CalculationId, string ResultJson, CalculationValidationOutcome Outcome, DateTimeOffset ExecutedAt, string ExecutedByPrincipalId);
