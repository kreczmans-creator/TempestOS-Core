using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Files;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;
using Tempest.Workspace.Files;
using Tempest.Workspace.Viewing;

namespace Tempest.Desktop.Viewing;

/// <summary>
/// The document and drawing viewer (`TD-80`) — one open document, its
/// pages, and the zoom/pan/fit controls over it.
/// </summary>
/// <remarks>
/// <para>
/// A view over <see cref="DocumentViewSession"/>, which decides
/// everything: which page, what zoom, where the view is, and what a failed
/// open means. This control renders that decision and turns gestures into
/// calls back onto it. It holds no geometry of its own, so "what should
/// zooming do" is answered in one place that runs with no UI in the
/// process (`TD-72`'s discipline, applied again).
/// </para>
/// <para>
/// Pages are rasterised on demand at the current zoom rather than rendered
/// once and stretched — zooming into a drawing shows more of the drawing.
/// </para>
/// </remarks>
public sealed class DocumentViewerView : UserControl
{
    /// <summary>Arrow-key pan distance, in rendered pixels per keypress (review board finding #4, `WP 16.5A-R1`).</summary>
    private const double KeyboardPanStep = 40;

    /// <summary>An annotation shape's own stroke width, in rendered pixels (`TD-98`).</summary>
    private const double AnnotationStrokeThickness = 2.0;

    /// <summary>An arrow annotation's own head length, in rendered pixels.</summary>
    private const double AnnotationArrowHeadLength = 12.0;

    /// <summary>An arrow annotation's own head half-angle, in radians (about 25°).</summary>
    private const double AnnotationArrowHeadAngle = Math.PI / 7;

    /// <summary>
    /// Mirrors <see cref="PdfDocumentPageSource.MaxRasterEdge"/>'s own
    /// value exactly (`TD-101`) — kept as this view's own constant, rather
    /// than referenced from that platform-guarded class directly, so this
    /// general-purpose control (meaningful, and buildable, on every
    /// platform regardless of whether a PDF renderer is available on it)
    /// does not have to carry that class's own <see cref="System.Runtime.Versioning.SupportedOSPlatformAttribute"/>
    /// triple itself merely to compare against one of its constants.
    /// </summary>
    private const int TiledRenderMaxRasterEdge = 8000;

