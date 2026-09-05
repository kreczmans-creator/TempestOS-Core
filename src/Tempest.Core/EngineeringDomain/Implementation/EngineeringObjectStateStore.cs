using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Persists and reads <see cref="EngineeringObjectState"/> — the durable
/// half of engineering-object rehydration (`TD-85`).
/// </summary>
/// <remarks>
/// <para>
/// Writes through the platform's single <see cref="IPersistenceStore"/>,
/// one record per object, keyed by the object's own Id — the same
/// substrate and shape <c>EngineeringDocumentStore</c> already uses for
/// documents and revisions. This introduces <b>no new storage
/// mechanism</b> and no second authority: the document remains the
/// object's identity and revision history; this record carries the object
/// state the document was never designed to hold.
/// </para>
/// <para>
/// A corrupted state record is skipped with a warning rather than failing
/// the whole rehydration, mirroring `TD-60`'s established discipline for
/// passive read paths — one unreadable object must not cost the user
/// every other object they own.
/// </para>
/// <para>
/// <b>`TD-87`/`ADR-0120`: schema versioning.</b> Every write stamps
/// <see cref="CurrentSchemaVersion"/> and every enum reachable from
/// <see cref="EngineeringObjectState"/> serialises as its member name
/// (<see cref="StateJsonOptions"/>). On read, a record with no
/// <see cref="EngineeringObjectState.SchemaVersion"/> (or <c>0</c>)
/// normalises to <c>1</c> explicitly, and an optional
/// <see cref="IStateMigrationRegistry"/> supplied at construction walks
/// the record forward one version at a time until it reaches
/// <see cref="TargetSchemaVersion"/> or no further migration applies,
/// whichever comes first. A record that does not end the walk exactly at
/// <see cref="TargetSchemaVersion"/> — no migration path reaches it, one
/// throws, or the record started ahead of it (a newer build) — is logged
/// and skipped exactly as an unparseable record already is; migration
/// never runs on write (`ADR-0120` Decision 2/5).
/// </para>
/// </remarks>
public sealed class EngineeringObjectStateStore : IEngineeringObjectStateStore
{
    /// <summary>The <see cref="IPersistenceStore"/> collection engineering object state lives in.</summary>
    public const string StateCollectionName = "EngineeringDomain.ObjectState";

    /// <summary>
    /// The <see cref="EngineeringObjectState.SchemaVersion"/> this build
    /// captures and expects a fully-migrated record to hold (`TD-87`,
    /// `ADR-0120`). <c>1</c> at first release; bumped only alongside the
    /// migration(s) that let every existing record reach it.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The shared serialiser options for both <see cref="SaveAsync"/> and
    /// <see cref="Deserialise"/> — every enum reachable from
    /// <see cref="EngineeringObjectState"/> writes as its member name from
    /// this build onward (`ADR-0120` Decision 4). The built-in converter
    /// reads both a name and a number, so a record written before this
    /// change — still numeric — still deserialises identically; no
    /// migration is needed for this specific change.
    /// </summary>
    private static readonly JsonSerializerOptions StateJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IPersistenceStore _persistenceStore;
    private readonly IStateMigrationRegistry? _migrations;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="EngineeringObjectStateStore"/>
    /// class, targeting <see cref="CurrentSchemaVersion"/> — the one
    /// constructor visible to this platform's DI container
    /// (`TempestServiceProvider`), which requires exactly one public
    /// constructor and resolves every one of its parameters, so this stays
    /// the container-facing shape rather than growing a raw <c>int</c> or
    /// <c>int?</c> the container would have to be taught to satisfy
    /// (`TD-69`'s missing-default-parameter-support defect class, handed
    /// to `WP 16.4B`, not worked around here).
    /// </summary>
    /// <param name="persistenceStore">The substrate every state record is written to and read from.</param>
    /// <param name="migrations">
    /// The migration chain(s) a record may need to reach
    /// <see cref="TargetSchemaVersion"/> (`TD-87`, `ADR-0120`).
    /// <see langword="null"/> — the default — runs only the normalisation
    /// step: every existing hand-assembled store keeps compiling and
    /// passing unchanged.
    /// </param>
    /// <param name="logger">An optional logger used to record a skipped record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="persistenceStore"/> is <see langword="null"/>.</exception>
    public EngineeringObjectStateStore(
        IPersistenceStore persistenceStore,
        IStateMigrationRegistry? migrations = null,
        ILogger? logger = null)
        : this(persistenceStore, migrations, logger, CurrentSchemaVersion)
    {
    }

