using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.CalcSheets;

public sealed class CalcSheetPdfRendererTests
{
    private static CalcSheet Sheet(bool issued = true)
    {
        var calculator = new TestWorksheetCalculator();
        var inputs = new CalcInputSet(
        [
            CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Drawing("DRG-001", "B")),
            CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Drawing("DRG-001", "B")),
            CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Specification("SPEC-7")),
            CalcInputValue.Choice("grade", "S275", CalcProvenance.Assumed("Not stated.")),
        ]);
        var result = CalcRunner.Run(calculator, inputs);
        var report = KnownAnswerRunner.Run(calculator);
        var at = new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
        var header = new CalcSheetHeader("P-100 — Test", "CALC-001", calculator.Template.Title, "A", issued ? CalcSheetStatus.Issued : CalcSheetStatus.Draft, "s.kreczman", "j.reviewer", at, at, issued ? at : null, "Client Co", "ISS-1");
        var footer = new CalcSheetFooter(calculator.Template.Reference, "0123456789abcdef", "calc-engine/1.0.0", report.Passed, report.Total, at, "TempestOS 0.26.0");
        return CalcSheetBuilder.Build(calculator.Template, inputs, result, header, footer);
    }

    [Fact]
    public void Render_ProducesAPdfWellUnder200Kb_WithEverySectionAsText()
    {
        var bytes = new CalcSheetPdfRenderer().Render(Sheet()).ToArray();
        var artefactDir = Path.Combine(DesktopTestHelpers.RepositoryRoot, "TestResults", "calc-sheets");
        Directory.CreateDirectory(artefactDir);
        File.WriteAllBytes(Path.Combine(artefactDir, "sample-calc-sheet.pdf"), bytes);

        Assert.True(bytes.Length > 1_000, "the PDF is empty");
        Assert.True(bytes.Length < 200 * 1024, $"the sheet is {bytes.Length / 1024} KB; the gate is 200 KB");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        var text = PdfTextExtractor.ExtractText(bytes);
        foreach (var marker in new[] { "CALCULATION SHEET", "ISSUED", "Scope and method", "Inputs", "Assumptions", "Working", "Results", "Checks", "Applicability", "Verification", "sigma = F / A", "60 MPa", "PASS", "calc-engine/1.0.0", "DRG-001" })
            Assert.Contains(marker, text, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderPack_PutsTheCoverFirst_ThenEverySheet()
    {
        var cover = Sheet() with
        {
            PackIndex = [new CalcPackIndexRow(1, "CALC-001", "Desktop stress check", "A", "ISSUED", "Direct stress PASS")],
            Revisions = [new CalcRevisionRow("A", new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero), "s.kreczman", "First issue")],
        };
        var bytes = new CalcSheetPdfRenderer().RenderPack(cover, [Sheet(), Sheet()]).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);
        Assert.True(bytes.Length < 400 * 1024, $"the pack is {bytes.Length / 1024} KB");
        Assert.Contains("Pack index", text, StringComparison.Ordinal);
        Assert.Contains("Revisions", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("Pack index", StringComparison.Ordinal) < text.IndexOf("sigma = F / A", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_NeverInterpretsAHostileString()
    {
        var hostile = Sheet() with { Header = Sheet().Header with { Title = "<script>alert(1)</script> %PDF-1.7 \n endobj" } };
        var bytes = new CalcSheetPdfRenderer().Render(hostile).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);
        Assert.Contains("<script>alert(1)</script>", text, StringComparison.Ordinal);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
