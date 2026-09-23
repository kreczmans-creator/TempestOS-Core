using System.Runtime.Versioning;
using System.Text;
using Avalonia.Headless.XUnit;
using PDFtoImage;
using SkiaSharp;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Documents.Invoicing;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.Documents;

/// <summary>
/// The invoice document renderer (`WP 21.2A`, scope item 2) — a real PDF,
/// real ink on page 1, byte-identical across two renders of the same
/// model, every `PHYSICAL_REVIEW.md`-worthy field genuinely readable back
/// out of the PDF's own text, and pagination for a long invoice — the
/// identical acceptance shape <c>QuotationSheetRendererTests</c> already
/// establishes for the quote sheet.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class InvoiceDocumentRendererTests
{
    [Fact]
    public void DocumentType_And_TemplateName_MatchTheDesignSystemMapping()
    {
        var renderer = new InvoiceDocumentRenderer();
        Assert.Equal("INVOICE", renderer.DocumentType);
        Assert.Equal("invoice", renderer.TemplateName);
    }

    [AvaloniaFact]
    public void Render_ProducesAValidOnePagePdf()
    {
        var bytes = new InvoiceDocumentRenderer().Render(InvoiceDocumentModelFixtures.Minimal()).ToArray();

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length)), StringComparison.Ordinal);
        Assert.Equal(1, Conversion.GetPageCount(bytes));
    }

    [AvaloniaFact]
    public void Render_TheSameModelTwice_YieldsByteIdenticalPdfs()
    {
        var renderer = new InvoiceDocumentRenderer();
        var model = InvoiceDocumentModelFixtures.Minimal();

        Assert.Equal(renderer.Render(model).ToArray(), renderer.Render(model).ToArray());
    }

    [AvaloniaFact]
    public void Render_EveryFieldTheInvoiceNames_IsReadableBackOutOfTheText()
    {
        var model = InvoiceDocumentModelFixtures.Minimal();
        var identity = OrganisationIdentity.TempestDefaults with
        {
            BankSortCode = "12-34-56", BankAccountNumber = "12345678", BankAccountName = "Tempest Design Engineering Ltd", BankIban = "GB00TEST00000000000000",
        };
        var bytes = new InvoiceDocumentRenderer().Render(model, identity).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("INVOICE", text, StringComparison.Ordinal);
        Assert.Contains(model.IssuerName, text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectCode, text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectName, text, StringComparison.Ordinal);
        Assert.Contains(model.Client, text, StringComparison.Ordinal);
        Assert.Contains(model.Reference, text, StringComparison.Ordinal);
        Assert.Contains(model.PurchaseOrderReference!, text, StringComparison.Ordinal);
        Assert.Contains(model.PaymentTermsDisplay, text, StringComparison.Ordinal);
        Assert.Contains(model.Status, text, StringComparison.Ordinal);
        Assert.Contains(model.Currency, text, StringComparison.Ordinal);
        Assert.Contains(model.Total, text, StringComparison.Ordinal);

        foreach (var line in model.Lines)
        {
            Assert.Contains(line.Description, text, StringComparison.Ordinal);
            Assert.Contains(line.Amount, text, StringComparison.Ordinal);
        }

        // The "Payment details" section — Settings -> Organisation's own bank fields (`WP 21.2A`).
        Assert.Contains(identity.BankSortCode!, text, StringComparison.Ordinal);
        Assert.Contains(identity.BankAccountNumber!, text, StringComparison.Ordinal);
        Assert.Contains(identity.BankIban!, text, StringComparison.Ordinal);

        // The footer.
        Assert.Contains("Tempest Design Engineering Ltd", text, StringComparison.Ordinal);
        Assert.Contains(model.ApplicationVersionText, text, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 1", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_NoBankDetailsRecorded_SaysSoRatherThanDrawingAnEmptySection()
    {
        var bytes = new InvoiceDocumentRenderer().Render(InvoiceDocumentModelFixtures.Minimal(), OrganisationIdentity.TempestDefaults).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("No bank details recorded", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_SixtyLines_Paginates_AndEveryPageCarriesTheFooter()
    {
        var model = InvoiceDocumentModelFixtures.WithLineCount(60);

        var bytes = new InvoiceDocumentRenderer().Render(model).ToArray();
        var pageCount = Conversion.GetPageCount(bytes);

        Assert.True(pageCount > 1, $"60 lines must overflow a single A4 page; got {pageCount} page(s).");

        for (var i = 0; i < pageCount; i++)
        {
            using var page = Conversion.ToImage(bytes, new Index(i), options: new RenderOptions(Dpi: 72));
            Assert.True(HasDarkPixels(page, top: (int)(page.Height * 0.88), bottom: page.Height), $"Page {i + 1} of {pageCount}'s own footer band must rasterise visible ink.");
        }

        var text = PdfTextExtractor.ExtractText(bytes);
        Assert.Contains($"Page {pageCount} of {pageCount}", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_NullModel_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new InvoiceDocumentRenderer().Render(null!));

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

/// <summary>Fixture <see cref="InvoiceDocumentModel"/>s for the renderer tests.</summary>
internal static class InvoiceDocumentModelFixtures
{
    public static InvoiceDocumentModel Minimal() => new(
        IssuerName: "Priya Patel",
        ProjectCode: "PRJ-100",
        ProjectName: "Acme Gantry Upgrade",
        Client: "Acme Industries Ltd",
        Reference: "INV-2026-001",
        PurchaseOrderReference: "PO-4471",
        IssueDate: new DateOnly(2026, 3, 4),
        DueDate: new DateOnly(2026, 4, 3),
        PaymentTermsDisplay: "30 days",
        Currency: "GBP",
        Lines:
        [
            new InvoiceDocumentLineRow("Concept design", "12", "£150.00", "£1,800.00"),
            new InvoiceDocumentLineRow("Detailed calculation pack", null, null, "£2,500.00"),
        ],
        Total: "£4,300.00",
        Status: "Sent",
        GeneratedAtUtc: new DateTimeOffset(2026, 3, 4, 9, 5, 0, TimeSpan.Zero),
        ApplicationVersionText: "TempestOS 0.21.0 (2e655db)");

    public static InvoiceDocumentModel WithLineCount(int count) =>
        Minimal() with
        {
            Lines =
            [
                .. Enumerable.Range(1, count).Select(i => new InvoiceDocumentLineRow(
                    $"Line {i} — a reasonably long description of the work covered by this particular line item, so it wraps across more than one row of the table.",
                    i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "£150.00",
                    $"£{i * 150m:0.00}")),
            ],
        };
}