    /// <summary>
    /// The test-only seam behind <see cref="TargetSchemaVersion"/> (`TD-87`,
    /// `ADR-0120`): lets a test build a store whose read path targets a
    /// schema version other than <see cref="CurrentSchemaVersion"/>, to
    /// exercise a migration chain that runs past this build's own fixed
    /// current version without bumping that constant. <c>internal</c> —
    /// reachable only from <c>Tempest.Core.Tests</c>
    /// (<c>InternalsVisibleTo</c>, <c>AssemblyInfo.cs</c>) — deliberately
    /// not the container-visible constructor above, for the same reason
    /// that one exists: the container requires exactly one <em>public</em>
    /// constructor, so this stays invisible to it rather than becoming a
    /// second one it would refuse to resolve at all
    /// (<see cref="Tempest.Core.DependencyInjection.AmbiguousConstructorException"/>).
    /// </summary>
    /// <param name="persistenceStore">The substrate every state record is written to and read from.</param>
    /// <param name="migrations">The migration chain(s) a record may need to reach <paramref name="targetSchemaVersion"/>.</param>
    /// <param name="logger">An optional logger used to record a skipped record.</param>
    /// <param name="targetSchemaVersion">The <see cref="EngineeringObjectState.SchemaVersion"/> this store's own read path requires a record to reach before handing it back.</param>
    /// <exception cref="ArgumentNullException"><paramref name="persistenceStore"/> is <see langword="null"/>.</exception>
    internal EngineeringObjectStateStore(
        IPersistenceStore persistenceStore,
        IStateMigrationRegistry? migrations,
        ILogger? logger,
        int targetSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(persistenceStore);

        _persistenceStore = persistenceStore;
        _migrations = migrations;
        _logger = logger;
        TargetSchemaVersion = targetSchemaVersion;
    }

    /// <summary>
    /// The <see cref="EngineeringObjectState.SchemaVersion"/> this store's
    /// own read path requires a record to reach — <see cref="CurrentSchemaVersion"/>
    /// unless a different value was supplied at construction (`TD-87`,
    /// `ADR-0120`).
    /// </summary>
    public int TargetSchemaVersion { get; }

    /// <inheritdoc />
    public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _persistenceStore.WriteAsync(
            StateCollectionName,
            state.Id.ToString("N"),
            JsonSerializer.Serialize(state, StateJsonOptions),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EngineeringObjectState?> FindAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var json = await _persistenceStore.ReadAsync(StateCollectionName, objectId.ToString("N"), cancellationToken).ConfigureAwait(false);
        return json is null ? null : Deserialise(objectId, json);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default)
    {
        var keys = await _persistenceStore.ListKeysAsync(StateCollectionName, cancellationToken).ConfigureAwait(false);
        var states = new List<EngineeringObjectState>(keys.Count);

        foreach (var key in keys)
        {
            if (!Guid.TryParseExact(key, "N", out var objectId))
            {
                // A foreign file beside the store's own is not a record.
                _logger?.Warning($"Ignoring non-state key '{key}' in '{StateCollectionName}'.");
                continue;
            }

            var json = await _persistenceStore.ReadAsync(StateCollectionName, key, cancellationToken).ConfigureAwait(false);
            if (json is null)
                continue;

            if (Deserialise(objectId, json) is { } state)
                states.Add(state);
        }

        return states;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid objectId, CancellationToken cancellationToken = default) =>
        _persistenceStore.DeleteAsync(StateCollectionName, objectId.ToString("N"), cancellationToken);

