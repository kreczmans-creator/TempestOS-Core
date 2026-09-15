using Tempest.Workspace.Layout;
using Tempest.Workspace.Viewing;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Viewing;

/// <summary>
/// Opens an attachment as a document tab in the workspace (`TD-80`).
/// </summary>
/// <remarks>
/// <para>
/// The whole path from "an engineering object has an attachment" to "the
/// drawing is on screen, next to the object it belongs to": read the real
/// bytes through <c>TD-31</c>'s content store, decide the format from what
/// they are, build the page source, and dock a viewer beside the document
/// area as an ordinary <c>TD-72</c> panel.
/// </para>
/// <para>
/// Ordinary is the point. The viewer is not a special surface with a
/// reserved slot — it registers a <see cref="WorkspacePanelDescriptor"/>
/// like any other panel, so it tabs, splits, floats onto a second monitor,
/// collapses and persists with no code here for any of it. Opening a
/// second document is the same call again; there is no fixed number of
/// viewers, because there is no fixed grid to run out of.
/// </para>
/// <para>
/// Opening never navigates. The shell stays exactly where it was, so the
/// project context, the open object and the Explorer selection are all
/// still there when the tab is closed — a viewer that took over the
/// window would make "look at the drawing" cost the user their place.
/// </para>
/// </remarks>
public sealed class AttachmentViewerLauncher
{
    /// <summary>
    /// How many leading bytes <see cref="TryOpenStreamedAsync"/> peeks to
    /// detect a format (`TD-96`) — comfortably past
    /// <see cref="DocumentFormatDetector"/>'s longest signature (WEBP's,
    /// 12 bytes into the stream) without reading anything resembling a
    /// whole file.
    /// </summary>
    private const int FormatSniffLength = 32;

    private readonly WorkspacePanelRegistry _registry;
    private readonly WorkspaceLayoutController _layout;
    private readonly Guid _documentAreaPanelId;
    private readonly IAttachmentContentStore? _contentStore;
    private readonly Dictionary<Guid, Guid> _panelsByAttachment = [];

    /// <summary>Initialises a new instance of the <see cref="AttachmentViewerLauncher"/> class.</summary>
    /// <param name="registry">Where this launcher registers the panel it opens.</param>
    /// <param name="layout">The layout tree the opened panel is docked into.</param>
    /// <param name="documentAreaPanelId">The panel a document is tabbed alongside.</param>
    /// <param name="contentStore">
    /// The store this launcher reads an attachment's bytes through as a
    /// stream rather than a fully-materialised array (`TD-96`). Optional,
    /// and defaulting to <see langword="null"/>, so a caller that has not
    /// wired one keeps this launcher's previous behaviour exactly — it
    /// reads through <see cref="IHasAttachments.ReadAttachmentContentAsync"/>
    /// alone, as it always has.
    /// </param>
    public AttachmentViewerLauncher(
        WorkspacePanelRegistry registry,
        WorkspaceLayoutController layout,
        Guid documentAreaPanelId,
        IAttachmentContentStore? contentStore = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(layout);

        _registry = registry;
        _layout = layout;
        _documentAreaPanelId = documentAreaPanelId;
        _contentStore = contentStore;
    }

    /// <summary>Every attachment currently open in a viewer.</summary>
    public IReadOnlyCollection<Guid> OpenAttachmentIds => _panelsByAttachment.Keys;

    /// <summary>The panel showing <paramref name="attachmentId"/>, or <see langword="null"/> when it is not open.</summary>
    public Guid? PanelFor(Guid attachmentId) =>
        _panelsByAttachment.TryGetValue(attachmentId, out var panelId) ? panelId : null;

    /// <summary>The viewer showing <paramref name="attachmentId"/>, or <see langword="null"/> when it is not open.</summary>
    /// <remarks>
    /// The counterpart of <see cref="PanelFor"/> for callers that need the
    /// surface rather than its identity — chiefly a test that opened a
    /// document by pressing the button a user presses, and so never held
    /// the view <see cref="OpenAsync"/> returned.
    /// </remarks>
    public DocumentViewerView? ViewerFor(Guid attachmentId) =>
        PanelFor(attachmentId) is { } panelId ? _registry.Find(panelId)?.Content as DocumentViewerView : null;

