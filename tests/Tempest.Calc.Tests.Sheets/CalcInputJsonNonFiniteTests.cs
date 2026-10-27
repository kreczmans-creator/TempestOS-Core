using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>
/// Shot-fix A, item 1: the worksheet form can hold a number that is not finite (an empty or mistyped field), and the Guided test writes the form
/// with <see cref="CalcInputJson.Write"/>. JSON has no NaN, so the writer used to throw on the UI thread. It now writes such a number as absent.
/// </summary>
public sealed class CalcInputJsonNonFiniteTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_Non_Finite_Number_Is_Written_As_Absent_And_Reads_Back_Without_It(double bad)
    {
        var set = new CalcInputSet(
        [
            CalcInputValue.Quantity("f_max", bad, "mm", CalcProvenance.Manual),
            CalcInputValue.Quantity("L", 2.5, "m", CalcProvenance.Assumed("site")),
            CalcInputValue.Choice("kind", "a"),
        ]);

        var json = CalcInputJson.Write(set);
        var (read, notices) = CalcInputJson.Read(json);

        Assert.Empty(notices);
        Assert.Null(read.Find("f_max"));
        Assert.Equal(2.5, read.Find("L")!.Number);
        Assert.Equal("a", read.Find("kind")!.Text);
    }

    [Fact]
    public void An_Empty_Number_Is_Not_Written_And_The_Document_Still_Reads()
    {
        // The form holds an empty required number as a value with a unit and a source but no number.
        var set = new CalcInputSet([new CalcInputValue("S_e", Unit: "MPa", Provenance: CalcProvenance.Manual), CalcInputValue.Of("n", 3)]);

        var (read, notices) = CalcInputJson.Read(CalcInputJson.Write(set));

        Assert.Empty(notices);
        Assert.Null(read.Find("S_e"));
        Assert.Equal(3, read.Find("n")!.Number);
    }

    [Fact]
    public void A_Vector_Or_A_Cell_With_A_Non_Finite_Number_Is_Written_Without_It()
    {
        var set = new CalcInputSet(
        [
            CalcInputValue.VectorOf("theta", [1, double.NaN, 3], "deg"),
            CalcInputValue.Table("segments", [CalcRow.Of(("l", new CalcCell(double.PositiveInfinity, "mm")), ("name", new CalcCell(Text: "A"))), CalcRow.Of(("l", new CalcCell(4, "mm")))]),
        ]);

        var (read, notices) = CalcInputJson.Read(CalcInputJson.Write(set));

        Assert.Empty(notices);
        Assert.Null(read.Find("theta"));
        var rows = read.Find("segments")!.Rows!;
        Assert.Equal(2, rows.Count);
        Assert.Null(rows[0].Get("l")!.Number);
        Assert.Equal("mm", rows[0].Get("l")!.Unit);
        Assert.Equal("A", rows[0].Get("name")!.Text);
        Assert.Equal(4, rows[1].Get("l")!.Number);
    }

    [Fact]
    public void A_Withheld_Value_Is_Still_Written_As_It_Was()
    {
        var withheld = new CalcInputValue("C_r", Provenance: new CalcProvenance(CalcProvenanceKind.Reference, "Bearings/6206@1 (" + CalcCsv.WithheldMarker + ")"));
        var json = CalcInputJson.Write(new CalcInputSet([withheld]));
        Assert.Contains("\"C_r\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Finite_Values_Are_Written_Exactly_As_Before()
    {
        var set = new CalcInputSet([CalcInputValue.Quantity("F", 12.5, "kN", CalcProvenance.Drawing("D-1", "B")), CalcInputValue.Switch("on", true)]);
        var (read, notices) = CalcInputJson.Read(CalcInputJson.Write(set));
        Assert.Empty(notices);
        Assert.Equal(set.Find("F"), read.Find("F"));
        Assert.Equal(set.Find("on"), read.Find("on"));
    }
}
