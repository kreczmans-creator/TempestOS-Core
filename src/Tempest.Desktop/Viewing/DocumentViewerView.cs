using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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

        _openExternally = ToolbarButton(
            "Open externally", "Open externally", OpenExternally);

        _unavailable.Children.Add(_unavailableHeadline);
        _unavailable.Children.Add(_unavailableDetail);
        _unavailable.Children.Add(_openExternally);
        _unavailable.IsVisible = false;
        _openExternally.IsVisible = false;

        _canvas.Children.Add(_page);
        _canvas.ClipToBounds = true;

        var content = new Grid();
        content.Children.Add(_canvas);
        content.Children.Add(_unavailable);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(content, 1);
        root.Children.Add(toolbar);
        root.Children.Add(content);

        Content = root;

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
        _renderedPage = -1;
        _renderedZoom = 0;
        _renderedRotation = 0;
        _rotationDegrees = 0;
        _sizedPage = -1;
        Session = session;
        SyncPageSize();
        Refresh();
    }

    /// <summary>Opens a document that did not load, showing why.</summary>
    public void OpenUnavailable(DocumentViewSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _source?.Dispose();
        _source = null;
        _rendered = null;
        _page.Source = null;
        _sizedPage = -1;
        _rotationDegrees = 0;
        _renderedRotation = 0;
        Session = session;
        Refresh();
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
        var viewport = session.Viewport;

        if (Math.Abs(page.Width - viewport.ContentWidth) > 0.01 ||
            Math.Abs(page.Height - viewport.ContentHeight) > 0.01)
        {
            Session = session.WithPageSize(page.Width, page.Height);
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
            var raw = _source.RenderPage(pageIndex, zoom);
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
        var swapped = _rotationDegrees is 90 or 270;

        // The viewport's own fit/zoom maths runs over the page's un-rotated
        // size throughout (`TD-99`'s own disclosed scope cut — rotation is
        // a render-only feature, not a viewport-model change), so only the
        // rendered box's own width and height are swapped here, to match
        // the bitmap `RotateForDisplay` actually produced.
        _page.Width = swapped ? viewport.RenderedHeight : viewport.RenderedWidth;
        _page.Height = swapped ? viewport.RenderedWidth : viewport.RenderedHeight;
        Canvas.SetLeft(_page, -viewport.OffsetX);
        Canvas.SetTop(_page, -viewport.OffsetY);
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
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Session is not { IsReady: true })
            return;

        _dragOrigin = e.GetPosition(_canvas);
        e.Pointer.Capture(_canvas);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
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
