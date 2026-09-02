using Tempest.App.Workspace.Viewing;

namespace Tempest.Core.Tests.Workspace.Viewing;

/// <summary>
/// The viewer's zoom, pan and fit geometry (`TD-80`), with no UI in the
/// process.
/// </summary>
/// <remarks>
/// Every rule the viewer obeys when a user scrolls, drags or resizes is
/// decided by this type, so it can be pinned here as arithmetic rather
/// than by raising pointer events at a control and inspecting pixels.
/// </remarks>
public class DocumentViewportTests
{
    private const double Tolerance = 0.0001;

    [Fact]
    public void ANewViewport_OpensFitted_ShowingTheWholeDocument()
    {
        // Opening a drawing shows the drawing, not its top-left corner.
        var viewport = DocumentViewport.Create(1000, 500, 400, 400);

        Assert.True(viewport.IsFitted);
        Assert.Equal(0.4, viewport.Zoom, Tolerance);
        Assert.Equal(400, viewport.RenderedWidth, Tolerance);
        Assert.Equal(200, viewport.RenderedHeight, Tolerance);
    }

    [Fact]
    public void FitZoom_UsesTheMoreConstrainingAxis()
    {
        // Fitting on the wrong axis is the classic viewer defect: the page
        // fills the width and its bottom half is off-screen.
        var wide = DocumentViewport.Create(2000, 100, 400, 400);
        Assert.Equal(0.2, wide.Zoom, Tolerance);

        var tall = DocumentViewport.Create(100, 2000, 400, 400);
        Assert.Equal(0.2, tall.Zoom, Tolerance);
    }

    [Fact]
    public void ContentSmallerThanTheView_IsCentred_NotPinnedToACorner()
    {
        var viewport = DocumentViewport.Create(200, 100, 400, 400).ActualSize();

        Assert.Equal(1.0, viewport.Zoom, Tolerance);
        Assert.Equal(-100, viewport.OffsetX, Tolerance);
        Assert.Equal(-150, viewport.OffsetY, Tolerance);
    }

    [Fact]
    public void PanningPastAnEdge_StopsAtIt()
    {
        // The guarantee that stops a drawing being scrolled off into grey
        // space and looking like it vanished.
        var viewport = DocumentViewport.Create(1000, 1000, 200, 200).ActualSize();

        Assert.Equal(0, viewport.PanBy(-9999, -9999).OffsetX, Tolerance);
        Assert.Equal(0, viewport.PanBy(-9999, -9999).OffsetY, Tolerance);
        Assert.Equal(800, viewport.PanBy(9999, 9999).OffsetX, Tolerance);
        Assert.Equal(800, viewport.PanBy(9999, 9999).OffsetY, Tolerance);
    }

    [Fact]
    public void PanningLeftAndUp_MovesTheOppositeWayFromPanningRightAndDown()
    {
        // Written because the first implementation ran the delta through
        // the positive-dimension sanitiser, which silently turned every
        // negative pan into a one-pixel pan the other way.
        // Parked in the middle of the scrollable range, so neither
        // direction is clamped and the sign is the only thing under test.
        var start = DocumentViewport.Create(1000, 1000, 200, 200).ActualSize();
        Assert.InRange(start.OffsetX, 100, 700);
        Assert.InRange(start.OffsetY, 100, 700);

        Assert.Equal(start.OffsetX - 100, start.PanBy(-100, 0).OffsetX, Tolerance);
        Assert.Equal(start.OffsetX + 100, start.PanBy(100, 0).OffsetX, Tolerance);
        Assert.Equal(start.OffsetY - 100, start.PanBy(0, -100).OffsetY, Tolerance);
        Assert.Equal(start.OffsetX, start.PanBy(0, 0).OffsetX, Tolerance);
    }

    [Fact]
    public void ZoomingAboutAPoint_KeepsThatPointUnderThePointer()
    {
        // What makes wheel-zoom feel like a magnifier rather than a slider.
        var viewport = DocumentViewport.Create(1000, 1000, 400, 400).ActualSize();

        const double anchorX = 120;
        const double anchorY = 260;
        var contentXBefore = (viewport.OffsetX + anchorX) / viewport.Zoom;
        var contentYBefore = (viewport.OffsetY + anchorY) / viewport.Zoom;

        var zoomed = viewport.ZoomAbout(viewport.Zoom * 2, anchorX, anchorY);

        var contentXAfter = (zoomed.OffsetX + anchorX) / zoomed.Zoom;
        var contentYAfter = (zoomed.OffsetY + anchorY) / zoomed.Zoom;

        Assert.Equal(contentXBefore, contentXAfter, 0.001);
        Assert.Equal(contentYBefore, contentYAfter, 0.001);
    }

