using Tempest.Workspace.Files;

namespace Tempest.Desktop.Documents;

/// <summary>Whether <see cref="DocumentExporter.ExportAsync{TModel}"/> actually wrote a file, and, either way, the one line to show the user (`WP 21.2A`).</summary>
/// <param name="Succeeded">Whether a file was written. <see langword="false"/> for a cancelled picker — never treated as an error.</param>
/// <param name="Destination">The path written to, when <see cref="Succeeded"/>. <see langword="null"/> otherwise.</param>
/// <param name="Message">What to show the user — "Exported to '...'." or "Export was cancelled.", the exact wording <see cref="Tempest.Desktop.Views.ProjectQuoteView.OnExportAsync"/> already established.</param>
public sealed record DocumentExportResult(bool Succeeded, string? Destination, string Message);

/// <summary>
/// Runs a renderer, names the file <c>&lt;reference&gt;-&lt;template&gt;.pdf</c>,
/// and writes it through <see cref="IFilePicker.PickSavePathAsync"/> — the
/// same file-picker path <see cref="Tempest.Desktop.Views.ProjectQuoteView.OnExportAsync"/>
/// (`ADR-0152`) and <see cref="Tempest.Desktop.Editors.ObjectEditorView"/>'s
/// own attachment Export already use (`WP 18.2B`, §2), lifted here once
/// (`WP 21.2A`, scope item 1) so every one of the six export buttons this
/// Work Package wires shares it rather than re-implementing "pick a path,
/// render, write the bytes" six more times.
/// </summary>
public sealed class DocumentExporter
{
    private readonly IFilePicker _filePicker;

    /// <summary>Initialises a new instance of the <see cref="DocumentExporter"/> class.</summary>
    public DocumentExporter(IFilePicker filePicker)
    {
        ArgumentNullException.ThrowIfNull(filePicker);

        _filePicker = filePicker;
    }

    /// <summary>
    /// Renders <paramref name="model"/> through <paramref name="renderer"/>
    /// and saves it, pre-named <c>&lt;reference&gt;-&lt;renderer.TemplateName&gt;.pdf</c>
    /// (scope item 1's own naming rule) — a cancelled picker is reported,
    /// never thrown.
    /// </summary>
    public async Task<DocumentExportResult> ExportAsync<TModel>(
        IDocumentRenderer<TModel> renderer, TModel model, string reference, OrganisationIdentity? identity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        var fileName = $"{SanitiseFileNameSegment(reference)}-{renderer.TemplateName}.pdf";

        var destination = await _filePicker
            .PickSavePathAsync(new SavePickerRequest($"Export {reference}", fileName), cancellationToken)
            .ConfigureAwait(true);

        if (destination is null)
            return new DocumentExportResult(false, null, "Export was cancelled.");

        var bytes = renderer.Render(model, identity);
        await File.WriteAllBytesAsync(destination, bytes.ToArray(), cancellationToken).ConfigureAwait(true);

        return new DocumentExportResult(true, destination, $"Exported to '{destination}'.");
    }

    /// <summary>Replaces every character <see cref="Path.GetInvalidFileNameChars"/> names with <c>-</c> — the identical helper <see cref="Tempest.Desktop.Views.ProjectQuoteView"/> already carries, duplicated here rather than shared across two small, otherwise-unrelated classes.</summary>
    private static string SanitiseFileNameSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        return new string(chars);
    }
}
