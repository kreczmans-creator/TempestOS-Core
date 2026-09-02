using System.Text;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Tempest.App.Workspace.Viewing;
using Tempest.Desktop.Viewing;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The renderers behind the viewer (`TD-80`) — that a PDF really is
/// rasterised, an image really is decoded, and text really is paginated.
/// </summary>
/// <remarks>
/// These assert pixels and page counts, not that a method returned
/// something. A viewer that produced a blank bitmap of the right size
/// would satisfy every weaker assertion and show the user nothing.
/// </remarks>
public class DocumentPageSourceTests
{
    /// <summary>
    /// A real PDF whose pages carry drawn vector content, in three
    /// different page sizes.
    /// </summary>
    /// <remarks>
    /// Vector content specifically: a page containing only text could be
    /// "rendered" by a text extractor, and the drawings mock-ups 2 and 3
    /// are about are paths. A filled red rectangle proves a genuine
    /// rasteriser ran.
    /// </remarks>
    internal static byte[] MultiPagePdf()
    {
        var pdf = new StringBuilder();
        pdf.Append("%PDF-1.4\n");
        pdf.Append("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n");
        pdf.Append("2 0 obj<</Type/Pages/Kids[3 0 R 5 0 R 7 0 R]/Count 3>>endobj\n");

        // Page 1: A4 portrait, 595x842.
        pdf.Append("3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]/Contents 4 0 R>>endobj\n");
        pdf.Append("4 0 obj<</Length 44>>stream\n1 0 0 rg 50 50 400 600 re f\nendstream\nendobj\n");

        // Page 2: landscape, 842x595 — a different size, so page turning
        // must re-fit rather than keep the previous page's zoom.
        pdf.Append("5 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 842 595]/Contents 6 0 R>>endobj\n");
        pdf.Append("6 0 obj<</Length 44>>stream\n0 0 1 rg 60 60 600 400 re f\nendstream\nendobj\n");

        // Page 3: small square.
        pdf.Append("7 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 8 0 R>>endobj\n");
        pdf.Append("8 0 obj<</Length 42>>stream\n0 1 0 rg 20 20 160 160 re f\nendstream\nendobj\n");

        pdf.Append("trailer<</Root 1 0 R>>\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>A real, byte-for-byte valid 4x3 red PNG.</summary>
    /// <remarks>
    /// Written out literally rather than produced by rendering and saving
    /// a bitmap: under the headless platform these tests run on,
    /// <c>RenderTargetBitmap.Save</c> writes zero bytes, so the "PNG" a
    /// generated helper produced was an empty array — and every assertion
    /// about decoding it was really an assertion about nothing. Found by
    /// probing what the helper actually returned.
    /// </remarks>
    internal static byte[] Png() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x03,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x3B, 0x96, 0x39, 0x91,
        0x00, 0x00, 0x00, 0x10, 0x49, 0x44, 0x41, 0x54,
        0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0x00, 0x47,
        0x0C, 0x38, 0x39, 0x00, 0xF5, 0x31, 0x0B, 0xF5, 0x35, 0x7B, 0xFB, 0x82,
        0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    ];

    /// <summary>
    /// Whether the platform these tests run on decodes images for real.
    /// </summary>
    /// <remarks>
    /// The headless platform substitutes a stub decoder that reports every
    /// image as 1x1 and accepts bytes that are not an image at all. That
    /// is a property of the test platform, not of
    /// <see cref="ImageDocumentPageSource"/>, and asserting real
    /// dimensions against it would be asserting a falsehood. Probed with a
    /// PNG of known size rather than assumed from the platform name, and
    /// the image assertions below say plainly which half of this they are
    /// in. Real decoding is exercised on a real platform, and is
    /// disclosed as untested here rather than quietly claimed.
    /// </remarks>
    private static bool ImageDecodingIsReal()
    {
        using var stream = new MemoryStream(Png(), writable: false);
        using var bitmap = new Bitmap(stream);
        return bitmap.PixelSize.Width == 4 && bitmap.PixelSize.Height == 3;
    }

    /// <summary>
    /// How many pixels of <paramref name="bitmap"/> are not white.
    /// </summary>
    /// <remarks>
    /// Read straight out of the bitmap with <c>CopyPixels</c>. Any route
    /// that re-encodes and decodes would be testing the codec rather than
    /// the renderer, and a blank page would still pass.
    /// </remarks>
    private static int CountNonWhitePixels(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];

        if (bitmap is WriteableBitmap writeable)
        {
            // The PDF renderer hands back a WriteableBitmap, whose pixels
            // are read by locking it — CopyPixels is not supported for it.
            using var locked = writeable.Lock();
            for (var row = 0; row < size.Height; row++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    locked.Address + (row * locked.RowBytes), pixels, row * stride, stride);
            }
        }
        else
        {
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), pixels.Length, stride);
            }
            finally
            {
                handle.Free();
            }
        }

        var count = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i] < 250 || pixels[i + 1] < 250 || pixels[i + 2] < 250)
                count++;
        }

        return count;
    }

    [AvaloniaFact]
    public void APdf_ReportsItsRealPageCount()
    {
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, MultiPagePdf())!;

        Assert.Equal(3, source.PageCount);
    }

    [AvaloniaFact]
    public void APdf_ReportsEachPagesOwnSize()
    {
        // Pages of different sizes in one document is normal — a drawing
        // sheet bound with a portrait cover — and a viewer that assumes
        // one size crops or letterboxes every page but the first.
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, MultiPagePdf())!;

        Assert.Equal(new Size(595, 842), source.PageSize(0));
        Assert.Equal(new Size(842, 595), source.PageSize(1));
        Assert.Equal(new Size(200, 200), source.PageSize(2));
    }

    [AvaloniaFact]
    public void APdfPage_RasterisesAtTheRequestedScale()
    {
        // Rendering at the zoom rather than scaling one fixed render is
        // what makes zooming into a drawing reveal more of the drawing.
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, MultiPagePdf())!;

        using var atOne = source.RenderPage(0, 1.0);
        using var atThree = source.RenderPage(0, 3.0);

        Assert.Equal(595, atOne.PixelSize.Width);
        Assert.Equal(1785, atThree.PixelSize.Width);
    }

    [AvaloniaFact]
    public void APdfPage_ActuallyContainsTheDrawnContent()
    {
        // The assertion that separates a real rasteriser from a blank
        // bitmap of the correct dimensions.
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, MultiPagePdf())!;

        using var page = source.RenderPage(0, 1.0);

        Assert.True(CountNonWhitePixels(page) > 1000, "A page with a 400x600 filled rectangle must rasterise visible content.");
    }

    [AvaloniaFact]
    public void DifferentPages_RenderDifferentContent()
    {
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, MultiPagePdf())!;

        using var first = source.RenderPage(0, 1.0);
        using var third = source.RenderPage(2, 1.0);

        Assert.NotEqual(first.PixelSize, third.PixelSize);
        Assert.True(CountNonWhitePixels(third) > 100, "Page 3's filled square must rasterise too.");
    }

    [AvaloniaFact]
    public void AnOutOfRangePageIndex_IsClamped_RatherThanThrowing()
    {
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, MultiPagePdf())!;

        Assert.Equal(source.PageSize(2), source.PageSize(99));
        Assert.Equal(source.PageSize(0), source.PageSize(-4));
        using var page = source.RenderPage(99, 1.0);
        Assert.True(page.PixelSize.Width > 0);
    }

    [AvaloniaFact]
    public void AnExtremeZoom_IsCappedSoOneRenderCannotExhaustMemory()
    {
        // At 32x an A4 page would ask for a bitmap of roughly 360
        // megapixels. The cap degrades sharpness, never availability.
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, MultiPagePdf())!;

        using var page = source.RenderPage(0, DocumentViewport.MaxZoom);

        Assert.True(page.PixelSize.Width <= PdfDocumentPageSource.MaxRasterEdge);
        Assert.True(page.PixelSize.Height <= PdfDocumentPageSource.MaxRasterEdge);
    }

    [AvaloniaFact]
    public void BytesThatAreNotAPdf_AreReportedAsUnopenable_RatherThanRenderedAsBlank()
    {
        Assert.Throws<DocumentRenderException>(() =>
            DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, "not a pdf at all"u8.ToArray()));
    }

    [AvaloniaFact]
    public void AnImage_IsASinglePage_AtItsOwnPixelSize()
    {
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Image, Png())!;

        // True on every platform: an image is one page, and its natural
        // size is a positive number of pixels rather than a DIP size that
        // depends on whatever DPI the file claims.
        Assert.Equal(1, source.PageCount);
        Assert.True(source.PageSize(0).Width > 0);
        Assert.True(source.PageSize(0).Height > 0);

        if (!ImageDecodingIsReal())
            return;

        Assert.Equal(4, source.PageSize(0).Width);
        Assert.Equal(3, source.PageSize(0).Height);
    }

    [AvaloniaFact]
    public void BytesThatAreNotAnImage_AreReportedAsUndecodable()
    {
        if (!ImageDecodingIsReal())
        {
            // The stub decoder accepts anything. What can still be checked
            // is that the failure path does not itself crash: whatever the
            // decoder returns, opening it either yields a usable source or
            // this layer's own exception — never an unhandled one.
            try
            {
                using var lenient = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Image, [1, 2, 3, 4, 5, 6, 7, 8]);
                Assert.True(lenient is null || lenient.PageCount == 1);
            }
            catch (DocumentRenderException)
            {
            }

            return;
        }

        Assert.Throws<DocumentRenderException>(() =>
            DocumentPageSourceFactory.Create(ViewableDocumentFormat.Image, [1, 2, 3, 4, 5, 6, 7, 8]));
    }

    [AvaloniaFact]
    public void Text_IsPaginatedSoTheSamePageNavigationServesIt()
    {
        var lines = string.Join('\n', Enumerable.Range(0, TextDocumentPageSource.LinesPerPage * 2 + 5).Select(i => $"line {i}"));
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Text, Encoding.UTF8.GetBytes(lines))!;

        Assert.Equal(3, source.PageCount);
    }

    [AvaloniaFact]
    public void ShortText_IsASinglePage_AndRenders()
    {
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Text, "Property,Value\nYield,250\n"u8.ToArray())!;

        Assert.Equal(1, source.PageCount);
        using var page = source.RenderPage(0, 1.0);
        Assert.True(page.PixelSize.Width > 0 && page.PixelSize.Height > 0);
    }

    [AvaloniaFact]
    public void TextWithInvalidUtf8_StillOpens()
    {
        // A datasheet with one bad byte is still a datasheet worth reading.
        byte[] content = [0x48, 0x69, 0xFF, 0xFE, 0x0A, 0x74, 0x77, 0x6F];

        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Text, content)!;

        Assert.Equal(1, source.PageCount);
    }

    [AvaloniaFact]
    public void AnUnsupportedFormat_YieldsNoSource_RatherThanAnEmptyOne()
    {
        // Null is the signal the launcher turns into "this format cannot
        // be displayed", which is a different message from "damaged".
        Assert.Null(DocumentPageSourceFactory.Create(ViewableDocumentFormat.Unsupported, [1, 2, 3]));
    }
}
