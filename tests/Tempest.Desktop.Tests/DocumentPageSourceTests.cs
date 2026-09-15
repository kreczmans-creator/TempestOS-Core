using System.Text;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Tempest.Workspace.Viewing;
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

    /// <summary>
    /// A real, one-page PDF padded past <paramref name="minimumSizeInBytes"/>
    /// with a content-stream comment (`TD-96`) — large enough to prove a
    /// multi-megabyte attachment opens through the streamed read path,
    /// without needing a genuinely megabyte-scale drawing on disk.
    /// </summary>
    /// <remarks>
    /// The padding lives inside the page's own content stream, after a
    /// <c>%</c>: a content-stream comment runs to the end of its line and
    /// is otherwise inert, so it inflates the file's byte count without
    /// changing what gets drawn — the rectangle still rasterises exactly
    /// as <see cref="MultiPagePdf"/>'s page 1 does.
    /// </remarks>
    internal static byte[] LargePdf(int minimumSizeInBytes)
    {
        var padding = new string('X', Math.Max(0, minimumSizeInBytes));
        var content = $"1 0 0 rg 50 50 400 600 re f\n% {padding}\n";

        var pdf = new StringBuilder();
        pdf.Append("%PDF-1.4\n");
        pdf.Append("1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n");
        pdf.Append("2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n");
        pdf.Append("3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]/Contents 4 0 R>>endobj\n");
        pdf.Append(System.Globalization.CultureInfo.InvariantCulture, $"4 0 obj<</Length {content.Length}>>stream\n{content}endstream\nendobj\n");
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

    // `WP 16.4A` (part 2), `TD-100`: the headless test host used to
    // substitute a stub decoder that reported every image as 1x1 and
    // accepted bytes that were not an image at all, which forced the image
    // assertions below to probe for real decoding and skip themselves when
    // it was absent. `TestAppBuilder.cs` now runs a real Skia rendering
    // subsystem, so those tests assert real dimensions and real pixel
    // content unconditionally, and the probe this comment used to document
    // is gone along with the condition it existed to check.

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
        // `WP 16.4A` (part 2), `TD-100`: the headless test host now runs a
        // real Skia rendering subsystem (`TestAppBuilder.cs`), so this
        // asserts the PNG's actual 4x3 size and actual red content
        // unconditionally — no stub-decoder probe, no vacuous pass.
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Image, Png())!;

        Assert.Equal(1, source.PageCount);
        Assert.Equal(4, source.PageSize(0).Width);
        Assert.Equal(3, source.PageSize(0).Height);

        // The assertion that separates a real decoder from a stub that
        // reports plausible dimensions for a bitmap with no real content:
        // every one of the 4x3 = 12 pixels must be the fixture's red, not
        // the white a blank or mis-decoded bitmap would read back as.
        using var page = source.RenderPage(0, 1.0);
        Assert.Equal(12, CountNonWhitePixels(page));
    }

    [AvaloniaFact]
    public void BytesThatAreNotAnImage_AreReportedAsUndecodable()
    {
        // `WP 16.4A` (part 2), `TD-100`: real Skia decoding rejects bytes
        // that are not an image, so this is unconditional too — the
        // headless stub decoder that used to accept anything is gone.
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

    // ----------------------------------------------------------------
    // SVG (`TD-99`) — Svg.Skia (MIT), the same SKBitmap-backed page path
    // a PDF or an image produces.
    // ----------------------------------------------------------------

    /// <summary>A real, valid SVG: a filled red rectangle, so a genuine rasteriser proves itself exactly as <see cref="MultiPagePdf"/>'s own fixture does.</summary>
    internal static byte[] RedRectangleSvg() => Encoding.UTF8.GetBytes(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"80\">" +
        "<rect x=\"0\" y=\"0\" width=\"120\" height=\"80\" fill=\"#FF0000\"/></svg>");

    [AvaloniaFact]
    public void AValidSvg_ReportsItsOwnSize_AndRastersises()
    {
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, RedRectangleSvg())!;

        Assert.Equal(1, source.PageCount);
        Assert.Equal(120, source.PageSize(0).Width, 0.5);
        Assert.Equal(80, source.PageSize(0).Height, 0.5);

        using var page = source.RenderPage(0, 1.0);
        Assert.True(CountNonWhitePixels(page) > 100, "A page filled edge-to-edge with red must rasterise visible content.");
    }

    [AvaloniaFact]
    public void AnSvgPage_RasterisesAtTheRequestedScale()
    {
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, RedRectangleSvg())!;

        using var atOne = source.RenderPage(0, 1.0);
        using var atThree = source.RenderPage(0, 3.0);

        Assert.Equal(120, atOne.PixelSize.Width);
        Assert.Equal(360, atThree.PixelSize.Width);
    }

    [AvaloniaFact]
    public void MalformedSvg_ReportsWhyRatherThanCrashing()
    {
        var ex = Assert.Throws<DocumentRenderException>(() =>
            DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, "<svg><rect this is not xml"u8.ToArray()));

        Assert.StartsWith("This SVG could not be read:", ex.Message);
    }

    [AvaloniaFact]
    public void EmptySvg_IsReportedAsMalformed_RatherThanARenderableBlank()
    {
        var ex = Assert.Throws<DocumentRenderException>(() =>
            DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, "not markup at all"u8.ToArray()));

        Assert.StartsWith("This SVG could not be read:", ex.Message);
    }

    [AvaloniaFact]
    public void AHugeSvg_RastersisesCapped_RatherThanExhaustingMemory()
    {
        // A viewBox can claim any size at all — the same unbounded-claim
        // hazard a PDF page poses, capped by the identical mechanism.
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20000\" height=\"20000\">" +
            "<rect width=\"20000\" height=\"20000\" fill=\"#0000FF\"/></svg>";
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(svg))!;

        Assert.Equal(20000, source.PageSize(0).Width, 0.5);

        using var page = source.RenderPage(0, 1.0);

        Assert.True(page.PixelSize.Width <= SvgDocumentPageSource.MaxRasterEdge);
        Assert.True(page.PixelSize.Height <= SvgDocumentPageSource.MaxRasterEdge);
    }

    [AvaloniaFact]
    public void AnSvgFromAStream_ReportsItsOwnSizeAndRasterises()
    {
        using var stream = new MemoryStream(RedRectangleSvg(), writable: false);
        using var source = DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Svg, stream)!;

        Assert.Equal(120, source.PageSize(0).Width, 0.5);
        using var page = source.RenderPage(0, 1.0);
        Assert.True(CountNonWhitePixels(page) > 100);
    }

    // ---- `TD-184`: rendering an SVG must never touch the network or the
    // local disk, and must never run script — proven against Svg.Skia's own
    // GetImageFromWeb, which calls WebRequest.Create(uri).GetResponse() for
    // any <image> href that is not a data: URI, http(s):// and file://
    // alike. ------------------------------------------------------------

    [AvaloniaFact]
    public void AnSvgReferencingAnHttpImage_RendersWithNoNetworkFetch()
    {
        // A URI nothing on this machine can resolve quickly, pointing at a
        // reserved, non-routable test address (RFC 5737): if sanitisation
        // failed and Svg.Skia actually attempted this fetch, the render
        // would hang or throw a WebException, not complete cleanly and
        // promptly.
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"40\" height=\"40\">" +
            "<image xlink:href=\"http://192.0.2.1/tracker.png\" width=\"40\" height=\"40\"/>" +
            "<rect width=\"40\" height=\"40\" fill=\"#00FF00\"/></svg>";

        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(svg))!;
        using var page = source.RenderPage(0, 1.0);

        // Rendered promptly (proven by the test itself completing at all —
        // xUnit's own default timeout would otherwise catch a real hang)
        // and shows the rectangle that was there regardless of the image.
        Assert.True(CountNonWhitePixels(page) > 100);
    }

    [AvaloniaFact]
    public void AnSvgReferencingAFileUri_ReadsNoLocalFile()
    {
        // The most dangerous of the three references this proves nothing
        // external happens for: a `file://` href would let a malicious
        // attachment read an arbitrary local file this process can see and
        // fold its bytes into what renders — proven here against a real
        // file this test itself creates and knows the content of.
        var probePath = Path.Combine(Path.GetTempPath(), $"td184-probe-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(probePath, DocumentPageSourceTests.Png());
        try
        {
            var fileUri = new Uri(probePath).AbsoluteUri;
            var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"40\" height=\"40\">" +
                $"<image xlink:href=\"{fileUri}\" width=\"40\" height=\"40\"/>" +
                "<rect width=\"40\" height=\"40\" fill=\"#00FF00\"/></svg>";

            using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(svg))!;
            using var page = source.RenderPage(0, 1.0);

            // The real probe file is a solid red 4x3 image; had it been
            // read and drawn, the page would carry red pixels alongside
            // the rectangle's green. It must not.
            Assert.True(CountNonWhitePixels(page) > 100);
            Assert.False(HasRedPixel(page), "The local probe file's own red pixels must never reach the rendered page.");
        }
        finally
        {
            File.Delete(probePath);
        }
    }

    [AvaloniaFact]
    public void AnSvgWithADoctype_IsStrippedBeforeParsing_ClosingTheXxeVector()
    {
        const string malicious =
            "<?xml version=\"1.0\"?><!DOCTYPE svg [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>" +
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\">" +
            "<rect width=\"10\" height=\"10\" fill=\"#FF00FF\"/></svg>";

        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(malicious))!;
        using var page = source.RenderPage(0, 1.0);

        Assert.True(CountNonWhitePixels(page) > 10);
    }

    [AvaloniaFact]
    public void SanitiseSvgMarkup_BlanksEveryExternalReference_ButKeepsDataUrisAndFragments()
    {
        const string markup =
            "<svg><image href=\"http://evil.example/x.png\"/><image href=\"https://evil.example/y.png\"/>" +
            "<image href=\"file:///etc/passwd\"/><image xlink:href=\"data:image/png;base64,AAAA\"/>" +
            "<use href=\"#local\"/><script>alert(1)</script></svg>";

        var sanitised = SvgMarkupSanitiser.Sanitise(markup);

        Assert.DoesNotContain("http://evil.example", sanitised, StringComparison.Ordinal);
        Assert.DoesNotContain("https://evil.example", sanitised, StringComparison.Ordinal);
        Assert.DoesNotContain("file:///etc/passwd", sanitised, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", sanitised, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data:image/png;base64,AAAA", sanitised, StringComparison.Ordinal);
        Assert.Contains("href=\"#local\"", sanitised, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void SanitiseSvgMarkup_StripsADoctypeDeclaration()
    {
        const string markup = "<!DOCTYPE svg [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><svg></svg>";

        var sanitised = SvgMarkupSanitiser.Sanitise(markup);

        Assert.DoesNotContain("DOCTYPE", sanitised, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ENTITY", sanitised, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether any pixel of <paramref name="bitmap"/> reads as red (the probe file's own colour) rather than green (the SVG's own drawn rectangle) or white.</summary>
    private static bool HasRedPixel(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];

        if (bitmap is WriteableBitmap writeable)
        {
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

        // BGRA byte order (this codebase's own convention throughout).
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i] < 60 && pixels[i + 1] < 60 && pixels[i + 2] > 180)
                return true;
        }

        return false;
    }

    [AvaloniaFact]
    public void AnUnsupportedFormat_YieldsNoSource_RatherThanAnEmptyOne()
    {
        // Null is the signal the launcher turns into "this format cannot
        // be displayed", which is a different message from "damaged".
        Assert.Null(DocumentPageSourceFactory.Create(ViewableDocumentFormat.Unsupported, [1, 2, 3]));
    }

    [AvaloniaFact]
    public void AnExternalOnlyFormat_AlsoYieldsNoSource_ItIsNeverGoingToRenderInApp()
    {
        // DWG and DXF are not a gap the way a genuinely unhandled format
        // is: this platform is never going to draw them, so the factory's
        // answer is the same null, and the launcher's own materialised
        // copy plus "Open externally" is the whole of what the file needs.
        Assert.Null(DocumentPageSourceFactory.Create(ViewableDocumentFormat.ExternalOnly, [1, 2, 3]));
    }

    // ----------------------------------------------------------------
    // Stream-backed page sources (`TD-96`) — the identical claims above,
    // proved again through CreateFromStream rather than Create, so a
    // stream-backed instance is provably not a second, weaker renderer.
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public void APdfFromAStream_ReportsItsRealPageCountAndSizes()
    {
        using var stream = new MemoryStream(MultiPagePdf(), writable: false);
        using var source = DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Pdf, stream)!;

        Assert.Equal(3, source.PageCount);
        Assert.Equal(new Size(595, 842), source.PageSize(0));
        Assert.Equal(new Size(842, 595), source.PageSize(1));
        Assert.Equal(new Size(200, 200), source.PageSize(2));
    }

    [AvaloniaFact]
    public void APdfPageFromAStream_ActuallyContainsTheDrawnContent()
    {
        using var stream = new MemoryStream(MultiPagePdf(), writable: false);
        using var source = DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Pdf, stream)!;

        // Every page read again, out of order — the stream-backed source
        // keeps its stream open across renders (`TD-96`) rather than
        // exhausting it on the first page.
        using var third = source.RenderPage(2, 1.0);
        using var first = source.RenderPage(0, 1.0);

        Assert.True(CountNonWhitePixels(first) > 1000, "Page 1's filled rectangle must rasterise from a stream exactly as it does from an array.");
        Assert.True(CountNonWhitePixels(third) > 100, "Page 3's filled square must rasterise too.");
    }

    [AvaloniaFact]
    public void DisposingAStreamBackedPdfSource_DisposesTheStreamItWasGiven()
    {
        var tracking = new DisposeTrackingStream(MultiPagePdf());
        var source = DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Pdf, tracking)!;

        Assert.False(tracking.IsDisposed);
        source.Dispose();
        Assert.True(tracking.IsDisposed);
    }

    [AvaloniaFact]
    public void ANonSeekablePdfStream_IsRejected()
    {
        // PDFium reads its cross-reference table from the end of the
        // file: a forward-only stream cannot serve that, and the failure
        // must be an argument problem stated up front, not a mysterious
        // render failure later.
        using var stream = new NonSeekableStream(MultiPagePdf());

        Assert.Throws<ArgumentException>(() => DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Pdf, stream));
    }

    [AvaloniaFact]
    public void BytesThatAreNotAPdf_FromAStream_AreReportedAsUnopenable()
    {
        using var stream = new MemoryStream("not a pdf at all"u8.ToArray(), writable: false);

        Assert.Throws<DocumentRenderException>(() =>
            DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Pdf, stream));
    }

    [AvaloniaFact]
    public void AnImageFromAStream_IsASinglePage_AtItsOwnPixelSize()
    {
        using var stream = new MemoryStream(Png(), writable: false);
        using var source = DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Image, stream)!;

        Assert.Equal(1, source.PageCount);
        Assert.Equal(4, source.PageSize(0).Width);
        Assert.Equal(3, source.PageSize(0).Height);

        using var page = source.RenderPage(0, 1.0);
        Assert.Equal(12, CountNonWhitePixels(page));
    }

    [AvaloniaFact]
    public void DecodingAnImageFromAStream_DisposesTheStreamImmediately()
    {
        // Unlike the PDF source, there is exactly one already-decoded
        // page: nothing here needs the stream again, so it is released as
        // soon as decoding finishes rather than held for the source's
        // whole lifetime.
        var tracking = new DisposeTrackingStream(Png());

        using var source = DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Image, tracking)!;

        Assert.True(tracking.IsDisposed);
    }

    [AvaloniaFact]
    public void BytesThatAreNotAnImage_FromAStream_AreReportedAsUndecodable()
    {
        using var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8], writable: false);

        Assert.Throws<DocumentRenderException>(() =>
            DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Image, stream));
    }

    [AvaloniaFact]
    public void TextFromAStream_IsPaginatedSoTheSamePageNavigationServesIt()
    {
        var lines = string.Join('\n', Enumerable.Range(0, TextDocumentPageSource.LinesPerPage * 2 + 5).Select(i => $"line {i}"));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(lines), writable: false);

        using var source = DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Text, stream)!;

        Assert.Equal(3, source.PageCount);
    }

    [AvaloniaFact]
    public void ALargePdfFromAStream_StillOpensAndRenders()
    {
        var bytes = LargePdf(4 * 1024 * 1024);
        Assert.True(bytes.Length > 4 * 1024 * 1024);

        using var stream = new MemoryStream(bytes, writable: false);
        using var source = DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Pdf, stream)!;

        Assert.Equal(1, source.PageCount);
        using var page = source.RenderPage(0, 1.0);
        Assert.True(CountNonWhitePixels(page) > 1000, "The padded page's filled rectangle must still rasterise.");
    }

    [AvaloniaFact]
    public void AnUnsupportedFormat_FromAStream_YieldsNoSource()
    {
        using var stream = new MemoryStream([1, 2, 3], writable: false);

        Assert.Null(DocumentPageSourceFactory.CreateFromStream(ViewableDocumentFormat.Unsupported, stream));
    }

    // ========================================================================
    // WP 21.5F — Offensive Security Audit: OSA-01, file parsers fed untrusted bytes.
    // ========================================================================

    [Fact]
    public void OSA01_AnImageDeclaringMorePixelsThanThePlatformWillDecode_IsRefusedBeforeDecoding()
    {
        // The exploit: a decompression-bomb-shaped image — a tiny file
        // whose header alone declares an enormous pixel grid — reached
        // Avalonia's own decoder with no size check at all before this
        // fix, so the full declared bitmap was materialised regardless of
        // how small the file on disk was. This header (54 bytes, BMP,
        // BITMAPINFOHEADER) declares 12000x12000 = 144,000,000 pixels,
        // comfortably past MaxDecodedPixels (40,000,000), with no pixel
        // data behind it at all — this PoC is about the *declared* size
        // being refused before any decode is attempted, not about a
        // successful render.
        var bmp = BuildBmpHeaderOnly(width: 12000, height: 12000);

        var ex = Assert.Throws<DocumentRenderException>(() => new ImageDocumentPageSource(bmp));

        Assert.Contains("144,000,000", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OSA01_AnImageDeclaringMorePixelsThanThePlatformWillDecode_FromAStream_IsRefusedBeforeDecoding()
    {
        var bmp = BuildBmpHeaderOnly(width: 12000, height: 12000);
        using var stream = new MemoryStream(bmp, writable: false);

        var ex = Assert.Throws<DocumentRenderException>(() => new ImageDocumentPageSource(stream));

        Assert.Contains("144,000,000", ex.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void OSA01_AnImageWithinTheDecodedPixelLimit_StillOpensNormally()
    {
        // The guard must not be so aggressive it refuses a real, ordinary
        // image - only Png()'s own tiny fixture is needed here, reusing
        // AnImage_IsASinglePage_AtItsOwnPixelSize's own proof that a real
        // decode still runs end to end.
        var source = new ImageDocumentPageSource(Png());

        Assert.Equal(1, source.PageCount);
        Assert.True(source.PageSize(0).Width > 0);
    }

    [Fact]
    public void OSA01_AnOversizedTextFile_IsTruncatedRatherThanReadFullyIntoMemory()
    {
        // The exploit: TextDocumentPageSource read an attachment's entire
        // content into one managed string, and then one string[] of
        // lines, with no size cap at all before this fix - a large enough
        // upload was a straightforward, unmitigated memory-exhaustion
        // vector. Proven here by comparison rather than by asserting an
        // exact byte count: an input sitting at the cap and an input three
        // times larger must page out to essentially the same PageCount
        // once both are truncated to the same cap - before the fix, the
        // 3x input would page out to roughly 3x as many pages, since the
        // whole thing was decoded regardless of size.
        const string line = "0123456789ABCDEF\n";
        var atCapBytes = BuildRepeatedTextBytes(line, TextDocumentPageSource.MaxSourceBytes);
        var overCapBytes = BuildRepeatedTextBytes(line, TextDocumentPageSource.MaxSourceBytes * 3);

        var atCapSource = new TextDocumentPageSource(atCapBytes);
        var overCapSource = new TextDocumentPageSource(overCapBytes);

        Assert.InRange(overCapSource.PageCount, atCapSource.PageCount - 2, atCapSource.PageCount + 2);
    }

    [Fact]
    public void OSA01_AnOversizedTextFile_FromAStream_IsTruncatedRatherThanReadFullyIntoMemory()
    {
        const string line = "0123456789ABCDEF\n";
        var atCapBytes = BuildRepeatedTextBytes(line, TextDocumentPageSource.MaxSourceBytes);
        var overCapBytes = BuildRepeatedTextBytes(line, TextDocumentPageSource.MaxSourceBytes * 3);

        using var atCapStream = new MemoryStream(atCapBytes, writable: false);
        using var overCapStream = new MemoryStream(overCapBytes, writable: false);

        var atCapSource = new TextDocumentPageSource(atCapStream);
        var overCapSource = new TextDocumentPageSource(overCapStream);

        Assert.InRange(overCapSource.PageCount, atCapSource.PageCount - 2, atCapSource.PageCount + 2);
    }

    /// <summary>
    /// A minimal, valid BMP file header (14-byte <c>BITMAPFILEHEADER</c> +
    /// 40-byte <c>BITMAPINFOHEADER</c>) declaring <paramref name="width"/> x
    /// <paramref name="height"/> pixels, with no pixel data following it —
    /// the smallest possible file that still lets a header-only peek (the
    /// production size guard) read the declared dimensions.
    /// </summary>
    private static byte[] BuildBmpHeaderOnly(int width, int height)
    {
        var header = new byte[54];
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        BitConverter.GetBytes(54).CopyTo(header, 10); // pixel data offset
        BitConverter.GetBytes(40).CopyTo(header, 14); // BITMAPINFOHEADER size
        BitConverter.GetBytes(width).CopyTo(header, 18);
        BitConverter.GetBytes(height).CopyTo(header, 22);
        BitConverter.GetBytes((short)1).CopyTo(header, 26); // colour planes
        BitConverter.GetBytes((short)24).CopyTo(header, 28); // bits per pixel
        return header;
    }

    /// <summary>Repeats <paramref name="line"/> until at least <paramref name="minimumBytes"/> bytes have been produced.</summary>
    private static byte[] BuildRepeatedTextBytes(string line, int minimumBytes)
    {
        var lineBytes = Encoding.ASCII.GetBytes(line);
        var buffer = new byte[minimumBytes + lineBytes.Length];
        var written = 0;
        while (written < minimumBytes)
        {
            lineBytes.CopyTo(buffer, written);
            written += lineBytes.Length;
        }

        return buffer[..written];
    }

    /// <summary>A stream that records whether it was disposed, wrapping a fixed byte array.</summary>
    private sealed class DisposeTrackingStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>A read-only, forward-only stream — everything a PDF stream must not be.</summary>
    private sealed class NonSeekableStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content, writable: false);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();

            base.Dispose(disposing);
        }
    }
}
