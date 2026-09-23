using System.Text;
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

    /// <summary>
    /// Extensions this launcher refuses to hand to the OS shell via
    /// <b>Open externally</b>, under any name (`TD-184`) — executables,
    /// shell shortcuts and scripts for every OS this platform ships to,
    /// checked against the file's own <em>rightmost</em> extension
    /// (<see cref="Path.GetExtension(string)"/>'s own convention — the one
    /// Windows itself treats as the type that runs, so
    /// <c>invoice.pdf.exe</c> is refused for its trailing <c>.exe</c> and
    /// nothing about the <c>.pdf</c> ahead of it matters, exactly as it
    /// would not to Windows either).
    /// </summary>
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // `WP 21.5F` (OSA-02) named these six beside the set below; the union is kept at merge.
        ".inf", ".isp", ".sct", ".shb", ".shs", ".vxd",
        // Windows native/shell-executed.
        ".exe", ".scr", ".com", ".bat", ".cmd", ".pif", ".msi", ".msp", ".mst",
        ".hta", ".cpl", ".msc", ".reg", ".scf", ".chm", ".dll", ".sys", ".drv",
        // Shortcuts / shell links.
        ".lnk", ".url", ".website", ".webloc",
        // Script hosts.
        ".js", ".jse", ".vbs", ".vbe", ".vb", ".vbscript", ".ws", ".wsf", ".wsh",
        ".ps1", ".ps1xml", ".psc1", ".psd1", ".psm1",
        // Packaged/installer or JVM-executed.
        ".jar", ".application", ".gadget", ".appref-ms", ".msix", ".appx",
        // macOS/Linux executed.
        ".command", ".sh", ".bash", ".zsh", ".workflow", ".action", ".desktop",
        ".appimage", ".run",
    };

    /// <summary>Windows reserved device names — never a legal file base name regardless of extension (`TD-184`).</summary>
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private readonly WorkspacePanelRegistry _registry;
    private readonly WorkspaceLayoutController _layout;
    private readonly Guid _documentAreaPanelId;
    private readonly IAttachmentContentStore? _contentStore;
    private readonly Dictionary<Guid, Guid> _panelsByAttachment = [];

    /// <summary>
    /// The materialised-copy directory this launcher created for each open
    /// attachment's <b>Open externally</b> path, so <see cref="Close"/> can
    /// delete it — one fresh, randomly-named directory per launch
    /// (`TD-184`), never reused across opens even of the identical
    /// attachment.
    /// </summary>
    private readonly Dictionary<Guid, string> _materialisedDirectoriesByAttachment = [];

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
            await view.LoadAnnotationsAsync(owner as IHasAttachmentAnnotations, attachment.Id, cancellationToken).ConfigureAwait(true);
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

        // `TD-98`: loaded here, once, for both the Ready and the
        // unavailable case alike — harmless in the unavailable case, since
        // no page is showing for a stroke to land on, and it means a
        // document that opens Ready straight into an existing attachment
        // shows its own markup immediately, not after a first render.
        await view.LoadAnnotationsAsync(owner as IHasAttachmentAnnotations, attachment.Id, cancellationToken).ConfigureAwait(true);

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

    private void OpenLoadedContent(
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
            // and only when it is not null (`TD-99`, hardened `TD-184`).
            var materialisedPath = MaterialiseForExternalOpen(attachment.Id, attachment.FileName, format, bytes, out var refusedReason);
            view.OpenUnavailable(DocumentViewSession.Unavailable(
                attachment.Id, attachment.FileName, attachment.ContentType, DocumentViewStatus.Unsupported,
                format, materialisedPath, refusedReason));
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
    /// Writes <paramref name="bytes"/> to a real file on local disk, for
    /// the OS shell to open — never beside the persistence root, which this
    /// launcher has no path to and should not need one for a copy that
    /// exists only until the OS is done with it (`TD-99`, hardened
    /// `TD-184`).
    /// </summary>
    /// <param name="attachmentId">The attachment being materialised — the key this launcher remembers the written directory under, for <see cref="Close"/>.</param>
    /// <param name="fileName">The attachment's own recorded file name — untrusted: never used for the written extension without checking it first.</param>
    /// <param name="format">
    /// The format this platform already, positively identified the bytes
    /// as (`DocumentFormatDetector`) — for <see cref="ViewableDocumentFormat.ExternalOnly"/>
    /// specifically, that identification (not a fresh, unverified read of
    /// <paramref name="fileName"/>'s own extension) decides what gets
    /// written.
    /// </param>
    /// <param name="bytes">The attachment's own real, verified bytes.</param>
    /// <param name="refusedReason">
    /// Set, and no file written, when the name resolves to an extension
    /// this launcher will not hand to the OS shell under any
    /// circumstances (`TD-184`) — <see langword="null"/> for every other
    /// outcome, including a write that was attempted and simply failed.
    /// </param>
    /// <returns>The written path, or <see langword="null"/> if the write was refused or itself failed.</returns>
    /// <remarks>
    /// <para>
    /// <b>The vulnerability this closes (`TD-184`):</b> before this method,
    /// the materialised file's own extension was <paramref name="fileName"/>'s,
    /// completely unexamined, and <c>DocumentViewerView.OpenExternally</c>
    /// hands the written path to <c>Process.Start(UseShellExecute: true)</c>
    /// — so an attachment named <c>invoice.pdf.exe</c> whose bytes are
    /// genuinely executable ran as code the instant a user pressed the
    /// button any honestly-labelled attachment already offers. Two things
    /// changed: the written extension is never trusted from the name alone
    /// (checked against <see cref="DangerousExtensions"/>, refused outright
    /// if it matches, resolved from the detector's own verified
    /// <see cref="ViewableDocumentFormat.ExternalOnly"/> match rather than
    /// the raw name when one exists) — and the directory a copy lands in is
    /// a fresh, randomly-named one this call creates for itself, never the
    /// attachment id (predictable, and therefore something an attacker who
    /// already knows or can guess the id could pre-create or symlink ahead
    /// of a later open).
    /// </para>
    /// </remarks>
    private string? MaterialiseForExternalOpen(
        Guid attachmentId, string fileName, ViewableDocumentFormat format, byte[] bytes, out string? refusedReason)
    {
        refusedReason = null;

        var safeName = SanitiseFileName(fileName);

        if (format is ViewableDocumentFormat.ExternalOnly)
        {
            // The detector already positively matched exactly `.dwg` or
            // `.dxf` — see `DocumentFormatDetector.FromContentType` — to
            // reach `ExternalOnly` at all, so that verified match, not a
            // second, unverified read of the raw name, is what gets
            // written. Neither extension is remotely close to anything in
            // `DangerousExtensions`, so no further check is needed here.
            var knownExtension = Path.GetExtension(fileName).ToLowerInvariant() is ".dwg" or ".dxf" ? Path.GetExtension(fileName).ToLowerInvariant() : ".dwg";
            var baseName = Path.GetFileNameWithoutExtension(safeName);
            safeName = (string.IsNullOrEmpty(baseName) ? "attachment" : baseName) + knownExtension;
        }
        else if (IsDangerousExtension(safeName))
        {
            refusedReason = "looks like it could run as a program, a script or a shortcut rather than open as a document, image or drawing.";
            return null;
        }

        try
        {
            string directory;
            do
            {
                directory = Path.Combine(Path.GetTempPath(), "TempestOS", "Viewer", Guid.NewGuid().ToString("N"));
            }
            while (Directory.Exists(directory));

            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, safeName);
            File.WriteAllBytes(path, bytes);

            lock (_materialisedDirectoriesByAttachment)
                _materialisedDirectoriesByAttachment[attachmentId] = directory;

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

    /// <summary>Whether <paramref name="fileName"/>'s own rightmost extension is one this launcher refuses to hand to the OS shell under any name (`TD-184`).</summary>
    private static bool IsDangerousExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension) && DangerousExtensions.Contains(extension);
    }

    /// <summary>
    /// A file name safe to write under the OS's own temporary folder and
    /// hand to the shell (`TD-184`): no path separators (nothing can escape
    /// the fresh directory <see cref="MaterialiseForExternalOpen"/> creates
    /// for it), no NUL or control characters, no Unicode bidirectional
    /// override characters (which can visually disguise a real extension —
    /// <c>U+202E</c>, the right-to-left override, is the classic
    /// "exe.pdf"-reads-as-"fdp.exe" trick), no trailing dots or spaces
    /// (which Windows itself silently strips when resolving a path, so a
    /// name ending "…exe." or "…exe " is exactly as dangerous as one ending
    /// "…exe"), and never a bare Windows reserved device name. Falls back
    /// to a fixed, inert name if nothing of the original survives.
    /// </summary>
    private static string SanitiseFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "attachment";

        var builder = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            if (ch is '/' or '\\' or '\0')
                continue;

            if (char.IsControl(ch))
                continue;

            // Bidirectional formatting controls: LRE/RLE/PDF/LRO/RLO
            // (U+202A-U+202E) and LRI/RLI/FSI/PDI (U+2066-U+2069).
            if (ch is (>= '‪' and <= '‮') or (>= '⁦' and <= '⁩'))
                continue;

            builder.Append(ch);
        }

        var candidate = builder.ToString().Trim().TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(candidate))
            return "attachment";

        if (candidate.Length > 200)
            candidate = candidate[..200];

        var baseName = Path.GetFileNameWithoutExtension(candidate);
        if (ReservedDeviceNames.Contains(baseName))
            candidate = "_" + candidate;

        return candidate;
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

        DeleteMaterialisedDirectory(attachmentId);
    }

    /// <summary>
    /// Deletes the fresh, per-launch directory <see cref="MaterialiseForExternalOpen"/>
    /// created for <paramref name="attachmentId"/>, if there was one
    /// (`TD-184`) — best-effort: a file the OS still has open (the external
    /// application the user just launched, most plausibly) is left for the
    /// OS's own temp-folder housekeeping rather than treated as this call's
    /// failure.
    /// </summary>
    private void DeleteMaterialisedDirectory(Guid attachmentId)
    {
        string? directory;
        lock (_materialisedDirectoriesByAttachment)
        {
            if (!_materialisedDirectoriesByAttachment.Remove(attachmentId, out directory))
                return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
