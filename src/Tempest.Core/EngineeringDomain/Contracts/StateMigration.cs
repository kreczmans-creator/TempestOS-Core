namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// One ordered step of an <see cref="EngineeringObjectState"/> migration
/// (`TD-87`, `ADR-0120`) — a plain function from one schema version to the
/// next, applied only on read, never on write.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a version-keyed DTO hierarchy: nothing ever
/// constructs an old-shaped <see cref="EngineeringObjectState"/> in code —
/// a past version only ever exists as bytes on disk — so an ordered
/// function composes exactly what a migration needs (`ADR-0120`
/// Decision 2) with no parallel type hierarchy.
/// </para>
/// <para>
/// Written next to the type it affects, the same locality
/// <see cref="IRehydratable{TSelf}"/>/<c>CaptureTypeState</c> already
/// established, and registered by that Kind's own declaring class.
/// </para>
/// </remarks>
public interface IStateMigration
{
    /// <summary>
    /// The Kind this migration applies to, or <see langword="null"/> to
    /// apply to every Kind — a change to a field
    /// <see cref="EngineeringObjectState"/> itself owns (<c>Status</c>,
    /// <c>DisplayName</c>, <c>Metadata</c>, <c>BomLine</c>, <c>History</c>,
    /// <c>Attachments</c>) rather than a Kind's own <c>TypeState</c>.
    /// </summary>
    string? Kind { get; }

    /// <summary>
    /// The version this migration upgrades from. Applied when a record's
    /// current <see cref="EngineeringObjectState.SchemaVersion"/> equals
    /// this value; the migrated record's own
    /// <see cref="EngineeringObjectState.SchemaVersion"/> is always this
    /// value plus one, stamped by the store, not by the migration itself.
    /// </summary>
    int FromVersion { get; }

    /// <summary>Produces the next-versioned shape of <paramref name="state"/>.</summary>
    EngineeringObjectState Migrate(EngineeringObjectState state);
}

/// <summary>
/// The <see cref="IStateMigration"/> chain <see cref="EngineeringObjectStateStore"/>'s
/// own read path walks (`TD-87`, `ADR-0120`) — a common (Kind-less) chain,
/// and one chain per Kind.
/// </summary>
public interface IStateMigrationRegistry
{
    /// <summary>
    /// Registers <paramref name="migration"/> under its own
    /// <see cref="IStateMigration.Kind"/> (or the common chain, when
    /// <see langword="null"/>) and <see cref="IStateMigration.FromVersion"/>.
    /// </summary>
    void Register(IStateMigration migration);

    /// <summary>
    /// The migration to apply next for a record of <paramref name="kind"/>
    /// currently at <paramref name="fromVersion"/> — the common chain's own
    /// migration for that version, when one is registered, ahead of that
    /// Kind's own (`ADR-0120` Decision 2: "common chain first, then that
    /// Kind's own chain"); <see langword="null"/> when neither has one, the
    /// signal that no further migration applies.
    /// </summary>
    IStateMigration? Find(string kind, int fromVersion);
}
