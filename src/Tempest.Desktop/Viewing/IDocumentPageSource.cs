using Avalonia;
using Avalonia.Media.Imaging;

namespace Tempest.Desktop.Viewing;

/// <summary>
/// A loaded document the viewer can ask for pages (`TD-80`).
/// </summary>
/// <remarks>
/// <para>
/// The seam between "what a format is" and "how the viewer behaves". The
/// viewer knows page counts, page sizes and how to draw a bitmap; it knows
/// nothing about PDF, PNG or text. Adding a format is a new implementation
/// of this interface and a line in
/// <see cref="DocumentPageSourceFactory"/> — no change to the viewport
/// maths, the page navigation, the control, or the workspace integration.
/// </para>
/// <para>
/// Page indices here are <b>zero-based</b>, deliberately unlike
/// <c>DocumentViewSession.CurrentPage</c>, which is one-based because it
/// is what the user reads. The conversion happens once, at the call site
/// that renders, rather than being smeared across both models where the
/// two conventions could quietly drift into each other.
/// </para>
/// </remarks>
public interface IDocumentPageSource : IDisposable
{
    /// <summary>How many pages the document has. Always at least 1.</summary>
    int PageCount { get; }

    /// <summary>
    /// The natural size of <paramref name="pageIndex"/> in the document's
    /// own units — points for a PDF, pixels for an image.
    /// </summary>
    Size PageSize(int pageIndex);

    /// <summary>
    /// Renders <paramref name="pageIndex"/> at <paramref name="scale"/>
    /// times its natural size.
    /// </summary>
    /// <remarks>
    /// Rendering at the requested scale rather than rendering once and
    /// scaling the bitmap is what makes zooming into a drawing show more
    /// detail instead of larger pixels — the whole reason a vector format
    /// is worth rasterising on demand.
    /// </remarks>
    Bitmap RenderPage(int pageIndex, double scale);
}

/// <summary>
/// A page source that can also rasterise one tile of a page directly,
/// rather than only the whole page at once (`TD-101`) — implemented by
/// <see cref="PdfDocumentPageSource"/>, the one format a real engineering
/// drawing (an A0 sheet at deep zoom) makes this worth having: a whole-page
/// render at a high enough zoom hits <see cref="PdfDocumentPageSource.MaxRasterEdge"/>
/// and is capped, degrading sharpness; rendering the page as a grid of
/// tiles instead lets each tile reach the full requested scale with no cap,
/// and lets a viewer cache tiles it has already rasterised rather than
/// asking the underlying renderer again for a region it already has.
/// </summary>
public interface ITiledDocumentPageSource
{
    /// <summary>
    /// Renders one <paramref name="tileSize"/>-by-<paramref name="tileSize"/>-pixel
    /// tile of <paramref name="pageIndex"/> at <paramref name="scale"/> —
    /// the tile at grid position (<paramref name="column"/>, <paramref name="row"/>),
    /// where tile (0, 0) covers the page's own top-left
    /// <paramref name="tileSize"/>/<paramref name="scale"/> content units in
    /// each direction. A tile at the page's own right or bottom edge is
    /// narrower or shorter than <paramref name="tileSize"/> pixels — never
    /// padded — so the caller always knows a page's real extent from the
    /// tiles it actually receives.
    /// </summary>
    /// <exception cref="DocumentRenderException">The tile could not be rendered, or lies entirely outside the page.</exception>
    Bitmap RenderTile(int pageIndex, double scale, int column, int row, int tileSize);
}
