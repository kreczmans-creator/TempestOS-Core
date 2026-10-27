using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>A calculator with no inputs whose run is whatever a test passes in: for showing how outputs and checks are displayed.</summary>
public sealed class DelegateCalculator(Action<CalcRunContext> run) : ICalcCalculator
{
    public CalcTemplate Template { get; } = new(
        "test.display", "1.0.0", CalcDiscipline.Mechanical, "Display (test)",
        "Calculates what the test asks for.", "Result: whatever the test records.",
        [], [], [], [], [], Category: "Strength");

    public void Run(CalcRunContext c) => run(c);
}

/// <summary>
/// C-2 fix (H1 to H3): how a result is shown. At least four and at most five significant figures; a whole number exactly; a value that must be
/// at least a limit rounded down and one checked against a maximum rounded up when it exceeds it, so no displayed value appears on the passing
/// side of a limit its check fails; a utilisation known by what the template declares, never by its name; the noise rule only for outputs a
/// template flags; and one formatter for the sheet, the PDF, the form, the banner and the register.
/// </summary>
public sealed class DisplayRulesTests
{
    private static CalcRunResult Run(Action<CalcRunContext> body) => CalcRunner.Run(new DelegateCalculator(body), new CalcInputSet());

    // ---- precision --------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(144.93187, "MPa", "144.9 MPa")]
    [InlineData(2194.8, "N*m", "2194.8 N·m")]
    [InlineData(2194.8000001, "N*m", "2194.8 N·m")]
    [InlineData(2.0, "kN", "2 kN")]
    [InlineData(2.5, "kN", "2.500 kN")]
    [InlineData(321190.4, "kN/m", "321190 kN/m")]
    [InlineData(12.34, "mm", "12.34 mm")]
    [InlineData(0.01234, "mm", "0.01234 mm")]
    [InlineData(20.918, "µm", "20.92 µm")]
    [InlineData(57.947, "kPa", "57.95 kPa")]
    [InlineData(57947.0, "Pa", "57947 Pa")]
    [InlineData(16667.0, "h", "16667 h")]
    [InlineData(4.4, "m^3/h", "4.400 m³/h")]
    [InlineData(61.5, "degC", "61.50 °C")]
    [InlineData(0.0, "kN", "0 kN")]
    [InlineData(1.5, "furlong/fortnight", "1.5 furlong/fortnight")]
    public void A_Scalar_Is_Shown_At_The_Decimals_Of_Its_Quantity_Type_And_Never_With_Fewer_Than_Four_Figures(double displayed, string unit, string expected)
    {
        var shown = UnitCatalog.TryParse(unit, out _, out _) ? ScalarValue.Of(displayed, unit) : new ScalarValue(displayed, Dimension.None, new Unit(unit, Dimension.None, 1.0));
        Assert.Equal(expected, CalcDisplay.Format(shown));
    }

    [Theory]
    [InlineData(3.15951, "cm^3", "3.160 cm³")]
    [InlineData(0.0034, "m^3", "0.003400 m³")]
    [InlineData(0.4, "ms", "0.4000 ms")]
    [InlineData(123456.7, "kPa", "123457 kPa")]
    [InlineData(1234.56789, "MPa", "1234.6 MPa")]
    [InlineData(99999.4, "mm", "99999 mm")]
    [InlineData(0.00012345, "m", "0.0001234 m")]
    public void A_Result_Shows_At_Least_Four_And_At_Most_Five_Significant_Figures(double displayed, string unit, string expected) =>
        Assert.Equal(expected, CalcDisplay.Format(ScalarValue.Of(displayed, unit)));

    [Theory]
    [InlineData(150000.0, "h", "150000 h")]
    [InlineData(13824.0, "m^3", "13824 m³")]
    [InlineData(5.0, "MJ", "5 MJ")]
    [InlineData(1234567.0, "Pa", "1234567 Pa")]
    [InlineData(2.0000000001, "kN", "2 kN")]
    [InlineData(-3.0, "kN", "-3 kN")]
    [InlineData(100.0, "mm", "100 mm")]
    public void A_Whole_Number_Is_Printed_Exactly_Without_Decimals(double displayed, string unit, string expected) =>
        Assert.Equal(expected, CalcDisplay.Format(ScalarValue.Of(displayed, unit)));

    [Fact]
    public void A_Tiny_Value_Is_Not_A_Whole_Number_And_Is_Never_Written_As_Zero_By_The_Display()
    {
        Assert.Equal("1.8626e-15 MPa", CalcDisplay.Format(ScalarValue.Of(1.8626e-15, "MPa")));
        Assert.Equal("-3.2742e-14 kN·m", CalcDisplay.Format(ScalarValue.Of(-3.2742e-14, "kN*m")));
    }

