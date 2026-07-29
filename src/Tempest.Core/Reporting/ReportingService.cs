using Tempest.Core.Logging;

namespace Tempest.Core.Reporting;

/// <summary>
/// The concrete <see cref="IReportingService"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Registrations are held in a single, lock-guarded dictionary keyed by
/// <see cref="IReportDefinition.Id"/>. <see cref="RegisterDefinition{TDefinition}"/>
/// is expected to be called only during Module Initialisation
/// (single-threaded by construction, per `Host Lifecycle.md`), so the
/// lock exists for <see cref="GenerateAsync"/>'s own safety, not to
/// serialise registration against itself.
/// </para>
/// <para>
/// <see cref="GenerateAsync"/> looks up the registered renderer under
/// the lock, then invokes it outside the lock — so two concurrent
/// generations, for the same or different definitions, never block on
/// each other waiting for rendering to complete. A renderer's own
/// exception is logged at <see cref="LogLevel.Warning"/> and rethrown
/// unmodified — never swallowed or wrapped — mirroring the Command
/// Framework's own dispatch failure model (`ADR-0038`).
/// </para>
/// </remarks>
public sealed class ReportingService : IReportingService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IReportDefinition> _definitionsById = new();
    private readonly Dictionary<string, Func<ReportRequest, CancellationToken, Task<ReportResult>>> _renderersById = new();
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ReportingService"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record registration and generation
    /// activity via the logging abstraction. May be <see langword="null"/>
    /// if logging is not required.
    /// </param>
    public ReportingService(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void RegisterDefinition<TDefinition>(TDefinition definition, IReportRenderer<TDefinition> renderer) where TDefinition : IReportDefinition
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(renderer);

        lock (_gate)
        {
            if (_definitionsById.ContainsKey(definition.Id))
                throw new DuplicateReportDefinitionException(definition.Id);

            _definitionsById[definition.Id] = definition;
            _renderersById[definition.Id] = (request, cancellationToken) => renderer.RenderAsync(definition, request, cancellationToken);
        }

        _logger?.Information($"Report definition '{definition.Id}' ('{definition.Name}') registered.");
    }

    /// <inheritdoc />
    public async Task<ReportResult> GenerateAsync(string definitionId, ReportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(request);

        Func<ReportRequest, CancellationToken, Task<ReportResult>> render;

        lock (_gate)
        {
            if (!_renderersById.TryGetValue(definitionId, out var found))
                throw new ReportDefinitionNotFoundException(definitionId);

            render = found;
        }

        try
        {
            var result = await render(request, cancellationToken).ConfigureAwait(false);

            _logger?.Information($"Report '{definitionId}' generated ({result.ContentType}, {result.Content.Length} bytes).");

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.Warning($"Report generation failed for definition '{definitionId}'.", ex);
            throw;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IReportDefinition> RegisteredDefinitions
    {
        get
        {
            lock (_gate)
                return _definitionsById.Values.ToList();
        }
    }
}
