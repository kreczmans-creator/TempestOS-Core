namespace Tempest.Core.ExportImport;

/// <summary>
/// Coordinates exporting one or more <see cref="IExportable"/> sources into
/// a single portable artifact.
/// </summary>
/// <remarks>
/// Does not itself check permissions — a caller invoking
/// <see cref="ExportAsync"/> through the Command Framework inherits whatever
/// authorization that command's own handler enforces via
/// <see cref="Identity.IPermissionEvaluator"/>, mirroring
/// <see cref="Reporting.IReportingService"/>'s own established convention —
/// the enforcement point is the caller, not this service.
/// </remarks>
public interface IExportService
{
    /// <summary>
    /// Exports every source in <paramref name="sources"/> into a single
    /// artifact written to <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The stream the combined artifact is written to.</param>
    /// <param name="sources">The sources to export, in the order they are written.</param>
    /// <param name="cancellationToken">A token observed for the duration of the export.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> or <paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A source's own <see cref="IExportable.ExportAsync"/> exception, or an
    /// <see cref="IOException"/> from <paramref name="destination"/> itself,
    /// propagates to the caller unmodified — this service does not wrap or
    /// reinterpret either failure.
    /// </remarks>
    Task ExportAsync(Stream destination, IReadOnlyList<IExportable> sources, CancellationToken cancellationToken = default);
}