    private EngineeringObjectState? Deserialise(Guid objectId, string json)
    {
        EngineeringObjectState? state;
        try
        {
            state = JsonSerializer.Deserialize<EngineeringObjectState>(json, StateJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger?.Warning($"Engineering object state '{objectId}' is unreadable and was skipped during rehydration.", ex);
            return null;
        }

        if (state is null)
            return null;

        // `TD-87`/`ADR-0120` Decision 1: a record written before
        // `SchemaVersion` existed leaves it at the CLR default for `int`
        // (`0`), not `1` — normalised explicitly rather than trusted to a
        // serialiser default that is not this platform's contract to rely
        // on.
        if (state.SchemaVersion <= 0)
            state = state with { SchemaVersion = 1 };

        // A record from a newer build than this one cannot be bridged
        // forward — there is nothing to migrate it "back" with, and no
        // migration will ever be registered for a version this build does
        // not yet know about. Caught before the migration loop below,
        // which would otherwise simply find nothing to apply and fall
        // through to the "no migration path" check below anyway — this
        // earlier, more specific check exists only to log a clearer,
        // distinct reason ("ahead", not merely "stuck").
        if (state.SchemaVersion > TargetSchemaVersion)
        {
            _logger?.Warning(
                $"Engineering object state '{objectId}' (Kind '{state.Kind}') is stuck at schema version " +
                $"{state.SchemaVersion}, newer than this build's target schema version {TargetSchemaVersion} — it " +
                "was NOT reconstructed and was skipped.");
            return null;
        }

        try
        {
            // `ADR-0120` Decision 2: the common (Kind-less) chain first,
            // then that Kind's own chain, repeated until the record
            // reaches TargetSchemaVersion or no further migration applies
            // — `IStateMigrationRegistry.Find` embodies that ordering for
            // a single version step, so this loop only has to keep asking
            // for the next one.
            while (state.SchemaVersion != TargetSchemaVersion)
            {
                if (_migrations?.Find(state.Kind, state.SchemaVersion) is not { } migration)
                    break;

                state = migration.Migrate(state) with { SchemaVersion = migration.FromVersion + 1 };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Warning(
                $"Engineering object state '{objectId}' (Kind '{state.Kind}') is stuck at schema version " +
                $"{state.SchemaVersion} — its migration threw and the record was skipped.", ex);
            return null;
        }

        // `ADR-0120` Decision 5: "no migration path to the store's current
        // [target] version" — the loop above stopped short (a version
        // step nothing bridges) rather than throwing, so it must be
        // checked here explicitly; falling through silently would return
        // a record at the wrong schema version rather than skipping it.
        if (state.SchemaVersion != TargetSchemaVersion)
        {
            _logger?.Warning(
                $"Engineering object state '{objectId}' (Kind '{state.Kind}') is stuck at schema version " +
                $"{state.SchemaVersion}, with no migration path to this build's target schema version " +
                $"{TargetSchemaVersion} — it was NOT reconstructed and was skipped.");
            return null;
        }

        return state;
    }
}

/// <summary>The concrete <see cref="IStateMigrationRegistry"/> (`TD-87`, `ADR-0120`).</summary>
/// <remarks>
/// <b>Review-board finding, `v0.16.0`.</b> Two collisions used to be
/// possible and silent: registering two migrations for the identical
/// <c>(Kind, FromVersion)</c> pair (last-wins, `TD-69`'s DI-container
/// defect class recurring here); and registering a common (Kind-less)
/// migration and a Kind-specific migration at the <em>same</em>
/// <see cref="IStateMigration.FromVersion"/> — since <see cref="Find"/>
/// always returns the common chain's entry first (`ADR-0120` Decision 2),
/// the Kind-specific migration would never run, yet the record would
/// still advance to the target version and look fully migrated. Both are
/// now impossible: <see cref="Register"/> throws
/// <see cref="DuplicateStateMigrationException"/> or
/// <see cref="ConflictingStateMigrationException"/> instead of allowing
/// either registration to complete, in either order. No opt-in replace
/// exists — unlike `IServiceCollection.Add`'s own <c>allowReplace</c>,
/// nothing depends on being able to override a migration once
/// registered, so no escape hatch was added for a hazard that would only
/// reopen.
/// </remarks>
public sealed class StateMigrationRegistry : IStateMigrationRegistry
{
    private readonly Dictionary<string, Dictionary<int, IStateMigration>> _byKind = new(StringComparer.Ordinal);
    private readonly Dictionary<int, IStateMigration> _common = new();

    /// <inheritdoc />
    /// <exception cref="DuplicateStateMigrationException">
    /// A migration is already registered for the identical chain (common,
    /// or that same Kind) and <see cref="IStateMigration.FromVersion"/>.
    /// </exception>
    /// <exception cref="ConflictingStateMigrationException">
    /// Registering <paramref name="migration"/> would leave a common
    /// migration and a Kind-specific migration both targeting the same
    /// <see cref="IStateMigration.FromVersion"/> — regardless of which of
    /// the two is registered first, since <see cref="Find"/> always
    /// prefers the common chain (`ADR-0120` Decision 2), so the other one
    /// would never run.
    /// </exception>
    public void Register(IStateMigration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);

        var fromVersion = migration.FromVersion;

        if (migration.Kind is not { } kind)
        {
            // Registering a common (Kind-less) migration.
            if (_common.TryGetValue(fromVersion, out _))
                throw new DuplicateStateMigrationException(null, fromVersion);

            // Would this common migration silently shadow an
            // already-registered Kind-specific migration at the same
            // FromVersion, for any Kind? Checked before the write below,
            // not after — a guard that only catches "common registered
            // second" and not "common registered first" is worse than
            // none, because it reads as complete.
            foreach (var (existingKind, chain) in _byKind)
            {
                if (chain.ContainsKey(fromVersion))
                    throw new ConflictingStateMigrationException(existingKind, fromVersion);
            }

            _common[fromVersion] = migration;
            return;
        }

        // Looked up, never written, until every check below has passed —
        // see this method's own remarks: a throwing registration must
        // leave no trace, including no empty chain for a Kind that was
        // never actually registered (`WP 16.4B-R3`).
        var kindChainAlreadyExists = _byKind.TryGetValue(kind, out var existingChain);
        var kindChain = kindChainAlreadyExists ? existingChain! : new Dictionary<int, IStateMigration>();

        if (kindChain.TryGetValue(fromVersion, out _))
            throw new DuplicateStateMigrationException(kind, fromVersion);

        // The symmetric direction of the same check: a common migration
        // already registered at this FromVersion would shadow the
        // Kind-specific migration being registered now.
        if (_common.TryGetValue(fromVersion, out _))
            throw new ConflictingStateMigrationException(kind, fromVersion);

        kindChain[fromVersion] = migration;

        // Only wired into `_byKind` now that this registration is
        // actually succeeding — a brand-new chain built above for a throw
        // that happened first (either check above) never reaches here, so
        // `_byKind` never gains a phantom empty entry for a Kind whose
        // registration failed.
        if (!kindChainAlreadyExists)
            _byKind[kind] = kindChain;
    }

    /// <summary>
    /// Whether a chain — populated or not — exists for <paramref name="kind"/>.
    /// Internal, and reached only by <c>Tempest.Core.Tests</c>
    /// (<c>InternalsVisibleTo</c>), to verify a throwing
    /// <see cref="Register"/> call left no phantom entry — see that
    /// method's own remarks (`WP 16.4B-R3`). Not read anywhere in
    /// production: <see cref="Find"/> never needs to know whether a chain
    /// merely exists, only whether it holds a migration for one version.
    /// </summary>
    internal bool HasChainFor(string kind) => _byKind.ContainsKey(kind);

    /// <inheritdoc />
    public IStateMigration? Find(string kind, int fromVersion)
    {
        if (_common.TryGetValue(fromVersion, out var common))
            return common;

        return _byKind.TryGetValue(kind, out var chain) && chain.TryGetValue(fromVersion, out var migration)
            ? migration
            : null;
    }
}

/// <summary>
/// Thrown when <see cref="StateMigrationRegistry.Register"/> is called
/// for a chain (common, or a specific Kind) that already has a migration
/// registered for the same <see cref="IStateMigration.FromVersion"/> —
/// first registration wins; a colliding, later registration is rejected,
/// never a silent last-wins overwrite (the same `TD-69` defect class
/// `WP 16.4B` fixed for <see cref="Tempest.Core.DependencyInjection.IServiceCollection.Add"/>,
/// recurring here one file away).
/// </summary>
public sealed class DuplicateStateMigrationException : EngineeringDomainException
{
    /// <summary>Initialises a new instance of the <see cref="DuplicateStateMigrationException"/> class.</summary>
    /// <param name="kind">The contested Kind, or <see langword="null"/> for the common (Kind-less) chain.</param>
    /// <param name="fromVersion">The contested <see cref="IStateMigration.FromVersion"/>.</param>
    public DuplicateStateMigrationException(string? kind, int fromVersion)
        : base(kind is null
            ? $"A common (Kind-less) migration is already registered for FromVersion {fromVersion}."
            : $"A migration is already registered for Kind '{kind}' at FromVersion {fromVersion}.")
    {
        Kind = kind;
        FromVersion = fromVersion;
    }

