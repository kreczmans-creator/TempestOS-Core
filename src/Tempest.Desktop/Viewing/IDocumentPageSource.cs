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
