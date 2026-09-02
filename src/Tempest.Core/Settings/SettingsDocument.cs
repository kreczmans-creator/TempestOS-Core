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
/// </remarks>
public sealed class SettingsDocument<TDocument>
    where TDocument : class
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ILogger? _logger;

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
    /// <exception cref="ArgumentNullException"><paramref name="settingsProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> or <paramref name="displayName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public SettingsDocument(ISettingsProvider settingsProvider, string key, string displayName, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        _settingsProvider = settingsProvider;
        _logger = logger;
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

        try
        {
            return JsonSerializer.Deserialize<TDocument>(json);
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