    [Fact]
    public void Display_Rounds_The_Text_Only_And_Never_The_Value()
    {
        var value = ScalarValue.Of(144.93187, "MPa");
        var output = new CalcOutput("sigma", "Stress", value);
        var before = value.Si;
        Assert.Equal("144.9 MPa", output.ValueText);
        Assert.Equal(before, ((ScalarValue)output.Value).Si);
        Assert.Equal(144.93187e6, ((ScalarValue)output.Value).Si, 3);

        // An override of the decimals is for one output, raises the decimals only and never goes below four figures.
        Assert.Equal("144.93 MPa", new CalcOutput("sigma", "Stress", value, 2).ValueText);
        Assert.Equal("144.9 MPa", new CalcOutput("sigma", "Stress", value, 0).ValueText);
    }

    [Theory]
    [InlineData("144.9 MPa", 0.1)]
    [InlineData("0.003400 N·m", 1e-6)]
    [InlineData("150000 h", 1.0)]
    [InlineData("-2194.8 N·m", 0.1)]
    [InlineData("1.8626e-15 MPa", 1e-19)]
    [InlineData("30.00°", 0.01)]
    [InlineData("0.94", 0.01)]
    [InlineData("1.4999", 0.0001)]
    public void The_Size_Of_The_Last_Shown_Digit_Is_Known_So_A_Reader_Typing_What_The_Form_Shows_Can_Be_Allowed_Half_Of_It(string shown, double expected)
    {
        Assert.Equal(expected, CalcDisplay.ShownResolution(shown)!.Value, expected * 1e-9);

        // The resolution of the text the display prints is never coarser than the least decimals of its quantity type, and never finer than five figures.
        Assert.True(CalcDisplay.ShownResolution(CalcDisplay.Format(ScalarValue.Of(144.93187, "MPa")))!.Value >= 1e-2);
    }

    [Fact]
    public void A_Text_That_Is_Not_A_Number_Has_No_Resolution()
    {
        Assert.Null(CalcDisplay.ShownResolution("∞"));
        Assert.Null(CalcDisplay.ShownResolution("PASS"));
        Assert.Null(CalcDisplay.ShownResolution(""));
        Assert.Null(CalcDisplay.ShownResolution(null));
    }

    // ---- H1: the noise rule is the template's declaration, never a comparison between different outputs -------------------------------------

    [Fact]
    public void An_Output_Is_Never_Shown_As_Zero_For_Being_Small_Beside_Another_Output_Of_Its_Dimension()
    {
        // The three cases the audit found: 3.1595 cm³ beside 13824 m³ (printed 0), 0.4 ms beside 150000 h, 3 mN·m beside 5 MJ.
        var r = Run(c =>
        {
            c.Record("Z_x", "section modulus", "from the geometry", ScalarValue.Of(3.15951, "cm^3"), "Section modulus");
            c.Record("S_run", "run flexibility", "from the geometry", ScalarValue.Of(13824, "m^3"), "Flexibility of the runs");
            c.Record("t_a", "time", "from the data", ScalarValue.Of(0.4, "ms"), "Short time");
            c.Record("t_b", "time", "from the data", ScalarValue.Of(150000, "h"), "Long time");
            c.Record("T_s", "torque", "from the data", ScalarValue.Of(0.003, "N*m"), "Small torque");
            c.Record("E_b", "energy", "from the data", ScalarValue.Of(5, "MJ"), "Large energy");

            foreach (var (symbol, text) in new[] { ("Z_x", "Section modulus"), ("S_run", "Flexibility of the runs"), ("t_a", "Short time"), ("t_b", "Long time"), ("T_s", "Small torque"), ("E_b", "Large energy") })
                c.Output(symbol, text);
        });

        Assert.Equal("3.160 cm³", r.FindOutput("Z_x")!.ValueText);
        Assert.Equal("13824 m³", r.FindOutput("S_run")!.ValueText);
        Assert.Equal("0.4000 ms", r.FindOutput("t_a")!.ValueText);
        Assert.Equal("150000 h", r.FindOutput("t_b")!.ValueText);
        Assert.Equal("0.003000 N·m", r.FindOutput("T_s")!.ValueText);
        Assert.Equal("5 MJ", r.FindOutput("E_b")!.ValueText);
    }

    [Fact]
    public void Only_An_Output_The_Template_Flags_As_Noise_Prone_Is_Shown_As_Zero_And_Only_When_It_Is_Noise_Beside_Its_Working_Values()
    {
        var r = Run(c =>
        {
            c.Record("sigma_t_b", "hoop", "from the geometry", ScalarValue.Of(144.9, "MPa"), "Hoop stress");
            c.Record("sigma_r_b", "radial", "zero at a free surface", ScalarValue.Of(1.8626e-15, "MPa"), "Radial stress");
            c.Record("sigma_r_a", "radial", "a real small stress", ScalarValue.Of(0.002, "MPa"), "Radial stress, bore");
            c.Record("sigma_x", "other", "an unflagged tiny stress", ScalarValue.Of(2.5e-15, "MPa"), "Another tiny stress");
            c.MarkNoiseProne("sigma_r_b", "sigma_r_a");
            c.Output("sigma_t_b", "Hoop stress");
            c.Output("sigma_r_b", "Radial stress, outer");
            c.Output("sigma_r_a", "Radial stress, bore");
            c.Output("sigma_x", "Another tiny stress");
        });

        Assert.Equal("144.9 MPa", r.FindOutput("sigma_t_b")!.ValueText);
        Assert.Equal("0 MPa", r.FindOutput("sigma_r_b")!.ValueText);
        Assert.Equal("0.002000 MPa", r.FindOutput("sigma_r_a")!.ValueText);
        Assert.Equal("2.5e-15 MPa", r.FindOutput("sigma_x")!.ValueText);
        Assert.Equal(1.8626e-9, ((ScalarValue)r.FindOutput("sigma_r_b")!.Value).Si, 12);
    }