    private readonly Image _page = new() { Stretch = Stretch.Fill };
    private readonly Canvas _canvas = new() { Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x41)) };
    private readonly TextBlock _pageIndicator = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
    private readonly TextBlock _zoomIndicator = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
    private readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
    private readonly StackPanel _unavailable = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 6 };
    private readonly TextBlock _unavailableHeadline = new() { FontSize = 15, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _unavailableDetail = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 420, TextAlignment = TextAlignment.Center, Opacity = 0.8 };
    private readonly Button _openExternally;
    private readonly Button _previousPage;
    private readonly Button _nextPage;
    private readonly Button _zoomIn;
    private readonly Button _zoomOut;
    private readonly Button _fit;
    private readonly Button _actualSize;
    private readonly Button _rotateLeft;
    private readonly Button _rotateRight;

    // ---- Markup and annotation (`TD-98`) --------------------------------
    private readonly Canvas _annotationLayer = new();
    private readonly StackPanel _annotationToolbar;
    private readonly Button _rectangleTool;
    private readonly Button _ellipseTool;
    private readonly Button _freehandTool;
    private readonly Button _arrowTool;
    private readonly Button _textNoteTool;
    private readonly Button _deleteAnnotation;
    private readonly Button _clearPage;
    private readonly Button _saveAnnotatedCopy;
    private readonly List<Button> _colorSwatches = new();
    private readonly ConfirmationDialog _confirmClearPage = new();
    private readonly InputDialog _noteInput = new();

    /// <summary>Every annotation known for the attachment currently open, across every page.</summary>
    private readonly List<AttachmentAnnotation> _annotations = new();

    private IHasAttachmentAnnotations? _annotationOwner;
    private Guid _annotationAttachmentId;
    private AnnotationTool? _activeTool;
    private string _activeColorHex = AnnotationPalette.Default;
    private Guid? _selectedAnnotationId;
    private Point? _drawStartCanvas;
    private AnnotationPoint _drawStartNative;
    private List<AnnotationPoint>? _freehandNativePoints;
    private Control? _drawPreview;

    /// <summary>
    /// Asks the user to choose a destination for <b>Save annotated
    /// copy…</b> — real by default, overridable so a test can assert what
    /// was written without a real save dialog appearing (`TD-98`).
    /// </summary>
    public IFilePicker FilePicker { get; set; }

    /// <summary>
    /// This tab's own tiled-render cache (`TD-101`) — cleared, never
    /// reused, whenever a different document opens into this tab: a cached
    /// tile is keyed by page index and scale alone, which a different
    /// attachment's own page 1 at 1.0x would collide with if the cache
    /// outlived the document it was rasterised from.
    /// </summary>
    private readonly TileCache _tileCache = new();

    private IDocumentPageSource? _source;
    private Bitmap? _rendered;
    private Point? _dragOrigin;
    private double _renderedZoom;
    private int _renderedPage = -1;
    private int _sizedPage = -1;
    private int _rotationDegrees;
    private int _renderedRotation;

    /// <summary>Initialises a new instance of the <see cref="DocumentViewerView"/> class.</summary>
    public DocumentViewerView()
    {
        _previousPage = ToolbarButton("‹", "Previous page", () => Apply(s => s.PreviousPage()));
        _nextPage = ToolbarButton("›", "Next page", () => Apply(s => s.NextPage()));
        _zoomOut = ToolbarButton("−", "Zoom out", ZoomOut);
        _zoomIn = ToolbarButton("+", "Zoom in", ZoomIn);
        _fit = ToolbarButton("Fit", "Fit the whole page in the view", FitToView);
        _actualSize = ToolbarButton("100%", "Show the page at its actual size", ActualSize);
        _rotateLeft = ToolbarButton("⟲", "Rotate left", RotateLeft);
        _rotateRight = ToolbarButton("⟳", "Rotate right", RotateRight);

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(6, 4),
            Children = { _title, _previousPage, _pageIndicator, _nextPage, _zoomOut, _zoomIndicator, _zoomIn, _fit, _actualSize, _rotateLeft, _rotateRight },
        };

        // ---- Annotations toolbar group (`TD-98`) ------------------------
        _rectangleTool = ToolbarButton("▭", "Rectangle annotation", () => SetActiveTool(AnnotationTool.Rectangle));
        _ellipseTool = ToolbarButton("◯", "Ellipse annotation", () => SetActiveTool(AnnotationTool.Ellipse));
        _freehandTool = ToolbarButton("✎", "Freehand annotation", () => SetActiveTool(AnnotationTool.Freehand));
        _arrowTool = ToolbarButton("↗", "Arrow annotation", () => SetActiveTool(AnnotationTool.Arrow));
        _textNoteTool = ToolbarButton("💬", "Text note annotation", () => SetActiveTool(AnnotationTool.TextNote));
        _deleteAnnotation = ToolbarButton("Delete", "Delete selected annotation", () => _ = DeleteSelectedAnnotationAsync());
        _clearPage = ToolbarButton("Clear page", "Clear page annotations", () => _ = ClearPageAnnotationsAsync());
        _saveAnnotatedCopy = ToolbarButton("Save annotated copy…", "Save annotated copy…", () => _ = SaveAnnotatedCopyAsync());

        _annotationToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(6, 0, 6, 4),
            Children = { _rectangleTool, _ellipseTool, _freehandTool, _arrowTool, _textNoteTool },
        };

        foreach (var swatch in AnnotationPalette.Swatches)
        {
            var colorHex = swatch.Hex;
            var button = ToolbarButton("⬤", swatch.Name, () => SetActiveColor(colorHex));
            button.Foreground = new SolidColorBrush(Color.Parse(colorHex));
            _colorSwatches.Add(button);
            _annotationToolbar.Children.Add(button);
        }

        _annotationToolbar.Children.Add(_deleteAnnotation);
        _annotationToolbar.Children.Add(_clearPage);
        _annotationToolbar.Children.Add(_saveAnnotatedCopy);

        _openExternally = ToolbarButton(
            "Open externally", "Open externally", OpenExternally);

        _unavailable.Children.Add(_unavailableHeadline);
        _unavailable.Children.Add(_unavailableDetail);
        _unavailable.Children.Add(_openExternally);
        _unavailable.IsVisible = false;
        _openExternally.IsVisible = false;

        _canvas.Children.Add(_page);
        // Added after the page so it hit-tests first — a click on a
        // rendered annotation shape reaches that shape's own handler before
        // it could ever reach `_canvas`'s pan-start handler beneath it.
        _canvas.Children.Add(_annotationLayer);
        _canvas.ClipToBounds = true;

        var content = new Grid();
        content.Children.Add(_canvas);
        content.Children.Add(_unavailable);
        content.Children.Add(_confirmClearPage);
        content.Children.Add(_noteInput);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(_annotationToolbar, 1);
        Grid.SetRow(content, 2);
        root.Children.Add(toolbar);
        root.Children.Add(_annotationToolbar);
        root.Children.Add(content);

        Content = root;

        FilePicker = new AvaloniaFilePicker(this);

        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += OnPointerReleased;
        _canvas.PointerWheelChanged += OnPointerWheelChanged;

        // The viewport belongs to the view's real size, so a resize is a
        // viewport change rather than a re-fit that discards the user's place.
        _canvas.SizeChanged += (_, e) => Apply(s => s.WithViewport(s.Viewport.WithViewportSize(e.NewSize.Width, e.NewSize.Height)));

        // Keyboard operability (review board finding #4, `WP 16.5A-R1`) —
        // panning a zoomed document previously worked only through the
        // pointer handlers above. Attached at the root, exactly the
        // pattern `DigitalThreadGraphView.OnGraphKeyDown` already
        // established for this codebase's other pannable/zoomable
        // surface: it fires regardless of which of this view's own
        // controls (any toolbar button — all real tab stops already)
        // currently holds focus, rather than requiring a new focus target
        // of its own.
        KeyDown += OnViewerKeyDown;

        Refresh();
    }

    /// <summary>The document currently open, or <see langword="null"/> before one is.</summary>
    public DocumentViewSession? Session { get; private set; }

    /// <summary>Raised whenever the session changes — a page turn, a zoom, a pan or a resize.</summary>
    public event Action<DocumentViewSession>? SessionChanged;

    /// <summary>The rendered page currently on screen, for tests and diagnostics.</summary>
    public Bitmap? RenderedPage => _rendered;

    /// <summary>Whether the unavailable surface (Missing/Corrupt/Unsupported) is showing.</summary>
    public bool IsShowingUnavailableState => _unavailable.IsVisible;

    /// <summary>The headline shown when a document could not be opened.</summary>
    public string UnavailableHeadline => _unavailableHeadline.Text ?? string.Empty;

    /// <summary>The page indicator's text, exactly as a user reads it.</summary>
    public string PageIndicatorText => _pageIndicator.Text ?? string.Empty;

    /// <summary>The zoom indicator's text, exactly as a user reads it.</summary>
    public string ZoomIndicatorText => _zoomIndicator.Text ?? string.Empty;

    /// <summary>
    /// Whether the page and zoom controls are on screen at all — false
    /// whenever there is nothing to page through or zoom.
    /// </summary>
    public bool AreViewControlsVisible => _fit.IsVisible;

    /// <summary>
    /// This document's rotation from its stored orientation, in degrees
    /// clockwise (always 0, 90, 180 or 270). Remembered for as long as this
    /// tab shows this document — every zoom, pan, page turn and resize
    /// leaves it exactly as the user set it; opening a different document
    /// into this same tab resets it to 0 (`TD-99`).
    /// </summary>
    public int RotationDegrees => _rotationDegrees;

    /// <summary>
    /// Launches a materialised copy of the current document in whatever
    /// the operating system has registered for its format — real by
    /// default (<see cref="Process.Start(ProcessStartInfo)"/>),
    /// overridable so a test can assert the call was made without a real
    /// application actually opening (`TD-99`).
    /// </summary>
    public Action<string> ExternalLauncher { get; set; } = DefaultExternalLauncher;

    /// <summary>Opens a document that loaded, with the source that renders its pages.</summary>
    public void Open(DocumentViewSession session, IDocumentPageSource source)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(source);

        _source?.Dispose();
        _source = source;
        _tileCache.Clear();
        _renderedPage = -1;
        _renderedZoom = 0;
        _renderedRotation = 0;
        _rotationDegrees = 0;
        _sizedPage = -1;
        Session = session;
        ResetAnnotationInteractionState();
        SyncPageSize();
        Refresh();
    }

    /// <summary>Opens a document that did not load, showing why.</summary>
    public void OpenUnavailable(DocumentViewSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _source?.Dispose();
        _source = null;
        _tileCache.Clear();
        _rendered = null;
        _page.Source = null;
        _sizedPage = -1;
        _rotationDegrees = 0;
        _renderedRotation = 0;
        Session = session;
        ResetAnnotationInteractionState();
        Refresh();
    }

    /// <summary>
    /// Loads every annotation recorded for <paramref name="attachmentId"/>
    /// under <paramref name="owner"/> (`TD-98`), remembering both so a
    /// stroke drawn afterwards is written back to the same place. Called by
    /// <see cref="AttachmentViewerLauncher"/> right after a document opens;
    /// harmless — annotations simply stay empty — when <paramref name="owner"/>
    /// is <see langword="null"/> (an owner that does not carry the facet, or
    /// a streamed-open path that has not been given one).
    /// </summary>
    public async Task LoadAnnotationsAsync(IHasAttachmentAnnotations? owner, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        _annotationOwner = owner;
        _annotationAttachmentId = attachmentId;
        _annotations.Clear();
        _selectedAnnotationId = null;

        if (owner is not null)
        {
            var loaded = await owner.GetAttachmentAnnotationsAsync(attachmentId, cancellationToken).ConfigureAwait(true);
            _annotations.AddRange(loaded);
        }

        RenderAnnotationShapes();
        UpdateAnnotationToolbarState();
    }

    /// <summary>Clears every piece of in-progress drawing/selection state — a document opening into this tab starts with none of it (`TD-98`).</summary>
    private void ResetAnnotationInteractionState()
    {
        _annotationOwner = null;
        _annotationAttachmentId = Guid.Empty;
        _annotations.Clear();
        _activeTool = null;
        _selectedAnnotationId = null;
        _drawStartCanvas = null;
        _freehandNativePoints = null;
        _drawPreview = null;
        _annotationLayer.Children.Clear();
        UpdateAnnotationToolbarState();
    }

    /// <summary>Rotates the current document 90° anticlockwise from where it is now.</summary>
    public void RotateLeft() => SetRotation(NormaliseDegrees(_rotationDegrees - 90));

    /// <summary>Rotates the current document 90° clockwise from where it is now.</summary>
    public void RotateRight() => SetRotation(NormaliseDegrees(_rotationDegrees + 90));

    /// <summary>
    /// Opens the current document in its own application — the action
    /// behind the "Open externally" button, offered whenever this platform
    /// holds the file but has no in-app renderer for its format (`TD-99`).
    /// Does nothing when no document is open or its materialised copy
    /// could not be written.
    /// </summary>
    public void OpenExternally()
    {
        if (Session is { MaterialisedPath: { } path })
            ExternalLauncher(path);
    }

    private static int NormaliseDegrees(int degrees) => ((degrees % 360) + 360) % 360;

    private void SetRotation(int degrees)
    {
        // Matches `DocumentViewSession`'s own convention for every other
        // control when nothing renderable is open (page navigation, zoom):
        // ignored rather than silently recorded for later, since there is
        // no document for it to apply to yet.
        if (Session is not { IsReady: true } session)
            return;

        if (degrees == _rotationDegrees)
            return;

        _rotationDegrees = degrees;

        // `TD-101`: every 90° step toggles whether the page's own width and
        // height are swapped, so the viewport's content size is swapped
        // here too — the fix for the rotation fit bug `WP 20.2B` disclosed.
        // Stays fitted if it was fitted (`DocumentViewport.WithContentSizeSwapped`'s
        // own convention), so a rotated landscape page now fits with no
        // manual zoom step, exactly as this WP's brief asks.
        session = session.WithViewport(session.Viewport.WithContentSizeSwapped());
        Session = session;

        RenderCurrentPage(session);
        PositionPage(session);
    }

    private static void DefaultExternalLauncher(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch
        {
            // Best-effort, matching `SettingsView.DefaultOpenFolder`'s own
            // convention: no application registered for this file type, or
            // no shell to hand it to (a container, a minimal CI image), is
            // not this button's failure to report.
        }
    }

    /// <summary>Zooms one step closer, about the centre of the view.</summary>
    public void ZoomIn() => Apply(s => s.WithViewport(s.Viewport.ZoomIn()));

    /// <summary>Zooms one step further out, about the centre of the view.</summary>
    public void ZoomOut() => Apply(s => s.WithViewport(s.Viewport.ZoomOut()));

    /// <summary>Fits the whole page in the view.</summary>
    public void FitToView() => Apply(s => s.WithViewport(s.Viewport.FitToView()));

    /// <summary>Shows the page at its actual size.</summary>
    public void ActualSize() => Apply(s => s.WithViewport(s.Viewport.ActualSize()));

    /// <summary>Turns to the next page.</summary>
    public void NextPage() => Apply(s => s.NextPage());

    /// <summary>Turns to the previous page.</summary>
    public void PreviousPage() => Apply(s => s.PreviousPage());

    /// <summary>Turns to a specific page, clamped into the document.</summary>
    public void GoToPage(int page) => Apply(s => s.GoToPage(page));

    /// <summary>Pans by a delta in rendered pixels.</summary>
    public void PanBy(double deltaX, double deltaY) => Apply(s => s.WithViewport(s.Viewport.PanBy(deltaX, deltaY)));

    private void Apply(Func<DocumentViewSession, DocumentViewSession> operation)
    {
        if (Session is not { } session)
            return;

        var updated = operation(session);
        if (ReferenceEquals(updated, session))
            return;

        Session = updated;

        // Before anything is drawn: a page of a different size is different
        // content, and the viewport has to know that before it decides a
        // zoom or a rendered size.
        SyncPageSize();

        Refresh();
        SessionChanged?.Invoke(Session);
    }

    /// <summary>
    /// Tells the viewport the current page's own size, when it differs
    /// from the page before it.
    /// </summary>
    /// <remarks>
    /// A document's pages are not all one size — a drawing set mixes A3
    /// landscape sheets with A4 portrait ones, and this codebase's own
    /// multi-page fixture was built that way on purpose. The viewport
    /// carries the content size that decides both the fit zoom and the
    /// rendered width and height the page is drawn at, so a page turn that
    /// left it on the previous page's size drew the new page stretched into
    /// the old page's shape: a portrait sheet squashed into landscape at
    /// roughly half its true height. Found by the `TD-80` visual audit —
    /// and the fixture written with three deliberately different page
    /// sizes had been there the whole time, exercising the model that was
    /// already correct and never the view that failed to ask it.
    /// </remarks>
    private void SyncPageSize()
    {
        if (_source is null || Session is not { IsReady: true } session)
            return;

        var pageIndex = session.CurrentPage - 1;
        if (pageIndex == _sizedPage)
            return;

        _sizedPage = pageIndex;

        var page = _source.PageSize(pageIndex);

        // `TD-101`: the viewport's own content size tracks the page's
        // currently-<i>displayed</i> (rotated) bounding box, not its native
        // one — see `SetRotation` and `DocumentViewport.WithContentSizeSwapped`.
        // A page turn keeps whatever rotation is already set, so the new
        // page's native size is rotated by the same amount before it is
        // compared against/written into the viewport.
        var (width, height) = AnnotationGeometry.RotatedSize(page.Width, page.Height, _rotationDegrees);
        var viewport = session.Viewport;

        if (Math.Abs(width - viewport.ContentWidth) > 0.01 ||
            Math.Abs(height - viewport.ContentHeight) > 0.01)
        {
            Session = session.WithPageSize(width, height);
        }
    }

    private void Refresh()
    {
        if (Session is not { } session)
        {
            _canvas.IsVisible = false;
            _unavailable.IsVisible = true;
            _unavailableHeadline.Text = "No document open";
            _unavailableDetail.Text = "Open an attachment from an engineering object to view it here.";
            _openExternally.IsVisible = false;
            SetViewControlsShown(false);
            return;
        }

        _title.Text = session.FileName;

        if (!session.IsReady)
        {
            _canvas.IsVisible = false;
            _unavailable.IsVisible = true;
            (_unavailableHeadline.Text, _unavailableDetail.Text) = DescribeUnavailable(session);
            _pageIndicator.Text = string.Empty;
            _zoomIndicator.Text = string.Empty;
            SetViewControlsShown(false);
            _openExternally.IsVisible = session.Status is DocumentViewStatus.Unsupported && session.MaterialisedPath is not null;
            return;
        }

        _canvas.IsVisible = true;
        _unavailable.IsVisible = false;
        _openExternally.IsVisible = false;

        SetViewControlsShown(true);

        _pageIndicator.Text = session.IsMultiPage
            ? $"Page {session.CurrentPage} of {session.PageCount}"
            : "1 page";
        _zoomIndicator.Text = $"{Math.Round(session.Viewport.Zoom * 100)}%";

        _previousPage.IsEnabled = session.CanGoToPreviousPage;
        _nextPage.IsEnabled = session.CanGoToNextPage;
        _previousPage.IsVisible = session.IsMultiPage;
        _nextPage.IsVisible = session.IsMultiPage;
        _zoomIn.IsEnabled = true;
        _zoomOut.IsEnabled = true;
        _fit.IsEnabled = true;
        _actualSize.IsEnabled = true;
        _rotateLeft.IsEnabled = true;
        _rotateRight.IsEnabled = true;

        RenderCurrentPage(session);
        PositionPage(session);
    }

    private static (string Headline, string Detail) DescribeUnavailable(DocumentViewSession session) => session.Status switch
    {
        // Three distinct answers, deliberately. "We never had this file",
        // "we had it and it is damaged" and "it is fine and we cannot draw
        // it" call for different actions from the user, and a single
        // "could not open" would hide which one they are in.
        DocumentViewStatus.Missing => (
            "No content stored",
            $"'{session.FileName}' is recorded as an attachment, but this platform holds no file for it. " +
            "Attachments created before content storage — and external references — describe a file rather than containing one."),

        DocumentViewStatus.Corrupt => (
            "This attachment is damaged",
            $"'{session.FileName}' has stored content, but it no longer matches the size and checksum recorded when it was attached. " +
            "The content has not been shown, because it is not the file that was attached."),

        // A drawing format this platform stores but was never going to
        // render in-app (`TD-99`, Product Owner decision 2026-09-15 §5) —
        // said honestly, rather than folded into the generic answer below,
        // which would tell the user their CAD file is a gap when it is a
        // deliberate choice with a working way through it.
        _ when session.Format is ViewableDocumentFormat.ExternalOnly => (
            "This drawing opens in its own application",
            $"'{session.FileName}' is a format TempestOS stores but does not render in-app. " +
            "Open externally shows it in the application registered for it on this computer."),

        // `TD-184`: the file's own name or content looks like something
        // that runs as code — an executable, a script, a shortcut — rather
        // than a document, image or drawing. Said honestly, the same way
        // the ExternalOnly case above is: this is a deliberate refusal with
        // a real reason, not the generic "we have no viewer" answer, and
        // Open externally is not offered at all for it.
        _ when session.ExternalOpenRefusedReason is { } reason => (
            "This file was not opened",
            $"'{session.FileName}' {reason} TempestOS will not hand it to another application to run."),

        _ => (
            "This format cannot be displayed",
            $"'{session.FileName}' is intact, and TempestOS has no viewer for {DescribeType(session.ContentType)}. " +
            "PDF documents and drawings, PNG/JPEG/BMP/GIF/WebP images and text files can be viewed." +
            (session.MaterialisedPath is not null ? " Open externally may still show it in another application." : string.Empty)),
    };

    private static string DescribeType(string contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? "this file type" : $"'{contentType}'";

    private void RenderCurrentPage(DocumentViewSession session)
    {
        if (_source is null)
            return;

        var pageIndex = session.CurrentPage - 1;
        var zoom = session.Viewport.Zoom;

        // Re-rasterise only when the page, the zoom or the rotation
        // actually changed. A pan must not re-render: it is the same
        // pixels in a different place, and re-rendering a large drawing on
        // every pointer move makes panning unusable.
        if (_rendered is not null && pageIndex == _renderedPage && Math.Abs(zoom - _renderedZoom) < 0.0001 &&
            _rotationDegrees == _renderedRotation)
        {
            return;
        }

        try
        {
            // The page source itself is never asked to rotate anything
            // (`TD-99`): it renders the document exactly as stored, at the
            // requested scale, and this view turns the resulting bitmap —
            // the same separation `IDocumentPageSource`'s own remarks draw
            // between "what a format is" and "how the viewer behaves".
            var raw = RenderWholePage(pageIndex, zoom);
            _rendered = RotateForDisplay(raw, _rotationDegrees);
            _renderedPage = pageIndex;
            _renderedZoom = zoom;
            _renderedRotation = _rotationDegrees;
            _page.Source = _rendered;
        }
        catch (DocumentRenderException)
        {
            // A page that will not render is this page's failure, not the
            // document's: the rest of the document stays open and
            // navigable rather than the whole tab collapsing.
            _rendered = null;
            _page.Source = null;
        }
    }

    /// <summary>
    /// Renders the whole of page <paramref name="pageIndex"/> at
    /// <paramref name="zoom"/> — through <see cref="_source"/>'s own
    /// <see cref="IDocumentPageSource.RenderPage"/> directly for the
    /// ordinary case, or, when that single render would exceed
    /// <see cref="PdfDocumentPageSource.MaxRasterEdge"/> and the source
    /// supports it, composed from cached tiles instead (`TD-101`): the
    /// fix for the case that ceiling exists for at all — an A0 sheet at a
    /// deep zoom — which would otherwise ask for one enormous bitmap and
    /// get it back capped, and therefore blurred, rather than at the scale
    /// actually requested. Every tile this composes from is cached in
    /// <see cref="_tileCache"/>, so a later render at the identical page
    /// and zoom (a rotation, a page-and-back, a zoom-out-and-back-in) pays
    /// for only whichever tiles are not already there.
    /// </summary>
    private Bitmap RenderWholePage(int pageIndex, double zoom)
    {
        if (_source is ITiledDocumentPageSource tiled)
        {
            var nativeSize = _source.PageSize(pageIndex);
            var longestEdge = Math.Max(nativeSize.Width, nativeSize.Height) * zoom;

            if (longestEdge > TiledRenderMaxRasterEdge)
                return ComposeFromTiles(tiled, pageIndex, zoom, nativeSize);
        }

        return _source!.RenderPage(pageIndex, zoom);
    }

    /// <summary>Builds one bitmap covering the whole of <paramref name="nativeSize"/> at <paramref name="scale"/> from <see cref="_tileCache"/>'s own tiles, rendering (and caching) only the ones not already there.</summary>
    private Bitmap ComposeFromTiles(ITiledDocumentPageSource tiled, int pageIndex, double scale, Size nativeSize)
    {
        var renderedWidth = Math.Max(1, (int)Math.Round(nativeSize.Width * scale));
        var renderedHeight = Math.Max(1, (int)Math.Round(nativeSize.Height * scale));

        var target = new RenderTargetBitmap(new PixelSize(renderedWidth, renderedHeight), new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        {
            var tiles = TileGrid.VisibleTiles(
                nativeSize.Width, nativeSize.Height, scale,
                viewportOffsetX: 0, viewportOffsetY: 0, viewportWidth: renderedWidth, viewportHeight: renderedHeight, ringSize: 0);

            foreach (var (column, row) in tiles)
            {
                var key = new TileCache.Key(pageIndex, scale, column, row);
                var bitmap = _tileCache.TryGet(key);
                if (bitmap is null)
                {
                    bitmap = tiled.RenderTile(pageIndex, scale, column, row, TileGrid.TileSize);
                    _tileCache.Add(key, bitmap, EstimateBitmapBytes(bitmap));
                }

                var x = column * TileGrid.TileSize;
                var y = row * TileGrid.TileSize;
                context.DrawImage(bitmap, new Rect(x, y, bitmap.PixelSize.Width, bitmap.PixelSize.Height));
            }
        }

        return target;
    }

    /// <summary>A tile bitmap's own approximate memory footprint — BGRA8888, 4 bytes per pixel, the same format every page source in this file renders to.</summary>
    private static long EstimateBitmapBytes(Bitmap bitmap) =>
        (long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4;

    /// <summary>
    /// Turns <paramref name="source"/> by <paramref name="degrees"/>
    /// clockwise (0, 90, 180 or 270), returning a new bitmap whose own
    /// pixel width and height are swapped for a 90° or 270° turn — a real
    /// rotation of the rendered image, not a visual transform layered over
    /// an unchanged bitmap (`TD-99`).
    /// </summary>
    private static Bitmap RotateForDisplay(Bitmap source, int degrees)
    {
        if (degrees == 0)
            return source;

        var sourceSize = source.PixelSize;
        var swapped = degrees is 90 or 270;
        var targetSize = swapped ? new PixelSize(sourceSize.Height, sourceSize.Width) : sourceSize;

        var target = new RenderTargetBitmap(targetSize, new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        {
            // Move the source's own centre to the origin, rotate about it,
            // then move to the (possibly narrower or wider) target's own
            // centre — the standard rotate-about-centre composition, built
            // from the same `Matrix` primitives `TempestLogoControl`
            // already uses for its own transforms.
            var toOrigin = Matrix.CreateTranslation(-sourceSize.Width / 2.0, -sourceSize.Height / 2.0);
            var rotate = Matrix.CreateRotation(degrees * Math.PI / 180.0);
            var toCentre = Matrix.CreateTranslation(targetSize.Width / 2.0, targetSize.Height / 2.0);

            using (context.PushTransform(toOrigin * rotate * toCentre))
            {
                context.DrawImage(source, new Rect(0, 0, sourceSize.Width, sourceSize.Height));
            }
        }

        return target;
    }

    private void PositionPage(DocumentViewSession session)
    {
        var viewport = session.Viewport;

        // `TD-101`: the viewport's own content size already tracks the
        // page's currently-displayed (rotated) bounding box (`SetRotation`,
        // `DocumentViewport.WithContentSizeSwapped`), so `RenderedWidth`/
        // `RenderedHeight` are already the right shape for the bitmap
        // `RotateForDisplay` produced — no second swap needed here, unlike
        // before this WP fixed the rotation fit bug at its root.
        _page.Width = viewport.RenderedWidth;
        _page.Height = viewport.RenderedHeight;
        Canvas.SetLeft(_page, -viewport.OffsetX);
        Canvas.SetTop(_page, -viewport.OffsetY);

        PositionAnnotationLayer(session);
    }

    /// <summary>
    /// Shows or hides the page and zoom controls as a group.
    /// </summary>
    /// <remarks>
    /// Hidden rather than merely disabled when nothing is open. A row of
    /// page arrows, a zoom stepper, Fit and 100% over a surface reading
    /// "No content stored" is chrome for a document that is not there: it
    /// says the viewer is working and the user simply has not found the
    /// right button, when in fact none of them can do anything. The
    /// message is the whole content of that state, and it should be the
    /// whole of what the state shows.
    /// </remarks>
    private void SetViewControlsShown(bool shown)
    {
        _previousPage.IsVisible = shown;
        _nextPage.IsVisible = shown;
        _pageIndicator.IsVisible = shown;
        _zoomOut.IsVisible = shown;
        _zoomIndicator.IsVisible = shown;
        _zoomIn.IsVisible = shown;
        _fit.IsVisible = shown;
        _actualSize.IsVisible = shown;
        _rotateLeft.IsVisible = shown;
        _rotateRight.IsVisible = shown;

        _previousPage.IsEnabled = shown;
        _nextPage.IsEnabled = shown;
        _zoomIn.IsEnabled = shown;
        _zoomOut.IsEnabled = shown;
        _fit.IsEnabled = shown;
        _actualSize.IsEnabled = shown;
        _rotateLeft.IsEnabled = shown;
        _rotateRight.IsEnabled = shown;

        // The Annotations toolbar group is the same kind of chrome — real
        // only once a page is actually on screen for markup to land on.
        _annotationToolbar.IsVisible = shown;
        if (!shown)
        {
            _activeTool = null;
            _selectedAnnotationId = null;
        }

        UpdateAnnotationToolbarState();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Session is not { IsReady: true } session)
            return;

        if (_activeTool is { } tool)
        {
            BeginDrawing(tool, session, e);
            return;
        }

        // A press that reaches this far missed every annotation shape (each
        // marks its own press `Handled`), so it is a true click on empty
        // page — which clears a selection before panning, exactly as
        // clicking empty space deselects in any ordinary drawing tool.
        if (_selectedAnnotationId is not null)
        {
            _selectedAnnotationId = null;
            RenderAnnotationShapes();
            UpdateAnnotationToolbarState();
        }

        _dragOrigin = e.GetPosition(_canvas);
        e.Pointer.Capture(_canvas);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (Session is { IsReady: true } session && _activeTool is { } tool && _drawStartCanvas is not null)
        {
            UpdateDrawingPreview(tool, e.GetPosition(_canvas));
            return;
        }

        if (_dragOrigin is not { } origin)
            return;

        var position = e.GetPosition(_canvas);

        // Dragging moves the paper under the pointer, so the offset moves
        // the opposite way: drag right, see what was to the left.
        PanBy(origin.X - position.X, origin.Y - position.Y);
        _dragOrigin = position;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (Session is { IsReady: true } session && _activeTool is { } tool && _drawStartCanvas is not null)
        {
            e.Pointer.Capture(null);
            _ = FinishDrawingAsync(tool, session, e.GetPosition(_canvas));
            return;
        }

        _dragOrigin = null;
        e.Pointer.Capture(null);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (Session is not { IsReady: true } session)
            return;

        var anchor = e.GetPosition(_canvas);
        var factor = e.Delta.Y > 0 ? DocumentViewport.ZoomStep : 1 / DocumentViewport.ZoomStep;

        Apply(s => s.WithViewport(session.Viewport.ZoomAbout(session.Viewport.Zoom * factor, anchor.X, anchor.Y)));
        e.Handled = true;
    }

    /// <summary>
    /// Review board finding #4 (`WP 16.5A-R1`) — arrow-key panning, the
    /// keyboard-only path <see cref="OnPointerPressed"/>/<see cref="OnPointerMoved"/>/
    /// <see cref="OnPointerReleased"/> above never offered. Direction
    /// follows the same convention <see cref="OnPointerMoved"/>'s own
    /// remarks already state for the pointer path ("drag right, see what
    /// was to the left" — i.e. a positive <see cref="PanBy(double, double)"/>
    /// delta reveals content further right/down): <c>Right</c>/<c>Down</c>
    /// reveal what is to the right/below (positive delta), <c>Left</c>/<c>Up</c>
    /// reveal what is to the left/above (negative delta) — the same
    /// direction a mouse-wheel/scrollbar user already expects.
    /// </summary>
    private void OnViewerKeyDown(object? sender, KeyEventArgs e)
    {
        if (Session is not { IsReady: true })
            return;

        switch (e.Key)
        {
            case Key.Left:
                PanBy(-KeyboardPanStep, 0);
                e.Handled = true;
                break;

            case Key.Right:
                PanBy(KeyboardPanStep, 0);
                e.Handled = true;
                break;

            case Key.Up:
                PanBy(0, -KeyboardPanStep);
                e.Handled = true;
                break;

            case Key.Down:
                PanBy(0, KeyboardPanStep);
                e.Handled = true;
                break;
        }
    }

    // ================================================================
    // Markup and annotation (`TD-98`)
    // ================================================================

    /// <summary>The tool currently armed for drawing, or <see langword="null"/> when the viewer is in its ordinary pan/select mode.</summary>
    public AnnotationTool? ActiveTool => _activeTool;

    /// <summary>The colour a newly drawn annotation is recorded with.</summary>
    public string ActiveColorHex => _activeColorHex;

    /// <summary>The annotation currently selected (hit-tested, highlighted), or <see langword="null"/>.</summary>
    public Guid? SelectedAnnotationId => _selectedAnnotationId;

    /// <summary>Every annotation on the page currently showing, for the attachment currently open.</summary>
    public IReadOnlyList<AttachmentAnnotation> AnnotationsOnCurrentPage =>
        Session is { IsReady: true } session
            ? [.. _annotations.Where(a => a.AttachmentId == _annotationAttachmentId && a.PageIndex == session.CurrentPage - 1)]
            : [];

    /// <summary>
    /// Arms <paramref name="tool"/> for drawing — clicking the tool already
    /// active turns drawing off and returns to ordinary pan/select, exactly
    /// as clicking its own toolbar button does (this is what that button
    /// calls).
    /// </summary>
    public void SetActiveTool(AnnotationTool tool)
    {
        _activeTool = _activeTool == tool ? null : tool;
        CancelInProgressDrawing();
        UpdateAnnotationToolbarState();
    }

    /// <summary>Sets the colour the next drawn annotation is recorded with — one of <see cref="AnnotationPalette.Swatches"/>.</summary>
    public void SetActiveColor(string colorHex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(colorHex);

        _activeColorHex = colorHex;
        UpdateAnnotationToolbarState();
    }

    /// <summary>Selects (or, called again with the same id, deselects) an annotation — the hit-test result a click on its rendered shape already drives.</summary>
    public void SelectAnnotation(Guid? annotationId)
    {
        _selectedAnnotationId = annotationId;
        RenderAnnotationShapes();
        UpdateAnnotationToolbarState();
    }

    /// <summary>
    /// Draws and persists a new annotation on the page currently showing,
    /// with <see cref="ActiveColorHex"/> — exactly what completing a drag
    /// with <paramref name="tool"/> armed does, exposed directly so a test
    /// can drive it the same way <see cref="RotateLeft"/>/<see cref="ZoomIn"/>
    /// already let one drive the viewport with no pointer simulation
    /// needed. Does nothing (returns <see langword="null"/>) when no
    /// document is open, no annotation owner was given, or fewer than two
    /// points are supplied.
    /// </summary>
    public Task<AttachmentAnnotation?> DrawAnnotationAsync(AnnotationTool tool, IReadOnlyList<AnnotationPoint> points, string? text = null) =>
        points.Count < 2 && tool != AnnotationTool.TextNote
            ? Task.FromResult<AttachmentAnnotation?>(null)
            : AddAnnotationAsync(tool, points, text);

    /// <summary>Deletes <see cref="SelectedAnnotationId"/>, if there is one. Does nothing otherwise.</summary>
    public async Task DeleteSelectedAnnotationAsync()
    {
        if (_annotationOwner is not { } owner || _selectedAnnotationId is not { } id)
            return;

        await owner.DeleteAttachmentAnnotationAsync(id).ConfigureAwait(true);
        _annotations.RemoveAll(a => a.Id == id);
        _selectedAnnotationId = null;
        RenderAnnotationShapes();
        UpdateAnnotationToolbarState();
    }

    /// <summary>
    /// Clears every annotation on the page currently showing, after the
    /// user confirms — the Annotations toolbar group's own "Clear page"
    /// action. Does nothing (asks nothing) when the page already carries no
    /// annotation.
    /// </summary>
    public async Task ClearPageAnnotationsAsync()
    {
        if (_annotationOwner is not { } owner || Session is not { IsReady: true } session)
            return;

        var pageIndex = session.CurrentPage - 1;
        if (!_annotations.Any(a => a.AttachmentId == _annotationAttachmentId && a.PageIndex == pageIndex))
            return;

        var confirmed = await _confirmClearPage.ConfirmAsync(
            "Clear page annotations?",
            $"This removes every piece of markup on page {session.CurrentPage}. It cannot be undone.",
            "Clear").ConfigureAwait(true);

        if (!confirmed)
            return;

        await owner.ClearAttachmentAnnotationsAsync(_annotationAttachmentId, pageIndex).ConfigureAwait(true);
        _annotations.RemoveAll(a => a.AttachmentId == _annotationAttachmentId && a.PageIndex == pageIndex);
        _selectedAnnotationId = null;
        RenderAnnotationShapes();
        UpdateAnnotationToolbarState();
    }

    /// <summary>
    /// Writes a copy of the page currently showing — a PDF for a PDF
    /// source, a PNG for every other format — with its own annotations
    /// burned in, to wherever <see cref="FilePicker"/> sends it. The
    /// original attachment is never opened for writing by this method, so
    /// it is left exactly as it was regardless of what the user picks or
    /// cancels.
    /// </summary>
    public async Task SaveAnnotatedCopyAsync()
    {
        if (Session is not { IsReady: true } session || _rendered is null || _source is null)
            return;

        var pageIndex = session.CurrentPage - 1;
        var onPage = _annotations.Where(a => a.AttachmentId == _annotationAttachmentId && a.PageIndex == pageIndex).ToList();
        var nativeSize = _source.PageSize(pageIndex);

        using var composed = AnnotatedCopyExporter.ComposeAnnotatedPage(
            _rendered, onPage, nativeSize.Width, nativeSize.Height, _rotationDegrees, _renderedZoom);

        var isPdf = session.Format is ViewableDocumentFormat.Pdf;
        var extension = isPdf ? "pdf" : "png";
        var baseName = string.IsNullOrWhiteSpace(session.FileName) ? "document" : System.IO.Path.GetFileNameWithoutExtension(session.FileName);

        var path = await FilePicker.PickSavePathAsync(
            new SavePickerRequest("Save annotated copy…", $"{baseName} (annotated).{extension}")).ConfigureAwait(true);

        if (path is null)
            return;

        var bytes = isPdf ? AnnotatedCopyExporter.EncodePdf(composed) : AnnotatedCopyExporter.EncodePng(composed);
        await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(true);
    }

    private async Task<AttachmentAnnotation?> AddAnnotationAsync(AnnotationTool tool, IReadOnlyList<AnnotationPoint> points, string? text)
    {
        if (_annotationOwner is not { } owner || Session is not { IsReady: true } session)
            return null;

        var pageIndex = session.CurrentPage - 1;
        var added = await owner.AddAttachmentAnnotationAsync(
            _annotationAttachmentId, pageIndex, tool, points, _activeColorHex, text).ConfigureAwait(true);

        _annotations.Add(added);
        RenderAnnotationShapes();
        return added;
    }

    private void CancelInProgressDrawing()
    {
        _drawStartCanvas = null;
        _freehandNativePoints = null;

        if (_drawPreview is { } preview)
        {
            _annotationLayer.Children.Remove(preview);
            _drawPreview = null;
        }
    }

    private void BeginDrawing(AnnotationTool tool, DocumentViewSession session, PointerPressedEventArgs e)
    {
        var canvasPoint = e.GetPosition(_canvas);

        if (tool == AnnotationTool.TextNote)
        {
            _ = HandleTextNoteClickAsync(session, canvasPoint);
            return;
        }

        _drawStartCanvas = canvasPoint;
        _drawStartNative = CanvasToNativePoint(canvasPoint, session);
        e.Pointer.Capture(_canvas);

        var brush = ActiveColorBrush();

        if (tool == AnnotationTool.Freehand)
        {
            _freehandNativePoints = [_drawStartNative];
            var polyline = new Polyline
            {
                Points = new Points { canvasPoint },
                Stroke = brush,
                StrokeThickness = AnnotationStrokeThickness,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
            };
            _drawPreview = polyline;
            _annotationLayer.Children.Add(polyline);
            return;
        }

        Control preview = tool switch
        {
            AnnotationTool.Rectangle => new Rectangle { Stroke = brush, StrokeThickness = AnnotationStrokeThickness, Fill = Brushes.Transparent, Width = 0, Height = 0 },
            AnnotationTool.Ellipse => new Ellipse { Stroke = brush, StrokeThickness = AnnotationStrokeThickness, Fill = Brushes.Transparent, Width = 0, Height = 0 },
            AnnotationTool.Arrow => new Line { Stroke = brush, StrokeThickness = AnnotationStrokeThickness, StartPoint = canvasPoint, EndPoint = canvasPoint },
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, "Unhandled draw tool."),
        };

        if (tool != AnnotationTool.Arrow)
        {
            Canvas.SetLeft(preview, canvasPoint.X);
            Canvas.SetTop(preview, canvasPoint.Y);
        }

        _drawPreview = preview;
        _annotationLayer.Children.Add(preview);
    }

    private void UpdateDrawingPreview(AnnotationTool tool, Point canvasPoint)
    {
        switch (tool)
        {
            case AnnotationTool.Freehand when _drawPreview is Polyline polyline && _freehandNativePoints is { } native && Session is { IsReady: true } session:
                native.Add(CanvasToNativePoint(canvasPoint, session));
                polyline.Points.Add(canvasPoint);
                break;

            case AnnotationTool.Rectangle when _drawPreview is Rectangle rectangle && _drawStartCanvas is { } rStart:
            {
                var (topLeft, size) = FromCorners(rStart, canvasPoint);
                Canvas.SetLeft(rectangle, topLeft.X);
                Canvas.SetTop(rectangle, topLeft.Y);
                rectangle.Width = size.Width;
                rectangle.Height = size.Height;
                break;
            }

            case AnnotationTool.Ellipse when _drawPreview is Ellipse ellipse && _drawStartCanvas is { } eStart:
            {
                var (topLeft, size) = FromCorners(eStart, canvasPoint);
                Canvas.SetLeft(ellipse, topLeft.X);
                Canvas.SetTop(ellipse, topLeft.Y);
                ellipse.Width = size.Width;
                ellipse.Height = size.Height;
                break;
            }

            case AnnotationTool.Arrow when _drawPreview is Line line:
                line.EndPoint = canvasPoint;
                break;
        }
    }

    private async Task FinishDrawingAsync(AnnotationTool tool, DocumentViewSession session, Point canvasPoint)
    {
        CancelInProgressDrawing();

        IReadOnlyList<AnnotationPoint> points = tool == AnnotationTool.Freehand
            ? []
            : [_drawStartNative, CanvasToNativePoint(canvasPoint, session)];

        if (points.Count < 2)
            return;

        await AddAnnotationAsync(tool, points, text: null).ConfigureAwait(true);
    }

    private async Task HandleTextNoteClickAsync(DocumentViewSession session, Point canvasPoint)
    {
        var text = await _noteInput.PromptAsync("Add note", "Note text").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(text))
            return;

        var native = CanvasToNativePoint(canvasPoint, session);
        await AddAnnotationAsync(AnnotationTool.TextNote, [native], text).ConfigureAwait(true);
    }

    private static (Point TopLeft, Size Size) FromCorners(Point a, Point b) =>
        (new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Size(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)));

    private SolidColorBrush ActiveColorBrush() => new(Color.Parse(_activeColorHex));

    /// <summary>Positions the annotation overlay exactly over the rendered page and redraws it — called every time <c>PositionPage</c> is (`TD-98`, `TD-101`).</summary>
    private void PositionAnnotationLayer(DocumentViewSession session)
    {
        _annotationLayer.Width = _page.Width;
        _annotationLayer.Height = _page.Height;
        Canvas.SetLeft(_annotationLayer, Canvas.GetLeft(_page));
        Canvas.SetTop(_annotationLayer, Canvas.GetTop(_page));

        RenderAnnotationShapes();
    }

    private void RenderAnnotationShapes()
    {
        _annotationLayer.Children.Clear();

        if (Session is not { IsReady: true } session || _source is null)
            return;

        var pageIndex = session.CurrentPage - 1;

        foreach (var annotation in _annotations.Where(a => a.AttachmentId == _annotationAttachmentId && a.PageIndex == pageIndex))
        {
            var shape = BuildAnnotationShape(annotation, session);
            shape.Tag = annotation.Id;
            shape.PointerPressed += OnAnnotationShapePressed;
            _annotationLayer.Children.Add(shape);
        }
    }

    private Control BuildAnnotationShape(AttachmentAnnotation annotation, DocumentViewSession session)
    {
        var brush = new SolidColorBrush(Color.Parse(annotation.ColorHex));
        var thickness = _selectedAnnotationId == annotation.Id ? AnnotationStrokeThickness + 1.5 : AnnotationStrokeThickness;

        return annotation.Tool switch
        {
            AnnotationTool.Rectangle => BuildRectangleShape(annotation, session, brush, thickness),
            AnnotationTool.Ellipse => BuildEllipseShape(annotation, session, brush, thickness),
            AnnotationTool.Arrow => BuildArrowShape(annotation, session, brush, thickness),
            AnnotationTool.Freehand => BuildFreehandShape(annotation, session, brush, thickness),
            AnnotationTool.TextNote => BuildTextNoteShape(annotation, session, brush),
            _ => new Rectangle { Width = 0, Height = 0 },
        };
    }

    private Rectangle BuildRectangleShape(AttachmentAnnotation annotation, DocumentViewSession session, IBrush brush, double thickness)
    {
        var (topLeft, size) = BoundingBox(annotation.Points, session);
        var rectangle = new Rectangle { Width = size.Width, Height = size.Height, Stroke = brush, StrokeThickness = thickness, Fill = Brushes.Transparent };
        Canvas.SetLeft(rectangle, topLeft.X);
        Canvas.SetTop(rectangle, topLeft.Y);
        return rectangle;
    }

    private Ellipse BuildEllipseShape(AttachmentAnnotation annotation, DocumentViewSession session, IBrush brush, double thickness)
    {
        var (topLeft, size) = BoundingBox(annotation.Points, session);
        var ellipse = new Ellipse { Width = size.Width, Height = size.Height, Stroke = brush, StrokeThickness = thickness, Fill = Brushes.Transparent };
        Canvas.SetLeft(ellipse, topLeft.X);
        Canvas.SetTop(ellipse, topLeft.Y);
        return ellipse;
    }

    private Polyline BuildArrowShape(AttachmentAnnotation annotation, DocumentViewSession session, IBrush brush, double thickness)
    {
        var start = NativeToCanvasPoint(annotation.Points[0], session);
        var end = NativeToCanvasPoint(annotation.Points[^1], session);
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        var left = new Point(end.X - AnnotationArrowHeadLength * Math.Cos(angle - AnnotationArrowHeadAngle), end.Y - AnnotationArrowHeadLength * Math.Sin(angle - AnnotationArrowHeadAngle));
        var right = new Point(end.X - AnnotationArrowHeadLength * Math.Cos(angle + AnnotationArrowHeadAngle), end.Y - AnnotationArrowHeadLength * Math.Sin(angle + AnnotationArrowHeadAngle));

        return new Polyline
        {
            Points = new Points { start, end, left, end, right },
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
    }

    private Polyline BuildFreehandShape(AttachmentAnnotation annotation, DocumentViewSession session, IBrush brush, double thickness)
    {
        var points = new Points();
        foreach (var point in annotation.Points)
            points.Add(NativeToCanvasPoint(point, session));

        return new Polyline { Points = points, Stroke = brush, StrokeThickness = thickness, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round };
    }

    private Border BuildTextNoteShape(AttachmentAnnotation annotation, DocumentViewSession session, IBrush brush)
    {
        var anchor = NativeToCanvasPoint(annotation.Points[0], session);
        var border = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(DesignTokens.BadgeCornerRadius),
            Padding = new Thickness(6, 3),
            MaxWidth = 220,
            Child = new TextBlock
            {
                Text = annotation.Text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontSize = DesignTokens.FontSizeCaption,
            },
        };

        Canvas.SetLeft(border, anchor.X);
        Canvas.SetTop(border, anchor.Y);
        return border;
    }

    private (Point TopLeft, Size Size) BoundingBox(IReadOnlyList<AnnotationPoint> points, DocumentViewSession session)
    {
        var canvasPoints = points.Select(p => NativeToCanvasPoint(p, session)).ToList();
        var minX = canvasPoints.Min(p => p.X);
        var minY = canvasPoints.Min(p => p.Y);
        var maxX = canvasPoints.Max(p => p.X);
        var maxY = canvasPoints.Max(p => p.Y);
        return (new Point(minX, minY), new Size(Math.Max(1, maxX - minX), Math.Max(1, maxY - minY)));
    }

    private void OnAnnotationShapePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.Tag is not Guid annotationId)
            return;

        // Handled here so this press never also reaches `_canvas`'s own
        // pan-start/draw-start handler beneath the shape.
        e.Handled = true;

        SelectAnnotation(_selectedAnnotationId == annotationId ? null : annotationId);
    }

    /// <summary>Converts a point stored in a page's native (unrotated) units into this control's own canvas-local pixels, at the current zoom and rotation.</summary>
    private Point NativeToCanvasPoint(AnnotationPoint native, DocumentViewSession session)
    {
        var nativeSize = _source!.PageSize(session.CurrentPage - 1);
        var (rx, ry) = AnnotationGeometry.ToRotatedContentSpace(native.X, native.Y, nativeSize.Width, nativeSize.Height, _rotationDegrees);
        var zoom = session.Viewport.Zoom;
        return new Point(rx * zoom, ry * zoom);
    }

    /// <summary>The exact inverse of <see cref="NativeToCanvasPoint"/> — a canvas-local pixel back to the page's native (unrotated) units, what a drawn stroke is stored as.</summary>
    private AnnotationPoint CanvasToNativePoint(Point canvasPoint, DocumentViewSession session)
    {
        var zoom = session.Viewport.Zoom;
        var rx = (canvasPoint.X - Canvas.GetLeft(_page)) / zoom;
        var ry = (canvasPoint.Y - Canvas.GetTop(_page)) / zoom;

        var nativeSize = _source!.PageSize(session.CurrentPage - 1);
        var (nx, ny) = AnnotationGeometry.FromRotatedContentSpace(rx, ry, nativeSize.Width, nativeSize.Height, _rotationDegrees);
        return new AnnotationPoint(nx, ny);
    }

    private void UpdateAnnotationToolbarState()
    {
        var ready = Session is { IsReady: true };

        SetToolActiveVisual(_rectangleTool, _activeTool == AnnotationTool.Rectangle);
        SetToolActiveVisual(_ellipseTool, _activeTool == AnnotationTool.Ellipse);
        SetToolActiveVisual(_freehandTool, _activeTool == AnnotationTool.Freehand);
        SetToolActiveVisual(_arrowTool, _activeTool == AnnotationTool.Arrow);
        SetToolActiveVisual(_textNoteTool, _activeTool == AnnotationTool.TextNote);

        for (var i = 0; i < _colorSwatches.Count && i < AnnotationPalette.Swatches.Count; i++)
            SetToolActiveVisual(_colorSwatches[i], string.Equals(AnnotationPalette.Swatches[i].Hex, _activeColorHex, StringComparison.OrdinalIgnoreCase));

        _deleteAnnotation.IsEnabled = ready && _selectedAnnotationId is not null;
        _clearPage.IsEnabled = ready;
        _saveAnnotatedCopy.IsEnabled = ready;
    }

    private static void SetToolActiveVisual(Button button, bool active)
    {
        button.FontWeight = active ? FontWeight.Bold : FontWeight.Normal;
        button.Opacity = active ? 1.0 : 0.75;
    }

    private static Button ToolbarButton(string caption, string tooltip, Action onClick)
    {
        var button = new Button
        {
            Content = caption,
            Padding = new Thickness(10, 2),
            MinWidth = 34,
        };

        ToolTip.SetTip(button, tooltip);
        // Review board finding #4 (`WP 16.5A-R1`): the glyph-only buttons
        // ("‹"/"›"/"−"/"+") had no `AutomationProperties.SetName`, so
        // their accessible name resolved to the bare glyph character.
        // `tooltip` is already the real, descriptive name every caller
        // passes ("Previous page", "Next page", "Zoom out", "Zoom in",
        // …) — set uniformly here rather than per-button.
        AutomationProperties.SetName(button, tooltip);
        button.Click += (_, _) => onClick();
        return button;
    }
}
