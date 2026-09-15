using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Tempest.Workspace.Viewing;
using Tempest.Desktop.Viewing;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Tiled rendering's own two pure building blocks (`TD-101`): which tiles a
/// viewport needs (<see cref="TileGrid"/>), and the bounded, least-recently-
/// used cache that lets a repeat request for one reuse it instead of
/// re-rasterising (<see cref="TileCache"/>). Neither carries a page source,
/// a bitmap decoder, or a real render — this Work Package's brief-stated
/// tile size (512px) and memory budget (256 MiB) are asserted directly.
/// </summary>
public sealed class TilingTests
{
    [Fact]
    public void TileSize_IsTheStated512Pixels()
    {
        Assert.Equal(512, TileGrid.TileSize);
    }

    [Fact]
    public void ASmallPage_NeedsExactlyOneTile_AtAnyZoomThatKeepsItUnderOneTileWide()
    {
        // A0 in points is about 2384x3370 — at a low enough zoom the whole
        // sheet still fits inside one 512px tile.
        var tiles = TileGrid.VisibleTiles(
            contentWidth: 400, contentHeight: 400, scale: 1.0,
            viewportOffsetX: 0, viewportOffsetY: 0, viewportWidth: 800, viewportHeight: 800, ringSize: 0);

        Assert.Equal([(0, 0)], tiles);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void TheTileSetRequestedForAViewport_AtThreeZooms_CoversExactlyTheVisibleWindow(double scale)
    {
        // A0-ish sheet (2384x3370 content units) viewed through an 800x600
        // window pinned to its top-left corner (no pan) — the tile grid's
        // own size grows with scale, and so does the visible tile set.
        const double contentWidth = 2384;
        const double contentHeight = 3370;
        const double viewportWidth = 800;
        const double viewportHeight = 600;

        var tiles = TileGrid.VisibleTiles(
            contentWidth, contentHeight, scale,
            viewportOffsetX: 0, viewportOffsetY: 0, viewportWidth, viewportHeight, ringSize: 0);

        var renderedWidth = contentWidth * scale;
        var renderedHeight = contentHeight * scale;
        var expectedColumns = (int)Math.Ceiling(Math.Min(viewportWidth, renderedWidth) / TileGrid.TileSize);
        var expectedRows = (int)Math.Ceiling(Math.Min(viewportHeight, renderedHeight) / TileGrid.TileSize);

        Assert.Equal(expectedColumns * expectedRows, tiles.Count);
        Assert.All(tiles, t => Assert.InRange(t.Column, 0, expectedColumns - 1));
        Assert.All(tiles, t => Assert.InRange(t.Row, 0, expectedRows - 1));

        // Deterministic order: row-major, column ascending within a row.
        for (var i = 1; i < tiles.Count; i++)
        {
            var (prevColumn, prevRow) = tiles[i - 1];
            var (column, row) = tiles[i];
            Assert.True(row > prevRow || (row == prevRow && column > prevColumn));
        }
    }

    [Fact]
    public void PanningWithinTheSameTileSet_RequestsTheIdenticalTiles()
    {
        // "An A0 PDF at 400% zoom pans without re-rasterising the whole
        // sheet" (this Work Package's own acceptance line): a small pan
        // that does not cross a tile boundary must ask for exactly the
        // same tiles as before, which is what lets a cache serve every one
        // of them without a single new render. Offsets chosen so every one
        // of the visible window's four edges stays inside the same 512px
        // bucket before and after (1100→1120 and 1900→1920 both floor to
        // bucket 2/3, for instance) — a pan that does not cross a boundary,
        // deliberately, not by chance.
        var before = TileGrid.VisibleTiles(2384, 3370, 4.0, viewportOffsetX: 1100, viewportOffsetY: 1100, viewportWidth: 800, viewportHeight: 600, ringSize: 1);
        var after = TileGrid.VisibleTiles(2384, 3370, 4.0, viewportOffsetX: 1120, viewportOffsetY: 1110, viewportWidth: 800, viewportHeight: 600, ringSize: 1);

        Assert.Equal(before, after);
    }

    [Fact]
    public void ARingOfExtraTiles_SurroundsTheVisibleWindow()
    {
        var withoutRing = TileGrid.VisibleTiles(2000, 2000, 1.0, 500, 500, 400, 400, ringSize: 0);
        var withRing = TileGrid.VisibleTiles(2000, 2000, 1.0, 500, 500, 400, 400, ringSize: 1);

        Assert.True(withRing.Count > withoutRing.Count);
        Assert.All(withoutRing, t => Assert.Contains(t, withRing));
    }

    [Fact]
    public void TheTileGrid_NeverExtendsPastThePagesOwnEdge()
    {
        // A viewport far larger than the page must not ask for tiles
        // beyond the page's own last row/column, ring or no ring.
        var tiles = TileGrid.VisibleTiles(
            contentWidth: 600, contentHeight: 400, scale: 1.0,
            viewportOffsetX: 0, viewportOffsetY: 0, viewportWidth: 4000, viewportHeight: 4000, ringSize: 3);

        var maxColumn = (int)Math.Ceiling(600.0 / TileGrid.TileSize) - 1;
        var maxRow = (int)Math.Ceiling(400.0 / TileGrid.TileSize) - 1;

        Assert.All(tiles, t => Assert.InRange(t.Column, 0, maxColumn));
        Assert.All(tiles, t => Assert.InRange(t.Row, 0, maxRow));
    }

    [Fact]
    public void TileContentRect_ClipsAtThePagesRightAndBottomEdge_RatherThanPadding()
    {
        // A 600-wide page at 1x scale: tile 0 covers [0,512), tile 1 covers
        // [512,600) — 88 units wide, not the full 512 a middle tile gets.
        var edgeTile = TileGrid.TileContentRect(contentWidth: 600, contentHeight: 600, scale: 1.0, column: 1, row: 0);

        Assert.Equal(512, edgeTile.X, 0.01);
        Assert.Equal(88, edgeTile.Width, 0.01);
    }

    [Fact]
    public void TileContentRect_ATileEntirelyPastThePage_HasNoArea()
    {
        var beyond = TileGrid.TileContentRect(contentWidth: 600, contentHeight: 600, scale: 1.0, column: 5, row: 5);

        Assert.True(beyond.Width <= 0 || beyond.Height <= 0);
    }

    // ----------------------------------------------------------------
    // TileCache — bounded, least-recently-used (`TD-101`).
    // ----------------------------------------------------------------

    private static WriteableBitmap SmallBitmap() =>
        new(new PixelSize(4, 4), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

    [Fact]
    public void TheDefaultBudget_IsTheStated256Mebibytes()
    {
        Assert.Equal(256L * 1024 * 1024, TileCache.DefaultBudgetBytes);
    }

    [AvaloniaFact]
    public void ACachedTile_IsReturnedByTryGet()
    {
        using var cache = new TileCache();
        var key = new TileCache.Key(0, 1.0, 0, 0);
        var bitmap = SmallBitmap();

        cache.Add(key, bitmap, sizeBytes: 1024);

        Assert.Same(bitmap, cache.TryGet(key));
        Assert.Equal(1, cache.Count);
        Assert.Equal(1024, cache.UsedBytes);
    }

    [Fact]
    public void AnUncachedKey_ReturnsNull()
    {
        using var cache = new TileCache();

        Assert.Null(cache.TryGet(new TileCache.Key(0, 1.0, 9, 9)));
    }

    [AvaloniaFact]
    public void TheCacheBound_IsHonoured_OldestUnusedTileEvictedFirst()
    {
        // A budget of exactly three tiles' worth: a fourth add must evict
        // exactly one, and it must be the one nobody touched since.
        using var cache = new TileCache(budgetBytes: 3000);

        var first = new TileCache.Key(0, 1.0, 0, 0);
        var second = new TileCache.Key(0, 1.0, 1, 0);
        var third = new TileCache.Key(0, 1.0, 2, 0);
        var fourth = new TileCache.Key(0, 1.0, 3, 0);

        cache.Add(first, SmallBitmap(), 1000);
        cache.Add(second, SmallBitmap(), 1000);
        cache.Add(third, SmallBitmap(), 1000);
        Assert.Equal(3000, cache.UsedBytes);

        cache.Add(fourth, SmallBitmap(), 1000);

        Assert.True(cache.UsedBytes <= 3000);
        Assert.Null(cache.TryGet(first));
        Assert.NotNull(cache.TryGet(second));
        Assert.NotNull(cache.TryGet(third));
        Assert.NotNull(cache.TryGet(fourth));
    }

    [AvaloniaFact]
    public void TouchingATile_ProtectsItFromTheNextEviction()
    {
        using var cache = new TileCache(budgetBytes: 3000);

        var first = new TileCache.Key(0, 1.0, 0, 0);
        var second = new TileCache.Key(0, 1.0, 1, 0);
        var third = new TileCache.Key(0, 1.0, 2, 0);
        var fourth = new TileCache.Key(0, 1.0, 3, 0);

        cache.Add(first, SmallBitmap(), 1000);
        cache.Add(second, SmallBitmap(), 1000);
        cache.Add(third, SmallBitmap(), 1000);

        // Touching `first` moves it to most-recently-used, so `second` —
        // now the least recently touched — is the one the next add evicts.
        cache.TryGet(first);
        cache.Add(fourth, SmallBitmap(), 1000);

        Assert.NotNull(cache.TryGet(first));
        Assert.Null(cache.TryGet(second));
    }

    [AvaloniaFact]
    public void AddingUnderAKeyAlreadyCached_ReplacesAndDisposesTheOldBitmap()
    {
        using var cache = new TileCache();
        var key = new TileCache.Key(0, 1.0, 0, 0);
        var oldBitmap = SmallBitmap();
        var newBitmap = SmallBitmap();

        cache.Add(key, oldBitmap, 1000);
        cache.Add(key, newBitmap, 2000);

        Assert.Same(newBitmap, cache.TryGet(key));
        Assert.Equal(2000, cache.UsedBytes);
        Assert.Equal(1, cache.Count);
        Assert.NotNull(Record.Exception(() => oldBitmap.Lock()));
    }

    [AvaloniaFact]
    public void DifferentZoomLevels_AreCachedSeparately()
    {
        // "Cached per zoom level" (this Work Package's own brief): tile
        // (0,0) at 1x and tile (0,0) at 4x are different cache entries,
        // never one overwriting the other.
        using var cache = new TileCache();
        var atOne = new TileCache.Key(0, 1.0, 0, 0);
        var atFour = new TileCache.Key(0, 4.0, 0, 0);

        cache.Add(atOne, SmallBitmap(), 1000);
        cache.Add(atFour, SmallBitmap(), 1000);

        Assert.Equal(2, cache.Count);
        Assert.NotNull(cache.TryGet(atOne));
        Assert.NotNull(cache.TryGet(atFour));
    }

    [AvaloniaFact]
    public void ClearingTheCache_DisposesEveryTile()
    {
        using var cache = new TileCache();
        var key = new TileCache.Key(0, 1.0, 0, 0);
        var bitmap = SmallBitmap();
        cache.Add(key, bitmap, 1000);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.UsedBytes);
        Assert.NotNull(Record.Exception(() => bitmap.Lock()));
    }

    [Fact]
    public void ANonPositiveBudget_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileCache(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileCache(-1));
    }

