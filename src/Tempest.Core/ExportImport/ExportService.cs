using Tempest.Core.Logging;

namespace Tempest.Core.ExportImport;

/// <summary>
/// The concrete <see cref="IExportService"/> implementation.
/// </summary>
/// <remarks>
/// Holds no mutable state of its own — every export writes each source's
/// own bytes to a private buffer, tags it with that source's own
/// <see cref="IExportableKind.Kind"/> (falling back to its runtime type
/// name), and hands the resulting sections to <see cref="IExportFormat"/>
/// for framing. Two concurrent calls, each with its own distinct
/// destination stream and source list, never interfere with each other.
/// </remarks>
public sealed class ExportService : IExportService
{
    private readonly IExportFormat _format;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ExportService"/> class.
    /// </summary>
    /// <param name="format">The format this service frames every export's own sections with.</param>
    /// <param name="logger">
    /// An optional logger used to record export activity via the logging
    /// abstraction. May be <see langword="null"/> if logging is not required.
    /// </param>
    public ExportService(IExportFormat format, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(format);

        _format = format;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExportAsync(Stream destination, IReadOnlyList<IExportable> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(sources);

        var sections = new List<ExportSection>(sources.Count);

        foreach (var source in sources)
        {
            using var buffer = new MemoryStream();

            await source.ExportAsync(buffer, cancellationToken).ConfigureAwait(false);

            var kind = source is IExportableKind keyed ? keyed.Kind : source.GetType().FullName!;

            sections.Add(new ExportSection(kind, source.SchemaVersion, buffer.ToArray()));
        }

        await _format.WriteAsync(sections, destination, cancellationToken).ConfigureAwait(false);

        _logger?.Information(
            $"Exported {sections.Count} source(s): {string.Join(", ", sections.Select(section => $"{section.Kind} (v{section.SchemaVersion})"))}.");
    }
}