    [Fact]
    public void ZoomIsClamped_AtBothEnds()
    {
        var viewport = DocumentViewport.Create(1000, 1000, 400, 400);

        Assert.Equal(DocumentViewport.MaxZoom, viewport.ZoomTo(1e9).Zoom, Tolerance);
        Assert.Equal(DocumentViewport.MinZoom, viewport.ZoomTo(1e-9).Zoom, Tolerance);
    }

    [Fact]
    public void ZoomInThenZoomOut_ReturnsToWhereItStarted()
    {
        var viewport = DocumentViewport.Create(1000, 800, 400, 400).ActualSize();

        var round = viewport.ZoomIn().ZoomOut();

        Assert.Equal(viewport.Zoom, round.Zoom, Tolerance);
    }

    [Fact]
    public void ActualSize_IsOneToOneWithTheDocumentsOwnUnits()
    {
        var viewport = DocumentViewport.Create(1000, 500, 400, 400).ActualSize();

        Assert.Equal(1.0, viewport.Zoom, Tolerance);
        Assert.Equal(1000, viewport.RenderedWidth, Tolerance);
    }

    [Fact]
    public void ResizingTheWindow_KeepsAFittedViewFitted()
    {
        var viewport = DocumentViewport.Create(1000, 500, 400, 400);
        Assert.True(viewport.IsFitted);

        var resized = viewport.WithViewportSize(800, 800);

        Assert.True(resized.IsFitted);
        Assert.Equal(0.8, resized.Zoom, Tolerance);
    }

    [Fact]
    public void ResizingTheWindow_KeepsAZoomedViewWhereTheUserPutIt()
    {
        // A resize is not a user action on the document. Re-fitting here
        // would throw away the detail they had zoomed into every time the
        // window moved.
        var viewport = DocumentViewport.Create(1000, 500, 400, 400).ZoomTo(4.0);

        var resized = viewport.WithViewportSize(600, 600);

        Assert.Equal(4.0, resized.Zoom, Tolerance);
        Assert.False(resized.IsFitted);
    }

    [Fact]
    public void IsScrollable_IsTrueOnlyWhenTheContentOverflowsTheView()
    {
        Assert.False(DocumentViewport.Create(1000, 500, 400, 400).IsScrollable);
        Assert.True(DocumentViewport.Create(1000, 500, 400, 400).ActualSize().IsScrollable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ADegenerateSize_DoesNotProduceNaN(double bad)
    {
        // A control that has not been laid out reports zero bounds, and a
        // malformed document can report a zero-sized page. Both reach this
        // type routinely, and a NaN here propagates into the render.
        var viewport = DocumentViewport.Create(bad, bad, bad, bad);

        Assert.True(double.IsFinite(viewport.Zoom));
        Assert.True(double.IsFinite(viewport.OffsetX));
        Assert.True(double.IsFinite(viewport.OffsetY));
        Assert.True(double.IsFinite(viewport.RenderedWidth));
    }

    [Fact]
    public void ANonFiniteZoomOrPan_IsIgnoredRatherThanPropagated()
    {
        var viewport = DocumentViewport.Create(1000, 1000, 400, 400).ActualSize();

        Assert.True(double.IsFinite(viewport.ZoomTo(double.NaN).Zoom));
        Assert.Equal(viewport.OffsetX, viewport.PanBy(double.NaN, 0).OffsetX, Tolerance);
    }

    [Fact]
    public void ChangingPageSize_RefitsForTheNewPage()
    {
        // Turning to a landscape page in a portrait document has to re-fit;
        // keeping the old zoom would show a fragment of the new page.
        var viewport = DocumentViewport.Create(600, 800, 400, 400).ZoomTo(3.0);

        var turned = viewport.WithContentSize(1600, 400);

        Assert.True(turned.IsFitted);
        Assert.Equal(0.25, turned.Zoom, Tolerance);
    }

    [Fact]
    public void EveryOperation_ReturnsANewViewport_LeavingTheOriginalUntouched()
    {
        var viewport = DocumentViewport.Create(1000, 1000, 400, 400);
        var zoom = viewport.Zoom;

        viewport.ZoomIn();
        viewport.PanBy(100, 100);
        viewport.ActualSize();

        Assert.Equal(zoom, viewport.Zoom, Tolerance);
    }
}
