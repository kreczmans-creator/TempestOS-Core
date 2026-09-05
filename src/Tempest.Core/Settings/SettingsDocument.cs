using System.Reflection;
using System.Text.Json;
using Tempest.Core.Logging;

namespace Tempest.Core.Settings;

/// <summary>
/// One JSON-serialised document held under a single
/// <see cref="ISettingDefinition.Key"/> — the shape nine separate settings
/// stores across Core, App and Desktop had each written out for themselves
/// (`WP-D2`, `TD-112`).
/// </summary>
/// <typeparam name="TDocument">
/// The plain, JSON-serialisable shape stored under <see cref="Key"/> — a
/// DTO record, or a list of them.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>The recovery contract is unchanged.</b> A missing value and a corrupt
/// one both degrade to <see langword="null"/>, which every caller already
/// reads as "use the documented first-run defaults"; neither raises, which
/// is <c>TD-60</c>'s own "never an exception" guarantee. This type exists to
/// stop that contract being restated nine times, not to alter it.
/// </para>
/// <para>
/// <b>What is new is that corruption is now audible.</b> Six of the nine
/// stores had no logger at all, so a torn write silently discarded a user's
/// window geometry, recent objects, favourites, panel layout, workspace
/// arrangement or saved macros with no record anywhere that it had happened.
/// Degrading safely is correct; degrading invisibly is not, and the two were
/// conflated. A caller that supplies no <see cref="ILogger"/> keeps exactly
/// the old behaviour.
/// </para>
/// <para>
/// <b>`TD-87`/`ADR-0120` Decision 6: an optional schema-version chain,
/// for a document that is a JSON object.</b> A caller that supplies no
/// <paramref name="migrations"/> — every current caller — keeps exactly
/// today's behaviour, at no cost: <see cref="LoadAsync"/> only ever
/// inspects <typeparamref name="TDocument"/> for a <c>SchemaVersion</c>
/// property when a chain was actually supplied. When one is, and
/// <typeparamref name="TDocument"/> declares a public, writable
/// <c>int SchemaVersion</c>, an absent or <c>0</c> value normalises to
/// <c>1</c> the same explicit way <c>EngineeringObjectStateStore</c>'s
/// own read path does, and the chain is walked the same way: repeatedly
/// applying whichever migration's <c>FromVersion</c> matches the
/// document's current version, until none does.
/// </para>
/// </remarks>
public sealed class SettingsDocument<TDocument>
    where TDocument : class
{
    /// <summary>
    /// The <typeparamref name="TDocument"/>'s own <c>SchemaVersion</c>
    /// property, found once per closed generic type rather than by name
    /// on every load — <see langword="null"/> for the great majority of
    /// documents, which declare none.
    /// </summary>
    private static readonly PropertyInfo? SchemaVersionProperty = typeof(TDocument).GetProperty("SchemaVersion", typeof(int));

    private readonly ISettingsProvider _settingsProvider;
    private readonly ILogger? _logger;
    private readonly IReadOnlyList<ISettingsMigration<TDocument>>? _migrations;

    /// <summary>
    /// Initialises a new instance of the <see cref="SettingsDocument{TDocument}"/>
    /// class, registering its own setting definition idempotently.
    /// </summary>
    /// <param name="settingsProvider">The substrate this document is stored in.</param>
    /// <param name="key">The <see cref="ISettingDefinition.Key"/> to store under.</param>
    /// <param name="displayName">The definition's own human-readable name.</param>
    /// <param name="logger">
    /// An optional logger used to record a corrupt stored value.
    /// <see langword="null"/> reproduces the previous, silent behaviour
    /// exactly.
    /// </param>
    /// <param name="migrations">
    /// The ordered migration chain a stored <typeparamref name="TDocument"/>
    /// may need to reach its current schema (`TD-87`, `ADR-0120`
    /// Decision 6). <see langword="null"/> — the default — runs no
    /// normalisation and no chain at all: today's behaviour, unchanged.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="settingsProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> or <paramref name="displayName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public SettingsDocument(
        ISettingsProvider settingsProvider,
        string key,
        string displayName,
        ILogger? logger = null,
        IReadOnlyList<ISettingsMigration<TDocument>>? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        _settingsProvider = settingsProvider;
        _logger = logger;
        _migrations = migrations;
        Key = key;

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(key, displayName, string.Empty));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Already registered by a prior instance against the same
            // ISettingsProvider (a restart) — idempotent, not an error. This
            // is the second block every one of the nine stores had written
            // out identically.
        }
    }

    /// <summary>Gets the key this document is stored under.</summary>
    public string Key { get; }

    /// <summary>
    /// Reads the stored document.
    /// </summary>
    /// <param name="cancellationToken">A token observed while reading.</param>
    /// <returns>
    /// The stored document, or <see langword="null"/> when nothing is stored
    /// <i>or</i> what is stored cannot be read — the caller's cue to use its
    /// own documented defaults. Never throws for either reason.
    /// </returns>
    public async Task<TDocument?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await _settingsProvider.GetValueAsync(Key, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(json))
            return null;

        TDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<TDocument>(json);
        }
        catch (JsonException ex)
        {
            // A corrupted stored value (e.g. a torn write) degrades to the
            // caller's own documented defaults — TD-60's contract, kept. The
            // difference is that it now leaves a trace.
            _logger?.Warning(
                $"Stored setting '{Key}' could not be read and was discarded; " +
                $"falling back to defaults. {ex.Message}");

            return null;
        }

        // `TD-87`/`ADR-0120` Decision 6: only when a chain was actually
        // supplied, and only when TDocument declares a SchemaVersion, is
        // there anything to normalise or migrate — every other caller
        // pays nothing.
        if (document is not null && _migrations is not null && SchemaVersionProperty is { CanRead: true, CanWrite: true })
            document = ApplyMigrations(document);

        return document;
    }

    /// <summary>
    /// Walks <paramref name="document"/> forward through <see cref="_migrations"/>,
    /// normalising an absent/<c>0</c> <c>SchemaVersion</c> to <c>1</c> the
    /// same explicit way <c>EngineeringObjectStateStore</c>'s own read
    /// path does, then repeatedly applying whichever migration's
    /// <c>FromVersion</c> matches the document's current version until
    /// none does.
    /// </summary>
    private TDocument? ApplyMigrations(TDocument document)
    {
        var version = (int)SchemaVersionProperty!.GetValue(document)!;
        if (version <= 0)
        {
            version = 1;
            SchemaVersionProperty.SetValue(document, version);
        }

        while (FindMigration(version) is { } migration)
        {
            document = migration.Migrate(document);
            version = migration.FromVersion + 1;
            SchemaVersionProperty.SetValue(document, version);
        }

        // `TD-87`/`ADR-0120`: a document the chain could not carry all the
        // way is discarded and logged, never handed back at whatever
        // version the walk happened to stop at. `EngineeringObjectStateStore`
        // has had this check since `WP 16.3B` — where Technical Review
        // rejected the implementation once, specifically for omitting it —
        // and this seam did not, an asymmetry the `v0.16.0` review board
        // found. There is no `TargetSchemaVersion` constant here, because
        // the chain is supplied per consumer rather than fixed by the
        // platform, so the target is the highest version this consumer's
        // own migrations can reach. Stopping below it means the chain has
        // a hole at `version`: a migration exists for some later version
        // that this document can never arrive at.
        var targetVersion = HighestReachableVersion();

        // **Deliberately asymmetric with `EngineeringObjectStateStore`, and
        // this is the reasoning rather than an oversight.** That store has
        // a fixed, platform-wide `CurrentSchemaVersion`, so a record above
        // it genuinely means "written by a newer build" and is rightly
        // discarded. Here the target is derived from *the calling
        // consumer's own supplied chain*, which is not a build version at
        // all: a caller supplying one migration at `FromVersion` 1 has a
        // target of 2, and a document at 5 is not thereby "from the
        // future" — it simply sits past everything this caller claims to
        // transform. Discarding it would throw away a document no
        // migration ever said it could not read.
        // The `v0.16.0` independent security review proposed a symmetric
        // "ahead" discard here by analogy with the state store. The
        // analogy does not hold, for the reason above; the analysis was
        // run, the change was written, and it broke
        // `ADocumentAlreadyPastEveryRegisteredMigration_IsLeftAlone`, a
        // pre-existing test that encodes this intent. Reverted rather than
        // overridden. The residual risk the reviewer correctly identified
        // — an older build silently reading a document a newer build gave
        // a new meaning to — is real but needs a per-consumer declared
        // current version, which this seam does not have; recorded as
        // `TD-134` rather than approximated with a target that means
        // something else.
        if (version < targetVersion)
        {
            _logger?.Warning(
                $"Stored setting '{Key}' is at schema version {version} and no migration bridges it to " +
                $"version {targetVersion}; it was discarded and the caller's own defaults apply. " +
                "This is a gap in the supplied migration chain, not a malformed document.");

            return null;
        }

        return document;
    }

    /// <summary>
    /// The schema version this consumer's own supplied migration chain can
    /// carry a document to — one past the highest <c>FromVersion</c> any
    /// supplied migration declares. <c>1</c> when no migration is supplied,
    /// so a document already at <c>1</c> is never treated as stuck.
    /// </summary>
    private int HighestReachableVersion()
    {
        var highest = 1;

        foreach (var migration in _migrations!)
        {
            if (migration.FromVersion + 1 > highest)
                highest = migration.FromVersion + 1;
        }

        return highest;
    }

    private ISettingsMigration<TDocument>? FindMigration(int fromVersion)
    {
        foreach (var migration in _migrations!)
        {
            if (migration.FromVersion == fromVersion)
                return migration;
        }

        return null;
    }

    /// <summary>Writes <paramref name="document"/> as this key's stored value.</summary>
    /// <param name="document">The document to store.</param>
    /// <param name="cancellationToken">A token observed while writing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public Task SaveAsync(TDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        return _settingsProvider.SetValueAsync(Key, JsonSerializer.Serialize(document), cancellationToken);
    }
}
