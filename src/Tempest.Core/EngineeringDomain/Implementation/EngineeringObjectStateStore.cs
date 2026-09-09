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
public sealed class EngineeringObjectStateStore : IEngineeringObjectStateStore, ITransactionalStateWriter
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

    private readonly IQueryablePersistenceStore _persistenceStore;
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
        IQueryablePersistenceStore persistenceStore,
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
        IQueryablePersistenceStore persistenceStore,
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
    /// <remarks>
    /// One record, one transaction. Since `ADR-0145` the domain never
    /// calls this — every durable change to an engineering object is
    /// written through <see cref="ITransactionalStateWriter"/>, inside the
    /// transaction that also carries the object's documents, revisions,
    /// references, attachment bytes and audit row. This remains for a
    /// caller outside the domain (a migration tool, a test fixture
    /// seeding a record) that wants to write one state record on its own.
    /// </remarks>
    public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var json = JsonSerializer.Serialize(state, StateJsonOptions);

        return _persistenceStore.ExecuteInTransactionAsync(
            (transaction, token) => transaction.WriteAsync(StateCollectionName, state.Id.ToString("N"), json, token),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EngineeringObjectState?> FindAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var key = objectId.ToString("N");
        var values = await _persistenceStore.ReadManyAsync(StateCollectionName, [key], cancellationToken).ConfigureAwait(false);

        return values.TryGetValue(key, out var json) && json is not null ? Deserialise(objectId, json) : null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>One query, not <c>1 + n</c> (`ADR-0145`).</b> This used to list
    /// every key and then read each one, so a restart cost one round trip
    /// per object before a single object had been reconstructed.
    /// <see cref="IQueryablePersistenceStore.ReadAllAsync"/> returns the
    /// whole collection in one indexed read; a key that is not an object
    /// Id is still skipped with the same warning, because a foreign
    /// record beside this store's own is still not a record of its.
    /// </remarks>
    public async Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _persistenceStore.ReadAllAsync(StateCollectionName, cancellationToken).ConfigureAwait(false);
        var states = new List<EngineeringObjectState>(records.Count);

        foreach (var (key, json) in records)
        {
            if (!Guid.TryParseExact(key, "N", out var objectId))
            {
                // A foreign record beside the store's own is not a record.
                _logger?.Warning($"Ignoring non-state key '{key}' in '{StateCollectionName}'.");
                continue;
            }

            if (Deserialise(objectId, json) is { } state)
                states.Add(state);
        }

        return states;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid objectId, CancellationToken cancellationToken = default) =>
        _persistenceStore.ExecuteInTransactionAsync(
            (transaction, token) => transaction.DeleteAsync(StateCollectionName, objectId.ToString("N"), token),
            cancellationToken);

    /// <inheritdoc />
    async Task ITransactionalStateWriter.SaveAsync(IPersistenceTransaction transaction, EngineeringObjectState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(state);

        await transaction.WriteAsync(
            StateCollectionName,
            state.Id.ToString("N"),
            JsonSerializer.Serialize(state, StateJsonOptions),
            cancellationToken).ConfigureAwait(false);

        // `WP 18.1B`: the search index is written inside the same
        // transaction as the object state it describes — never a separate
        // write — so a rolled-back mutation leaves no index row and a
        // committed one is searchable the instant the commit lands. A
        // soft-deleted object (`state.IsDeleted`) is removed from the
        // index rather than re-indexed: it still exists durably (this is
        // not a hard delete), but nothing "recently changed" or findable
        // should surface it as a live object.
        if (state.IsDeleted)
            await transaction.RemoveFromIndexAsync(state.Id, cancellationToken).ConfigureAwait(false);
        else
            await IndexStateAsync(transaction, state, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Walks every object state and repopulates the search index from
    /// scratch, inside one transaction — self-healing (`WP 18.1B`): runs
    /// at host start, and does nothing unless the index is currently empty
    /// while the object state collection is not, so an index that was
    /// built normally by every write above is never redundantly rebuilt.
    /// </summary>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        if (!await _persistenceStore.IsSearchIndexEmptyAsync(cancellationToken).ConfigureAwait(false))
            return;

        var states = await ListAsync(cancellationToken).ConfigureAwait(false);
        if (states.Count == 0)
            return;

        await _persistenceStore.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                foreach (var state in states)
                {
                    if (state.IsDeleted)
                        continue;

                    await IndexStateAsync(transaction, state, token).ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes one object state's own search-index row (`WP 18.1B`): its title, business identifier, Kind, resolved project, and Kind-specific <see cref="BuildRefs"/> text.</summary>
    private static async Task IndexStateAsync(IPersistenceTransaction transaction, EngineeringObjectState state, CancellationToken cancellationToken)
    {
        var projectId = await ResolveProjectIdAsync(transaction, state, cancellationToken).ConfigureAwait(false);

        await transaction.IndexTextAsync(
            state.Id, state.Kind, projectId, state.DisplayName, state.Identifier, BuildRefs(state), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The canonical Kind naming a Mechanical Product Structure project — mirrors <c>MechanicalObjectFactoryRegistry.Project</c> (`Tempest.Workspace`, not referenceable here) as a plain literal.</summary>
    private const string ProjectKind = "Project";

    /// <summary>The most hops <see cref="ResolveProjectIdAsync"/> walks up a parent chain before giving up — generous for any structure this platform actually builds, and a hard stop against a corrupt cyclic parent chain looping forever.</summary>
    private const int MaxProjectAncestryHops = 32;

    /// <summary>
    /// Walks <paramref name="state"/>'s own parent chain to find the
    /// project it sits under (`WP 18.1B`) — the object itself, if it is a
    /// <see cref="ProjectKind"/>; otherwise the nearest ancestor that is;
    /// <see langword="null"/> if none is found (a standalone object, or an
    /// ancestor whose own state is missing or unreadable).
    /// </summary>
    private static async Task<Guid?> ResolveProjectIdAsync(IPersistenceTransaction transaction, EngineeringObjectState state, CancellationToken cancellationToken)
    {
        if (string.Equals(state.Kind, ProjectKind, StringComparison.Ordinal))
            return state.Id;

        var parentId = state.ParentId;
        var hops = 0;

        while (parentId is { } id && hops++ < MaxProjectAncestryHops)
        {
            var json = await transaction.ReadAsync(StateCollectionName, id.ToString("N"), cancellationToken).ConfigureAwait(false);
            if (json is null)
                return null;

            EngineeringObjectState? parentState;
            try
            {
                parentState = JsonSerializer.Deserialize<EngineeringObjectState>(json, StateJsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }

            if (parentState is null)
                return null;

            if (string.Equals(parentState.Kind, ProjectKind, StringComparison.Ordinal))
                return parentState.Id;

            parentId = parentState.ParentId;
        }

        return null;
    }

    /// <summary>
    /// Kind-specific extra searchable text (`WP 18.1B` §1) — today, just
    /// Evidence's own issue reference, each citation's library and record
    /// id, and each declared figure's name, space-joined; <see langword="null"/>
    /// for every other Kind, or an Evidence record with none of these set
    /// yet.
    /// </summary>
    private static string? BuildRefs(EngineeringObjectState state)
    {
        // Fully qualified throughout, deliberately: `Tempest.Core.Evidence`
        // is both this platform's Evidence namespace and, within it, the
        // `Evidence` class itself — an unqualified `using` here would have
        // the namespace shadow the type (`EvidenceWorkspaceTests.cs` names
        // the identical ambiguity the same way).
        if (!string.Equals(state.Kind, Tempest.Core.Evidence.Evidence.CanonicalKind, StringComparison.Ordinal))
            return null;

        var parts = new List<string>();

        if (state.TypeJson<List<Tempest.Core.Evidence.EvidenceCitation>>(nameof(Tempest.Core.Evidence.Evidence.Citations)) is { } citations)
        {
            foreach (var citation in citations)
            {
                if (citation?.Pin is { } pin)
                    parts.Add($"{pin.Library} {pin.RecordId}");
            }
        }

        if (state.TypeJson<List<Tempest.Core.Evidence.DeclaredFigure>>(nameof(Tempest.Core.Evidence.Evidence.DeclaredFigures)) is { } figures)
        {
            foreach (var figure in figures)
            {
                if (!string.IsNullOrWhiteSpace(figure?.Name))
                    parts.Add(figure.Name);
            }
        }

        if (state.TypeJson<Tempest.Core.Evidence.IssueRecord>(nameof(Tempest.Core.Evidence.Evidence.Issue)) is { IssueReference.Length: > 0 } issue)
            parts.Add(issue.IssueReference);

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

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
