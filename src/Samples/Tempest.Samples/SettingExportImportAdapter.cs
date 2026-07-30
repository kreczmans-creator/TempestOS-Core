using Tempest.Core.ExportImport;
using Tempest.Core.Settings;

namespace Tempest.Samples;

/// <summary>
/// Exports and re-imports a single Settings value, round-tripping it
/// through <see cref="IExportPayloadSerializer"/> — demonstrating
/// Export/Import's own Settings integration, and the optional
/// Serialization abstraction, together in one small adapter.
/// </summary>
/// <remarks>
/// Implements <see cref="IExportable"/>, <see cref="IExportableKind"/>, and
/// <see cref="IImportable"/> together: the same adapter both produces and
/// consumes its own artifact section, keyed by <see cref="Kind"/>. Reads
/// and writes through <see cref="ISettingsProvider"/> only — never touches
/// <see cref="Tempest.Core.Persistence.IPersistenceStore"/> directly, per
/// `ADR-0051`.
/// </remarks>
public sealed class SettingExportImportAdapter : IExportable, IExportableKind, IImportable
{
    /// <summary>The schema version this adapter's own payload shape uses.</summary>
    public const int CurrentSchemaVersion = 1;

    private const string ValueDataKey = "Value";

    private readonly ISettingsProvider _settingsProvider;
    private readonly IExportPayloadSerializer _serializer;
    private readonly string _settingKey;

    /// <summary>
    /// Initialises a new instance of the <see cref="SettingExportImportAdapter"/> class.
    /// </summary>
    /// <param name="settingsProvider">The Settings service this adapter reads from and writes to.</param>
    /// <param name="serializer">The serializer this adapter uses for its own payload.</param>
    /// <param name="settingKey">The setting key this adapter round-trips.</param>
    /// <param name="kind">This adapter's own artifact section kind.</param>
    public SettingExportImportAdapter(ISettingsProvider settingsProvider, IExportPayloadSerializer serializer, string settingKey, string kind)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(settingKey);
        ArgumentNullException.ThrowIfNull(kind);

        _settingsProvider = settingsProvider;
        _serializer = serializer;
        _settingKey = settingKey;
        Kind = kind;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public int SchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var value = await _settingsProvider.GetValueAsync(_settingKey, cancellationToken).ConfigureAwait(false);

        var payload = _serializer.Serialize(new Dictionary<string, string> { [ValueDataKey] = value });

        await destination.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ImportAsync(Stream payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var buffer = new MemoryStream();

        await payload.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        var data = _serializer.Deserialize(buffer.ToArray());

        await _settingsProvider.SetValueAsync(_settingKey, data[ValueDataKey], cancellationToken).ConfigureAwait(false);
    }
}
