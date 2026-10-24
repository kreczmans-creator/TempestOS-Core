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
    public void An_Issued_Sheet_Carries_Its_Reference_Diagram_Read_Against_Its_Inputs()
    {
        // The owner's ask: the annotated diagram goes into the export, so the sheet is not only text and maths.
        var beam = new Tempest.Calc.Structural.BeamFormulasCalculator();
        var inputs = CalcTemplateHasher.DefaultInputs(beam.Template).With(CalcInputValue.Quantity("L", 6, "m", CalcProvenance.Drawing("DRG-1")));
        var at = new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);
        var header = new CalcSheetHeader("P-1", "CALC-S01", beam.Template.Title, "A", CalcSheetStatus.Issued, "s.kreczman", "s.kreczman", at, at, at, "Client", "ISS-1");
        var footer = new CalcSheetFooter(beam.Template.Reference, "0123456789abcdef", "calc-engine/1.0.0", 1, 1, at);
        var sheet = CalcSheetBuilder.Build(beam.Template, inputs, CalcRunner.Run(beam, inputs), header, footer);

        var text = PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(sheet).ToArray());

        Assert.Contains("Figure 1 — Simply supported", text, StringComparison.Ordinal);
        Assert.Contains("L = 6 m", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("Figure 1", StringComparison.Ordinal) < text.IndexOf("2. Inputs", StringComparison.Ordinal));

        // AMA: the method's works are cited by superscript number and listed once, at the very end.
        Assert.Contains("Basis: ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ref: ", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("9. References", StringComparison.Ordinal) > text.IndexOf("8. Verification", StringComparison.Ordinal));
        Assert.Contains("1. Young WC, Budynas RG, Sadegh AM. Roark's Formulas for Stress and Strain.", text, StringComparison.Ordinal);

        // A sheet without its inputs (a pack cover, an archive) has no figure.
        Assert.DoesNotContain("Figure 1", PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(sheet with { InputValues = null }).ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public void A_Symbol_The_Mono_Face_Lacks_Is_Drawn_In_The_Body_Face_In_Place()
    {
        // Found by the showcase journey: "R_tot = Σ R_i" printed a box for Σ, which Space Mono has no glyph for.
        var texts = new List<Tempest.Desktop.Documents.DocumentTemplate.TextRun>
        {
            new(10, 20, "R_tot = Σ R_i", 9, false, SkiaSharp.SKTextAlign.Left, SkiaSharp.SKColors.Black, Tempest.Desktop.Documents.DocumentFontRole.Mono),
            new(10, 30, "= 2.5 K/W", 9, false, SkiaSharp.SKTextAlign.Left, SkiaSharp.SKColors.Black, Tempest.Desktop.Documents.DocumentFontRole.Mono),
        };

        Tempest.Desktop.Documents.DocumentTemplate.FallBackForMissingGlyphs(texts);

        Assert.Equal(["R_tot = ", "Σ", " R_i", "= 2.5 K/W"], texts.Select(t => t.Text));
        Assert.Equal(Tempest.Desktop.Documents.DocumentFontRole.Body, texts[1].FontRole);
        Assert.All(new[] { texts[0], texts[2], texts[3] }, t => Assert.Equal(Tempest.Desktop.Documents.DocumentFontRole.Mono, t.FontRole));
        Assert.True(texts[0].X < texts[1].X && texts[1].X < texts[2].X);
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
