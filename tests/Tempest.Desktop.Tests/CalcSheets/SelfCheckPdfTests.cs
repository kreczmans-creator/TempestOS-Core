using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>DA-12 in the PDF: the preparer's statement is printed whole, up to the 500 characters the service accepts, under the checker line; a sheet checked by someone else prints no such line.</summary>
public sealed class SelfCheckPdfTests
{
    private static CalcSheet Sheet(string? selfCheck)
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
        var at = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
        var header = new CalcSheetHeader("P-100 — Test", "CALC-001", calculator.Template.Title, "A", CalcSheetStatus.Issued, "s.kreczman", "s.kreczman", at, at, at, "Client Co", "ISS-1", null, selfCheck);
        var footer = new CalcSheetFooter(calculator.Template.Reference, "0123456789abcdef", "calc-engine/1.0.0", report.Passed, report.Total, at, "TempestOS 0.26.0");
        return CalcSheetBuilder.Build(calculator.Template, inputs, result, header, footer);
    }

    [Fact]
    public void The_Statement_Is_Printed_Whole_Even_At_The_500_Character_Cap()
    {
        // Nineteen distinct 26-character sentences (492 characters): the last ones are past the renderer's 400-character field cap and must still be
        // printed. The extractor joins the PDF's wrapped lines without a space, so each sentence is one unbreakable token.
        var statement = string.Concat(Enumerable.Range(0, 19).Select(i => $"Sentence_{i:00}_stands_here. ")).TrimEnd();
        Assert.InRange(statement.Length, 450, CalcSheetStandard.MaxSelfCheckLength);

        var text = PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(Sheet(statement)).ToArray());
        Assert.Contains("Self-checked by the preparer: Sentence_00_stands_here.", text, StringComparison.Ordinal);
        Assert.True(text.Contains("Sentence_18_stands_here.", StringComparison.Ordinal), "the end of the statement is missing from: " + text.Split("Issue:")[0]);
        Assert.DoesNotContain("…", text.Split("Issue:")[0].Split("Self-checked")[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_Sheet_Checked_By_Someone_Else_Prints_No_Self_Check_Line()
    {
        var text = PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(Sheet(null)).ToArray());
        Assert.DoesNotContain("Self-checked", text, StringComparison.Ordinal);
    }
}
