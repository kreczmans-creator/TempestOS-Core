using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Viewing;

/// <summary>
/// Why the viewer is not showing a document, or that it is (`TD-80`).
/// </summary>
/// <remarks>
/// Deliberately a superset of <see cref="AttachmentContentStatus"/> rather
/// than a reuse of it: the store answers "are the bytes there and intact",
/// and the viewer must additionally answer "and can this platform render
/// them". Collapsing <see cref="Unsupported"/> into
/// <see cref="AttachmentContentStatus.Corrupt"/> would tell a user their
/// file is damaged when it is perfectly sound and merely a format nothing
/// here decodes — a false accusation about their data, which is worse than
/// an admission about ours.
/// </remarks>
public enum DocumentViewStatus
{
    /// <summary>The document is loaded and renderable.</summary>
    Ready,

    /// <summary>No content is stored for this attachment.</summary>
    Missing,

    /// <summary>Content is stored but failed its own integrity check.</summary>
    Corrupt,

    /// <summary>Content is intact, and this platform has no renderer for it.</summary>
    Unsupported,
}

/// <summary>
/// One open document in the viewer: which attachment, what state it is in,
/// which page is showing, and where the user is looking (`TD-80`).
/// </summary>
/// <remarks>
/// <para>
/// Immutable, pure, and free of any rendering type — the whole of the
/// viewer's <em>behaviour</em> (page navigation, zoom, pan, fit, and what
/// a failed load means) is decided here and tested with no UI in the
/// process. The view applies it and draws; it does not decide anything.
/// </para>
/// <para>
/// Page numbers are one-based throughout, because that is what the page
/// navigation says out loud and a viewer with an off-by-one in its page
/// box is a viewer nobody trusts. They are clamped rather than validated:
/// asking for page 0 or page 900 of a 12-page document is a thing a
/// held-down key does, not an error worth raising.
/// </para>
/// </remarks>
public sealed record DocumentViewSession
{
    private DocumentViewSession(
        Guid attachmentId,
        string fileName,
        string contentType,
        DocumentViewStatus status,
        ViewableDocumentFormat format,
        int pageCount,
        int currentPage,
        DocumentViewport viewport)
    {
        AttachmentId = attachmentId;
        FileName = fileName;
        ContentType = contentType;
        Status = status;
        Format = format;
        PageCount = pageCount;
        CurrentPage = currentPage;
        Viewport = viewport;
    }

    /// <summary>The attachment being viewed.</summary>
    public Guid AttachmentId { get; private init; }

    /// <summary>Its file name, as the tab and header show it.</summary>
    public string FileName { get; private init; }

    /// <summary>Its declared content type.</summary>
    public string ContentType { get; private init; }

    /// <summary>Whether it is showing, and if not, why not.</summary>
    public DocumentViewStatus Status { get; private init; }

    /// <summary>The format this platform decided it is.</summary>
    public ViewableDocumentFormat Format { get; private init; }

    /// <summary>How many pages it has — 1 for a single-page document, 0 when nothing loaded.</summary>
    public int PageCount { get; private init; }

    /// <summary>The page showing, one-based.</summary>
    public int CurrentPage { get; private init; }

    /// <summary>Where the user is looking, and how closely.</summary>
    public DocumentViewport Viewport { get; private init; }

    /// <summary>Whether a document is actually showing.</summary>
    public bool IsReady => Status is DocumentViewStatus.Ready;

    /// <summary>Whether page navigation applies at all.</summary>
    public bool IsMultiPage => PageCount > 1;

    /// <summary>Whether there is a page after this one.</summary>
    public bool CanGoToNextPage => IsReady && CurrentPage < PageCount;

    /// <summary>Whether there is a page before this one.</summary>
    public bool CanGoToPreviousPage => IsReady && CurrentPage > 1;

    /// <summary>A loaded, renderable document, opened fitted to the given view.</summary>
    public static DocumentViewSession Ready(
        Guid attachmentId,
        string fileName,
        string contentType,
        ViewableDocumentFormat format,
        int pageCount,
        double contentWidth,
        double contentHeight,
        double viewportWidth,
        double viewportHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return new DocumentViewSession(
            attachmentId,
            fileName,
            contentType ?? string.Empty,
            DocumentViewStatus.Ready,
            format,
            Math.Max(1, pageCount),
            1,
            DocumentViewport.Create(contentWidth, contentHeight, viewportWidth, viewportHeight));
    }

    /// <summary>A document that could not be shown, and the reason.</summary>
    /// <remarks>
    /// Still a session, with the same identity and file name a ready one
    /// has. A failed open is a tab the user can see, name, and close — not
    /// a dialog that appears and leaves nothing behind.
    /// </remarks>
    public static DocumentViewSession Unavailable(
        Guid attachmentId,
        string fileName,
        string contentType,
        DocumentViewStatus status,
        ViewableDocumentFormat format = ViewableDocumentFormat.Unsupported)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (status is DocumentViewStatus.Ready)
            throw new ArgumentOutOfRangeException(nameof(status), status, "A ready session must be created through Ready.");

        return new DocumentViewSession(
            attachmentId, fileName, contentType ?? string.Empty, status, format, 0, 0,
            DocumentViewport.Create(1, 1, 1, 1));
    }

    /// <summary>The status a content read maps to, before any format decision.</summary>
    public static DocumentViewStatus StatusFor(AttachmentContentStatus contentStatus) => contentStatus switch
    {
        AttachmentContentStatus.Available => DocumentViewStatus.Ready,
        AttachmentContentStatus.Corrupt => DocumentViewStatus.Corrupt,
        _ => DocumentViewStatus.Missing,
    };

    /// <summary>Turns to <paramref name="page"/>, clamped into the document.</summary>
    /// <remarks>Turning a page re-fits, since a new page is a new thing to look at.</remarks>
    public DocumentViewSession GoToPage(int page)
    {
        if (!IsReady)
            return this;

        var target = Math.Min(Math.Max(page, 1), PageCount);
        if (target == CurrentPage)
            return this;

        return this with { CurrentPage = target, Viewport = Viewport.FitToView() };
    }

    /// <summary>Turns to the next page, stopping at the last.</summary>
    public DocumentViewSession NextPage() => GoToPage(CurrentPage + 1);

    /// <summary>Turns to the previous page, stopping at the first.</summary>
    public DocumentViewSession PreviousPage() => GoToPage(CurrentPage - 1);

    /// <summary>Replaces the viewport — the result of a zoom, a pan, a fit or a resize.</summary>
    public DocumentViewSession WithViewport(DocumentViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        return IsReady ? this with { Viewport = viewport } : this;
    }

    /// <summary>Re-fits for a page whose size differs from the last one's.</summary>
    public DocumentViewSession WithPageSize(double width, double height) =>
        IsReady ? this with { Viewport = Viewport.WithContentSize(width, height) } : this;
}
