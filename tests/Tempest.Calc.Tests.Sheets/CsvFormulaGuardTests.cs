using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>Security review of W1, L7: the worksheet CSV export and the import writer share one definition of "a spreadsheet would run this".</summary>
public sealed class CsvFormulaGuardTests
{
    private static readonly CalcTemplate Template = new(
        "test.csv-guard", "1.0.0", CalcDiscipline.Mechanical, "Guard", "scope", "method",
        [new CalcInputSpec("note", "Note", CalcInputKind.Text, IsOptional: true)],
        [], [], [], []);

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+cmd|' /C calc'!A0")]
    [InlineData("@SUM(1)")]
    [InlineData("\t=1")]
    [InlineData("\r=1")]
    [InlineData("\n=1+1")]
    [InlineData("  =1+1")]
    [InlineData(" \u00A0 =1+1")]
    [InlineData("＝1+1")]
    [InlineData("＋cmd")]
    [InlineData("＠SUM(1)")]
    [InlineData("-2+3+cmd|' /C calc'!A0")]
    public void A_Text_Whose_First_Visible_Character_Starts_A_Formula_Is_Defused_On_Export(string text)
    {
        Assert.True(CalcCsv.IsFormulaLike(text));
        var csv = CalcCsv.ExportInputs(Template, new CalcInputSet([CalcInputValue.Choice("note", text)]));
        Assert.Contains("'" + text, csv, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("-1.5e3")]
    [InlineData(" -5")]
    [InlineData("plain text")]
    [InlineData("a=b")]
    [InlineData("")]
    [InlineData("  ")]
    public void A_Plain_Signed_Number_Or_An_Ordinary_Text_Is_Not_Defused(string text) => Assert.False(CalcCsv.IsFormulaLike(text));

    [Fact]
    public void The_Guard_And_The_Export_Agree()
    {
        foreach (var text in new[] { "=1", " =1", "\n=1", "＝1", "x" })
            Assert.Equal(CsvFormulaGuard.StartsLikeFormula(text), CalcCsv.IsFormulaLike(text));
    }
}
