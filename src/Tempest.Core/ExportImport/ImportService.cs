using Tempest.Core.Logging;

namespace Tempest.Core.ExportImport;

/// <summary>
/// The concrete <see cref="IImportService"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Registrations are held in a single, lock-guarded dictionary keyed by
/// <see cref="IImportable.Kind"/> — mirroring
/// <see cref="Reporting.ReportingService"/>'s own
/// <c>_definitionsById</c>/<c>_renderersById</c> pattern.
/// <see cref="RegisterImportable"/> is expected to be called only during
/// Module Initialisation (single-threaded by construction), so the lock
/// exists for <see cref="ImportAsync"/>'s own safety, not to serialise
/// registration against itself.
/// </para>
/// <para>
/// <see cref="ImportAsync"/> resolves and schema-checks every section
/// before importing any of them — an incompatible section anywhere in the
/// artifact aborts the entire call before a single <see cref="IImportable.ImportAsync"/>
/// is invoked, satisfying `Platform Service Contracts.md`'s own "never
/// attempts a best-effort partial import" requirement.
/// </para>
/// <para>
/// Registered under both its own concrete type and <see cref="IImportService"/>
/// in <c>TempestHost</c> — the same already-built instance under two
/// service-type keys — mirroring `ADR-0044`'s own dual-registration
/// precedent for <c>CurrentPrincipalAccessor</c>: a module that needs
/// <see cref="RegisterImportable"/> resolves the concrete type, while every
/// ordinary consumer resolves only the read-only <see cref="IImportService"/>
/// interface, both against the exact same object.
/// </para>
/// </remarks>
public sealed class ImportService : IImportService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IImportable> _importablesByKind = new();
    private readonly IExportFormat _format;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ImportService"/> class.
    /// </summary>
    /// <param name="format">The format this service reads every import's own sections with.</param>
    /// <param name="logger">
    /// An optional logger used to record registration and import activity
    /// via the logging abstraction. May be <see langword="null"/> if
    /// logging is not required.
    /// </param>
    public ImportService(IExportFormat format, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(format);

        _format = format;
        _logger = logger;
    }

    /// <summary>
    /// Registers <paramref name="importable"/> under its own <see cref="IImportable.Kind"/>,
    /// so a future <see cref="ImportAsync"/> call can route a matching
    /// artifact section back to it.
    /// </summary>
    /// <param name="importable">The importable to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="importable"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateImportableKindException">An importable is already registered under <see cref="IImportable.Kind"/>.</exception>
    public void RegisterImportable(IImportable importable)
    {
        ArgumentNullException.ThrowIfNull(importable);

        lock (_gate)
        {
            if (_importablesByKind.ContainsKey(importable.Kind))
                throw new DuplicateImportableKindException(importable.Kind);

            _importablesByKind[importable.Kind] = importable;
        }

        _logger?.Information($"Importable '{importable.Kind}' (schema v{importable.SchemaVersion}) registered.");
    }

    /// <inheritdoc />
    public async Task ImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sections = await _format.ReadAsync(source, cancellationToken).ConfigureAwait(false);

        var resolved = new List<(ExportSection Section, IImportable Importable)>(sections.Count);

        foreach (var section in sections)
        {
            IImportable importable;

            lock (_gate)
            {
                if (!_importablesByKind.TryGetValue(section.Kind, out var found))
                {
                    _logger?.Warning($"Import rejected: no importable is registered for artifact section '{section.Kind}'.");
                    throw new IncompatibleExportSchemaException(section.Kind);
                }

                importable = found;
            }

            if (section.SchemaVersion != importable.SchemaVersion)
            {
                _logger?.Warning(
                    $"Import rejected: artifact section '{section.Kind}' has schema version {section.SchemaVersion}, " +
                    $"but the registered importable supports schema version {importable.SchemaVersion}.");

                throw new IncompatibleExportSchemaException(section.Kind, section.SchemaVersion, importable.SchemaVersion);
            }

            resolved.Add((section, importable));
        }

        foreach (var (section, importable) in resolved)
        {
            using var payload = new MemoryStream(section.Payload, writable: false);

            await importable.ImportAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        _logger?.Information(
            $"Imported {resolved.Count} section(s): {string.Join(", ", resolved.Select(r => $"{r.Section.Kind} (v{r.Section.SchemaVersion})"))}.");
    }
}