    [Fact]
    public void A_Noise_Prone_Output_Is_Shown_As_Zero_Also_Through_The_Output_Parameter_And_With_No_Working_Value_Of_Its_Dimension_It_Is_Left_Alone()
    {
        var withWork = Run(c =>
        {
            c.Record("L", "length", "input", ScalarValue.Of(250, "mm"), "Length");
            c.Record("res", "residual", "closure", ScalarValue.Of(8.7771e-14, "mm"), "Largest loop-closure residual");
            c.Output("res", "Largest loop-closure residual", noiseProne: true);
        });
        Assert.Equal("0 mm", withWork.FindOutput("res")!.ValueText);

        var alone = Run(c =>
        {
            c.Record("res", "residual", "closure", ScalarValue.Of(8.7771e-14, "mm"), "Largest loop-closure residual");
            c.Output("res", "Largest loop-closure residual", noiseProne: true);
        });
        Assert.Equal("8.7771e-14 mm", alone.FindOutput("res")!.ValueText);
    }

    [Fact]
    public void A_Value_Shown_As_Noise_Is_Shown_As_Zero_In_Its_Working_And_In_The_Substitutions_That_Quote_It_And_Keeps_Its_Value()
    {
        var r = Run(c =>
        {
            c.Record("sigma_t_b", "hoop", "from the geometry", ScalarValue.Of(144.9, "MPa"), "Hoop stress");
            c.Record("M_r", "reaction moment", "Σ member end moments", ScalarValue.Of(-1.4552e-14, "MPa"), "Moment at a pin");
            c.Record("M_abs", "|M|", "|-1.4552e-14| MPa", ScalarValue.Of(0, "MPa"), "Magnitude");
            c.Record("sum", "sum", "144.9 MPa − 1.4552e-14 MPa, 11.4552e-14", ScalarValue.Of(144.9, "MPa"), "Sum");
            c.MarkNoiseProne("M_r");
            c.Output("M_r", "Moment at a pin");
        });

        Assert.Equal("0 MPa", r.FindOutput("M_r")!.ValueText);
        Assert.Equal("0 MPa", r.Steps.Single(s => s.Symbol == "M_r").ResultText);
        Assert.Equal("|0| MPa", r.Steps.Single(s => s.Symbol == "M_abs").Substitution);

        // Only the number on its own is replaced: an operator minus is kept, and a longer number that ends in the same digits is left alone.
        Assert.Equal("144.9 MPa − 0 MPa, 11.4552e-14", r.Steps.Single(s => s.Symbol == "sum").Substitution);
        Assert.Equal(ScalarValue.Of(-1.4552e-14, "MPa").Si, ((ScalarValue)r.Steps.Single(s => s.Symbol == "M_r").Result).Si);
        Assert.Equal(ScalarValue.Of(-1.4552e-14, "MPa").Si, ((ScalarValue)r.FindOutput("M_r")!.Value).Si);
    }

