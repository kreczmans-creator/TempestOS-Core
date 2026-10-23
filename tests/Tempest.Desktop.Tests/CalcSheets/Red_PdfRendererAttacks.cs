using System.Diagnostics;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// Red team (offensive review): hostile strings and oversized text reaching
/// the PDF renderer. Every test asserts the secure behaviour: no exception,
/// no control character in the extracted text, a bounded file, and a
/// bounded render time.
/// </summary>
public sealed class Red_PdfRendererAttacks
{
    private static CalcSheet Sheet(string hostile, int referenceLength)
    {
        var calculator = new TestWorksheetCalculator();
        var inputs = new CalcInputSet(
        [
            CalcInputValue.Quantity("F", 12, "kN", new CalcProvenance(CalcProvenanceKind.Drawing, new string('R', referenceLength), hostile.Length <= CalcProvenance.MaxRevisionLength ? hostile : hostile[..CalcProvenance.MaxRevisionLength])), // the revision cap (Blue, R-05) would otherwise block the run before the sheet is laid out
            CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Assumed(hostile)),
            CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Specification(hostile)),
            CalcInputValue.Choice("grade", "S275", CalcProvenance.Assumed(hostile)),
        ]);
        var result = CalcRunner.Run(calculator, inputs);
        var at = new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
        var header = new CalcSheetHeader(hostile, hostile, hostile, hostile, CalcSheetStatus.Issued, hostile, hostile, at, at, at, hostile, hostile);
        var footer = new CalcSheetFooter(hostile, hostile, hostile, 1, 1, at, hostile);
        return CalcSheetBuilder.Build(calculator.Template, inputs, result, header, footer, [new CalcRevisionRow(hostile, at, hostile, hostile)]);
    }

    /// <summary>PDF syntax, CSV/DDE and script payloads and bidi overrides in every string of the sheet are drawn as text (no issue); control characters are not stripped (R-24).</summary>
    [Theory]
    [InlineData("=cmd()|' /C calc'!A0 <script>alert(1)</script>")]
    [InlineData("line1\r\nline2\t\u0000\u001b[31m\u2028forged")]
    [InlineData("\u202Ereversed \u200B\u200F𝔘𝔫𝔦𝔠𝔬𝔡𝔢")]
    [InlineData(") Tj ET endstream endobj %PDF-1.7 << /JavaScript (app.alert(1)) >>")]
    [InlineData("((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((")]
    public void Hostile_Strings_Are_Drawn_As_Text(string hostile)
    {
        var sw = Stopwatch.StartNew();
        var bytes = new CalcSheetPdfRenderer().Render(Sheet(hostile, 10)).ToArray();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"render took {sw.Elapsed.TotalSeconds:F1} s");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length < 400 * 1024, $"the sheet is {bytes.Length / 1024} KB");
        var text = PdfTextExtractor.ExtractText(bytes);

        // The document structure survives the payload (a string that broke out of its literal would corrupt the sections after it).
        foreach (var marker in new[] { "Scope and method", "Inputs", "Working", "Verification", "60 MPa" })
            Assert.True(text.Contains(marker, StringComparison.Ordinal), $"'{marker}' is missing from the extracted text: {text[..Math.Min(400, text.Length)]}");

        // R-24: the PDF renderer draws the raw string; the text renderer's Clean is not applied, so control characters land in the PDF text.
        Assert.DoesNotContain("\0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b", text, StringComparison.Ordinal);
    }

    /// <summary>R-05 (PDF form): a 200 KB provenance reference and a 200 KB title must not blow the page count, the file size or the render time.</summary>
    [Fact]
    public void Oversized_Text_Is_Bounded_In_The_Pdf()
    {
        var sw = Stopwatch.StartNew();
        var bytes = new CalcSheetPdfRenderer().Render(Sheet(new string('T', 200_000), 200_000)).ToArray();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"render took {sw.Elapsed.TotalSeconds:F1} s");
        Assert.True(bytes.Length < 400 * 1024, $"the sheet is {bytes.Length / 1024} KB");
    }

    /// <summary>Tried: a pack of fifty hostile sheets renders in budget.</summary>
    [Fact]
    public void Hostile_Pack_Renders_In_Budget()
    {
        var cover = Sheet("cover", 10) with
        {
            PackIndex = Enumerable.Range(1, 50).Select(i => new CalcPackIndexRow(i, "\u0000=cmd()", new string('x', 1_000), "A", "ISSUED", "\r\nPASS")).ToList(),
            Revisions = Enumerable.Range(1, 50).Select(i => new CalcRevisionRow("A", DateTimeOffset.UnixEpoch, "\u001b", new string('r', 1_000))).ToList(),
        };
        var sw = Stopwatch.StartNew();
        var bytes = new CalcSheetPdfRenderer().RenderPack(cover, Enumerable.Range(0, 50).Select(_ => Sheet("=cmd()\r\n", 1_000)).ToList()).ToArray();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"render took {sw.Elapsed.TotalSeconds:F1} s");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
