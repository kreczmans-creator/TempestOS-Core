using System.Runtime.Versioning;
using System.Text;
using Avalonia.Headless.XUnit;
using PDFtoImage;
using SkiaSharp;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Documents.DrawingRegisters;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.Documents;

/// <summary>The drawing register document renderer (`WP 21.2A`, scope item 2) — A4 <b>landscape</b>, per the design system's own <c>DrawingRegister.dc.html</c> grammar.</summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class DrawingRegisterDocumentRendererTests
{
    [Fact]
    public void DocumentType_And_TemplateName_MatchTheDesignSystemMapping()
    {
        var renderer = new DrawingRegisterDocumentRenderer();
        Assert.Equal("DRAWING REGISTER", renderer.DocumentType);
        Assert.Equal("drawing-register", renderer.TemplateName);
    }

    [AvaloniaFact]
    public void Render_IsLandscape_WiderThanItIsTall()
    {
        Assert.True(DocumentTemplate.LandscapePageWidth > DocumentTemplate.LandscapePageHeight);
        Assert.Equal(DocumentTemplate.PageHeight, DocumentTemplate.LandscapePageWidth);
        Assert.Equal(DocumentTemplate.PageWidth, DocumentTemplate.LandscapePageHeight);
    }

    [AvaloniaFact]
    public void Render_ProducesAValidOnePagePdf()
    {
        var bytes = new DrawingRegisterDocumentRenderer().Render(DrawingRegisterDocumentModelFixtures.Minimal()).ToArray();

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length)), StringComparison.Ordinal);
        Assert.Equal(1, Conversion.GetPageCount(bytes));
    }

    [AvaloniaFact]
    public void Render_EveryFieldTheRegisterNames_IsReadableBackOutOfTheText()
    {
        var model = DrawingRegisterDocumentModelFixtures.Minimal();
        var bytes = new DrawingRegisterDocumentRenderer().Render(model).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("DRAWING REGISTER", text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectCode, text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectName, text, StringComparison.Ordinal);

        foreach (var row in model.Rows)
        {
            Assert.Contains(row.Number, text, StringComparison.Ordinal);
            Assert.Contains(row.Title, text, StringComparison.Ordinal);
            Assert.Contains(row.Status, text, StringComparison.Ordinal);
        }

        Assert.Contains("Page 1 of 1", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_SixtyRows_Paginates_AndEveryPageCarriesTheFooter()
    {
        var model = DrawingRegisterDocumentModelFixtures.WithRowCount(60);

        var bytes = new DrawingRegisterDocumentRenderer().Render(model).ToArray();
        var pageCount = Conversion.GetPageCount(bytes);

        Assert.True(pageCount > 1, $"60 rows must overflow a single A4 landscape page; got {pageCount} page(s).");

        for (var i = 0; i < pageCount; i++)
        {
            using var page = Conversion.ToImage(bytes, new Index(i), options: new RenderOptions(Dpi: 72));
            Assert.True(page.Width > page.Height, $"Page {i + 1} must be landscape (wider than tall); got {page.Width}x{page.Height}.");
            Assert.True(HasDarkPixels(page, top: (int)(page.Height * 0.88), bottom: page.Height), $"Page {i + 1} of {pageCount}'s own footer band must rasterise visible ink.");
        }

        var text = PdfTextExtractor.ExtractText(bytes);
        Assert.Contains($"Page {pageCount} of {pageCount}", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_NullModel_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new DrawingRegisterDocumentRenderer().Render(null!));

    private static bool HasDarkPixels(SKBitmap bitmap, int top, int bottom)
    {
        var clampedTop = Math.Clamp(top, 0, bitmap.Height);
        var clampedBottom = Math.Clamp(bottom, 0, bitmap.Height);

        for (var y = clampedTop; y < clampedBottom; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red < 200 || pixel.Green < 200 || pixel.Blue < 200)
                    return true;
            }
        }

        return false;
    }
}

/// <summary>Fixture <see cref="DrawingRegisterDocumentModel"/>s for the renderer tests.</summary>
internal static class DrawingRegisterDocumentModelFixtures
{
    public static DrawingRegisterDocumentModel Minimal() => new(
        ProjectCode: "PRJ-100",
        ProjectName: "Acme Gantry Upgrade",
        Rows:
        [
            new DrawingRegisterRow("DWG-001", "Gantry General Arrangement", "3", "Issued", "Current: rev 3"),
            new DrawingRegisterRow("DWG-002", "Runway Beam Detail", "1", "Draft", "Current: rev 1"),
        ],
        GeneratedAtUtc: new DateTimeOffset(2026, 3, 4, 9, 5, 0, TimeSpan.Zero),
        ApplicationVersionText: "TempestOS 0.21.0 (2e655db)");

    public static DrawingRegisterDocumentModel WithRowCount(int count) =>
        Minimal() with
        {
            Rows =
            [
                .. Enumerable.Range(1, count).Select(i => new DrawingRegisterRow(
                    $"DWG-{i:000}",
                    $"Drawing {i} — a reasonably long title so this row wraps across more than one line of the table.",
                    "1",
                    "Draft",
                    "Current: rev 1")),
            ],
        };
}
