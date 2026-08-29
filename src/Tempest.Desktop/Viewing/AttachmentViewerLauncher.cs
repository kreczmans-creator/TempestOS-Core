using Tempest.App.Workspace.Layout;
using Tempest.App.Workspace.Viewing;
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
    private readonly WorkspacePanelRegistry _registry;
    private readonly WorkspaceLayoutController _layout;
    private readonly Guid _documentAreaPanelId;
    private readonly Dictionary<Guid, Guid> _panelsByAttachment = [];

    /// <summary>Initialises a new instance of the <see cref="AttachmentViewerLauncher"/> class.</summary>
    public AttachmentViewerLauncher(WorkspacePanelRegistry registry, WorkspaceLayoutController layout, Guid documentAreaPanelId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(layout);

        _registry = registry;
        _layout = layout;
        _documentAreaPanelId = documentAreaPanelId;
    }

    /// <summary>Every attachment currently open in a viewer.</summary>
    public IReadOnlyCollection<Guid> OpenAttachmentIds => _panelsByAttachment.Keys;

    /// <summary>The panel showing <paramref name="attachmentId"/>, or <see langword="null"/> when it is not open.</summary>
    public Guid? PanelFor(Guid attachmentId) =>
        _panelsByAttachment.TryGetValue(attachmentId, out var panelId) ? panelId : null;

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

    private static void OpenLoadedContent(
        DocumentViewerView view,
        IAttachment attachment,
        byte[] bytes,
        double viewportWidth,
        double viewportHeight)
    {
        var format = DocumentFormatDetector.Detect(attachment.ContentType, bytes);

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
            view.OpenUnavailable(DocumentViewSession.Unavailable(
                attachment.Id, attachment.FileName, attachment.ContentType, DocumentViewStatus.Unsupported, format));
            return;
        }

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
