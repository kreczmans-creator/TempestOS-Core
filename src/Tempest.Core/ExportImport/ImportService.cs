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
/// <see cref="ImportAsync"/> resolves, migrates and schema-checks every
/// section before importing any of them — an incompatible section anywhere
/// in the artifact aborts the entire call before a single
/// <see cref="IImportable.ImportAsync"/> is invoked, satisfying `Platform
/// Service Contracts.md`'s own "never attempts a best-effort partial
/// import" requirement.
/// </para>
/// <para>
/// <b>A section behind the registered schema version is walked forward
/// through registered migrations, one version at a time, before the
/// equality check</b> (`ADR-0051` addendum, `WP 20.3A`). See
/// <see cref="RegisterMigration"/> and <see cref="IExportSchemaMigration"/>'s
/// own remarks for the chain's shape; a section still behind after the walk
/// — because a step is missing — is refused exactly as an untouched
/// mismatch always was, naming the artifact's own original version.
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
    private readonly Dictionary<(string Kind, int FromSchemaVersion), IExportSchemaMigration> _migrationsByKindAndVersion = new();
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

    /// <summary>
    /// Registers <paramref name="migration"/> as the one step from
    /// <see cref="IExportSchemaMigration.FromSchemaVersion"/> to
    /// <see cref="IExportSchemaMigration.FromSchemaVersion"/> + 1 for
    /// <see cref="IExportSchemaMigration.Kind"/> (`ADR-0051` addendum,
    /// `WP 20.3A`). A section imported at that exact version is passed
    /// through it before the next step, or the final schema-version
    /// equality check, ever sees it — see <see cref="IExportSchemaMigration"/>'s
    /// own remarks for the chain's shape.
    /// </summary>
    /// <param name="migration">The migration to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="migration"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateExportSchemaMigrationException">
    /// A migration is already registered for the same <see cref="IExportSchemaMigration.Kind"/>
    /// and <see cref="IExportSchemaMigration.FromSchemaVersion"/>.
    /// </exception>
    public void RegisterMigration(IExportSchemaMigration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);

        var key = (migration.Kind, migration.FromSchemaVersion);

        lock (_gate)
        {
            if (_migrationsByKindAndVersion.ContainsKey(key))
                throw new DuplicateExportSchemaMigrationException(migration.Kind, migration.FromSchemaVersion);

            _migrationsByKindAndVersion[key] = migration;
        }

        _logger?.Information(
            $"Export schema migration registered for '{migration.Kind}': v{migration.FromSchemaVersion} -> v{migration.FromSchemaVersion + 1}.");
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

            var migrated = await MigrateIfBehindAsync(section, importable, cancellationToken).ConfigureAwait(false);

            if (migrated.SchemaVersion != importable.SchemaVersion)
            {
                var reason = migrated.SchemaVersion > importable.SchemaVersion
                    ? $"is newer than the registered importable's supported schema version {importable.SchemaVersion}"
                    : $"has no registered migration from v{migrated.SchemaVersion} to the registered importable's supported schema version {importable.SchemaVersion}";

                _logger?.Warning(
                    $"Import rejected: artifact section '{section.Kind}', originally schema version {section.SchemaVersion}, {reason}.");

                throw new IncompatibleExportSchemaException(section.Kind, section.SchemaVersion, importable.SchemaVersion);
            }

            resolved.Add((migrated, importable));
        }

        foreach (var (section, importable) in resolved)
        {
            using var payload = new MemoryStream(section.Payload, writable: false);

            await importable.ImportAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        _logger?.Information(
            $"Imported {resolved.Count} section(s): {string.Join(", ", resolved.Select(r => $"{r.Section.Kind} (v{r.Section.SchemaVersion})"))}.");
    }

    /// <summary>
    /// Walks <paramref name="section"/> forward through registered
    /// migrations, one version at a time, for as long as its schema version
    /// stays behind <paramref name="importable"/>'s own
    /// <see cref="IImportable.SchemaVersion"/> and a next step is
    /// registered (`ADR-0051` addendum, `WP 20.3A`).
    /// </summary>
    /// <returns>
    /// <paramref name="section"/> unchanged when its schema version already
    /// matches or already exceeds <paramref name="importable"/>'s own
    /// (nothing to walk forward from, in either case — <see cref="ImportAsync"/>'s
    /// own equality check after this call reports whichever of those two it
    /// actually is); otherwise the section as it stands after the last
    /// migration that could be applied, which may still be behind if a step
    /// in the chain has no registered migration.
    /// </returns>
    private async Task<ExportSection> MigrateIfBehindAsync(ExportSection section, IImportable importable, CancellationToken cancellationToken)
    {
        var current = section;

        while (current.SchemaVersion < importable.SchemaVersion)
        {
            IExportSchemaMigration? migration;

            lock (_gate)
                _migrationsByKindAndVersion.TryGetValue((current.Kind, current.SchemaVersion), out migration);

            if (migration is null)
                return current;

            var migratedPayload = await migration.MigrateAsync(current.Payload, cancellationToken).ConfigureAwait(false);
            current = current with { SchemaVersion = current.SchemaVersion + 1, Payload = migratedPayload };

            _logger?.Information(
                $"Artifact section '{current.Kind}' migrated to schema version {current.SchemaVersion}.");
        }

        return current;
    }
}
