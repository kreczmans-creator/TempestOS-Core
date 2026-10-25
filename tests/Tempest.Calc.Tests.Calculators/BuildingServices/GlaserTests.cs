using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class GlaserTests
{
    private static readonly GlaserCalculator Glaser = new();

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue Constant(double thetaE, double rh) => GlaserCalculator.Climate([.. Enumerable.Repeat((thetaE, rh), 12)]);

    private static readonly (double, double)[] Seasonal = [(2, 90), (3, 88), (6, 84), (9, 80), (13, 76), (16, 74), (18, 74), (18, 76), (15, 82), (11, 87), (6, 90), (3, 91)];

    private static CalcInputValue SeasonalRotated(int k) => GlaserCalculator.Climate([.. Seasonal.Skip(k).Concat(Seasonal.Take(k))]);

    private static CalcInputValue InsulatedBrick(double brickMu = 10) => GlaserCalculator.Layers(("", "Insulation", 100, 0.04, 2), ("", "Brick", 200, 0.8, brickMu));

    private static CalcInputSet Sheet(CalcInputValue layers, CalcInputValue climate) =>
        CalcTemplateHasher.NewSheetInputs(Glaser.Template).With(layers).With(climate);

    private static CalcRunResult Run(CalcInputSet inputs) => CalcRunner.Run(Glaser, inputs);

    // ------------------------------------------------------------------ physics

    [Fact]
    public void Magnus_Saturation_Pressure_Is_2337_Pa_At_20_C_And_Continuous_At_Zero()
    {
        Assert.InRange(GlaserModel.SaturationPressure(20), 2337 * 0.999, 2337 * 1.001);
        Assert.Equal(610.5 * Math.Exp(17.269 * 20 / 257.3), GlaserModel.SaturationPressure(20), 9);
        Assert.Equal(610.5, GlaserModel.SaturationPressure(0), 9);
        Assert.Equal(610.5, GlaserModel.SaturationPressure(-1e-12), 6);
        Assert.True(GlaserModel.SaturationPressure(-10) < GlaserModel.SaturationPressure(-9));
    }

    [Fact]
    public void Property_The_Dew_Point_Inverts_The_Saturation_Pressure()
    {
        Gen.Double[-45, 55].Sample(t => Math.Abs(GlaserModel.DewPoint(GlaserModel.SaturationPressure(t)) - t) < 1e-9, iter: 200);
    }

    private static readonly Gen<GlaserLayer> AnyLayer = Gen.Select(Gen.Double[0.002, 0.3], Gen.Double[0.02, 2.0], Gen.Double[1, 3000]).Select((d, l, m) => new GlaserLayer(d, l, m));

    [Fact]
    public void Property_The_Profile_Never_Rises_Above_Saturation_And_Moisture_Is_Conserved()
    {
        Gen.Select(AnyLayer.List[2, 8], Gen.Double[-15, 18], Gen.Double[0.4, 1.0], Gen.Double[0.2, 0.7], Gen.UInt).Sample((layers, thetaE, rhE, rhI, pinBits) =>
        {
            var n = layers.Count;
            var s = GlaserModel.CumulativeSd(layers);
            var theta = GlaserModel.NodeTemperatures(layers, 0.13, 0.04, 20, thetaE);
            var psat = theta.Select(GlaserModel.SaturationPressure).ToArray();
            var pi = rhI * GlaserModel.SaturationPressure(20);
            var pe = rhE * GlaserModel.SaturationPressure(thetaE);
            var pinned = new bool[n + 1];

            for (var k = 1; k < n; k++)
                pinned[k] = (pinBits >> k & 1) == 1;

            var touched = new bool[n + 1];
            var p = GlaserModel.Profile(s, psat, pi, pe, pinned, touched);
            var flux = GlaserModel.NodeFlux(s, p, touched, pinned);
            var into = GlaserModel.Delta0 * (p[0] - p[1]) / s[1];
            var outOf = GlaserModel.Delta0 * (p[n - 1] - p[n]) / (s[n] - s[n - 1]);
            var scale = Math.Abs(into) + Math.Abs(outOf) + 1e-18;
            var conserved = Math.Abs(flux.Sum() - (into - outOf)) <= 1e-9 * scale;
            var below = Enumerable.Range(1, n - 1).All(k => p[k] <= psat[k] * (1 + 1e-9) + 1e-9);
            var onlyKinksCondense = Enumerable.Range(1, n - 1).All(k => pinned[k] || !touched[k] || flux[k] >= -1e-7 * scale);
            var touchesAreOnSaturation = Enumerable.Range(1, n - 1).All(k => !touched[k] || Math.Abs(p[k] - psat[k]) <= 1e-9 * psat[k]);
            return conserved && below && onlyKinksCondense && touchesAreOnSaturation;
        }, iter: 400);
    }

    [Fact]
    public void A_Wall_That_Stays_Below_Saturation_Has_The_Straight_Diffusion_Line()
    {
        var layers = new[] { new GlaserLayer(0.015, 0.5, 10), new GlaserLayer(0.215, 0.8, 8) };
        var s = GlaserModel.CumulativeSd(layers);
        var psat = GlaserModel.NodeTemperatures(layers, 0.13, 0.04, 20, 2).Select(GlaserModel.SaturationPressure).ToArray();
        var touched = new bool[3];
        var p = GlaserModel.Profile(s, psat, 1168.0, 640.0, new bool[3], touched);
        Assert.False(touched[1]);
        Assert.Equal(1168.0 + (640.0 - 1168.0) * s[1] / s[2], p[1], 9);
    }

    [Fact]
    public void Two_Condensation_Planes_Are_Where_The_Profile_Rests_On_The_Saturation_Line()
    {
        var layers = new[] { new GlaserLayer(0.0125, 0.25, 8), new GlaserLayer(0.14, 0.04, 1), new GlaserLayer(0.1, 0.8, 10), new GlaserLayer(0.05, 0.04, 1), new GlaserLayer(0.003, 0.9, 150) };
        var s = GlaserModel.CumulativeSd(layers);
        var psat = GlaserModel.NodeTemperatures(layers, 0.13, 0.04, 20, 0).Select(GlaserModel.SaturationPressure).ToArray();
        var touched = new bool[6];
        var none = new bool[6];
        var p = GlaserModel.Profile(s, psat, 0.5 * GlaserModel.SaturationPressure(20), 0.9 * 610.5, none, touched);
        Assert.Equal([false, false, true, false, true, false], touched);
        Assert.Equal(psat[2], p[2], 9);
        Assert.Equal(psat[4], p[4], 9);

        // Between the planes the line is straight (the same slope on both sides of the free node 3), and the planes are convex kinks.
        var slope = (int k) => (p[k + 1] - p[k]) / (s[k + 1] - s[k]);
        Assert.Equal(slope(2), slope(3), 9);
        Assert.True(slope(2) > slope(1));
        Assert.True(slope(4) > slope(3));
    }

    // ------------------------------------------------------------------ the sheet

    [Fact]
    public void A_Year_Round_Condensing_Interface_Fails_The_Annual_Balance_And_Reports_It()
    {
        var result = Run(Sheet(InsulatedBrick(), Constant(0, 90)).With(Q("Mc_limit", 20000, "g/m^2")));
        Assert.False(result.IsBlocked);
        Assert.Contains(result.Checks, c => c.Name.StartsWith("Annual moisture balance", StringComparison.Ordinal) && !c.Passed);
        Assert.Equal(0, result.OutputSi("pass"));
        Assert.Equal(result.OutputSi("Mc_max"), result.OutputSi("Mc_end"), 9);
        Assert.Equal(12 * result.OutputSi("Mc_1"), result.OutputSi("Mc_12"), 9);
    }

    [Fact]
    public void The_Same_Wall_In_A_Seasonal_Climate_Dries_Out_And_Passes_The_Balance_But_Not_A_Tight_Limit()
    {
        var open = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)));
        Assert.Equal(0, open.OutputSi("Mc_end"), 12);
        Assert.Contains(open.Checks, c => c.Name.StartsWith("Annual moisture balance", StringComparison.Ordinal) && c.Passed);

        var limited = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)).With(Q("Mc_limit", 1000, "g/m^2")));
        Assert.Contains(limited.Checks, c => c.Name.StartsWith("Peak condensate", StringComparison.Ordinal) && !c.Passed);
        Assert.Equal(3110.811466156645e-3, limited.OutputSi("Mc_max"), 9);
        Assert.Equal(0, limited.OutputSi("pass"));
    }

    [Fact]
    public void Accumulation_Starts_In_The_First_Month_Of_A_Condensing_Run_Not_In_The_First_Row()
    {
        var result = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)));
        Assert.Equal(11, result.OutputSi("n_start"));

        // The winter run November to May straddles the year end; the answer is the same whichever month the climate record begins with.
        var reference = (Max: result.OutputSi("Mc_max"), End: result.OutputSi("Mc_end"));

        for (var k = 0; k < 12; k++)
        {
            var rotated = Run(Sheet(InsulatedBrick(), SeasonalRotated(k)));
            Assert.Equal(reference.Max, rotated.OutputSi("Mc_max"), 9);
            Assert.Equal(reference.End, rotated.OutputSi("Mc_end"), 12);
            Assert.Equal((10 - k + 12) % 12 + 1, (int)rotated.OutputSi("n_start"));
            Assert.Equal(result.OutputSi("Mc_3"), rotated.OutputSi($"Mc_{(2 - k + 12) % 12 + 1}"), 9);
        }
    }

    [Fact]
    public void A_Vapour_Control_Layer_On_The_Warm_Side_Cuts_The_Condensate()
    {
        var bare = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)));
        var foil = Run(Sheet(GlaserCalculator.Layers(("", "Foil", 0.2, 0.2, 100_000), ("", "Insulation", 100, 0.04, 2), ("", "Brick", 200, 0.8, 10)), GlaserCalculator.Climate(Seasonal)));
        Assert.True(foil.OutputSi("Mc_max") < 0.05 * bare.OutputSi("Mc_max"));
    }

    [Fact]
    public void The_Surface_Factor_Follows_R_Si_Over_R_T_And_Rises_With_Insulation()
    {
        var thin = Run(Sheet(GlaserCalculator.Layers(("", "Brick", 215, 0.8, 8)), GlaserCalculator.Climate(Seasonal)));
        var thick = Run(Sheet(GlaserCalculator.Layers(("", "Brick", 215, 0.8, 8), ("", "Insulation", 100, 0.04, 1)), GlaserCalculator.Climate(Seasonal)));
        Assert.Equal(1 - 0.13 / (0.13 + 215 / 800.0 + 0.04), thin.OutputSi("fRsi"), 12);
        Assert.True(thick.OutputSi("fRsi") > thin.OutputSi("fRsi"));
        Assert.Equal(thin.OutputSi("fRsi_min"), thick.OutputSi("fRsi_min"), 12);

        // A larger internal surface resistance for the surface check (behind furniture) gives a colder surface: a lower f_Rsi, from its own input.
        var withRsiF = Run(Sheet(GlaserCalculator.Layers(("", "Brick", 215, 0.8, 8)), GlaserCalculator.Climate(Seasonal)).With(Q("Rsi_f", 0.25, "m^2*K/W")));
        Assert.Equal(1 - 0.25 / (0.25 + 215 / 800.0 + 0.04), withRsiF.OutputSi("fRsi"), 12);
        Assert.True(withRsiF.OutputSi("fRsi") < thin.OutputSi("fRsi"));
    }

    [Fact]
    public void A_Higher_Critical_Humidity_Needs_A_Warmer_Surface_And_f_Rsi_Min_Is_Never_Negative()
    {
        Gen.Select(Gen.Double[30, 90], Gen.Double[30, 99.0]).Sample((rhInt, crit) =>
        {
            var a = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)).With(Q("RH_int", rhInt, "%")).With(Q("RH_crit", crit, "%")));
            var b = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)).With(Q("RH_int", rhInt, "%")).With(Q("RH_crit", Math.Min(100, crit + 1), "%")));
            return !a.IsBlocked && !b.IsBlocked && a.OutputSi("fRsi_min") >= 0 && b.OutputSi("fRsi_min") <= a.OutputSi("fRsi_min") + 1e-12;
        }, iter: 60);
    }

    [Fact]
    public void Property_Condensate_Is_Never_Negative_And_Peaks_At_Least_As_High_As_It_Ends()
    {
        Gen.Select(AnyLayer.List[2, 6], Gen.Double[-8, 8], Gen.Double[6, 12], Gen.Double[0.7, 1.0], Gen.Double[0.3, 0.65]).Sample((layers, mean, swing, rhE, rhI) =>
        {
            var thetaE = Enumerable.Range(0, 12).Select(m => mean - swing * Math.Cos((m + 0.5) / 12 * 2 * Math.PI)).ToArray();
            var pe = thetaE.Select(t => rhE * GlaserModel.SaturationPressure(t)).ToArray();
            var pi = Enumerable.Repeat(rhI * GlaserModel.SaturationPressure(20), 12).ToArray();
            var seconds = Enumerable.Repeat(GlaserModel.AverageMonthSeconds, 12).ToArray();
            var year = GlaserModel.Simulate(layers, 0.13, 0.04, 20, thetaE, pi, pe, seconds);
            return year.Months.All(m => m.Mc.All(x => x >= 0 && double.IsFinite(x)))
                && year.ResidualMc >= 0 && year.PeakInterfaceMc + 1e-15 >= year.ResidualMc / (layers.Count - 1)
                && (year.StartIndex >= 0 || year.PeakInterfaceMc == 0);
        }, iter: 300);
    }

    // ------------------------------------------------------------------ inputs, blocks, warnings

    [Fact]
    public void An_Empty_Layers_Or_Climate_Table_Blocks_And_Says_What_To_Enter()
    {
        var noLayers = Run(Sheet(CalcInputValue.Table("layers", []), GlaserCalculator.Climate(Seasonal)));
        Assert.Equal("layers", noLayers.Blocks[0].RuleId);
        Assert.StartsWith("layers: ", noLayers.Blocks[0].Message, StringComparison.Ordinal);

        var noClimate = Run(Sheet(InsulatedBrick(), CalcInputValue.Table("climate", [])));
        Assert.StartsWith("climate: ", noClimate.Blocks[0].Message, StringComparison.Ordinal);
        Assert.Contains("enter", noClimate.Blocks[0].Message, StringComparison.OrdinalIgnoreCase);

        var eleven = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate([.. Seasonal.Take(11)])));
        Assert.Equal("climate", eleven.Blocks[0].RuleId);
        Assert.Contains("12", eleven.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.0)]
    [InlineData(-3.0)]
    public void A_Vapour_Resistance_Factor_Below_One_Is_Refused(double mu)
    {
        var result = Run(Sheet(GlaserCalculator.Layers(("", "Layer", 100, 0.04, mu), ("", "Brick", 200, 0.8, 10)), GlaserCalculator.Climate(Seasonal)));
        Assert.True(result.IsBlocked);
    }

    [Fact]
    public void A_Humidity_Above_100_Percent_Or_A_Missing_Value_In_The_Climate_Is_Refused()
    {
        var over = GlaserCalculator.Climate([.. Seasonal.Select((m, i) => i == 4 ? (m.Item1, 101.0) : m)]);
        Assert.True(Run(Sheet(InsulatedBrick(), over)).IsBlocked);

        var gap = CalcInputValue.Table("climate", [.. Enumerable.Range(0, 12).Select(i => i == 3 ? CalcRow.Of(("month", new CalcCell(Text: "Apr")), ("theta_e", new CalcCell(9, "degC"))) : CalcRow.Of(("theta_e", new CalcCell(5, "degC")), ("RH_e", new CalcCell(80, "%"))))]);
        var blocked = Run(Sheet(InsulatedBrick(), gap));
        Assert.True(blocked.IsBlocked);
    }

    [Fact]
    public void The_B4_Layers_Table_Shape_Is_Accepted_And_An_Element_Name_Picks_Its_Rows()
    {
        var b4 = CalcInputValue.Table("layers",
        [
            CalcRow.Of(("element", new CalcCell(Text: "Cavity wall")), ("name", new CalcCell(Text: "Insulation")), ("d", new CalcCell(100, "mm")), ("lambda", new CalcCell(0.04, "W/(m*K)")), ("mu", new CalcCell(2))),
            CalcRow.Of(("element", new CalcCell(Text: "Cavity wall")), ("name", new CalcCell(Text: "Brick")), ("d", new CalcCell(200, "mm")), ("lambda", new CalcCell(0.8, "W/(m*K)")), ("mu", new CalcCell(10))),
            CalcRow.Of(("element", new CalcCell(Text: "Roof")), ("name", new CalcCell(Text: "Tile")), ("d", new CalcCell(20, "mm")), ("lambda", new CalcCell(1, "W/(m*K)")), ("mu", new CalcCell(30))),
        ]);

        var picked = Run(Sheet(b4, GlaserCalculator.Climate(Seasonal)).With(CalcInputValue.Choice("element", "Cavity wall")));
        var plain = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)));
        Assert.False(picked.IsBlocked);
        Assert.Equal(plain.OutputSi("Mc_max"), picked.OutputSi("Mc_max"), 12);
        Assert.Contains(picked.Warnings, w => w.RuleId == "element-filter");

        var missing = Run(Sheet(b4, GlaserCalculator.Climate(Seasonal)).With(CalcInputValue.Choice("element", "Floor")));
        Assert.Equal("element", missing.Blocks[0].RuleId);
    }

    [Fact]
    public void A_Moisture_Excess_Mode_Needs_Both_Points_In_Order_And_Limits_The_Pressure_To_Saturation()
    {
        var mode = CalcInputValue.Choice("humidity_mode", GlaserCalculator.MoistureExcess);
        var inputs = Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)).With(mode);
        var incomplete = Run(inputs.With(Q("dv_a", 5, "g/m^3")));
        Assert.Equal("humidity-class", incomplete.Blocks[0].RuleId);
        Assert.StartsWith("theta_a: ", incomplete.Blocks[0].Message, StringComparison.Ordinal);

        var reversed = Run(inputs.With(Q("dv_a", 5, "g/m^3")).With(Q("theta_a", 20, "degC")).With(Q("dv_b", 0, "g/m^3")).With(Q("theta_b", 0, "degC")));
        Assert.Equal("theta_b", reversed.Blocks[0].RuleId);

        var wet = Run(inputs.With(Q("dv_a", 20, "g/m^3")).With(Q("theta_a", 0, "degC")).With(Q("dv_b", 20, "g/m^3")).With(Q("theta_b", 20, "degC")));
        Assert.False(wet.IsBlocked);
        Assert.Contains(wet.Warnings, w => w.RuleId == "p-int-saturated");
    }

    [Fact]
    public void A_Breathable_Build_Up_Is_Warned_To_Use_En_15026_And_A_Plain_One_Is_Not()
    {
        var plain = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)));
        Assert.DoesNotContain(plain.Warnings, w => w.RuleId == "en15026");

        var timber = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)).With(CalcInputValue.Switch("breathable", true)));
        var warning = Assert.Single(timber.Warnings, w => w.RuleId == "en15026");
        Assert.Contains("EN 15026", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Single_Layer_Has_No_Interface_To_Condense_On()
    {
        var result = Run(Sheet(GlaserCalculator.Layers(("", "Concrete", 200, 1.5, 80)), Constant(-5, 95)));
        Assert.False(result.IsBlocked);
        Assert.Contains(result.Warnings, w => w.RuleId == "single-layer");
        Assert.Equal(0, result.OutputSi("Mc_max"));
    }

    [Fact]
    public void The_Ports_The_Brief_Names_Are_Declared_And_Produced()
    {
        var ports = Glaser.Template.OutputPorts!.Select(p => p.Symbol).ToList();
        Assert.Equal(["Mc_max", "fRsi", "pass"], ports);
        var result = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)));
        Assert.All(ports, p => Assert.NotNull(result.FindOutput(p)));
        Assert.Equal("g/m^2", Glaser.Template.OutputPorts!.Single(p => p.Symbol == "Mc_max").Unit);
    }

    [Fact]
    public void The_Glaser_Diagram_Series_Cover_Every_Node_For_The_Chosen_Month()
    {
        var result = Run(Sheet(InsulatedBrick(), GlaserCalculator.Climate(Seasonal)).With(CalcInputValue.Of("diagram_month", 3)));
        Assert.Equal(3, result.OutputSi("n_diag"));
        Assert.Equal(3, result.Series.First(s => s.Name == "Vapour pressure").Points.Count);
        Assert.Equal(3, result.Series.First(s => s.Name == "Saturation pressure").Points.Count);
        Assert.Equal(3, result.Series.First(s => s.Name == "Temperature").Points.Count);

        // In the March diagram the vapour pressure rests on the saturation line at the interface (the condensation plane).
        var sat = result.Series.First(s => s.Name == "Saturation pressure").Points[1].Y;
        var vap = result.Series.First(s => s.Name == "Vapour pressure").Points[1].Y;
        Assert.Equal(sat, vap, 9);
    }
}
