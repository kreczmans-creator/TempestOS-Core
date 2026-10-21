using Tempest.Core.Logging;
using Tempest.Core.Settings;

namespace Tempest.Desktop.Documents;

/// <summary>
/// The last revision each exported document reached, by its number — so
/// exporting <c>TESTY-TRIALX-REG-001</c> again saves <c>…-REG-001-R2</c>
/// (`v0.25.1` PO decision: every document is revision controlled; a
/// re-export is the same number at the next revision).
/// </summary>
/// <remarks>
/// Kept with the Desktop's other per-user settings, like Recent Objects.
/// A document with a revision of its own (a quote's approval revision, a
/// Document object's content revision, an evidence issue revision) passes
/// that instead and is not counted here.
/// </remarks>
public sealed class ExportRevisionLog
{
    /// <summary>The setting the log is stored under.</summary>
    public const string SettingKey = "Desktop.ExportRevisions";

    private readonly SettingsDocument<Dictionary<string, int>> _document;
    private Dictionary<string, int>? _revisions;

    /// <summary>Initialises a new instance of the <see cref="ExportRevisionLog"/> class.</summary>
    public ExportRevisionLog(ISettingsProvider settingsProvider, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        _document = new SettingsDocument<Dictionary<string, int>>(settingsProvider, SettingKey, "Export Revisions", logger);
    }

    /// <summary>The revision the next export of <paramref name="reference"/> takes: 1 the first time.</summary>
    public async Task<int> NextAsync(string reference, CancellationToken cancellationToken = default)
    {
        var revisions = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return revisions.TryGetValue(Key(reference), out var last) ? last + 1 : 1;
    }

    /// <summary>Records that <paramref name="reference"/> was exported at <paramref name="revision"/>.</summary>
    public async Task RecordAsync(string reference, int revision, CancellationToken cancellationToken = default)
    {
        var revisions = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var key = Key(reference);
        if (!revisions.TryGetValue(key, out var last) || revision > last)
            revisions[key] = revision;
        await _document.SaveAsync(revisions, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, int>> LoadAsync(CancellationToken cancellationToken) =>
        _revisions ??= new Dictionary<string, int>(await _document.LoadAsync(cancellationToken).ConfigureAwait(false) ?? [], StringComparer.OrdinalIgnoreCase);

    private static string Key(string reference) => reference.Trim().ToUpperInvariant();
}
