namespace Tempest.Core.ExportImport;

/// <summary>
/// Marks a service's data as exportable through a versioned, round-trip-safe
/// contract — explicitly distinct from <c>Persistence.IPersistenceStore</c>,
/// which is internal, platform-owned state (`ADR-0051`).
/// </summary>
public interface IExportable
{
    /// <summary>
    /// The schema version this exporter writes. Read on import to detect an
    /// incompatible or older artifact.
    /// </summary>
    int SchemaVersion { get; }

    /// <summary>
    /// Writes this source's own data to <paramref name="destination"/>. The
    /// byte layout is entirely this implementation's own concern —
    /// <see cref="IExportService"/> treats it as an opaque payload.
    /// </summary>
    /// <param name="destination">The stream this source writes its own data to.</param>
    /// <param name="cancellationToken">A token observed while writing.</param>
    Task ExportAsync(Stream destination, CancellationToken cancellationToken = default);
}
