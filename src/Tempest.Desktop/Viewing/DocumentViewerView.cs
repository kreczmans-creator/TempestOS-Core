using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Tempest.App.Workspace.Viewing;

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
    private readonly Image _page = new() { Stretch = Stretch.Fill };
    private readonly Canvas _canvas = new() { Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x41)) };
    private readonly TextBlock _pageIndicator = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
    private readonly TextBlock _zoomIndicator = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
    private readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
    private readonly StackPanel _unavailable = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 6 };
    private readonly TextBlock _unavailableHeadline = new() { FontSize = 15, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _unavailableDetail = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 420, TextAlignment = TextAlignment.Center, Opacity = 0.8 };
    private readonly Button _previousPage;
    private readonly Button _nextPage;
    private readonly Button _zoomIn;
    private readonly Button _zoomOut;
    private readonly Button _fit;
    private readonly Button _actualSize;

    private IDocumentPageSource? _source;
    private Bitmap? _rendered;
    private Point? _dragOrigin;
    private double _renderedZoom;
    private int _renderedPage = -1;
    private int _sizedPage = -1;

    /// <summary>Initialises a new instance of the <see cref="DocumentViewerView"/> class.</summary>
    public DocumentViewerView()
    {
        _previousPage = ToolbarButton("‹", "Previous page", () => Apply(s => s.PreviousPage()));
        _nextPage = ToolbarButton("›", "Next page", () => Apply(s => s.NextPage()));
        _zoomOut = ToolbarButton("−", "Zoom out", ZoomOut);
        _zoomIn = ToolbarButton("+", "Zoom in", ZoomIn);
        _fit = ToolbarButton("Fit", "Fit the whole page in the view", FitToView);
        _actualSize = ToolbarButton("100%", "Show the page at its actual size", ActualSize);

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(6, 4),
            Children = { _title, _previousPage, _pageIndicator, _nextPage, _zoomOut, _zoomIndicator, _zoomIn, _fit, _actualSize },
        };

        _unavailable.Children.Add(_unavailableHeadline);
        _unavailable.Children.Add(_unavailableDetail);
        _unavailable.IsVisible = false;

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

    /// <summary>Opens a document that loaded, with the source that renders its pages.</summary>
    public void Open(DocumentViewSession session, IDocumentPageSource source)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(source);

        _source?.Dispose();
        _source = source;
        _renderedPage = -1;
        _renderedZoom = 0;
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
        Session = session;
        Refresh();
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
            return;
        }

        _canvas.IsVisible = true;
        _unavailable.IsVisible = false;

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

        _ => (
            "This format cannot be displayed",
            $"'{session.FileName}' is intact, and TempestOS has no viewer for {DescribeType(session.ContentType)}. " +
            "PDF documents and drawings, PNG/JPEG/BMP/GIF/WebP images and text files can be viewed."),
    };

    private static string DescribeType(string contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? "this file type" : $"'{contentType}'";

    private void RenderCurrentPage(DocumentViewSession session)
    {
        if (_source is null)
            return;

        var pageIndex = session.CurrentPage - 1;
        var zoom = session.Viewport.Zoom;

        // Re-rasterise only when the page or the zoom actually changed. A
        // pan must not re-render: it is the same pixels in a different
        // place, and re-rendering a large drawing on every pointer move
        // makes panning unusable.
        if (_rendered is not null && pageIndex == _renderedPage && Math.Abs(zoom - _renderedZoom) < 0.0001)
            return;

        try
        {
            _rendered = _source.RenderPage(pageIndex, zoom);
            _renderedPage = pageIndex;
            _renderedZoom = zoom;
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

    private void PositionPage(DocumentViewSession session)
    {
        var viewport = session.Viewport;

        _page.Width = viewport.RenderedWidth;
        _page.Height = viewport.RenderedHeight;
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

        _previousPage.IsEnabled = shown;
        _nextPage.IsEnabled = shown;
        _zoomIn.IsEnabled = shown;
        _zoomOut.IsEnabled = shown;
        _fit.IsEnabled = shown;
        _actualSize.IsEnabled = shown;
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

    private static Button ToolbarButton(string caption, string tooltip, Action onClick)
    {
        var button = new Button
        {
            Content = caption,
            Padding = new Thickness(10, 2),
            MinWidth = 34,
        };

        ToolTip.SetTip(button, tooltip);
        button.Click += (_, _) => onClick();
        return button;
    }
}