    /// <summary>
    /// Opens <paramref name="attachment"/> of <paramref name="owner"/> in a
    /// viewer tab, reading its real content.
    /// </summary>
    /// <returns>The viewer that was opened or brought forward.</returns>
    public async Task<DocumentViewerView> OpenAsync(
        IHasAttachments owner,
        IAttachment attachment,
        double viewportWidth = 1000,
        double viewportHeight = 700,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(attachment);

        // Already open: bring it forward rather than opening a second tab
        // onto the same file, which is the behaviour every document
        // application has and the one a user expects.
        if (PanelFor(attachment.Id) is { } existingPanelId)
        {
            if (_layout.Tree.Contains(existingPanelId) &&
                _registry.Find(existingPanelId)?.Content is DocumentViewerView existing)
            {
                _layout.Apply(tree => tree.SelectPanel(existingPanelId));
                return existing;
            }

            // The panel is remembered here but no longer in the layout,
            // which is what closing the tab from the strip's own close
            // button leaves behind: `TD-72` removes the panel from the tree
            // and nothing tells this launcher. Forgetting it here is what
            // makes re-opening work — otherwise the second open selects a
            // panel that is not there, silently does nothing, and the
            // drawing is unreachable for the rest of the session.
            _panelsByAttachment.Remove(attachment.Id);
        }

        var view = new DocumentViewerView();

        // `TD-96`: read through a verified stream when this launcher has
        // been given a content store to do it with, and the format turns
        // out to be one the streamed path actually renders. Anything else
        // — no content store wired, or a format without a stream-based
        // source yet — falls back to the byte-array path unchanged.
        if (_contentStore is not null &&
            await TryOpenStreamedAsync(view, attachment, viewportWidth, viewportHeight, cancellationToken).ConfigureAwait(true))
        {
            Dock(view, attachment);
            return view;
        }

        var content = await owner.ReadAttachmentContentAsync(attachment.Id, cancellationToken).ConfigureAwait(true);

        if (content.Status is not AttachmentContentStatus.Available)
        {
            view.OpenUnavailable(DocumentViewSession.Unavailable(
                attachment.Id, attachment.FileName, attachment.ContentType,
                DocumentViewSession.StatusFor(content.Status)));
        }
        else
        {
            OpenLoadedContent(view, attachment, content.Bytes, viewportWidth, viewportHeight);
        }

        Dock(view, attachment);
        return view;
    }

    /// <summary>
    /// Opens <paramref name="attachment"/> through <see cref="_contentStore"/>'s
    /// streamed read (`TD-96`), never materialising its content as one
    /// array. Returns <see langword="false"/> only when the format is not
    /// (yet) one the streamed path renders — the caller then falls back to
    /// <see cref="OpenLoadedContent"/> over a fully read array, which still
    /// renders it; every other outcome (missing, corrupt, or genuinely
    /// unsupported) is handled here and reported identically to the
    /// byte-array path.
    /// </summary>
    private async Task<bool> TryOpenStreamedAsync(
        DocumentViewerView view,
        IAttachment attachment,
        double viewportWidth,
        double viewportHeight,
        CancellationToken cancellationToken)
    {
        var result = await _contentStore!.OpenReadAsync(
            attachment.Id, attachment.ContentHash, attachment.SizeInBytes, cancellationToken).ConfigureAwait(true);

        if (result.Status is not AttachmentContentStatus.Available)
        {
            result.Dispose();
            view.OpenUnavailable(DocumentViewSession.Unavailable(
                attachment.Id, attachment.FileName, attachment.ContentType,
                DocumentViewSession.StatusFor(result.Status)));
            return true;
        }

        var stream = result.Stream!;
        var handedOff = false;
        try
        {
            var header = new byte[FormatSniffLength];
            var headerLength = await ReadFullyAsync(stream, header, cancellationToken).ConfigureAwait(true);
            stream.Position = 0;

            var format = DocumentFormatDetector.Detect(attachment.ContentType, header.AsSpan(0, headerLength));

            IDocumentPageSource? source;
            try
            {
                source = DocumentPageSourceFactory.CreateFromStream(format, stream);
            }
            catch (DocumentRenderException)
            {
                // Same rule as the byte-array path: bytes that opened for
                // reading but did not render are a format problem, not a
                // damaged-content one — reported as such below rather than
                // accusing the user's file of being what it is not.
                source = null;
            }

            if (source is null)
                return false;

            handedOff = true;
            OpenSourceIntoView(view, attachment, format, source, viewportWidth, viewportHeight);
            return true;
        }
        finally
        {
            // The page source owns the stream from here once handed off —
            // disposing the view later disposes it (`DocumentViewerView.Open`).
            // Every other exit (unsupported format, or an exception above)
            // must close it itself, or the connection it holds leaks.
            if (!handedOff)
                stream.Dispose();
        }
    }

