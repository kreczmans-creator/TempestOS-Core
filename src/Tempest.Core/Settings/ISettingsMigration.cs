namespace Tempest.Core.Settings;

/// <summary>
/// One ordered step of a <see cref="SettingsDocument{TDocument}"/>
/// migration (`TD-87`, `ADR-0120` Decision 6) — the identical shape
/// <c>IStateMigration</c> gives <see cref="Tempest.Core.EngineeringDomain.EngineeringObjectState"/>,
/// degenerating from "per Kind" to "per <typeparamref name="TDocument"/>"
/// because the generic parameter already fixes the document shape per
/// call site; no Kind dispatch is needed here.
/// </summary>
/// <typeparam name="TDocument">The document shape this migration upgrades.</typeparam>
public interface ISettingsMigration<TDocument>
{
    /// <summary>
    /// The version this migration upgrades from. Applied when the stored
    /// document's current <c>SchemaVersion</c> equals this value.
    /// </summary>
    int FromVersion { get; }

    /// <summary>Produces the next-versioned shape of <paramref name="document"/>.</summary>
    TDocument Migrate(TDocument document);
}
