using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Tempest.Workspace.Viewing;
using Tempest.Desktop.Viewing;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Rotate left / Rotate right (`TD-98`, narrowed by `WP 20.2B` — markup and
/// annotation are still out, ADR-0115's own scope cut): applied at render
/// only. <see cref="IDocumentPageSource"/> is never asked to rotate
/// anything — <see cref="DocumentViewerView"/> turns the bitmap the source
/// already produced.
/// </summary>
public sealed class DocumentViewerRotationTests
{
    private static (DocumentViewerView Viewer, IDocumentPageSource Source) OpenMultiPagePdf()
    {
        var viewer = new DocumentViewerView();
        var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, DocumentPageSourceTests.MultiPagePdf())!;
        var session = DocumentViewSession.Ready(
            Guid.NewGuid(), "sheet.pdf", "application/pdf", ViewableDocumentFormat.Pdf,
            pageCount: 3, contentWidth: 595, contentHeight: 842, viewportWidth: 800, viewportHeight: 600);
        viewer.Open(session, source);
        return (viewer, source);
    }

    [AvaloniaFact]
    public void ANewDocument_OpensWithNoRotation()
    {
        var (viewer, source) = OpenMultiPagePdf();
        using var disposable = source;

        Assert.Equal(0, viewer.RotationDegrees);
    }

    [AvaloniaFact]
    public void RotateRight_TurnsTheRenderedBitmap_SwappingWidthAndHeight()
    {
        var (viewer, source) = OpenMultiPagePdf();
        using var disposable = source;

        // Zoomed to a fixed, non-fitted value first (`TD-101`): a fitted
        // view now re-fits to the rotated bounding box (see
        // `RotatingAFittedView_StaysFitted_ToTheRotatedBoundingBox` below),
        // which genuinely changes the rendered zoom, not only the bitmap's
        // own orientation — exactly the fix this WP's brief asks for. This
        // test's own claim is narrower and still true at any fixed zoom: a
        // rotation turns the rendered bitmap, swapping its pixel width and
        // height, and asks the page source for nothing different.
        viewer.ActualSize();
        var before = viewer.RenderedPage!.PixelSize;

        viewer.RotateRight();

        Assert.Equal(90, viewer.RotationDegrees);
        Assert.Equal(before.Width, viewer.RenderedPage!.PixelSize.Height);
        Assert.Equal(before.Height, viewer.RenderedPage!.PixelSize.Width);
    }

    [AvaloniaFact]
    public void RotateLeft_TurnsTheOtherWay_AlsoSwappingWidthAndHeight()
    {
        var (viewer, source) = OpenMultiPagePdf();
        using var disposable = source;

        // See `RotateRight_...`'s own remark: fixed zoom, so only the
        // rotation's own effect — the bitmap's own swap — is under test.
        viewer.ActualSize();
        var before = viewer.RenderedPage!.PixelSize;

        viewer.RotateLeft();

        Assert.Equal(270, viewer.RotationDegrees);
        Assert.Equal(before.Width, viewer.RenderedPage!.PixelSize.Height);
        Assert.Equal(before.Height, viewer.RenderedPage!.PixelSize.Width);
    }

    [AvaloniaFact]
    public void RotatingAFittedView_StaysFitted_ToTheRotatedBoundingBox()
    {
        // The rotation fit bug `WP 20.2B` disclosed, closed here (`TD-101`):
        // before this fix, ContentWidth/ContentHeight stayed the page's
        // native (unrotated) size throughout, so a rotated landscape page
        // could sit genuinely un-fitted — "Fit" was the last zoom the user
        // pressed, but the rotated bounding box no longer matched the
        // viewport at that zoom, and only a manual zoom step fixed it.
        var (viewer, source) = OpenMultiPagePdf();
        using var disposable = source;

        Assert.True(viewer.Session!.Viewport.IsFitted);
        var fittedZoomBeforeRotate = viewer.Session!.Viewport.Zoom;

        viewer.RotateRight();

        // Still fitted — to the *rotated* bounding box, not the stale one.
        Assert.True(viewer.Session!.Viewport.IsFitted);
        Assert.Equal(842, viewer.Session!.Viewport.ContentWidth, 0.5);
        Assert.Equal(595, viewer.Session!.Viewport.ContentHeight, 0.5);

        // A genuinely different zoom for a genuinely different (landscape)
        // bounding box, not the same zoom simply carried over — exactly
        // what "fits with no manual zoom step" means.
        Assert.NotEqual(fittedZoomBeforeRotate, viewer.Session!.Viewport.Zoom, 3);

        // No manual zoom step needed: the rendered box already reaches an
        // edge of the viewport on at least one axis, which is what
        // "fitted" means.
        var viewport = viewer.Session!.Viewport;
        Assert.True(
            Math.Abs(viewport.RenderedWidth - viewport.ViewportWidth) < 0.5 ||
            Math.Abs(viewport.RenderedHeight - viewport.ViewportHeight) < 0.5);
    }

    [AvaloniaFact]
    public void RotatingAZoomedInView_KeepsTheSameZoom_NotAForcedRefit()
    {
        // `DocumentViewport.WithViewportSize`'s own convention, applied to
        // rotation too (`WithContentSizeSwapped`): a user who deliberately
        // zoomed in should not have that undone by turning the page, any
        // more than by resizing the window.
        var (viewer, source) = OpenMultiPagePdf();
        using var disposable = source;

        viewer.ZoomIn();
        viewer.ZoomIn();
        var zoomedZoom = viewer.Session!.Viewport.Zoom;
        Assert.False(viewer.Session!.Viewport.IsFitted);

        viewer.RotateRight();

        Assert.Equal(zoomedZoom, viewer.Session!.Viewport.Zoom, 6);
    }

    [AvaloniaFact]
    public void FourRightTurns_ReturnExactlyToTheStartingOrientation()
    {
        var (viewer, source) = OpenMultiPagePdf();
        using var disposable = source;

        var before = viewer.RenderedPage!.PixelSize;

        viewer.RotateRight();
        viewer.RotateRight();
        viewer.RotateRight();
        viewer.RotateRight();

        Assert.Equal(0, viewer.RotationDegrees);
        Assert.Equal(before.Width, viewer.RenderedPage!.PixelSize.Width);
        Assert.Equal(before.Height, viewer.RenderedPage!.PixelSize.Height);
    }

    [AvaloniaFact]
    public void RotationIsRemembered_ThroughZoomPageTurnAndPan()
    {
        // "Remembered per document for the session" — for as long as this
        // tab shows this document, every other operation leaves it alone.
        var (viewer, source) = OpenMultiPagePdf();
        using var disposable = source;

        viewer.RotateRight();
        Assert.Equal(90, viewer.RotationDegrees);

        viewer.ZoomIn();
        Assert.Equal(90, viewer.RotationDegrees);

        viewer.NextPage();
        Assert.Equal(90, viewer.RotationDegrees);

        viewer.PanBy(10, 10);
        Assert.Equal(90, viewer.RotationDegrees);
    }

    [AvaloniaFact]
    public void OpeningADifferentDocumentIntoTheSameTab_ResetsRotationToZero()
    {
        var (viewer, firstSource) = OpenMultiPagePdf();
        viewer.RotateRight();
        Assert.Equal(90, viewer.RotationDegrees);

        using var secondSource = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Text, "Second document."u8.ToArray())!;
        var secondSession = DocumentViewSession.Ready(
            Guid.NewGuid(), "notes.txt", "text/plain", ViewableDocumentFormat.Text,
            pageCount: 1,
            contentWidth: TextDocumentPageSource.TextPageSize.Width,
            contentHeight: TextDocumentPageSource.TextPageSize.Height,
            viewportWidth: 400,
            viewportHeight: 300);

        viewer.Open(secondSession, secondSource);

        Assert.Equal(0, viewer.RotationDegrees);
        firstSource.Dispose();
    }

    [AvaloniaFact]
    public void RotateButtons_HaveRealAutomationNames()
    {
        var (viewer, source) = OpenMultiPagePdf();
        using var disposable = source;

        var rotateLeft = viewer.GetLogicalDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetName(b) == "Rotate left");
        var rotateRight = viewer.GetLogicalDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetName(b) == "Rotate right");

        Assert.True(rotateLeft.IsVisible);
        Assert.True(rotateRight.IsVisible);
    }

    [AvaloniaFact]
    public void RotationDoesNotChangeWhatThePageSourceIsAskedToRender()
    {
        // "Applied at render" (the brief's own words): the source is asked
        // for the same page at the same scale, rotated or not.
        using var recordingSource = new RecordingPageSource();
        var viewer = new DocumentViewerView();
        var session = DocumentViewSession.Ready(
            Guid.NewGuid(), "recorded.pdf", "application/pdf", ViewableDocumentFormat.Pdf,
            pageCount: 1, contentWidth: 100, contentHeight: 100, viewportWidth: 400, viewportHeight: 300);
        viewer.Open(session, recordingSource);

        var callsBeforeRotate = recordingSource.RenderCalls;

        viewer.RotateRight();

        Assert.Equal(callsBeforeRotate + 1, recordingSource.RenderCalls);
        Assert.Equal(0, recordingSource.LastPageIndex);
        Assert.Equal(viewer.Session!.Viewport.Zoom, recordingSource.LastScale, 0.0001);
    }

    private sealed class RecordingPageSource : IDocumentPageSource
    {
        private readonly IDocumentPageSource _inner =
            DocumentPageSourceFactory.Create(ViewableDocumentFormat.Image, DocumentPageSourceTests.Png())!;

        public int RenderCalls { get; private set; }
        public int LastPageIndex { get; private set; }
        public double LastScale { get; private set; }

        public int PageCount => 1;

        public Size PageSize(int pageIndex) => _inner.PageSize(pageIndex);

        public Bitmap RenderPage(int pageIndex, double scale)
        {
            RenderCalls++;
            LastPageIndex = pageIndex;
            LastScale = scale;
            return _inner.RenderPage(pageIndex, scale);
        }

        public void Dispose() => _inner.Dispose();
    }
}
