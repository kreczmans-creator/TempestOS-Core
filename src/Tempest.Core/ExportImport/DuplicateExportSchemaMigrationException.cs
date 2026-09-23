namespace Tempest.Core.ExportImport;

/// <summary>
/// Thrown when <see cref="ImportService.RegisterMigration"/> is called for
/// a <see cref="IExportSchemaMigration.Kind"/> and
/// <see cref="IExportSchemaMigration.FromSchemaVersion"/> pair that already
/// has a registered migration (`ADR-0051` addendum, `WP 20.3A`).
/// </summary>
/// <remarks>
/// First registration for a given step wins; a colliding, later
/// registration is rejected — never a silent override, the same discipline
/// <see cref="DuplicateImportableKindException"/> already applies to
/// <see cref="IImportable"/> registration.
/// </remarks>
public sealed class DuplicateExportSchemaMigrationException : ExportImportException
{
    /// <summary>
    /// Initialises a new instance of the
    /// <see cref="DuplicateExportSchemaMigrationException"/> class.
    /// </summary>
    /// <param name="kind">The kind that already has a registered migration for <paramref name="fromSchemaVersion"/>.</param>
    /// <param name="fromSchemaVersion">The schema version that already has a registered migration.</param>
    public DuplicateExportSchemaMigrationException(string kind, int fromSchemaVersion)
        : base($"A migration from schema version {fromSchemaVersion} is already registered for kind '{kind}'.")
    {
        Kind = kind;
        FromSchemaVersion = fromSchemaVersion;
    }

    /// <summary>Gets the kind that already has a registered migration for <see cref="FromSchemaVersion"/>.</summary>
    public string Kind { get; }

    /// <summary>Gets the schema version that already has a registered migration.</summary>
    public int FromSchemaVersion { get; }
}
