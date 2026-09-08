namespace Tempest.App.Workspace.Viewing;

/// <summary>
/// Where a document is being looked at, and how closely (`TD-80`).
/// </summary>
/// <remarks>
/// <para>
/// An immutable value with pure operations, deliberately holding no
/// control, no bitmap and no rendering concept at all — so every zoom,
/// pan and fit rule in this platform is decided here, tested with no UI in
/// the process, and merely *applied* by the view. The same discipline
/// `TD-72` used for the layout tree, for the same reason: geometry that
/// lives in an event handler can only be tested by raising events.
/// </para>
/// <para>
/// The coordinate model, stated once so nothing has to infer it:
/// <list type="bullet">
/// <item><see cref="ContentSize"/> is the document's own size, in its own units (a PDF page in points, an image in pixels).</item>
/// <item><see cref="Zoom"/> scales that to rendered pixels, so rendered size is <c>ContentSize * Zoom</c>.</item>
/// <item><see cref="Offset"/> is the top-left of the visible window <em>within the rendered content</em>, in rendered pixels.</item>
/// </list>
/// </para>
/// <para>
/// <see cref="Offset"/> is always clamped: content larger than the
/// viewport cannot be scrolled past its own edge, and content smaller than
/// the viewport is centred rather than pinned to a corner. Both are
/// invariants of this type rather than habits of its callers, which is
/// what stops "the drawing scrolled off into grey space" from being
/// reachable at all.
/// </para>
/// </remarks>
public sealed record DocumentViewport
{
    /// <summary>The closest a document can be zoomed — 32x.</summary>
    public const double MaxZoom = 32.0;

    /// <summary>The furthest a document can be zoomed out — 2%.</summary>
    public const double MinZoom = 0.02;

    /// <summary>The step a single zoom-in or zoom-out command applies.</summary>
    public const double ZoomStep = 1.25;

