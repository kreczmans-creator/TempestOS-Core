using System.Runtime.Versioning;
using System.Text;
using Avalonia.Headless.XUnit;
using PDFtoImage;
using SkiaSharp;
using Tempest.Core.Evidence;
using Tempest.Desktop.IssueSheets;
using Tempest.Workspace.Evidence;

namespace Tempest.Desktop.Tests.Evidence;

/// <summary>
/// The issue sheet PDF renderer (`WP 18.2B`, part 1) — that it really is a
/// PDF PDFium can open, that page 1 carries real ink rather than a blank
/// canvas of the right size, that the same model renders byte-identical
/// twice, and that a table too big for one page paginates with a footer
/// on every page it produces.
/// </summary>
/// <remarks>
/// Verified through <c>PDFtoImage.Conversion</c> directly — the same
/// PDFium backend <see cref="Tempest.Desktop.Viewing.PdfDocumentPageSource"/>
/// renders the viewer's own PDFs through, whose platform support this
/// class carries the identical <see cref="SupportedOSPlatformAttribute"/>
/// list for (`TD-80`).
/// </remarks>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public class IssueSheetRendererTests
{
    [AvaloniaFact]
    public void Render_ProducesAValidPdf_WithTheExpectedPageCount()
    {
        var bytes = new IssueSheetRenderer().Render(IssueSheetModelFixtures.Minimal()).ToArray();

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length)), StringComparison.Ordinal);
        Assert.Equal(1, Conversion.GetPageCount(bytes));
    }

    [AvaloniaFact]
    public void Render_Page1_CarriesRealInk_InTheTitleBlockRegion()
    {
        var bytes = new IssueSheetRenderer().Render(IssueSheetModelFixtures.Minimal()).ToArray();

        using var page = Conversion.ToImage(bytes, new Index(0), options: RasterAtOnePointPerPixel);

        // The title block is drawn at the top of the page — the region a
        // renderer that produced a blank white page (same page count,
        // same size, nothing drawn) would still pass a size-only check.
        Assert.True(
            HasDarkPixels(page, top: 0, bottom: page.Height / 3),
            "The title block, in the top third of page 1, must rasterise visible ink.");
    }

    [AvaloniaFact]
    public void Render_TheSameModelTwice_YieldsByteIdenticalPdfs()
    {
        var renderer = new IssueSheetRenderer();
        var model = IssueSheetModelFixtures.Minimal();

        var first = renderer.Render(model).ToArray();
        var second = renderer.Render(model).ToArray();

        // The PDF's own Creation/Modified metadata come from the model's
        // GeneratedAtUtc rather than the clock (`ADR-0148`), so nothing
        // here is time-of-day dependent — two renders of one model must
        // be the same bytes, not merely the same visible content.
        Assert.Equal(first, second);
    }

    [AvaloniaFact]
    public void Render_FortyCitationsAndThirtyFigures_Paginates_AndEveryPageCarriesTheFooter()
    {
        var model = IssueSheetModelFixtures.WithCitationsAndFigures(citationCount: 40, figureCount: 30);

        var bytes = new IssueSheetRenderer().Render(model).ToArray();
        var pageCount = Conversion.GetPageCount(bytes);

        Assert.True(pageCount > 1, $"40 citations and 30 declared figures must overflow a single A4 page; got {pageCount} page(s).");

        for (var i = 0; i < pageCount; i++)
        {
            using var page = Conversion.ToImage(bytes, new Index(i), options: RasterAtOnePointPerPixel);

            // The footer sits in the bottom margin band the renderer's own
            // FooterReserve constant reserves — the bottom ~12% of the
            // page comfortably contains it regardless of exact geometry.
            Assert.True(
                HasDarkPixels(page, top: (int)(page.Height * 0.88), bottom: page.Height),
                $"Page {i + 1} of {pageCount}'s own footer band must rasterise visible ink.");
        }
    }

    /// <summary>1 PDF point = 1 rasterised pixel — the same "72 DPI is the page's own true size" convention <c>PdfDocumentPageSource.BaseDpi</c> uses, so a pixel row here maps directly onto the renderer's own point-based layout constants.</summary>
    private static readonly RenderOptions RasterAtOnePointPerPixel = new(Dpi: 72);

    [AvaloniaFact]
    public void Render_NullModel_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new IssueSheetRenderer().Render(null!));

    /// <summary>Whether any pixel in the horizontal band <paramref name="top"/>..<paramref name="bottom"/> is materially darker than white — the same "did anything actually draw" check `DocumentPageSourceTests` applies via its own <c>CountNonWhitePixels</c>, scoped to one region rather than a whole page.</summary>
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

