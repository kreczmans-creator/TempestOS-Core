namespace Tempest.Core.Reporting;

/// <summary>
/// Renders one <typeparamref name="TDefinition"/> into a
/// <see cref="ReportResult"/>. Exactly one renderer is registered per
/// definition type — mirroring <see cref="Commands.ICommandHandler{TCommand}"/>'s
/// own one-handler-per-command-type rule.
/// </summary>
/// <typeparam name="TDefinition">The report definition type this renderer handles.</typeparam>
public interface IReportRenderer<TDefinition> where TDefinition : IReportDefinition
{
    /// <summary>
    /// Renders <paramref name="definition"/> against <paramref name="request"/>'s
    /// own parameters, producing the final output.
    /// </summary>
    /// <param name="definition">The report definition being rendered.</param>
    /// <param name="request">The parameters for this specific generation request.</param>
    /// <param name="cancellationToken">A token observed by this renderer's own implementation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="request"/> is <see langword="null"/>.</exception>
    Task<ReportResult> RenderAsync(TDefinition definition, ReportRequest request, CancellationToken cancellationToken = default);
}
