using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Viewing;
using Tempest.Workspace.Viewing;

namespace Tempest.Desktop.Tests;

/// <summary>
/// "Save annotated copy…" (`TD-98`) — a page's own annotations burned into
/// a copy of its rendered bitmap, encoded as a PNG directly or wrapped in a
/// hand-written single-page PDF, leaving the original untouched. The claim
/// this file exists to prove: <b>the burned-in export produces a file that
/// opens</b> — fed straight back through this platform's own real readers
/// (<see cref="PdfDocumentPageSource"/> for the PDF path, Avalonia's own
/// decoder for PNG) rather than merely asserting a non-empty byte array.
/// </summary>
public sealed class AnnotatedCopyExporterTests
{
    private static WriteableBitmap RedPage(int width, int height)
    {
        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var locked = bitmap.Lock();

        var rowBytes = new byte[locked.RowBytes];
        for (var x = 0; x < width; x++)
        {
            rowBytes[(x * 4) + 0] = 0x00; // B
            rowBytes[(x * 4) + 1] = 0x00; // G
            rowBytes[(x * 4) + 2] = 0xFF; // R
            rowBytes[(x * 4) + 3] = 0xFF; // A
        }

        for (var y = 0; y < height; y++)
            System.Runtime.InteropServices.Marshal.Copy(rowBytes, 0, locked.Address + (y * locked.RowBytes), rowBytes.Length);

        return bitmap;
    }

    private static AttachmentAnnotation Rectangle() => new(
        Guid.NewGuid(), Guid.NewGuid(), PageIndex: 0, AnnotationTool.Rectangle,
        [new AnnotationPoint(10, 10), new AnnotationPoint(50, 40)], "#12B981", null, DateTimeOffset.UtcNow, "tester");

    [AvaloniaFact]
    public void ComposeAnnotatedPage_IsTheSamePixelSizeAsTheSourcePage()
    {
        using var page = RedPage(100, 80);

        using var composed = AnnotatedCopyExporter.ComposeAnnotatedPage(page, [Rectangle()], 100, 80, rotationDegrees: 0, zoom: 1.0);

        Assert.Equal(page.PixelSize, composed.PixelSize);
    }

    [AvaloniaFact]
    public void EncodePng_ProducesARealPngThatDecodesBackToTheSameSize()
    {
        using var page = RedPage(64, 48);
        using var composed = AnnotatedCopyExporter.ComposeAnnotatedPage(page, [Rectangle()], 64, 48, 0, 1.0);

        var png = AnnotatedCopyExporter.EncodePng(composed);

        Assert.True(png.Length > 0);
        using var stream = new MemoryStream(png);
        using var decoded = new Bitmap(stream);
        Assert.Equal(64, decoded.PixelSize.Width);
        Assert.Equal(48, decoded.PixelSize.Height);
    }

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [AvaloniaFact]
    public void EncodePdf_ProducesARealPdf_ThatThisPlatformsOwnReaderOpens()
    {
        // The claim this Work Package's own brief states in exactly these
        // words: "the burned-in export produces a file that opens." Fed
        // straight back through PdfDocumentPageSource — the same reader
        // every attachment in this platform opens through — not merely
        // asserted to be a non-empty byte array.
        using var page = RedPage(120, 90);
        using var composed = AnnotatedCopyExporter.ComposeAnnotatedPage(page, [Rectangle()], 120, 90, 0, 1.0);

        var pdfBytes = AnnotatedCopyExporter.EncodePdf(composed);
        Assert.True(pdfBytes.Length > 0);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5));

        using var reopened = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, pdfBytes)!;
        Assert.Equal(1, reopened.PageCount);
        Assert.Equal(120, reopened.PageSize(0).Width, 0.5);
        Assert.Equal(90, reopened.PageSize(0).Height, 0.5);

        using var rendered = reopened.RenderPage(0, 1.0);
        Assert.True(rendered.PixelSize.Width > 0 && rendered.PixelSize.Height > 0);
    }

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [AvaloniaFact]
    public void EncodePdf_TheReopenedPageActuallyShowsTheSourcesRedContent()
    {
        // Not merely "a valid PDF" — the real pixel content survived the
        // round trip. The red page fixture, decoded back, must still be
        // overwhelmingly red rather than a blank or corrupted image.
        using var page = RedPage(80, 60);
        using var composed = AnnotatedCopyExporter.ComposeAnnotatedPage(page, [], 80, 60, 0, 1.0);

        var pdfBytes = AnnotatedCopyExporter.EncodePdf(composed);

        using var reopened = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Pdf, pdfBytes)!;
        using var rendered = reopened.RenderPage(0, 1.0);

        var size = rendered.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];
        using (var locked = ((WriteableBitmap)rendered).Lock())
        {
            for (var row = 0; row < size.Height; row++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    locked.Address + (row * locked.RowBytes), pixels, row * stride, stride);
            }
        }

        var redPixels = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i] < 60 && pixels[i + 1] < 60 && pixels[i + 2] > 180)
                redPixels++;
        }

        Assert.True(redPixels > (size.Width * size.Height) / 2, "Most of the reopened page must still read as the source's own red.");
    }

    [Fact]
    public void MinimalPdfWriter_RejectsAMismatchedPixelBufferLength()
    {
        Assert.Throws<ArgumentException>(() => MinimalPdfWriter.WriteSinglePageImagePdf(10, 10, new byte[5]));
    }

    [Fact]
    public void MinimalPdfWriter_RejectsNonPositiveDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MinimalPdfWriter.WriteSinglePageImagePdf(0, 10, []));
    }
}
