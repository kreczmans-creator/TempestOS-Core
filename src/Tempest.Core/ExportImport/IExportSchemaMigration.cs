namespace Tempest.Core.ExportImport;

/// <summary>
/// Upgrades one artifact section's own payload by exactly one schema
/// version — from <see cref="FromSchemaVersion"/> to
/// <see cref="FromSchemaVersion"/> + 1 — registered against the same
/// <see cref="IImportable.Kind"/> the section's own <see cref="IImportable"/>
/// is registered under (`ADR-0051` addendum, `WP 20.3A`).
/// </summary>
/// <remarks>
/// <para>
/// <b>A chain, one step at a time.</b> <see cref="ImportService.ImportAsync"/>
/// walks a section whose <see cref="ExportSection.SchemaVersion"/> is older
/// than the registered <see cref="IImportable.SchemaVersion"/> forward one
/// registered migration at a time — version <c>n</c> to <c>n + 1</c>, then
/// <c>n + 1</c> to <c>n + 2</c>, and so on — never a single jump from an
/// old version straight to the current one. A schema that has moved
/// through several versions needs a migration registered for every
/// intervening step; the walk stops, and the import is refused with
/// <see cref="IncompatibleExportSchemaException"/> naming the artifact's
/// own original version, the moment a step is missing.
/// </para>
/// <para>
/// <b>Compression and encryption stay out</b> — `ADR-0051`'s own disclosed
/// gap is unchanged by this mechanism. A migration only ever transforms
/// the same opaque, uncompressed, unencrypted bytes
/// <see cref="IExportable.ExportAsync"/> already writes and
/// <see cref="IImportable.ImportAsync"/> already reads; nothing about how
/// those bytes reach or leave the artifact changes.
/// </para>
/// </remarks>
public interface IExportSchemaMigration
{
    /// <summary>The <see cref="IImportable.Kind"/> this migration upgrades a section of.</summary>
    string Kind { get; }

    /// <summary>
    /// The schema version this migration reads. It writes
    /// <see cref="FromSchemaVersion"/> + 1 — the version the next migration
    /// in the chain (or the registered <see cref="IImportable"/> itself)
    /// expects to read.
    /// </summary>
    int FromSchemaVersion { get; }

    /// <summary>
    /// Reads <paramref name="payload"/>, written at
    /// <see cref="FromSchemaVersion"/>, and returns the equivalent payload
    /// at <see cref="FromSchemaVersion"/> + 1.
    /// </summary>
    /// <param name="payload">
    /// The section's own bytes — exactly as read from the artifact, or, for
    /// a step after the first, exactly as the prior migration in the same
    /// chain returned them.
    /// </param>
    /// <param name="cancellationToken">A token observed while migrating.</param>
    Task<byte[]> MigrateAsync(byte[] payload, CancellationToken cancellationToken = default);
}