    private DocumentViewport(double contentWidth, double contentHeight, double viewportWidth, double viewportHeight, double zoom, double offsetX, double offsetY)
    {
        ContentWidth = contentWidth;
        ContentHeight = contentHeight;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        Zoom = zoom;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>The document's own width, in its own units.</summary>
    public double ContentWidth { get; }

    /// <summary>The document's own height, in its own units.</summary>
    public double ContentHeight { get; }

    /// <summary>The width of the window looking at it, in pixels.</summary>
    public double ViewportWidth { get; }

    /// <summary>The height of the window looking at it, in pixels.</summary>
    public double ViewportHeight { get; }

    /// <summary>The scale factor from content units to rendered pixels.</summary>
    public double Zoom { get; }

    /// <summary>The horizontal top-left of the visible window within the rendered content.</summary>
    public double OffsetX { get; }

    /// <summary>The vertical top-left of the visible window within the rendered content.</summary>
    public double OffsetY { get; }

    /// <summary>The rendered width of the content at the current zoom.</summary>
    public double RenderedWidth => ContentWidth * Zoom;

    /// <summary>The rendered height of the content at the current zoom.</summary>
    public double RenderedHeight => ContentHeight * Zoom;

    /// <summary>Whether the rendered content is wider or taller than the window looking at it.</summary>
    public bool IsScrollable => RenderedWidth > ViewportWidth || RenderedHeight > ViewportHeight;

    /// <summary>
    /// A viewport onto <paramref name="contentWidth"/> by
    /// <paramref name="contentHeight"/>, fitted to the given window.
    /// </summary>
    /// <remarks>
    /// Opens fitted rather than at 1:1 — the sensible initial viewport a
    /// user expects when they open a drawing is the whole drawing, not its
    /// top-left corner at native scale.
    /// </remarks>
    public static DocumentViewport Create(double contentWidth, double contentHeight, double viewportWidth, double viewportHeight)
    {
        var content = Sanitise(contentWidth, contentHeight);
        var view = Sanitise(viewportWidth, viewportHeight);

        return new DocumentViewport(content.Width, content.Height, view.Width, view.Height, 1.0, 0, 0).FitToView();
    }

    /// <summary>The zoom at which the whole document is visible.</summary>
    public double FitZoom => Clamp(
        Math.Min(ViewportWidth / ContentWidth, ViewportHeight / ContentHeight),
        MinZoom,
        MaxZoom);

    /// <summary>Whether the current zoom is the fit-to-view zoom.</summary>
    public bool IsFitted => Math.Abs(Zoom - FitZoom) < 0.0001;

    /// <summary>Scales the document so all of it is visible, centred.</summary>
    public DocumentViewport FitToView() => WithZoom(FitZoom);

    /// <summary>Scales the document 1:1 with its own units, keeping the centre of the view fixed.</summary>
    public DocumentViewport ActualSize() => ZoomTo(1.0);

    /// <summary>
    /// Zooms about the centre of the current view, so the thing the user
    /// was looking at stays where it was.
    /// </summary>
    public DocumentViewport ZoomTo(double zoom) =>
        ZoomAbout(zoom, ViewportWidth / 2, ViewportHeight / 2);

    /// <summary>Zooms one step closer, about the centre of the view.</summary>
    public DocumentViewport ZoomIn() => ZoomTo(Zoom * ZoomStep);

    /// <summary>Zooms one step further out, about the centre of the view.</summary>
    public DocumentViewport ZoomOut() => ZoomTo(Zoom / ZoomStep);

    /// <summary>
    /// Zooms about a point in the viewport — the point under
    /// (<paramref name="anchorX"/>, <paramref name="anchorY"/>) stays under
    /// it.
    /// </summary>
    /// <remarks>
    /// This is what makes wheel-zoom feel like a magnifier rather than a
    /// slider: zooming toward the pointer keeps the detail the user is
    /// pointing at in place, instead of sliding it off the edge.
    /// </remarks>
    public DocumentViewport ZoomAbout(double zoom, double anchorX, double anchorY)
    {
        var target = Clamp(Sanitise(zoom), MinZoom, MaxZoom);

        // The content-space point currently under the anchor.
        var contentX = (OffsetX + anchorX) / Zoom;
        var contentY = (OffsetY + anchorY) / Zoom;

        // Where that same point will be once scaled, minus where we want
        // it to appear, is the new offset.
        var offsetX = (contentX * target) - anchorX;
        var offsetY = (contentY * target) - anchorY;

        return new DocumentViewport(ContentWidth, ContentHeight, ViewportWidth, ViewportHeight, target, offsetX, offsetY).Clamped();
    }

    /// <summary>Moves the view by a delta in rendered pixels, clamped to the content.</summary>
    public DocumentViewport PanBy(double deltaX, double deltaY) =>
        new DocumentViewport(
            ContentWidth, ContentHeight, ViewportWidth, ViewportHeight, Zoom,
            OffsetX + Finite(deltaX), OffsetY + Finite(deltaY)).Clamped();

    /// <summary>The same view, resized — a window resize, not a user action.</summary>
    /// <remarks>
    /// A fitted view stays fitted across a resize, and any other view keeps
    /// its zoom and is re-clamped. Re-fitting a view the user had zoomed
    /// into would throw their place away every time the window moved.
    /// </remarks>
    public DocumentViewport WithViewportSize(double width, double height)
    {
        var wasFitted = IsFitted;
        var view = Sanitise(width, height);
        var resized = new DocumentViewport(ContentWidth, ContentHeight, view.Width, view.Height, Zoom, OffsetX, OffsetY);

        return wasFitted ? resized.FitToView() : resized.Clamped();
    }

    /// <summary>The same view onto differently sized content — turning to a page of another size.</summary>
    /// <remarks>Re-fits, because a new page has no place the user was already looking at.</remarks>
    public DocumentViewport WithContentSize(double width, double height)
    {
        var content = Sanitise(width, height);

        return new DocumentViewport(content.Width, content.Height, ViewportWidth, ViewportHeight, Zoom, OffsetX, OffsetY).FitToView();
    }

    private DocumentViewport WithZoom(double zoom) =>
        new DocumentViewport(ContentWidth, ContentHeight, ViewportWidth, ViewportHeight, Clamp(zoom, MinZoom, MaxZoom), OffsetX, OffsetY).Clamped();

    /// <summary>
    /// Pins the offset inside the content: never past an edge when the
    /// content is larger than the view, and centred when it is smaller.
    /// </summary>
    private DocumentViewport Clamped()
    {
        var offsetX = ClampAxis(OffsetX, RenderedWidth, ViewportWidth);
        var offsetY = ClampAxis(OffsetY, RenderedHeight, ViewportHeight);

        return new DocumentViewport(ContentWidth, ContentHeight, ViewportWidth, ViewportHeight, Zoom, offsetX, offsetY);
    }

    private static double ClampAxis(double offset, double rendered, double viewport)
    {
        // Smaller than the window: there is nowhere to scroll to, and the
        // honest place for it is the middle. The negative offset is what
        // centres it.
        if (rendered <= viewport)
            return (rendered - viewport) / 2;

        return Clamp(offset, 0, rendered - viewport);
    }

    private static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);

    /// <summary>
    /// A finite, strictly positive dimension.
    /// </summary>
    /// <remarks>
    /// A zero or NaN size reaches this type routinely and legitimately — a
    /// control that has not been laid out yet reports zero bounds, and a
    /// malformed document can report a zero-sized page. Both would turn
    /// every derived value into NaN and propagate it into the render, so
    /// they are normalised at the boundary rather than guarded against at
    /// each use.
    /// </remarks>
    private static double Sanitise(double value) =>
        double.IsFinite(value) && value > 0 ? value : 1;

    /// <summary>
    /// A finite delta, which — unlike a dimension — is legitimately zero
    /// or negative.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Sanitise(double)"/> deliberately: reusing
    /// the dimension sanitiser here would turn a leftward pan into a
    /// rightward one-pixel pan, which is the kind of defect that looks
    /// like a rendering glitch and is really an arithmetic one.
    /// </remarks>
    private static double Finite(double value) => double.IsFinite(value) ? value : 0;

    private static (double Width, double Height) Sanitise(double width, double height) =>
        (Sanitise(width), Sanitise(height));
}