    /// <summary>The contested Kind, or <see langword="null"/> for the common (Kind-less) chain.</summary>
    public string? Kind { get; }

    /// <summary>The contested <see cref="IStateMigration.FromVersion"/>.</summary>
    public int FromVersion { get; }
}

/// <summary>
/// Thrown when <see cref="StateMigrationRegistry.Register"/> would leave
/// a common (Kind-less) migration and a Kind-specific migration both
/// registered for the same <see cref="IStateMigration.FromVersion"/> —
/// <see cref="StateMigrationRegistry.Find"/> always prefers the common
/// chain (`ADR-0120` Decision 2), so the Kind-specific migration would
/// never run, yet the record would still advance to the target version
/// and look fully migrated. Rejected regardless of which of the two is
/// registered first.
/// </summary>
public sealed class ConflictingStateMigrationException : EngineeringDomainException
{
    /// <summary>Initialises a new instance of the <see cref="ConflictingStateMigrationException"/> class.</summary>
    /// <param name="kind">The Kind whose own migration collides with the common chain at <paramref name="fromVersion"/>.</param>
    /// <param name="fromVersion">The contested <see cref="IStateMigration.FromVersion"/>.</param>
    public ConflictingStateMigrationException(string kind, int fromVersion)
        : base(
            $"A common (Kind-less) migration and a Kind-specific migration for Kind '{kind}' both target " +
            $"FromVersion {fromVersion}. The common migration always runs first (ADR-0120 Decision 2), so the " +
            "Kind-specific one would never run even though the record would still advance to the target version. " +
            "Register only one migration for this FromVersion.")
    {
        Kind = kind;
        FromVersion = fromVersion;
    }

    /// <summary>The Kind whose own migration collides with the common chain at <see cref="FromVersion"/>.</summary>
    public string Kind { get; }

    /// <summary>The contested <see cref="IStateMigration.FromVersion"/>.</summary>
    public int FromVersion { get; }
}
