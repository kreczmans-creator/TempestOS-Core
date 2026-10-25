using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Import;

/// <summary>Security review H2, L6 and M2 (the snapshot a provenance is verified against) at the mapping level.</summary>
public sealed class SecurityReviewMappingTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 25, 9, 30, 0, TimeSpan.Zero);

    private static CalcTemplate Template { get; } = new(
        "test.rotation", "1.0.0", CalcDiscipline.Mechanical, "Rotation", "scope", "method",
        [
            new CalcInputSpec("n", "Speed", CalcInputKind.Quantity, "rpm", Default: 1000),
            new CalcInputSpec("f", "Frequency", CalcInputKind.Quantity, "Hz", Default: 50),
            new CalcInputSpec("w", "Angular speed", CalcInputKind.Quantity, "rad/s", Default: 100),
            new CalcInputSpec("T_K", "Absolute temperature", CalcInputKind.Quantity, "K", Default: 300),
            new CalcInputSpec("T_C", "Temperature", CalcInputKind.Quantity, "degC", Default: 20),
            new CalcInputSpec("L", "Length", CalcInputKind.Quantity, "mm", Default: 10),
        ],
        [], [], [], []);

    private static CadMappingResult Map(string csv, string target, string? assumed = null)
    {
        var parse = CadImporter.Parse(Samples.Text(csv, "p.csv"), CadSourceFormat.GenericCsv);
        var profile = new CadMappingProfile("rot", 1, "Rotation", [new("v", target, assumed)]);
        return CadMapper.Map(parse, profile, Template, [], At);
    }

    [Theory]
    [InlineData("v", "3000", "1/min", "n")]
    [InlineData("v", "3000", "min^-1", "n")]
    [InlineData("v", "50", "Hz", "n")]
    [InlineData("v", "50", "1/s", "n")]
    [InlineData("v", "3000", "rpm", "f")]
    [InlineData("v", "314.159", "rad/s", "f")]
    [InlineData("v", "360", "deg/s", "f")]
    [InlineData("v", "3000", "1/min", "w")]
    [InlineData("v", "50", "Hz", "w")]
    public void H2_A_Speed_Is_Never_Converted_Between_A_Unit_That_Counts_Angle_And_One_That_Does_Not(string name, string value, string unit, string target)
    {
        var result = Map($"name,value,unit\n{name},{value},{unit}\n", target);
        Assert.False(result.CanApply, string.Join(" ", result.Rows.Select(r => r.Message)));
        Assert.Contains("factor of 2π", result.Rows[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("3000", "rpm", "n", 3000)]
    [InlineData("314.1592653589793", "rad/s", "n", 3000)]
    [InlineData("360", "deg/s", "n", 60)]
    [InlineData("3000", "rpm", "w", 314.1592653589793)]
    [InlineData("50", "Hz", "f", 50)]
    [InlineData("3000", "1/min", "f", 50)]
    [InlineData("0.05", "kHz", "f", 50)]
    [InlineData("50", "1/s", "f", 50)]
    public void H2_Like_Is_Converted_To_Like_With_The_Right_Factor(string value, string unit, string target, double expected)
    {
        var result = Map($"name,value,unit\nv,{value},{unit}\n", target);
        Assert.True(result.CanApply, string.Join(" ", result.Refusals));
        Assert.Equal(expected, result.Inputs.Single(i => i.Symbol == target).Number!.Value, 9);
    }

    [Theory]
    [InlineData("rpm", true)]
    [InlineData("rad/s", true)]
    [InlineData("deg/s", true)]
    [InlineData("°/s", true)]
    [InlineData("mrad", true)]
    [InlineData("rev/min", true)]
    [InlineData("Hz", false)]
    [InlineData("1/min", false)]
    [InlineData("1/s", false)]
    [InlineData("mm", false)]
    [InlineData("N*m", false)]
    [InlineData("", false)]
    public void H2_Which_Units_Carry_An_Angle(string unit, bool carries) => Assert.Equal(carries, CadUnits.CarriesAngle(unit));

    [Fact]
    public void L6_A_Celsius_Value_Mapped_To_Kelvin_Is_Converted_As_An_Absolute_Temperature_And_The_Row_Says_So()
    {
        var toKelvin = Map("name,value,unit\nv,10,degC\n", "T_K");
        Assert.True(toKelvin.CanApply);
        Assert.Equal(283.15, toKelvin.Inputs.Single(i => i.Symbol == "T_K").Number!.Value, 9);
        Assert.Contains("absolute temperature", toKelvin.Rows[0].Value, StringComparison.Ordinal);

        var toCelsius = Map("name,value,unit\nv,283.15,K\n", "T_C");
        Assert.Equal(10, toCelsius.Inputs.Single(i => i.Symbol == "T_C").Number!.Value, 9);
        Assert.Contains("absolute temperature", toCelsius.Rows[0].Value, StringComparison.Ordinal);

        Assert.DoesNotContain("absolute", Map("name,value,unit\nv,10,degC\n", "T_C").Rows[0].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void M2_The_Snapshot_Records_The_Value_As_The_Input_Took_It_Including_A_Unit_The_Profile_Assumed()
    {
        var result = Map("name,value\nv,40.5\n", "L", "mm");
        var snap = Assert.Single(result.Snapshot);
        Assert.Equal("mm", snap.Unit);
        Assert.Equal(0.0405, snap.Si!.Value, 12);
        Assert.Equal("L", snap.Target);
    }
}
