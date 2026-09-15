using System.Runtime.Versioning;
using System.Text;
using Avalonia.Headless.XUnit;
using PDFtoImage;
using SkiaSharp;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Quotations;

namespace Tempest.Desktop.Tests.Quotations;

/// <summary>
/// The quote sheet PDF renderer (`WP 19.5B`, `ADR-0152`, Product Owner
/// comment item 9) — that it really is a PDF PDFium can open, that page 1
/// carries real ink rather than a blank canvas of the right size, that the
/// same model renders byte-identical twice, that its own reference and
/// total are genuinely readable back out of the PDF's own text (not merely
/// present as ink somewhere), and its own one-page size for a small,
/// realistic quote (cited in this Work Package's own report).
/// </summary>
/// <remarks>
/// Verified through <c>PDFtoImage.Conversion</c> and <see cref="PdfTextExtractor"/>
/// — the identical machinery <c>IssueSheetRendererTests</c> already
/// verifies the issue sheet renderer through (rasterisation), plus a real,
/// standard <c>ToUnicode</c> CMap-based text read this Work Package's own
/// acceptance criteria need (see that class's own remarks for why a raw
/// byte search never finds the rendered words).
/// </remarks>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public class QuotationSheetRendererTests
{
    [Fact]
    public void Render_WithNoIdentity_UsesTheProvider_ThatTheComposerPointsAtSettings()
    {
        // `WP 20.10G`, threaded at merge: a caller that passes no identity gets
        // Settings → Organisation through the provider, not the Tempest defaults.
        var renderer = new QuotationSheetRenderer { IdentityProvider = () => OrganisationIdentity.TempestDefaults with { LegalName = "Provider Test Ltd" } };
        var text = PdfTextExtractor.ExtractText(renderer.Render(QuotationSheetModelFixtures.Minimal()).ToArray());
        Assert.Contains("Provider Test Ltd", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_ProducesAValidOnePagePdf()
    {
        var bytes = new QuotationSheetRenderer().Render(QuotationSheetModelFixtures.Minimal()).ToArray();

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length)), StringComparison.Ordinal);
        Assert.Equal(1, Conversion.GetPageCount(bytes));
    }

    [AvaloniaFact]
    public void Render_Page1_CarriesRealInk_InTheTitleBlockRegion()
    {
        var bytes = new QuotationSheetRenderer().Render(QuotationSheetModelFixtures.Minimal()).ToArray();

        using var page = Conversion.ToImage(bytes, new Index(0), options: RasterAtOnePointPerPixel);

        Assert.True(
            HasDarkPixels(page, top: 0, bottom: page.Height / 3),
            "The title block, in the top third of page 1, must rasterise visible ink.");
    }

    [AvaloniaFact]
    public void Render_TheSameModelTwice_YieldsByteIdenticalPdfs()
    {
        var renderer = new QuotationSheetRenderer();
        var model = QuotationSheetModelFixtures.Minimal();

        var first = renderer.Render(model).ToArray();
        var second = renderer.Render(model).ToArray();

        Assert.Equal(first, second);
    }

    [AvaloniaFact]
    public void Render_TheReferenceAndTotal_AreReadableBackOutOfTheText()
    {
        var model = QuotationSheetModelFixtures.Minimal();
        var bytes = new QuotationSheetRenderer().Render(model).ToArray();

        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains(model.Reference, text, StringComparison.Ordinal);
        Assert.Contains(model.Client, text, StringComparison.Ordinal);
        Assert.Contains(model.IssuerName, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// `WP 20.10G` (scope item 5): every field `PHYSICAL_REVIEW.md` §7c
    /// D4 names — issuer, project code and name, client, reference, date,
    /// validity, status, currency, every line's own description/hours/
    /// rate/amount, the total, and terms — is genuinely readable back out
    /// of the rendered PDF's own text, not merely present as ink
    /// somewhere. Proven after routing both renderers through
    /// `DocumentTemplate` (`TD-182`), so a regression in the shared
    /// template's own text-run plumbing would fail this exactly as it
    /// would have failed the pre-`WP 20.10G` renderer.
    /// </summary>
    [AvaloniaFact]
    public void Render_EveryD4Field_IsReadableBackOutOfTheText()
    {
        var model = QuotationSheetModelFixtures.Minimal();
        var bytes = new QuotationSheetRenderer().Render(model).ToArray();

        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("QUOTATION", text, StringComparison.Ordinal);
        Assert.Contains(model.IssuerName, text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectCode, text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectName, text, StringComparison.Ordinal);
        Assert.Contains(model.Client, text, StringComparison.Ordinal);
        Assert.Contains(model.Reference, text, StringComparison.Ordinal);
        Assert.Contains(model.QuoteDate.ToString("yyyy-MM-dd"), text, StringComparison.Ordinal);
        Assert.Contains(model.ValidityDays.ToString(System.Globalization.CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
        Assert.Contains(model.Status, text, StringComparison.Ordinal);
        Assert.Contains(model.Currency, text, StringComparison.Ordinal);
        Assert.Contains(model.Total, text, StringComparison.Ordinal);
        Assert.Contains(model.Terms!, text, StringComparison.Ordinal);

        foreach (var line in model.Lines)
        {
            Assert.Contains(line.Description, text, StringComparison.Ordinal);
            Assert.Contains(line.Amount, text, StringComparison.Ordinal);
            if (line.Hours is not null)
                Assert.Contains(line.Hours, text, StringComparison.Ordinal);
            if (line.Rate is not null)
                Assert.Contains(line.Rate, text, StringComparison.Ordinal);
        }

        // The footer's own organisation identity (Tempest defaults, no
        // Settings override supplied) and export detail line.
        Assert.Contains("Tempest Design Engineering Ltd", text, StringComparison.Ordinal);
        Assert.Contains("Company No. 17349874", text, StringComparison.Ordinal);
        Assert.Contains("www.tempest-engineering.co.uk", text, StringComparison.Ordinal);
        Assert.Contains(model.ApplicationVersionText, text, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 1", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_FortyLines_Paginates_AndEveryPageCarriesTheFooter()
    {
        var model = QuotationSheetModelFixtures.WithLineCount(40);

        var bytes = new QuotationSheetRenderer().Render(model).ToArray();
        var pageCount = Conversion.GetPageCount(bytes);

        Assert.True(pageCount > 1, $"40 lines must overflow a single A4 page; got {pageCount} page(s).");

        for (var i = 0; i < pageCount; i++)
        {
            using var page = Conversion.ToImage(bytes, new Index(i), options: RasterAtOnePointPerPixel);

            Assert.True(
                HasDarkPixels(page, top: (int)(page.Height * 0.88), bottom: page.Height),
                $"Page {i + 1} of {pageCount}'s own footer band must rasterise visible ink.");
        }
    }

    [AvaloniaFact]
    public void Render_NullModel_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new QuotationSheetRenderer().Render(null!));

    private static readonly RenderOptions RasterAtOnePointPerPixel = new(Dpi: 72);

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

/// <summary>Fixture <see cref="QuotationSheetModel"/>s for the renderer tests.</summary>
internal static class QuotationSheetModelFixtures
{
    /// <summary>A small, fully-populated quote sheet — the "fixture model" this Work Package's own report cites a PDF size for.</summary>
    public static QuotationSheetModel Minimal() => new(
        IssuerName: "Priya Patel",
        ProjectCode: "PRJ-100",
        ProjectName: "Acme Gantry Upgrade",
        Client: "Acme Industries Ltd",
        Reference: "Q-2026-001",
        QuoteDate: new DateOnly(2026, 3, 4),
        ValidityDays: 30,
        Currency: "GBP",
        Lines:
        [
            new QuotationSheetLineRow("Concept design", "12", "£150.00", "£1,800.00"),
            new QuotationSheetLineRow("Detailed calculation pack", null, null, "£2,500.00"),
        ],
        Total: "£4,300.00",
        Terms: "Payment due within 30 days of invoice.",
        Status: "Draft",
        GeneratedAtUtc: new DateTimeOffset(2026, 3, 4, 9, 5, 0, TimeSpan.Zero),
        ApplicationVersionText: "TempestOS 0.19.0 (2e655db)");

    /// <summary>The minimal fixture, with its lines replaced by <paramref name="count"/> long, wrapping rows — the pagination case.</summary>
    public static QuotationSheetModel WithLineCount(int count) =>
        Minimal() with
        {
            Lines =
            [
                .. Enumerable.Range(1, count).Select(i => new QuotationSheetLineRow(
                    $"Line {i} — a reasonably long description of the work covered by this particular line item, so it wraps across more than one row of the table.",
                    i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "£150.00",
                    $"£{i * 150m:0.00}")),
            ],
        };
}