    [Fact]
    public void A_Declared_Residual_Under_Its_Floor_Is_Shown_Below_The_Floor_Never_Zeroed_And_Above_It_As_It_Is()
    {
        var r = Run(c =>
        {
            c.Record("L", "length", "input", ScalarValue.Of(250, "mm"), "Length");
            c.Record("res", "residual", "closure", ScalarValue.Of(8.7771e-14, "mm"), "Largest loop-closure residual");
            c.Record("res_rel", "relative residual", "loop", new ScalarValue(1.9375e-16), "Relative residual");
            c.Record("res_big", "residual", "closure", ScalarValue.Of(3.2e-9, "mm"), "A residual above its floor");
            c.Output("res", "Largest loop-closure residual", noiseProne: true);
            c.Output("res_rel", "Relative residual", noiseProne: true);
            c.Output("res_big", "A residual above its floor", noiseProne: true);
            c.MarkResidual("res", 1e-10);
            c.MarkResidual("res_rel", CalcDisplay.ResidualFloor);
            c.MarkResidual("res_big", 1e-10);
        });

        // Below the floor: "below" the floor in the row and the working, never 0 (the noise rule alone would have shown 0 mm beside 250 mm).
        Assert.Equal("below 1e-10 mm", r.FindOutput("res")!.ValueText);
        Assert.Equal("below 1e-10 mm", r.Steps.Single(s => s.Symbol == "res").ResultText);
        Assert.Equal("below 1e-12", r.FindOutput("res_rel")!.ValueText);
        Assert.Equal("below 1e-12", r.Steps.Single(s => s.Symbol == "res_rel").ResultText);

        // At or above its floor a residual is information, and is shown as it is.
        Assert.Equal("3.2e-9 mm", r.FindOutput("res_big")!.ValueText);
        Assert.Equal(ScalarValue.Of(8.7771e-14, "mm").Si, ((ScalarValue)r.FindOutput("res")!.Value).Si);

        Assert.Equal("max residual below 1e-12", CalcDisplay.ResidualText("max residual", 1.9375e-16, CalcDisplay.ResidualFloor));
        Assert.Equal("max residual below 1e-10 mm", CalcDisplay.ResidualText("max residual", 8.7771e-14, 1e-10, "mm"));
        Assert.Equal("max residual = 3.2e-9 mm", CalcDisplay.ResidualText("max residual", 3.2e-9, 1e-10, "mm"));
        // A floor that is not a positive number is a template error: the run is refused, not shown with a floor of 0.
        Assert.True(Run(c => c.MarkResidual("res", 0)).IsBlocked);
    }

    [Fact]
    public void An_Entry_Of_A_Noise_Prone_Table_That_Is_Noise_Is_Shown_As_Zero_And_An_Unflagged_Table_Is_Left_Alone()
    {
        var r = Run(c =>
        {
            c.Record("j_sw", "j", "73 rows", VectorValue.Of([170.55, 44.141, 1.0443e-14, -44.141], "m/s^3"), "Table: follower jerk");
            c.Record("k_sw", "k", "73 rows", VectorValue.Of([170.55, 1.0443e-14], "m/s^3"), "Table: unflagged");
            c.Output("j_sw", "Table: follower jerk", noiseProne: true);
            c.Output("k_sw", "Table: unflagged");
        });

        Assert.Equal("[170.55, 44.141, 0, -44.141] m/s³", r.FindOutput("j_sw")!.ValueText);
        Assert.Equal("[170.55, 44.141, 0, -44.141] m/s³", r.Steps.Single(s => s.Symbol == "j_sw").ResultText);
        Assert.Equal(1.0443e-14, ((VectorValue)r.FindOutput("j_sw")!.Value).Si[2]);
        Assert.Contains("1.0443e-14", r.FindOutput("k_sw")!.ValueText, StringComparison.Ordinal);
    }

    // ---- H2: rounding that cannot cross a limit ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0.94057, "0.94")]
    [InlineData(0.375, "0.38")]
    [InlineData(1.0, "1.00")]
    [InlineData(1.0000000001, "1.00")]
    [InlineData(1.004, "1.01")]
    [InlineData(1.2481, "1.25")]
    [InlineData(0.0, "0.00")]
    [InlineData(0.00022, "0.00022")]
    [InlineData(0.0049, "0.0049")]
    [InlineData(0.0051, "0.01")]
    [InlineData(-0.5, "-0.50")]
    [InlineData(-0.456, "-0.46")]
    [InlineData(-0.00022, "-0.00022")]
    [InlineData(double.PositiveInfinity, "∞")]
    public void A_Utilisation_Is_Two_Decimals_Two_Figures_Below_0_005_And_Rounded_Up_When_It_Fails_And_Away_From_Zero_When_Negative(double u, string expected) =>
        Assert.Equal(expected, CalcDisplay.Utilisation(u));

    [Theory]
    [InlineData(0.504, 0.5, "0.51")]
    [InlineData(0.5, 0.5, "0.50")]
    [InlineData(0.496, 0.5, "0.50")]
    [InlineData(1.504, 1.5, "1.51")]
    [InlineData(0.804, 1.0, "0.80")]
    public void A_Utilisation_Is_Rounded_Up_Whenever_It_Exceeds_Its_Own_Limit_Not_Only_Above_One(double u, double limit, string expected) =>
        Assert.Equal(expected, CalcDisplay.Utilisation(u, limit));

    [Fact]
    public void A_Utilisation_Check_Against_A_Limit_Other_Than_One_Never_Shows_A_Failing_Value_At_Its_Limit()
    {
        var r = Run(c =>
        {
            c.Record("u_a", "u", "a ratio against a limit of 0.5", new ScalarValue(0.504), "Utilisation");
            var check = c.CheckUtilisation("Ratio against its limit", "u_a", limit: 0.5);
            Assert.False(check.Passed);
        });

        var failing = Assert.Single(r.Checks);
        Assert.Equal("u_a = 0.51", failing.DemandText);
        Assert.Equal("u_a ≤ 0.5", failing.CapacityText);
        Assert.False(r.Passed);
    }

