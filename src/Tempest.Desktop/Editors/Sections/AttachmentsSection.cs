using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Files;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The Documents Attachments section (`WP 10.7A`, `TD-80`, `WP 19.4B`) —
/// gated on <see cref="IHasAttachments"/>. Lists already-attached metadata,
/// the drop zone and Browse… (every Kind), the typed reference mini-form
/// (collapsed by default). Moved verbatim from <see cref="ObjectEditorView"/>'s
/// own former <c>PopulateAttachmentsAsync</c>/<c>OnAttachAsync</c>/
/// <c>OnAddFileViaPickerAsync</c>/<c>AttachFilesAsync</c>/the four
/// <c>OnAttachmentsDrag*</c>/<c>OnAttachmentsDrop</c> handlers/
/// <c>SetAttachmentsDropZoneHighlighted</c>/<c>OnExportAttachmentAsync</c>
/// (`WP 21.1B`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two compatibility seams the shell still keeps, deliberately, because
/// this section's split cannot remove them without either widening
/// <see cref="IEditorSection"/> itself or editing an existing test:</b>
/// </para>
/// <list type="bullet">
/// <item><description><c>ObjectEditorView.AttachFilesAsync</c> stays as a
/// one-line delegating wrapper — <c>AttachmentsSectionTests.AttachFilesAsync_...</c>
/// calls <c>editor.AttachFilesAsync(...)</c> directly (an `internal`
/// member reached via `InternalsVisibleTo`), and moving the real method
/// here without a same-named/same-signature forwarding member on the shell
/// would break that call.</description></item>
/// <item><description><c>ObjectEditorView.PopulateAttachmentsAsync</c>
/// stays too — <c>ObjectThatIsNotIHasAttachments_ShowsNoAttachmentsSection</c>
/// finds and invokes it by name through reflection (<c>GetPrivateMethod</c>),
/// since no live Kind reachable through the real Repository implements
/// nothing but <see cref="IEngineeringObject"/> to exercise the gate's
/// negative branch honestly any other way. The shell's own forwarding
/// method keeps the exact original name and signature
/// (<c>Task PopulateAttachmentsAsync(IEngineeringObject target)</c>) so
/// that reflection call keeps resolving, unchanged.</description></item>
/// </list>
/// <para>
/// <see cref="OpenAttachmentRequested"/> itself — the public event a
/// document viewer subscribes to — also stays on the shell (this section
/// has no reason to know a viewer exists), reached here only through
/// <see cref="EditorSectionContext.GetOpenAttachmentHandler"/>; see that
/// property's own remarks.
/// </para>
/// </remarks>
internal sealed class AttachmentsSection : IEditorSection
{
    private readonly StackPanel _listPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBox _fileNameBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _contentTypeBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _sizeBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _addButton = new() { Content = "Attach", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _browseButton = new() { Content = "Browse…", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly TextBlock _dropZoneLabel = new() { FontSize = DesignTokens.FontSizeBody, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _dropZone = new()
    {
        BorderThickness = new Thickness(1.5),
        CornerRadius = new CornerRadius(DesignTokens.PanelCornerRadius),
        Padding = new Thickness(DesignTokens.SpaceMd),
    };
    private Expander _referenceExpander = null!;
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    /// <summary>The object this section was last built from, so <see cref="ReloadIfPopulatedAsync"/> can rebuild the attachment rows without going back to the repository for a second read — mirrors the pre-split shell's own <c>_populatedTarget</c> field exactly.</summary>
    private IEngineeringObject? _lastSubject;

    public string Title => "Attachments";

    public bool AppliesTo(IEngineeringObject? subject) => subject is IHasAttachments;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        var dropZoneContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = DesignTokens.SpaceXs,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        dropZoneContent.Children.Add(_dropZoneLabel);
        dropZoneContent.Children.Add(_browseButton);
        _dropZone.Child = dropZoneContent;
        Avalonia.Automation.AutomationProperties.SetName(_dropZone, "Drop a file here, or Browse…");
        ThemeReactiveBrush.Bind(_dropZone, Border.BorderBrushProperty, BrandPalette.HairlineStrongBrushKey);
        ThemeReactiveBrush.Bind(_dropZone, Border.BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);

        var referencePanel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        referencePanel.Children.Add(EditorSectionHelpers.LabeledRow("File Name", _fileNameBox));
        referencePanel.Children.Add(EditorSectionHelpers.LabeledRow("Content Type", _contentTypeBox));
        referencePanel.Children.Add(EditorSectionHelpers.LabeledRow("Size (bytes)", _sizeBox));
        referencePanel.Children.Add(_addButton);
        _referenceExpander = new Expander
        {
            Header = "Record a reference without the file",
            IsExpanded = false,
            Content = referencePanel,
        };

        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(_listPanel);
        panel.Children.Add(new Separator());
        panel.Children.Add(_dropZone);
        panel.Children.Add(_referenceExpander);
        panel.Children.Add(_statusMessage);

        _addButton.Classes.Add(ChromeStyles.Primary);
        _browseButton.Classes.Add(ChromeStyles.Primary);
        _addButton.Click += async (_, _) => await OnAttachAsync().ConfigureAwait(true);
        _browseButton.Click += async (_, _) => await OnAddFileViaPickerAsync().ConfigureAwait(true);

        // `WP 19.4B` — the Attachments section's own drop target, mirroring
        // `EvidenceWorkspaceView`'s identical `SetAllowDrop`/`DragOverEvent`/
        // `DropEvent` wiring.
        DragDrop.SetAllowDrop(_dropZone, true);
        _dropZone.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        _dropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        _dropZone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        _dropZone.AddHandler(DragDrop.DropEvent, OnDrop);

        _expander = EditorSectionHelpers.BuildSection(Title, panel);
        _expander.IsVisible = false;
        return _expander;
    }

    public async Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        _lastSubject = subject;

        if (subject is not IHasAttachments attachable)
        {
            _expander.IsVisible = false;
            return;
        }

        _expander.IsVisible = true;
        _listPanel.Children.Clear();

        // `WP 19.4B`: every `IHasAttachments` Kind gets the drop zone and
        // Browse — the Evidence-only `Kind` gate this used to carry is
        // gone. `_ctx.EvidenceSupport` being `null` (no `IFilePicker`
        // wired) still leaves Browse — and the "or Browse…" half of the
        // drop zone's own label — honestly unavailable rather than run
        // without one; the drop zone itself and `AttachFilesAsync` need no
        // picker at all.
        _browseButton.IsVisible = _ctx.EvidenceSupport is not null;
        _dropZoneLabel.Text = _ctx.EvidenceSupport is not null ? "Drop a file here, or" : "Drop a file here.";

        var attachments = await attachable.GetAttachmentsAsync().ConfigureAwait(true);
        if (attachments.Count == 0)
        {
            _listPanel.Children.Add(new TextBlock { Text = "No attachments recorded.", Opacity = 0.7 });
        }
        else
        {
            var openHandler = _ctx.GetOpenAttachmentHandler();

            foreach (var attachment in attachments)
            {
                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        IconGeometry.Build(IconGeometry.Paperclip, 13),
                        new TextBlock
                        {
                            Text = $"{attachment.FileName}  ({attachment.ContentType}, {attachment.SizeInBytes:N0} bytes"
                                + (attachment.ContentHash is { } hash ? $", sha256 {hash}" : string.Empty) + ")",
                            FontSize = DesignTokens.FontSizeBody,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                };

                if (openHandler is not null)
                {
                    var open = new Button { Content = "Open", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
                    open.Classes.Add(ChromeStyles.Flat);
                    var captured = attachment;

                    // `WP 16.5A-R2` — named per file, not just "Open", so a
                    // screen reader user sweeping several attachments can
                    // tell them apart.
                    Avalonia.Automation.AutomationProperties.SetName(open, $"Open {captured.FileName}");
                    open.Click += (_, _) => _ctx.GetOpenAttachmentHandler()?.Invoke(attachable, captured);
                    row.Children.Add(open);
                }

                // `WP 18.2B`, §2: Export — offered for every attachment on
                // an Evidence record, not the issue sheet alone.
                if (_ctx.EvidenceSupport is not null && string.Equals(_ctx.ObjectKind, Core.Evidence.Evidence.CanonicalKind, StringComparison.Ordinal))
                {
                    var export = new Button { Content = "Export", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
                    export.Classes.Add(ChromeStyles.Flat);
                    var captured = attachment;
                    Avalonia.Automation.AutomationProperties.SetName(export, $"Export {captured.FileName}");
                    export.Click += async (_, _) => await OnExportAttachmentAsync(attachable, captured).ConfigureAwait(true);
                    row.Children.Add(export);
                }

                _listPanel.Children.Add(row);
            }
        }

        // `WP 21.2A` (re-applied at the `WP 21.1B` merge): a Document's own
        // editor offers "Export as report" — the technical-report renderer
        // over its revisions — once a subscriber for it exists.
        if (_ctx.GetExportReportHandler?.Invoke() is not null
            && string.Equals(_ctx.ObjectKind, DocumentObjectFactoryRegistry.Document, StringComparison.Ordinal))
        {
            var exportReport = new Button { Content = "Export as report", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
            exportReport.Classes.Add(ChromeStyles.Flat);
            Avalonia.Automation.AutomationProperties.SetName(exportReport, "Export as report");
            var targetId = _ctx.ObjectId;
            exportReport.Click += (_, _) => _ctx.GetExportReportHandler?.Invoke()?.Invoke(targetId);
            _listPanel.Children.Add(exportReport);
        }

        _fileNameBox.Text = string.Empty;
        _contentTypeBox.Text = string.Empty;
        _sizeBox.Text = string.Empty;
        _statusMessage.Text = string.Empty;
    }

    /// <summary>
    /// Runs <see cref="LoadAsync"/> fire-and-forget against
    /// <see cref="_lastSubject"/>, for the shell's own
    /// <c>OpenAttachmentRequested</c> accessor on its first subscriber
    /// (`WP 18.1A`) — a failure is reported through
    /// <see cref="EditorSectionContext.ReportAction"/> rather than thrown
    /// into the void. A no-op when nothing has populated this section yet
    /// (<see cref="_lastSubject"/> still <see langword="null"/>).
    /// </summary>
    internal async Task ReloadIfPopulatedAsync()
    {
        if (_lastSubject is null)
            return;

        try
        {
            await LoadAsync(_lastSubject, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _ctx.ReportAction($"Failed to load attachments: {ex.Message}", ActionOutcome.Failed);
        }
    }

    /// <summary>Files' own "add via picker" affordance — reads real bytes through <see cref="IFilePicker"/> and attaches them directly, never the metadata-only mini-form below it.</summary>
    private async Task OnAddFileViaPickerAsync()
    {
        if (_ctx.EvidenceSupport is null || _lastSubject is not IHasAttachments attachable)
            return;

        var picked = await _ctx.EvidenceSupport.FilePicker.PickFilesAsync(
            new FilePickerRequest("Pick files to attach", AllowMultiple: true)).ConfigureAwait(true);

        if (picked.Count == 0)
            return;

        foreach (var file in picked)
        {
            var content = await file.ReadAsync().ConfigureAwait(true);
            await attachable.AttachContentAsync(file.Name, file.ContentType, content).ConfigureAwait(true);
        }

        await _ctx.RefreshAsync().ConfigureAwait(true);
        var message = $"Attached {picked.Count} file(s).";
        _statusMessage.Text = message;
        _ctx.ReportAction(message, ActionOutcome.From(true));
    }

    /// <summary>
    /// Reads each path's bytes straight off disk and attaches it — the
    /// same real-bytes path <see cref="OnAddFileViaPickerAsync"/> uses,
    /// minus the <see cref="IFilePicker"/> indirection a drop does not
    /// need. One attachment per path (`WP 19.4B`, Scope §2).
    /// </summary>
    /// <remarks>Both <see cref="OnDrop"/> and <c>Tempest.Desktop.Tests</c> (via <see cref="ObjectEditorView.AttachFilesAsync"/>) call this directly — headless Avalonia cannot raise a real OS drag/drop reliably.</remarks>
    internal async Task AttachFilesAsync(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (paths.Count == 0 || _lastSubject is not IHasAttachments attachable)
            return;

        foreach (var path in paths)
        {
            var fileName = Path.GetFileName(path);
            var content = await File.ReadAllBytesAsync(path).ConfigureAwait(true);
            await attachable.AttachContentAsync(fileName, FileContentTypes.ForFileName(fileName), content).ConfigureAwait(true);
        }

        await _ctx.RefreshAsync().ConfigureAwait(true);
        var message = paths.Count == 1 ? $"Attached '{Path.GetFileName(paths[0])}'." : $"Attached {paths.Count} file(s).";
        _statusMessage.Text = message;
        _ctx.ReportAction(message, ActionOutcome.From(true));
    }

    private void OnDragEnter(object? sender, DragEventArgs e) => SetDropZoneHighlighted(true);

    private void OnDragLeave(object? sender, DragEventArgs e) => SetDropZoneHighlighted(false);

#pragma warning disable CS0618 // 'DragEventArgs.Data' is obsolete — see EvidenceWorkspaceView.OnListDragOver's own identical remark: the old IDataObject API is fully functional, Avalonia only warns, and the typed DataTransfer/DataFormat<T> replacement has no built-in file format.
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var isFileDrag = e.Data.Contains(DataFormats.Files);
        e.DragEffects = isFileDrag ? DragDropEffects.Copy : DragDropEffects.None;
        SetDropZoneHighlighted(isFileDrag);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        SetDropZoneHighlighted(false);

        var dropped = e.Data.GetFiles()?.OfType<Avalonia.Platform.Storage.IStorageFile>()
            .Select(f => f.Path.LocalPath).ToList();
        if (dropped is not { Count: > 0 })
            return;

        await AttachFilesAsync(dropped).ConfigureAwait(true);
    }
#pragma warning restore CS0618

    /// <summary>Paints the drop zone's border/background from the accent/hover tokens while a file drag is over it, or back to the resting hairline tokens otherwise.</summary>
    private void SetDropZoneHighlighted(bool highlighted)
    {
        var borderKey = highlighted ? BrandPalette.AccentBrushKey : BrandPalette.HairlineStrongBrushKey;
        var backgroundKey = highlighted ? BrandPalette.HoverBackgroundBrushKey : BrandPalette.SurfaceBackgroundBrushKey;
        var variant = _dropZone.ActualThemeVariant;

        if (Application.Current?.TryGetResource(borderKey, variant, out var border) == true && border is IBrush borderBrush)
            _dropZone.BorderBrush = borderBrush;
        if (Application.Current?.TryGetResource(backgroundKey, variant, out var background) == true && background is IBrush backgroundBrush)
            _dropZone.Background = backgroundBrush;
    }

    /// <summary>Export: reads one attachment's own verified bytes and saves them through <see cref="IFilePicker.PickSavePathAsync"/> (`WP 18.2B`, §2).</summary>
    private async Task OnExportAttachmentAsync(IHasAttachments attachable, IAttachment attachment)
    {
        if (_ctx.EvidenceSupport is null)
            return;

        var destination = await _ctx.EvidenceSupport.FilePicker
            .PickSavePathAsync(new SavePickerRequest($"Export {attachment.FileName}", attachment.FileName), CancellationToken.None)
            .ConfigureAwait(true);

        if (destination is null)
        {
            _statusMessage.Text = "Export was cancelled.";
            return;
        }

        var content = await attachable.ReadAttachmentContentAsync(attachment.Id).ConfigureAwait(true);
        if (!content.IsAvailable)
        {
            _statusMessage.Text = $"'{attachment.FileName}' could not be read — its stored content is {content.Status}.";
            _ctx.ReportAction(_statusMessage.Text, ActionOutcome.Failed);
            return;
        }

        await File.WriteAllBytesAsync(destination, content.Bytes, CancellationToken.None).ConfigureAwait(true);

        // Reading and saving a copy changes nothing in the domain — never
        // `ActionOutcome.Changed`.
        var message = $"Exported '{attachment.FileName}' to '{destination}'.";
        _statusMessage.Text = message;
        _ctx.ReportAction(message, ActionOutcome.NoChange);
    }

    private async Task OnAttachAsync()
    {
        if (string.IsNullOrWhiteSpace(_fileNameBox.Text))
        {
            _statusMessage.Text = "A file name is required.";
            return;
        }

        if (!long.TryParse(_sizeBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var sizeInBytes) || sizeInBytes < 0)
        {
            _statusMessage.Text = "Size (bytes) must be a non-negative whole number.";
            return;
        }

        var contentType = EditorSectionHelpers.NullIfEmpty(_contentTypeBox.Text) ?? "application/octet-stream";

        var result = await _ctx.CommandDispatcher.DispatchAsync(
            new AttachDocumentCommand(_ctx.ObjectId, _ctx.ObjectKind, _fileNameBox.Text, contentType, sizeInBytes),
            CancellationToken.None).ConfigureAwait(true);

        // Refresh() before the final message — see BillOfMaterialsSection's own identical remarks.
        var message = result.Succeeded ? "Attached." : result.Message ?? "Attach failed.";
        if (result.Succeeded)
            await _ctx.RefreshAsync().ConfigureAwait(true);
        _statusMessage.Text = message;
        _ctx.ReportAction(message, ActionOutcome.From(result.Succeeded));
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