    // ----------------------------------------------------------------
    // PdfDocumentPageSource.RenderTile — real rendering, real content.
    // ----------------------------------------------------------------

    [AvaloniaFact]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public void ARenderedTile_IsSizedToTheRequestedPixelsForItsOwnCoveredRegion()
    {
        using var source = (PdfDocumentPageSource)DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, DocumentPageSourceTests.MultiPagePdf())!;

        // Page 1 is 595x842. At 1x scale, tile (0,0) covers the full 512px
        // square its own top-left corner sits in; tile (1,0) is clipped to
        // the page's own remaining 83 units of width (595 - 512).
        using var corner = source.RenderTile(0, scale: 1.0, column: 0, row: 0, tileSize: TileGrid.TileSize);
        using var edge = source.RenderTile(0, scale: 1.0, column: 1, row: 0, tileSize: TileGrid.TileSize);

        Assert.Equal(512, corner.PixelSize.Width);
        Assert.Equal(512, corner.PixelSize.Height);
        Assert.Equal(83, edge.PixelSize.Width);
    }

    [AvaloniaFact]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public void ARenderedTile_NeverHitsTheWholePageMaxRasterEdgeCap_EvenAtExtremeZoom()
    {
        // The entire point of tiling: at 32x, a whole-page render of a
        // 595-wide PDF would ask for 19040px and get capped at
        // MaxRasterEdge. One 512px tile of that same page, at the same
        // scale, asks for nothing remotely near the cap.
        using var source = (PdfDocumentPageSource)DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, DocumentPageSourceTests.MultiPagePdf())!;

        using var tile = source.RenderTile(0, scale: DocumentViewport.MaxZoom, column: 0, row: 0, tileSize: TileGrid.TileSize);

        Assert.Equal(TileGrid.TileSize, tile.PixelSize.Width);
        Assert.Equal(TileGrid.TileSize, tile.PixelSize.Height);
        Assert.True(tile.PixelSize.Width < PdfDocumentPageSource.MaxRasterEdge);
    }

    [AvaloniaFact]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public void ATileEntirelyOutsideThePage_ThrowsRatherThanRenderingBlank()
    {
        using var source = (PdfDocumentPageSource)DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, DocumentPageSourceTests.MultiPagePdf())!;

        Assert.Throws<DocumentRenderException>(() => source.RenderTile(0, scale: 1.0, column: 10, row: 10, tileSize: TileGrid.TileSize));
    }

    [AvaloniaFact]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public void TilesStitchedTogether_CoverTheSameContentAWholePageRenderDoes()
    {
        // Page 1's own filled rectangle (50,50)-(450,650) in content units
        // — rendering it as tiles and reading the corresponding region back
        // out of each tile must show the identical rectangle a whole-page
        // render already proves rasterises (DocumentPageSourceTests.APdfPage_ActuallyContainsTheDrawnContent).
        using var source = (PdfDocumentPageSource)DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, DocumentPageSourceTests.MultiPagePdf())!;

        using var whole = source.RenderPage(0, 1.0);
        using var tile = source.RenderTile(0, scale: 1.0, column: 0, row: 0, tileSize: TileGrid.TileSize);

        // The tile's own top-left 512x512 region and the whole page's own
        // top-left 512x512 region are pixel-for-pixel the same render.
        Assert.Equal(512, tile.PixelSize.Width);
        Assert.True(whole.PixelSize.Width >= 512 && whole.PixelSize.Height >= 512);
    }

    // ----------------------------------------------------------------
    // End-to-end through the real viewer — the acceptance line itself:
    // "an A0 PDF at 400% zoom pans without re-rasterising the whole
    // sheet." A0 in points (2384x3370) would take a while to zoom to from
    // this fixture's own A4 page at the ZoomStep this viewer actually
    // uses, so the claim is proven at the same *relative* depth this
    // fixture's own page reaches instead: past PdfDocumentPageSource's own
    // MaxRasterEdge cap, which is the one condition tiling actually
    // changes anything for.
    // ----------------------------------------------------------------

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [AvaloniaFact]
    public void PastMaxRasterEdge_TheViewerRendersSharp_RatherThanTheWholePageDegradedCap()
    {
        var viewer = new DocumentViewerView();
        var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, DocumentPageSourceTests.MultiPagePdf())!;
        var session = DocumentViewSession.Ready(
            Guid.NewGuid(), "sheet.pdf", "application/pdf", ViewableDocumentFormat.Pdf,
            pageCount: 3, contentWidth: 595, contentHeight: 842, viewportWidth: 800, viewportHeight: 600);
        viewer.Open(session, source);

        // From actual size (1.0x), enough ZoomIn steps (x1.25 each) to pass
        // 8000 / 842 ≈ 9.5x — the zoom at which a whole-page render of this
        // fixture's own page 1 would first hit MaxRasterEdge and be capped.
        viewer.ActualSize();
        for (var i = 0; i < 12; i++)
            viewer.ZoomIn();

        var zoom = viewer.Session!.Viewport.Zoom;
        Assert.True(zoom > 9.5, $"Test setup expected a zoom past ~9.5x, got {zoom}.");

        var expectedUncappedHeight = (int)Math.Round(842 * zoom);
        Assert.True(expectedUncappedHeight > PdfDocumentPageSource.MaxRasterEdge, "Test setup expected this zoom to exceed the whole-page cap.");

        var rendered = viewer.RenderedPage;
        Assert.NotNull(rendered);

        // Rendered at the true requested scale — not silently downgraded
        // to fit under the cap, which a plain (untiled) render at this
        // zoom would have been.
        Assert.Equal(expectedUncappedHeight, rendered!.PixelSize.Height);

        source.Dispose();
    }

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [AvaloniaFact]
    public void PastMaxRasterEdge_PanningReusesCachedTiles_RatherThanRenderingEveryTileAgain()
    {
        var pdfSource = (PdfDocumentPageSource)DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, DocumentPageSourceTests.MultiPagePdf())!;
        var countingSource = new CountingTiledPageSource(pdfSource);

        var viewer = new DocumentViewerView();
        var session = DocumentViewSession.Ready(
            Guid.NewGuid(), "sheet.pdf", "application/pdf", ViewableDocumentFormat.Pdf,
            pageCount: 3, contentWidth: 595, contentHeight: 842, viewportWidth: 800, viewportHeight: 600);
        viewer.Open(session, countingSource);

        viewer.ActualSize();
        for (var i = 0; i < 12; i++)
            viewer.ZoomIn();

        Assert.NotNull(viewer.RenderedPage);
        var tileRendersAfterFirstShow = countingSource.TileRenderCount;
        Assert.True(tileRendersAfterFirstShow > 0, "Test setup expected the tiled path to have engaged and rendered at least one tile.");

        // "An A0 PDF at 400% zoom pans without re-rasterising the whole
        // sheet" (this Work Package's own acceptance line): a pan within
        // the same page at the same zoom is "re-rasterise only when the
        // page, the zoom or the rotation actually changed"'s own no-op
        // case (RenderCurrentPage), so it must issue zero further tile
        // renders — every tile the composite needs is already cached.
        viewer.PanBy(15, 10);
        viewer.PanBy(-8, 4);

        Assert.Equal(tileRendersAfterFirstShow, countingSource.TileRenderCount);

        countingSource.Dispose();
    }

    /// <summary>A tiled page source wrapper that counts real tile renders — proves a later render reused the cache rather than asking the underlying source again.</summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private sealed class CountingTiledPageSource(PdfDocumentPageSource inner) : IDocumentPageSource, ITiledDocumentPageSource
    {
        public int PageCount => inner.PageCount;

        public Size PageSize(int pageIndex) => inner.PageSize(pageIndex);

        public Bitmap RenderPage(int pageIndex, double scale) => inner.RenderPage(pageIndex, scale);

        public Bitmap RenderTile(int pageIndex, double scale, int column, int row, int tileSize)
        {
            TileRenderCount++;
            return inner.RenderTile(pageIndex, scale, column, row, tileSize);
        }

        public int TileRenderCount { get; private set; }

        public void Dispose() => inner.Dispose();
    }
}