    /// <summary>Fills <paramref name="buffer"/> from <paramref name="stream"/>, stopping early at end of stream — a short file sniffs shorter than <paramref name="buffer"/>'s length, not incompletely.</summary>
    private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(true);
            if (read == 0)
                break;

            total += read;
        }

        return total;
    }

    private static void OpenLoadedContent(
        DocumentViewerView view,
        IAttachment attachment,
        byte[] bytes,
        double viewportWidth,
        double viewportHeight)
    {
        var format = DocumentFormatDetector.Detect(attachment.ContentType, bytes, attachment.FileName);

        IDocumentPageSource? source;
        try
        {
            source = DocumentPageSourceFactory.Create(format, bytes);
        }
        catch (DocumentRenderException)
        {
            // The bytes passed the store's integrity check — they are the
            // file that was attached — and this platform still could not
            // open them. That is a format problem, not a damaged-content
            // problem, and saying "damaged" would accuse the user's data
            // of something untrue.
            source = null;
        }

        if (source is null)
        {
            // No in-app renderer, whatever the reason — a DWG this
            // platform was never going to draw, or a format nobody has
            // written a source for yet. Either way the file itself is
            // intact, so a real copy is put where the OS shell can hand it
            // to whatever is registered for it; the viewer's own "Open
            // externally" button is the only thing that reads this path,
            // and only when it is not null (`TD-99`).
            var materialisedPath = MaterialiseForExternalOpen(attachment.Id, attachment.FileName, bytes);
            view.OpenUnavailable(DocumentViewSession.Unavailable(
                attachment.Id, attachment.FileName, attachment.ContentType, DocumentViewStatus.Unsupported, format, materialisedPath));
            return;
        }

        OpenSourceIntoView(view, attachment, format, source, viewportWidth, viewportHeight);
    }

    private static void OpenSourceIntoView(
        DocumentViewerView view,
        IAttachment attachment,
        ViewableDocumentFormat format,
        IDocumentPageSource source,
        double viewportWidth,
        double viewportHeight)
    {
        var firstPage = source.PageSize(0);
        view.Open(
            DocumentViewSession.Ready(
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                format,
                source.PageCount,
                firstPage.Width,
                firstPage.Height,
                viewportWidth,
                viewportHeight),
            source);
    }

    /// <summary>
    /// Writes <paramref name="bytes"/> to a real file on local disk, under
    /// the attachment's own file name, for the OS shell to open — never
    /// beside the persistence root, which this launcher has no path to and
    /// should not need one for a copy that exists only until the OS is
    /// done with it (`TD-99`).
    /// </summary>
    /// <returns>The written path, or <see langword="null"/> if the write itself failed.</returns>
    /// <remarks>
    /// One subdirectory per attachment id, under the OS's own temporary
    /// folder, so two attachments that happen to share a file name never
    /// collide and a stale copy from an earlier session is easy to
    /// recognise as this launcher's own.
    /// </remarks>
    private static string? MaterialiseForExternalOpen(Guid attachmentId, string fileName, byte[] bytes)
    {
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "TempestOS", "Viewer", attachmentId.ToString("N"));
            Directory.CreateDirectory(directory);

            var safeName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = attachmentId.ToString("N");

            var path = Path.Combine(directory, safeName);
            File.WriteAllBytes(path, bytes);
            return path;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void Dock(DocumentViewerView view, IAttachment attachment)
    {
        var panelId = Guid.NewGuid();
        _registry.Register(new WorkspacePanelDescriptor(panelId, attachment.FileName, view));
        _panelsByAttachment[attachment.Id] = panelId;

        _layout.Apply(tree =>
        {
            // Tabbed with the document area, which is where a document
            // belongs. `Into` is the ordinary insert `TD-72` made possible
            // by having the only leaf be a tab group — no special case here.
            var group = tree.FindGroupContaining(_documentAreaPanelId);
            return group is null
                ? tree.DockToEdge(panelId, DockRelation.Right)
                : tree.Dock(panelId, group.Id, DockRelation.Into);
        });
    }

    /// <summary>Closes the viewer showing <paramref name="attachmentId"/>, if one is open.</summary>
    /// <remarks>
    /// Removes the panel from the layout and forgets it here. The shell is
    /// untouched, so closing a drawing returns the user to exactly the
    /// project and object they were on.
    /// </remarks>
    public void Close(Guid attachmentId)
    {
        if (!_panelsByAttachment.TryGetValue(attachmentId, out var panelId))
            return;

        _panelsByAttachment.Remove(attachmentId);
        _layout.Apply(tree => tree.Remove(panelId));
    }
}