    [Fact]
    public void A_Safety_Factor_That_Must_Be_At_Least_Its_Target_Is_Rounded_Down_And_Never_Reads_At_The_Target()
    {
        // 1.495 against 1.5 must not print 1.50; 1.49999 must not print 1.5.
        Assert.Equal("1.495", new CalcOutput("n_f", "Safety factor", new ScalarValue(1.495), Kind: CalcOutputKind.AtLeast).ValueText);
        Assert.Equal("1.4999", new CalcOutput("n_f", "Safety factor", new ScalarValue(1.49999), Kind: CalcOutputKind.AtLeast).ValueText);
        Assert.Equal("1.5", new CalcOutput("n_f", "Safety factor", new ScalarValue(1.5), Kind: CalcOutputKind.AtLeast).ValueText);
        Assert.Equal("2.3086", new CalcOutput("n_f", "Safety factor", new ScalarValue(2.30869), Kind: CalcOutputKind.AtLeast).ValueText);

        // A margin with a unit is rounded down too: -0.0083 in below the minimum stays below it.
        Assert.Equal("0.05999 mm", new CalcOutput("m_max", "Margin", ScalarValue.Of(0.059999, "mm"), Kind: CalcOutputKind.AtLeast).ValueText);

        // Declared by the template through the run, not guessed from the name.
        var r = Run(c =>
        {
            c.Record("n_f", "nf", "from the fatigue", new ScalarValue(1.49999), "Goodman safety factor");
            c.Record("k_f", "kf", "a plain factor", new ScalarValue(1.49999), "Fatigue concentration factor");
            c.MarkAtLeast("n_f");
            c.Output("n_f", "Goodman safety factor");
            c.Output("k_f", "Fatigue concentration factor");
        });
        Assert.Equal("1.4999", r.FindOutput("n_f")!.ValueText);
        Assert.Equal("1.5", r.FindOutput("k_f")!.ValueText);
    }

    [Fact]
    public void A_Safety_Factor_Check_Against_A_Target_Of_1_5_With_1_495_Prints_A_Utilisation_Above_One()
    {
        var r = Run(c =>
        {
            c.Record("n_f", "nf", "from the fatigue", new ScalarValue(1.495), "Goodman safety factor");
            c.Record("u_f", "n_req / n_f", "1.5 / 1.495", new ScalarValue(1.5 / 1.495), "Utilisation");
            c.CheckUtilisation("Safety factor against the target", "u_f");
        });

        var check = Assert.Single(r.Checks);
        Assert.False(check.Passed);
        Assert.Equal("u_f = 1.01", check.DemandText);
        Assert.Equal("1.01", check.UtilisationText);
    }

    [Fact]
    public void A_Failing_Check_Shows_Its_Demand_Rounded_Up_And_Its_Capacity_Rounded_Down_And_A_Passing_One_Rounds_To_The_Nearest()
    {
        var failing = Run(c =>
        {
            c.Record("theta", "angle", "from the geometry", ScalarValue.Of(40.004, "deg"), "Angle");
            c.Record("theta_max", "limit", "the allowed angle", ScalarValue.Of(40, "deg"), "Largest angle allowed");
            c.Check("Angle against its maximum", "theta", "theta_max");
            c.Record("s_a", "stress", "from the loads", ScalarValue.Of(144.9704, "MPa"), "Stress");
            c.Record("s_c", "capacity", "from the material", ScalarValue.Of(144.9696, "MPa"), "Capacity");
            c.Check("Stress against its capacity", "s_a", "s_c");
        });

        var angle = failing.Checks[0];
        Assert.False(angle.Passed);
        Assert.Equal("40.01°", angle.DemandText);
        Assert.Equal("40°", angle.CapacityText);

        var stress = failing.Checks[1];
        Assert.False(stress.Passed);
        Assert.Equal("145.0 MPa", stress.DemandText);
        Assert.Equal("144.9 MPa", stress.CapacityText);

        // 39.996 degrees against a maximum of 40 passes, and printing it as 40.00 is honest: it is within the limit.
        var passing = Run(c =>
        {
            c.Record("theta", "angle", "from the geometry", ScalarValue.Of(39.996, "deg"), "Angle");
            c.Record("theta_max", "limit", "the allowed angle", ScalarValue.Of(40, "deg"), "Largest angle allowed");
            c.Check("Angle against its maximum", "theta", "theta_max");
        });
        Assert.True(passing.Checks[0].Passed);
        Assert.Equal("40.00°", passing.Checks[0].DemandText);
    }

    // ---- utilisation is what the template declares, never what its name says ------------------------------------------------------------------

