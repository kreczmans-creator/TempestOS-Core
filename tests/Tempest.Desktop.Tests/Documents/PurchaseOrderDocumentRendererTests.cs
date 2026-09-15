using System.Runtime.Versioning;
using System.Text;
using Avalonia.Headless.XUnit;
using PDFtoImage;
using Tempest.Desktop.Documents.PurchaseOrders;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.Documents;

/// <summary>
/// The purchase order document renderer (`WP 21.2A`, scope item 2) — a
/// model-less renderer against a fixture, per this Work Package's own
/// brief and kill switch (see <see cref="PurchaseOrderDocumentModel"/>'s
/// own remarks): real and exercised even though no live "Export PO" button
/// exists anywhere in the shell.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class PurchaseOrderDocumentRendererTests
{
    [Fact]
    public void DocumentType_And_TemplateName_MatchTheDesignSystemMapping()
    {
        var renderer = new PurchaseOrderDocumentRenderer();
        Assert.Equal("PURCHASE ORDER", renderer.DocumentType);
        Assert.Equal("purchase-order", renderer.TemplateName);
    }

    [AvaloniaFact]
    public void Render_ProducesAValidOnePagePdf()
    {
        var bytes = new PurchaseOrderDocumentRenderer().Render(PurchaseOrderDocumentModelFixtures.Minimal()).ToArray();

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length)), StringComparison.Ordinal);
        Assert.Equal(1, Conversion.GetPageCount(bytes));
    }

    [AvaloniaFact]
    public void Render_EveryFieldTheOrderNames_IsReadableBackOutOfTheText()
    {
        var model = PurchaseOrderDocumentModelFixtures.Minimal();
        var bytes = new PurchaseOrderDocumentRenderer().Render(model).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("PURCHASE ORDER", text, StringComparison.Ordinal);
        Assert.Contains(model.IssuerName, text, StringComparison.Ordinal);
        Assert.Contains(model.ProjectCode, text, StringComparison.Ordinal);
        Assert.Contains(model.SupplierName, text, StringComparison.Ordinal);
        Assert.Contains(model.DeliveryAddress!, text, StringComparison.Ordinal);
        Assert.Contains(model.Reference, text, StringComparison.Ordinal);
        Assert.Contains(model.Total, text, StringComparison.Ordinal);
        Assert.Contains(model.Conditions!, text, StringComparison.Ordinal);

        foreach (var line in model.Lines)
        {
            Assert.Contains(line.Description, text, StringComparison.Ordinal);
            Assert.Contains(line.Amount, text, StringComparison.Ordinal);
        }

        Assert.Contains("Page 1 of 1", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Render_NullModel_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new PurchaseOrderDocumentRenderer().Render(null!));
}

/// <summary>Fixture <see cref="PurchaseOrderDocumentModel"/>s for the renderer tests.</summary>
internal static class PurchaseOrderDocumentModelFixtures
{
    public static PurchaseOrderDocumentModel Minimal() => new(
        IssuerName: "Priya Patel",
        ProjectCode: "PRJ-100",
        ProjectName: "Acme Gantry Upgrade",
        Reference: "PO-2026-001",
        Date: new DateOnly(2026, 3, 4),
        SupplierName: "Northbridge Fabrications Ltd",
        DeliveryAddress: "Unit 4, Riverside Industrial Estate, Leeds LS1 4AB",
        Currency: "GBP",
        Lines: [new PurchaseOrderDocumentLineRow("25mm bright mild steel bar, 6m length", "10", "£45.00", "£450.00")],
        Total: "£450.00",
        Conditions: "Delivery within 10 working days. Goods subject to inspection on receipt.",
        Status: "Draft",
        GeneratedAtUtc: new DateTimeOffset(2026, 3, 4, 9, 5, 0, TimeSpan.Zero),
        ApplicationVersionText: "TempestOS 0.21.0 (2e655db)");
}
