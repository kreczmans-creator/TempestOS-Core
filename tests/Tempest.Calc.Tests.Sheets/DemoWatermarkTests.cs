using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>Addendum C, C-6: a sheet whose header carries a watermark prints it on its own line; a sheet whose header carries none is unchanged.</summary>
public sealed class DemoWatermarkTests
{
    private static readonly DirectStressCalculator Calculator = new();

    private static CalcSheet Sheet(string? watermark)
    {
        var inputs = new CalcInputSet(
        [
            CalcInputValue.Quantity("F", 12, "kN", CalcProvenance.Drawing("DRG-001", "B")),
            CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Drawing("DRG-001", "B")),
            CalcInputValue.Quantity("sigma_allow", 160, "MPa", CalcProvenance.Specification("SPEC-7")),
            CalcInputValue.Choice("grade", "S275", CalcProvenance.Assumed("Not stated.")),
        ]);
        var at = new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);
        var header = new CalcSheetHeader("P-001 — Test project", "CALC-001", "Direct stress check", "A", CalcSheetStatus.Draft, "s.kreczman", null, at, null, null, Watermark: watermark);
        var footer = new CalcSheetFooter("test.direct-stress@1.0.0", "abcdef0123456789", "calc-engine/1.0.0", 1, 2, at);
        return CalcSheetBuilder.Build(Calculator.Template, inputs, CalcRunner.Run(Calculator, inputs), header, footer);
    }

    [Fact]
    public void A_Watermark_Is_Printed_On_The_Second_Line_Before_The_Project()
    {
        var lines = CalcSheetTextRenderer.Render(Sheet("DEMO — invented data")).Split('\n');

        Assert.Equal("CALCULATION SHEET — DRAFT", lines[0].TrimEnd('\r'));
        Assert.Equal("*** DEMO — invented data ***", lines[1].TrimEnd('\r'));
        Assert.StartsWith("Project: P-001", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void A_Sheet_With_No_Watermark_Is_Exactly_What_It_Was_Before()
    {
        var plain = CalcSheetTextRenderer.Render(Sheet(null));

        Assert.DoesNotContain("***", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("DEMO", plain, StringComparison.Ordinal);
        Assert.Equal(plain.Replace("*** DEMO — invented data ***\n", string.Empty, StringComparison.Ordinal), CalcSheetTextRenderer.Render(Sheet("DEMO — invented data")).Replace("*** DEMO — invented data ***\n", string.Empty, StringComparison.Ordinal));
        Assert.Null(Sheet(null).Header.Watermark);
    }

    [Fact]
    public void A_Hostile_Watermark_Is_Cleaned_Like_Every_Other_Header_Text()
    {
        var text = CalcSheetTextRenderer.Render(Sheet("DEMO\r\n<script>alert(1)</script>\u0007"));
        var line = text.Split('\n')[1].TrimEnd('\r');

        Assert.StartsWith("*** DEMO", line, StringComparison.Ordinal);
        Assert.EndsWith(" ***", line, StringComparison.Ordinal);
        Assert.DoesNotContain('\u0007', text);
    }
}