    [Fact]
    public void A_Utilisation_Is_Known_By_The_Declaration_Or_The_Check_Made_On_It_And_Not_By_Its_Symbol_Or_Description()
    {
        var r = Run(c =>
        {
            c.Record("u_ratio", "factor", "a factor, not a utilisation", new ScalarValue(0.94057), "Factor applied to utilisation");
            c.Record("u_ok", "u", "checked", new ScalarValue(0.94057), "Governing result");
            c.Record("frac", "share", "declared", new ScalarValue(0.94057), "Share of capacity used");
            c.Record("u_small", "u", "declared", new ScalarValue(0.00022), "Small utilisation");
            c.MarkUtilisation("frac", "u_small");
            c.Output("u_ratio", "Factor applied to utilisation");
            c.Output("u_ok", "Governing result");
            c.Output("frac", "Share of capacity used");
            c.Output("u_small", "Small utilisation");
            c.CheckUtilisation("Capacity against the demand", "u_ok");
        });

        Assert.Equal("0.94057", r.FindOutput("u_ratio")!.ValueText);
        Assert.Equal(CalcOutputKind.Value, r.FindOutput("u_ratio")!.Kind);
        Assert.Equal("0.94", r.FindOutput("u_ok")!.ValueText);
        Assert.Equal("0.94", r.FindOutput("frac")!.ValueText);
        Assert.Equal("0.00022", r.FindOutput("u_small")!.ValueText);
        Assert.False(CalcSheetStandard.IsUtilisation(r.FindOutput("u_ratio")!));
        Assert.True(CalcSheetStandard.IsUtilisation(r.FindOutput("u_ok")!));
    }

    [Fact]
    public void A_Utilisation_Output_And_Other_Plain_Numbers_Keep_Their_Forms()
    {
        Assert.Equal("0.94", new CalcOutput("u_life", "Life utilisation", new ScalarValue(0.94057), Kind: CalcOutputKind.Utilisation).ValueText);
        Assert.Equal("0.94057", new CalcOutput("u_life", "Life utilisation", new ScalarValue(0.94057)).ValueText);
        Assert.Equal("7.8516", new CalcOutput("Re_s1", "Reynolds number", new ScalarValue(7.8516)).ValueText);
        Assert.Equal("84.10 %", new CalcOutput("yield", "Yield", ScalarValue.Of(84.1, "%")).ValueText);
        Assert.Equal("30°", new CalcOutput("theta", "Angle", ScalarValue.Of(30, "deg")).ValueText);
        Assert.Equal("30.50°", new CalcOutput("theta", "Angle", ScalarValue.Of(30.5, "deg")).ValueText);
    }

    // ---- H3: one formatter for the sheet, the form, the banner and the register ------------------------------------------------------------

    [Fact]
    public void The_Form_Banner_And_Register_Print_The_Utilisation_The_Sheet_Prints()
    {
        var failing = new CalcCheck("Direct stress against allowable", "u = 1.01", "u ≤ 1", 1.004, false, "u");
        var passing = new CalcCheck("Direct stress against allowable", "u = 0.38", "u ≤ 1", 0.375, true, "u");
        var yesNo = new CalcCheck("Tension only", "tension", "tension", 0, true, "sigma", HasUtilisation: false);

        Assert.Equal("FAIL — Direct stress against allowable: utilisation 1.01", CalcSheetStandard.GoverningBanner(failing));
        Assert.Equal("PASS — Direct stress against allowable: utilisation 0.38", CalcSheetStandard.GoverningBanner(passing));
        Assert.Equal("PASS — Tension only", CalcSheetStandard.GoverningBanner(yesNo));
        Assert.Equal("Direct stress against allowable: u = 1.01 vs u ≤ 1 (u = 1.01)", CalcSheetStandard.FormCheckText(failing));
        Assert.Equal("Tension only: tension vs tension", CalcSheetStandard.FormCheckText(yesNo));

        // The register shows the stored utilisation (a number, 1e9 for an infinite one) the same way.
        Assert.Equal("1.01", CalcDisplay.StoredUtilisation(1.004));
        Assert.Equal("0.38", CalcDisplay.StoredUtilisation(0.375));
        Assert.Equal("—", CalcDisplay.StoredUtilisation(1e9));
        Assert.Equal(failing.UtilisationText, CalcDisplay.StoredUtilisation(failing.Utilisation));
    }

    [Fact]
    public void A_Result_Reads_The_Same_On_The_Text_Sheet_And_In_The_Output_The_Form_Shows()
    {
        var r = Run(c =>
        {
            c.Record("sigma", "stress", "from the loads", ScalarValue.Of(144.93187, "MPa"), "Stress");
            c.Record("u", "u", "a ratio", new ScalarValue(1.004), "Utilisation");
            c.Output("sigma", "Stress");
            c.Output("u", "Utilisation");
            c.CheckUtilisation("Stress against allowable", "u");
        });

        var sheet = CalcSheetBuilder.Build(new DelegateCalculator(_ => { }).Template, new CalcInputSet(), r, GoldenFiles.FixedHeader("test.display", "Display (test)"), GoldenFiles.FixedFooter(new DelegateCalculator(_ => { }).Template, 1, 1));
        var text = CalcSheetTextRenderer.Render(sheet);

        foreach (var output in r.Outputs)
            Assert.Contains($"{output.Symbol} = {output.ValueText}", text, StringComparison.Ordinal);

        Assert.Contains("utilisation 1.01", text, StringComparison.Ordinal);
        Assert.Contains("u = 1.01 vs u ≤ 1", text, StringComparison.Ordinal);
    }

