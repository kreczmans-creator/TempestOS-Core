using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>
/// Found by the C-7 soak: the inputs CSV is capped at <see cref="CalcCsv.MaxRows"/> rows, but the cap was checked only as each line ended, so a
/// file of one row too many whose last line has no line break after it was read in full. The cap is the same with or without a final line break.
/// </summary>
public sealed class CsvRowCapTests
{
    private static readonly DirectStressCalculator Calculator = new();

    /// <summary>A header and <paramref name="dataRows"/> rows of the same input.</summary>
    private static string File(int dataRows, bool finalLineBreak)
    {
        var text = "symbol,value\n" + string.Join("\n", Enumerable.Repeat("F,1", dataRows));
        return finalLineBreak ? text + "\n" : text;
    }

    private static bool Refused(IReadOnlyList<CalcNotice> notices) =>
        notices.Any(n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("more than", StringComparison.Ordinal));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_File_Of_Exactly_The_Cap_Is_Read(bool finalLineBreak)
    {
        // The header counts: a header and 9,999 rows are 10,000 rows.
        var (inputs, notices) = CalcCsv.ImportInputs(Calculator.Template, File(CalcCsv.MaxRows - 1, finalLineBreak));

        Assert.False(Refused(notices), string.Join("; ", notices.Select(n => n.Message)));
        Assert.Equal(1, inputs.Find("F")!.Number);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_File_One_Row_Over_The_Cap_Is_Refused_With_Or_Without_A_Final_Line_Break(bool finalLineBreak)
    {
        var (_, notices) = CalcCsv.ImportInputs(Calculator.Template, File(CalcCsv.MaxRows, finalLineBreak));

        Assert.True(Refused(notices), $"A header and {CalcCsv.MaxRows:N0} rows ({CalcCsv.MaxRows + 1:N0} rows, the cap is {CalcCsv.MaxRows:N0}) {(finalLineBreak ? "with" : "without")} a final line break was not refused.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    public void The_Row_That_Goes_Over_The_Cap_Is_Refused_And_Not_Read(string ending)
    {
        // Row 10,001 is the first past the cap; it sets a different input, so reading it would show.
        var (inputs, notices) = CalcCsv.ImportInputs(Calculator.Template, File(CalcCsv.MaxRows - 1, finalLineBreak: true) + "grade,S355" + ending);

        Assert.True(Refused(notices));
        Assert.Null(inputs.Find("grade"));
        Assert.Equal(1, inputs.Find("F")!.Number);
    }

    [Fact]
    public void The_Refusal_Names_The_Cap_And_Keeps_What_Was_Read_Before_It()
    {
        var (inputs, notices) = CalcCsv.ImportInputs(Calculator.Template, File(CalcCsv.MaxRows + 50, finalLineBreak: false));

        Assert.Contains(notices, n => n.Message.Contains($"more than {CalcCsv.MaxRows} rows", StringComparison.Ordinal));
        Assert.Equal(1, inputs.Find("F")!.Number);
    }
}
