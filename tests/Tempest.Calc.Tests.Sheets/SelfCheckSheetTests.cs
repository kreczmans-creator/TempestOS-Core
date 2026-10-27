using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>DA-12: a sheet checked by its preparer says so, in the owner's words, under the Checker line; a sheet checked by someone else does not.</summary>
public sealed class SelfCheckSheetTests
{
    private static readonly DirectStressCalculator Calculator = new();

    private static CalcSheet Sheet(string? selfCheck)
    {
        var inputs = new CalcInputSet(
        [
            CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Drawing("DRG-001", "B")),
            CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Drawing("DRG-001", "B")),
            CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Specification("SPEC-7")),
            CalcInputValue.Choice("grade", "S275", CalcProvenance.Assumed("Not stated.")),
            CalcInputValue.Switch("slender", false, CalcProvenance.Assumed("Stocky.")),
        ]);
        var result = CalcRunner.Run(Calculator, inputs);
        var at = new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);
        var header = new CalcSheetHeader("P-001 — Test project", "CALC-001", "Direct stress check", "A", CalcSheetStatus.Issued, "s.kreczman", "s.kreczman", at, at, at, "Client Co", "ISS-1", null, selfCheck);
        var footer = new CalcSheetFooter("test.direct-stress@1.0.0", "abcdef0123456789", "calc-engine/1.0.0", 1, 2, at);
        return CalcSheetBuilder.Build(Calculator.Template, inputs, result, header, footer);
    }

    [Fact]
    public void A_Self_Checked_Sheet_Prints_The_Statement_Under_The_Checker_Line()
    {
        const string statement = "No second engineer was available; I re-derived the stress by hand and it agrees.";
        var text = CalcSheetTextRenderer.Render(Sheet(statement));
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        var checker = lines.FindIndex(l => l.StartsWith("Checker: ", StringComparison.Ordinal));
        Assert.True(checker >= 0, "no Checker line");
        Assert.Equal(CalcSheetStandard.SelfCheckPrefix + statement, lines[checker + 1]);
        Assert.StartsWith("Issued: ", lines[checker + 2], StringComparison.Ordinal);
    }

    [Fact]
    public void A_Statement_Is_Printed_Clean_On_One_Line()
    {
        var text = CalcSheetTextRenderer.Render(Sheet("line one\r\nline two\tand a \u0007 control character"));
        // Every control character becomes one space (R-08), so the line stays one line.
        Assert.Contains(CalcSheetStandard.SelfCheckPrefix + "line one  line two and a   control character", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_Sheet_Checked_By_Someone_Else_Prints_No_Self_Check_Line(string? selfCheck)
    {
        var text = CalcSheetTextRenderer.Render(Sheet(selfCheck));
        Assert.DoesNotContain(CalcSheetStandard.SelfCheckPrefix, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Self-checked", text, StringComparison.Ordinal);
    }
}