    // ---- the direction of a displayed value is the verdict of its check (C2-21, F3 and F4) ---------------------------------------------------------

    [Fact]
    public void A_Failing_Checks_Demand_And_Capacity_Rows_Are_Not_Shown_On_The_Passing_Side()
    {
        // u = 1.0000012: the hub stress is 350.00042 MPa against 350 MPa. At one decimal the nearest text of the stress is 350.0, equal to the capacity while the check fails.
        var r = Run(c =>
        {
            c.Record("sigma_vm", "stress", "from the loads", ScalarValue.Of(350.00042, "MPa"), "Von Mises stress");
            c.Record("S_y", "capacity", "from the material", ScalarValue.Of(349.99999, "MPa"), "Yield strength");
            c.Output("sigma_vm", "Von Mises stress");
            c.Output("S_y", "Yield strength");
            c.Check("Stress against yield", "sigma_vm", "S_y");
        });

        var check = Assert.Single(r.Checks);
        Assert.False(check.Passed);
        Assert.Equal("350.1 MPa", check.DemandText);
        Assert.Equal("349.9 MPa", check.CapacityText);

        // The Results rows say the same as the check line: the demand up, the capacity down.
        Assert.Equal("350.1 MPa", r.FindOutput("sigma_vm")!.ValueText);
        Assert.Equal("349.9 MPa", r.FindOutput("S_y")!.ValueText);
        Assert.Equal(CalcDisplay.Rounding.Up, CalcDisplay.RoundingOf(r.FindOutput("sigma_vm")!));
        Assert.Equal(CalcDisplay.Rounding.Down, CalcDisplay.RoundingOf(r.FindOutput("S_y")!));
    }

    [Fact]
    public void A_Quantity_That_Is_Not_On_A_Failing_Check_Keeps_Its_Nearest_Text_And_A_Passing_Check_Changes_Nothing()
    {
        var r = Run(c =>
        {
            c.Record("sigma_vm", "stress", "from the loads", ScalarValue.Of(349.96, "MPa"), "Von Mises stress");
            c.Record("S_y", "capacity", "from the material", ScalarValue.Of(350, "MPa"), "Yield strength");
            c.Record("other", "stress", "a second quantity", ScalarValue.Of(100.04, "MPa"), "Second stress");
            c.Output("sigma_vm", "Von Mises stress");
            c.Output("S_y", "Yield strength");
            c.Output("other", "Second stress");
            c.Check("Stress against yield", "sigma_vm", "S_y");
        });

        Assert.True(r.Checks[0].Passed);
        Assert.Equal("350.0 MPa", r.FindOutput("sigma_vm")!.ValueText);
        Assert.Equal("100.0 MPa", r.FindOutput("other")!.ValueText);
    }

    [Fact]
    public void A_Failing_Demand_That_Is_A_Hair_From_A_Whole_Number_Is_Not_Printed_As_That_Whole_Number()
    {
        // 12.0000001 mm against 11.99999 mm: a clean-number snap would print 12 against 11.99, and the failing demand is above 12.
        var r = Run(c =>
        {
            c.Record("s_2", "deflection", "from the loads", ScalarValue.Of(12.0000001, "mm"), "Deflection");
            c.Record("y_cr", "limit", "from the geometry", ScalarValue.Of(11.99999, "mm"), "Buckling deflection");
            c.Output("s_2", "Deflection");
            c.Output("y_cr", "Buckling deflection");
            c.Check("Deflection against the buckling deflection", "s_2", "y_cr");
        });

        Assert.False(r.Checks[0].Passed);
        Assert.Equal("12.01 mm", r.FindOutput("s_2")!.ValueText);
        Assert.Equal("11.99 mm", r.FindOutput("y_cr")!.ValueText);
    }

    [Fact]
    public void A_Failure_A_Hair_Over_The_Limit_Is_Not_Erased_By_The_Clean_Number_Snap_In_The_Check_Line_Or_In_Results()
    {
        // 350.000000035 MPa against 350 MPa with a tolerance of 1e-12: u = 1 + 1e-10 fails. The clean-number snap would print the demand as 350.0, equal to the capacity.
        var r = Run(c =>
        {
            c.Record("sigma", "stress", "from the loads", ScalarValue.Of(350.000000035, "MPa"), "Stress");
            c.Record("S_y", "capacity", "from the material", ScalarValue.Of(350, "MPa"), "Yield strength");
            c.Output("sigma", "Stress");
            c.Output("S_y", "Yield strength");
            Assert.False(c.Check("Stress against yield", "sigma", "S_y", tolerance: 1e-12).Passed);
        });

        Assert.Equal("350.1 MPa", r.Checks[0].DemandText);
        Assert.Equal("350 MPa", r.Checks[0].CapacityText);
        Assert.Equal("350.1 MPa", r.FindOutput("sigma")!.ValueText);
        Assert.Equal("350 MPa", r.FindOutput("S_y")!.ValueText);
    }

