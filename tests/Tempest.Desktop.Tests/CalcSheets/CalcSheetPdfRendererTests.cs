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
        foreach (var marker in new[] { "CALCULATION SHEET", "ISSUED", "Scope and method", "Inputs", "Assumptions", "Working", "Results", "Checks", "Applicability", "Verification", "σ = F / A", "60 MPa", "PASS", "calc-engine/1.0.0", "DRG-001" })
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

        // The method reads as steps, each saying what it calculates beside its relation.
        Assert.Contains("WHAT IT CALCULATES", text, StringComparison.Ordinal);
        Assert.Contains("Simply supported beam, point load at a", text, StringComparison.Ordinal);
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
        Assert.True(text.IndexOf("Pack index", StringComparison.Ordinal) < text.IndexOf("σ = F / A", StringComparison.Ordinal));
    }

    private static string Collapse(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

    private static CalcSheet Empty() => new(
        GoldenFiles.FixedHeader("x", "Empty"), string.Empty, string.Empty, [], [], [], [], [], [], [], [],
        new CalcSheetFooter("x@1.0.0", "0000000000000000", "engine", 0, 0, null));

    [Fact]
    public void The_Pdf_Prints_The_Same_Headings_In_The_Same_Order_As_The_Text_Sheet()
    {
        // C-2, row 1: one heading wording, one order, and every section printed even when it is empty.
        var text = Collapse(PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(Empty()).ToArray()));
        var last = -1;

        foreach (var heading in CalcSheetStandard.Headings)
        {
            var at = text.IndexOf(heading, StringComparison.Ordinal);
            Assert.True(at > last, $"'{heading}' is missing or out of order in the PDF");
            last = at;
        }

        // Inputs, assumptions, working, results, checks, applicability and references each say "None." when they are empty.
        Assert.True(System.Text.RegularExpressions.Regex.Matches(text, "None\\.").Count >= 7, text);
    }

    [Fact]
    public void The_Pdf_Prints_A_Notice_In_The_Text_Sheets_Form_And_Lists_The_Defaults_Apart()
    {
        // F is given and the rest default: the defaults are listed apart from the warnings, in plain words with unit symbols.
        var calculator = new TestWorksheetCalculator();
        var inputs = new CalcInputSet([CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Drawing("DRG-001", "B"))]);
        var at = new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
        var sheet = CalcSheetBuilder.Build(calculator.Template, inputs, CalcRunner.Run(calculator, inputs), GoldenFiles.FixedHeader("x", "x") with { Status = CalcSheetStatus.Draft }, new CalcSheetFooter(calculator.Template.Reference, "0123456789abcdef", "engine", 1, 1, at));
        Assert.NotEmpty(sheet.DefaultNotices);
        Assert.DoesNotContain(sheet.Warnings, w => w.IsDefaultUsed);

        var pdf = Collapse(PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(sheet).ToArray()));
        var textSheet = Collapse(CalcSheetTextRenderer.Render(sheet));

        Assert.Contains("Defaults used (inputs left empty):", pdf, StringComparison.Ordinal);
        Assert.Contains("A: not entered, so the default of 200 mm² is used.", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("template default", pdf, StringComparison.Ordinal);

        // Every notice line of the text sheet is in the PDF, word for word (the PDF folds runs of spaces).
        foreach (var notice in sheet.Warnings.Concat(sheet.Blocks))
            Assert.Contains(Collapse(CalcSheetStandard.NoticeLine(notice)), pdf, StringComparison.Ordinal);

        Assert.Contains("Defaults used (inputs left empty): A: not entered", textSheet, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Pdf_Lists_The_Headline_Results_First_Marks_The_Governing_One_And_Gives_A_Yes_No_Check_No_Utilisation()
    {
        var calculator = CalcCalculatorComposition.All().Single(c => c.Template.Id == "mech.bearing-life");
        var inputs = CalcTemplateHasher.DefaultInputs(calculator.Template);
        var at = new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
        var sheet = CalcSheetBuilder.Build(calculator.Template, inputs, CalcRunner.Run(calculator, inputs), GoldenFiles.FixedHeader("x", "x"), new CalcSheetFooter(calculator.Template.Reference, "0123456789abcdef", "engine", 1, 1, at));
        var pdf = Collapse(PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(sheet).ToArray()));

        Assert.Equal("L_nmh", sheet.Outputs[0].Symbol);
        var results = pdf.Split("5. Results")[1].Split("6. Checks")[0];
        Assert.True(results.IndexOf("Modified rating life", StringComparison.Ordinal) < results.IndexOf("Reliability factor", StringComparison.Ordinal), results);
        Assert.Contains("(governing)", pdf, StringComparison.Ordinal);
        Assert.Contains("0.94", pdf, StringComparison.Ordinal);

        var yesNo = new CalcCheck("Fit classified", "clearance", "clearance", 0, true, HasUtilisation: false);
        Assert.Equal("—", yesNo.UtilisationText);
        var withYesNo = Sheet() with { Checks = [yesNo] };
        Assert.DoesNotContain("0.00", Collapse(PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(withYesNo).ToArray())).Split("6. Checks")[1].Split("7. Applicability")[0], StringComparison.Ordinal);
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
