namespace Tempest.Desktop.Viewing;

/// <summary>
/// Which tiles of a page are needed to cover a viewport, pure arithmetic
/// with no rendering, caching or Avalonia type in the process (`TD-101`) —
/// the same discipline <c>DocumentViewport</c> already applies to zoom/pan/
/// fit, so "which tiles does this viewport need" is answered in one place
/// that is trivial to test at any zoom.
/// </summary>
public static class TileGrid
{
    /// <summary>
    /// The pixel size of one tile's own edge (`TD-101`) — 512, the common
    /// middle ground this platform's own tiled rendering settled on: large
    /// enough that a full A0 sheet at 400% zoom needs a few dozen tiles
    /// rather than thousands (keeping per-tile PDFium call overhead low),
    /// small enough that one tile comfortably fits <see cref="TileCache.DefaultBudgetBytes"/>'s
    /// own budget many times over (a 512×512 BGRA8888 tile is exactly 1 MiB).
    /// </summary>
    public const int TileSize = 512;

    /// <summary>
    /// Every (column, row) tile covering the visible window
    /// (<paramref name="viewportOffsetX"/>, <paramref name="viewportOffsetY"/>,
    /// <paramref name="viewportWidth"/>, <paramref name="viewportHeight"/>)
    /// — in rendered pixels, the same coordinate space <c>DocumentViewport</c>'s
    /// own <c>OffsetX</c>/<c>OffsetY</c> use — into a page
    /// <paramref name="contentWidth"/> by <paramref name="contentHeight"/>
    /// content units rasterised at <paramref name="scale"/>, expanded by
    /// <paramref name="ringSize"/> extra tiles on every side so a small pan
    /// finds its next tile already cached rather than needing one fetched
    /// the instant the visible window's own edge reaches it.
    /// </summary>
    /// <returns>
    /// Tile coordinates in row-major order, column ascending within each
    /// row — deterministic, so two calls with identical inputs return an
    /// identical list. Empty for any non-positive dimension or scale.
    /// </returns>
    public static IReadOnlyList<(int Column, int Row)> VisibleTiles(
        double contentWidth,
        double contentHeight,
        double scale,
        double viewportOffsetX,
        double viewportOffsetY,
        double viewportWidth,
        double viewportHeight,
        int ringSize = 1)
    {
        if (contentWidth <= 0 || contentHeight <= 0 || scale <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
            return [];

        var renderedWidth = contentWidth * scale;
        var renderedHeight = contentHeight * scale;

        // The visible window, clamped to the page's own rendered bounds —
        // a viewport that overhangs the page's edge (the common case for
        // any page smaller than the window) must not ask for tiles beyond
        // the page's own last row/column.
        var visibleLeft = Math.Clamp(viewportOffsetX, 0, renderedWidth);
        var visibleTop = Math.Clamp(viewportOffsetY, 0, renderedHeight);
        var visibleRight = Math.Clamp(viewportOffsetX + viewportWidth, 0, renderedWidth);
        var visibleBottom = Math.Clamp(viewportOffsetY + viewportHeight, 0, renderedHeight);

        var maxColumn = Math.Max(0, (int)Math.Ceiling(renderedWidth / TileSize) - 1);
        var maxRow = Math.Max(0, (int)Math.Ceiling(renderedHeight / TileSize) - 1);

        // A visible span of zero width/height (the viewport's own edge
        // lands exactly on the page's) still names the one tile it sits
        // in, rather than an empty range — the tiny epsilon on the "last"
        // edge keeps that column/row inside the floor rather than one past
        // it.
        var firstColumn = Math.Max(0, (int)Math.Floor(visibleLeft / TileSize) - ringSize);
        var lastColumn = Math.Min(maxColumn, (int)Math.Floor(Math.Max(visibleLeft, visibleRight - 0.0001) / TileSize) + ringSize);
        var firstRow = Math.Max(0, (int)Math.Floor(visibleTop / TileSize) - ringSize);
        var lastRow = Math.Min(maxRow, (int)Math.Floor(Math.Max(visibleTop, visibleBottom - 0.0001) / TileSize) + ringSize);

        var tiles = new List<(int Column, int Row)>();
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = firstColumn; column <= lastColumn; column++)
                tiles.Add((column, row));
        }

        return tiles;
    }

    /// <summary>
    /// The native-content-space rectangle tile (<paramref name="column"/>, <paramref name="row"/>)
    /// covers, at <paramref name="scale"/> — tile (0, 0)'s own top-left is
    /// the page's own (0, 0); a tile at the page's right or bottom edge is
    /// narrower or shorter than <see cref="TileSize"/>/<paramref name="scale"/>,
    /// clipped to the page's real extent, never padded past it.
    /// </summary>
    public static (double X, double Y, double Width, double Height) TileContentRect(
        double contentWidth, double contentHeight, double scale, int column, int row)
    {
        var nativeTileEdge = TileSize / scale;
        var x = column * nativeTileEdge;
        var y = row * nativeTileEdge;
        var width = Math.Max(0, Math.Min(nativeTileEdge, contentWidth - x));
        var height = Math.Max(0, Math.Min(nativeTileEdge, contentHeight - y));
        return (x, y, width, height);
    }
}