    [Fact]
    public void A_Failure_Well_Over_The_Limit_Keeps_The_Clean_Number_It_Has()
    {
        // 1.25 held as 1.2500000000000002 is shown 1.25: the snap only yields where it would put a failure on the passing side.
        Assert.Equal("1.25", CalcDisplay.Utilisation(1.2500000000000002, passed: false));
        Assert.Equal("1.25", CalcDisplay.Utilisation(1.25, passed: false));
    }

    [Theory]
    [InlineData(1.0000000001, false, "1.01")] // a failing utilisation a hair over its limit is never printed as 1.00 (a clean-number snap printed it so)
    [InlineData(1.0000012, false, "1.01")]
    [InlineData(1.004, false, "1.01")]
    [InlineData(1.004, true, "1.00")] // a pass within the check's tolerance never prints above the limit
    [InlineData(1.0049, true, "1.00")]
    [InlineData(0.996, true, "1.00")]
    [InlineData(0.994, true, "0.99")]
    [InlineData(0.004, true, "0.0040")]
    [InlineData(-0.5, false, "-0.50")]
    [InlineData(-0.456, false, "-0.46")]
    public void The_Utilisation_Is_Rounded_By_The_Verdict_Of_Its_Check_Not_By_A_Threshold(double u, bool passed, string expected) =>
        Assert.Equal(expected, CalcDisplay.Utilisation(u, passed));

    [Fact]
    public void A_Check_With_A_Tolerance_Tighter_Than_The_Display_Snap_Prints_A_Failing_Utilisation_Above_Its_Limit()
    {
        var failing = Run(c =>
        {
            c.Record("u", "u", "a ratio a hair over 1", new ScalarValue(1.0 + 1e-10), "Utilisation");
            c.Output("u", "Utilisation");
            var check = c.CheckUtilisation("Stress against allowable", "u", tolerance: 1e-12);
            Assert.False(check.Passed);
        });

        Assert.Equal("u = 1.01", failing.Checks[0].DemandText);
        Assert.Equal("1.01", failing.Checks[0].UtilisationText);
        Assert.Equal("1.01", failing.FindOutput("u")!.ValueText);
        Assert.Equal("FAIL — Stress against allowable: utilisation 1.01", CalcSheetStandard.GoverningBanner(failing.Checks[0]));
    }

    [Fact]
    public void A_Check_That_Passes_Within_Its_Tolerance_Prints_The_Limit_And_Never_A_Value_Above_It()
    {
        var passing = Run(c =>
        {
            c.Record("u", "u", "a ratio within the check's tolerance of 1", new ScalarValue(1.006), "Utilisation");
            c.Output("u", "Utilisation");
            var check = c.CheckUtilisation("Stress against allowable", "u", tolerance: 0.01);
            Assert.True(check.Passed);
        });

        Assert.Equal("u = 1.00", passing.Checks[0].DemandText);
        Assert.Equal("1.00", passing.Checks[0].UtilisationText);
        Assert.Equal("1.00", passing.FindOutput("u")!.ValueText);
        Assert.Equal("PASS — Stress against allowable: utilisation 1.00", CalcSheetStandard.GoverningBanner(passing.Checks[0]));
    }

    [Fact]
    public void The_Banner_And_The_Register_Round_By_The_Stored_Verdict()
    {
        var passing = new CalcCheck("Stress against allowable", "u = 1.00", "u ≤ 1", 1.004, true, "u");
        var failing = passing with { Passed = false };

        Assert.Equal("1.00", passing.UtilisationText);
        Assert.Equal("1.01", failing.UtilisationText);
        Assert.Equal("1.00", CalcDisplay.StoredUtilisation(1.004, true));
        Assert.Equal("1.01", CalcDisplay.StoredUtilisation(1.004, false));
        Assert.Equal("—", CalcDisplay.StoredUtilisation(1e9, false));
    }

    [Fact]
    public void An_Output_Declared_As_A_Utilisation_Is_Shown_By_The_Verdict_Of_The_Check_Made_On_It_And_Rounds_Up_When_Nothing_Checks_It_Above_One()
    {
        var r = Run(c =>
        {
            c.Record("u_a", "u", "within tolerance", new ScalarValue(1.006), "Utilisation");
            c.Record("u_b", "u", "no check made", new ScalarValue(1.006), "Utilisation");
            c.MarkUtilisation("u_b");
            c.Output("u_a", "Utilisation a");
            c.Output("u_b", "Utilisation b");
            c.CheckUtilisation("Stress against allowable", "u_a", tolerance: 0.01);
        });

        Assert.Equal("1.00", r.FindOutput("u_a")!.ValueText);
        Assert.Equal("1.01", r.FindOutput("u_b")!.ValueText);
    }
}