/// <summary>Fixture <see cref="IssueSheetModel"/>s for the renderer tests — hand-built rather than routed through a real <c>Evidence</c>/<c>EvidenceTestHost</c>, since the renderer's own contract begins at the model, not at the Kind that produces it (that mapping is <c>IssueSheetModelFromTests</c>'s own job, in <c>Tempest.Core.Tests</c>).</summary>
internal static class IssueSheetModelFixtures
{
    /// <summary>A small, fully-populated issue sheet — the "fixture model" the Work Package's own report cites a PDF size for.</summary>
    public static IssueSheetModel Minimal() => new(
        ProjectCode: "PRJ-100",
        ProjectName: "Acme Gantry Upgrade",
        Client: "Acme Industries Ltd",
        EvidenceReference: "EVD-0042",
        Title: "Gantry crane end-truck bracket calculation",
        Revision: "A",
        IssueReference: "ISS-001",
        IssueDateUtc: new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero),
        Classification: EvidenceClassification.Calculation,
        AuthorDisplayName: "Priya Patel",
        AuthorDateUtc: new DateTimeOffset(2026, 2, 20, 14, 30, 0, TimeSpan.Zero),
        CheckerName: "J. Reviewer",
        CheckerOrganisation: "Acme Industries Ltd",
        CheckerPrincipalDisplayName: null,
        CheckDateUtc: new DateTimeOffset(2026, 2, 27, 10, 15, 0, TimeSpan.Zero),
        Outcome: CheckOutcome.AcceptedWithComments,
        Citations:
        [
            new IssueSheetCitationRow("Materials", "FX-STEEL-001", 3, "BSI, BS EN 10025-2, 2019, Table 7, S355"),
            new IssueSheetCitationRow("Standards", "BS-EN-1993-1-1", 2, null),
        ],
        DeclaredFigures:
        [
            new DeclaredFigure("Utilisation", DeclaredFigureRole.Result, "0.82 1"),
            new DeclaredFigure("Max stress", DeclaredFigureRole.Result, "142 MPa"),
        ],
        GeneratedAtUtc: new DateTimeOffset(2026, 3, 4, 9, 5, 0, TimeSpan.Zero),
        ApplicationVersionText: "TempestOS 0.18.0 (2e655db)",
        StoreSequence: 4711L);

    /// <summary>The minimal fixture, with its citations and declared figures replaced by <paramref name="citationCount"/> and <paramref name="figureCount"/> long, wrapping rows — the pagination case.</summary>
    public static IssueSheetModel WithCitationsAndFigures(int citationCount, int figureCount) =>
        Minimal() with
        {
            Citations =
            [
                .. Enumerable.Range(1, citationCount).Select(i => new IssueSheetCitationRow(
                    i % 2 == 0 ? "Materials" : "Standards",
                    $"FX-STEEL-{i:000}",
                    i,
                    $"BSI, BS EN 10025-2, 2019, Table {i}, S355 grade structural steel used for member {i} of the assembly this calculation checks.")),
            ],
            DeclaredFigures =
            [
                .. Enumerable.Range(1, figureCount).Select(i => new DeclaredFigure(
                    $"Figure {i} — utilisation ratio at load case {i}",
                    i % 2 == 0 ? DeclaredFigureRole.Input : DeclaredFigureRole.Result,
                    $"{i}.5 MPa")),
            ],
        };
}
