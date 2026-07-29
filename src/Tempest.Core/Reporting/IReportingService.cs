namespace Tempest.Core.Reporting;

/// <summary>
/// Registers report definitions and their renderers, and dispatches a
/// render request by definition Id. Registration is imperative — this
/// service is never resolved via open-generic or keyed DI (<c>RD-0040</c>).
/// </summary>
/// <remarks>
/// Does not itself check permissions — a caller invoking
/// <see cref="GenerateAsync"/> through the Command Framework inherits
/// whatever authorization that command's own handler enforces via
/// <see cref="Identity.IPermissionEvaluator"/>; a direct DI consumer is
/// trusted at the same level as any other constructor-injected
/// dependency. The enforcement point is the caller, not this service —
/// mirroring how Navigation and the Command Framework themselves impose
/// no authorization internally (<c>ADR-0032</c>, <c>ADR-0037</c>).
/// </remarks>
public interface IReportingService
{
    /// <summary>
    /// Registers <paramref name="definition"/> and the exact renderer
    /// that produces its output. Expected to be called only during
    /// Module Initialisation (single-threaded by construction) — not
    /// itself required to be thread-safe against concurrent
    /// registration.
    /// </summary>
    /// <typeparam name="TDefinition">The report definition type being registered.</typeparam>
    /// <param name="definition">The definition to register.</param>
    /// <param name="renderer">The renderer that produces this definition's own output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="renderer"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateReportDefinitionException">A definition is already registered under <see cref="IReportDefinition.Id"/>.</exception>
    void RegisterDefinition<TDefinition>(TDefinition definition, IReportRenderer<TDefinition> renderer) where TDefinition : IReportDefinition;

    /// <summary>
    /// Generates the report registered under <paramref name="definitionId"/>,
    /// dispatching to its own registered renderer. Safe for concurrent
    /// invocation once registration is complete, including two
    /// concurrent requests for the same or different definitions.
    /// </summary>
    /// <param name="definitionId">The registered <see cref="IReportDefinition.Id"/> to generate.</param>
    /// <param name="request">The parameters for this specific generation request.</param>
    /// <param name="cancellationToken">A token observed by the underlying renderer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definitionId"/> or <paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ReportDefinitionNotFoundException">No definition is registered under <paramref name="definitionId"/>.</exception>
    /// <remarks>
    /// A renderer's own exception propagates to the caller unmodified —
    /// this service does not swallow or wrap it, mirroring the Command
    /// Framework's own dispatch failure model (<c>ADR-0038</c>).
    /// </remarks>
    Task<ReportResult> GenerateAsync(string definitionId, ReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>Every registered definition's Id and Name. Never <see langword="null"/>.</summary>
    IReadOnlyList<IReportDefinition> RegisteredDefinitions { get; }
}
