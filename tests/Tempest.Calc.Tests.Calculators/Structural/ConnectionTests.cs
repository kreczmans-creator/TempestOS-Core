using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class ConnectionTests
{
    private static readonly ConnectionCalculator Ec3 = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Ec3.Template);

    /// <summary>The defaults with the pitch and gauge opened out so that only e₁ and e₂ govern the bearing factors (the finding's single-bolt reproductions).</summary>
    private static CalcInputSet EdgeOnly() => Defaults().With(CalcInputValue.Quantity("p_1", 200, "mm")).With(CalcInputValue.Quantity("p_2", 200, "mm"));

    [Theory]
    [InlineData(12, 1.75, 84.3)]
    [InlineData(16, 2.0, 157)]
    [InlineData(20, 2.5, 245)]
    [InlineData(24, 3.0, 353)]
    [InlineData(30, 3.5, 561)]
    [InlineData(36, 4.0, 817)]
    public void Stress_Area_Formula_Gives_The_Familiar_Values(double d, double pitch, double expected) =>
        Assert.Equal(expected, ConnectionCalculator.StressArea(d, pitch), expected * 0.003);

    [Theory]
    [InlineData("4.6", true, 0.6)]
    [InlineData("4.8", true, 0.5)]
    [InlineData("5.8", true, 0.5)]
    [InlineData("6.8", true, 0.5)]
    [InlineData("8.8", true, 0.6)]
    [InlineData("10.9", true, 0.5)]
    [InlineData("10.9", false, 0.6)]
    public void Alpha_V_Follows_Table_3_4(string grade, bool threads, double alphaV) => Assert.Equal(alphaV, ConnectionCalculator.AlphaV(grade, threads));

    [Fact]
    public void Shank_In_The_Shear_Plane_Uses_The_Gross_Area()
    {
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Switch("threads", false)));
        Assert.Equal(0.6 * 800 * Math.PI / 4 * 400 / 1.25, r.OutputSi("F_v_Rd"), 3);
    }

    [Fact]
    public void Edge_Distance_Below_The_Minimum_Fails_Spacing()
    {
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("e_1", 22, "mm")));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Spacing", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void Plate_Thinner_Than_3_Mm_Is_A_Hard_Fail()
    {
        Assert.Contains(CalcInputValidator.Validate(Ec3.Template, Defaults().With(CalcInputValue.Quantity("t_p", 2, "mm"))), n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void Bearing_Never_Exceeds_The_Plate_Limit()
    {
        foreach (var e1 in new[] { 30.0, 60, 120, 400 })
        {
            var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("e_1", e1, "mm")));
            Assert.True(r.OutputSi("F_b_Rd") <= 2.5 * 410e6 * 0.020 * 0.010 / 1.25 + 1e-6);
        }
    }

    [Fact]
    public void An_Oversize_Hole_Reduces_Bearing_By_0_8_And_Says_So_Even_When_The_Type_Is_Left_At_Normal()
    {
        // P1-struct.connection-ec3-01. M20 8.8, 10 mm S275, e1 = e2 = 40, d0 24: python3 2.5*(40/72)*410*20*10/1.25/1e3 = 91.111 kN, x 0.8 = 72.889 kN.
        var left = CalcRunner.Run(Ec3, EdgeOnly().With(CalcInputValue.Quantity("d_0", 24, "mm")));
        Assert.Equal(72.88889, left.OutputSi("F_b_Rd") / 1e3, 4);
        Assert.Contains(left.Warnings, w => w.RuleId == "hole_clearance");

        var oversize = CalcRunner.Run(Ec3, EdgeOnly().With(CalcInputValue.Quantity("d_0", 24, "mm")).With(CalcInputValue.Choice("hole_type", ConnectionCalculator.HoleOversize)));
        Assert.Equal(72.88889, oversize.OutputSi("F_b_Rd") / 1e3, 4);
        Assert.DoesNotContain(oversize.Warnings, w => w.RuleId == "hole_clearance");

        var slot = CalcRunner.Run(Ec3, EdgeOnly().With(CalcInputValue.Quantity("d_0", 24, "mm")).With(CalcInputValue.Choice("hole_type", ConnectionCalculator.HoleSlot)));
        Assert.Equal(54.66667, slot.OutputSi("F_b_Rd") / 1e3, 4);

        var normal = CalcRunner.Run(Ec3, EdgeOnly());
        Assert.Equal(99.39394, normal.OutputSi("F_b_Rd") / 1e3, 4);
        Assert.DoesNotContain(normal.Warnings, w => w.RuleId == "hole_clearance");
    }

    [Fact]
    public void A_Single_Lap_Joint_With_One_Bolt_Row_Caps_Bearing_At_1_5_Fu_D_T()
    {
        // P1-struct.connection-ec3-03. e1 = e2 = 60: k1*alpha_b = 2.5*0.9091 = 2.27 > 1.5; python3 gives 149.091 kN uncapped and 1.5*410*20*10/1.25/1e3 = 98.4 kN capped.
        var wide = EdgeOnly().With(CalcInputValue.Quantity("e_1", 60, "mm")).With(CalcInputValue.Quantity("e_2", 60, "mm"));
        var capped = CalcRunner.Run(Ec3, wide.With(CalcInputValue.Switch("single_lap", true)));
        Assert.Equal(98.4, capped.OutputSi("F_b_Rd") / 1e3, 6);
        Assert.DoesNotContain(capped.Warnings, w => w.RuleId == "single_lap");

        var unstated = CalcRunner.Run(Ec3, wide);
        Assert.Equal(149.09091, unstated.OutputSi("F_b_Rd") / 1e3, 4);
        Assert.Contains(unstated.Warnings, w => w.RuleId == "single_lap" && w.Message.Contains("98.4", StringComparison.Ordinal));

        var doubleShear = CalcRunner.Run(Ec3, wide.With(CalcInputValue.Switch("single_lap", true)).With(CalcInputValue.Of("n_s", 2)));
        Assert.Equal(149.09091, doubleShear.OutputSi("F_b_Rd") / 1e3, 4);
        Assert.DoesNotContain(doubleShear.Warnings, w => w.RuleId == "single_lap");

        // The default M20 case binds by 1 %: 99.39 > 98.40 kN.
        Assert.Contains(CalcRunner.Run(Ec3, EdgeOnly()).Warnings, w => w.RuleId == "single_lap");
    }

    [Fact]
    public void Weld_Methods_Agree_For_Pure_Longitudinal_Shear()
    {
        // τ∥ only: directional limit f_u/(√3 β_w γ_M2) equals the simplified strength.
        var fvwd = 410 / Math.Sqrt(3) / (0.85 * 1.25);
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("tau_par", fvwd, "MPa")));
        Assert.Equal(1.0, r.OutputSi("u_dir"), 6);
    }

    [Fact]
    public void Block_Tearing_Needs_All_Three_Inputs()
    {
        var partial = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("V_Ed", 100, "kN")));
        Assert.Contains(partial.Warnings, w => w.RuleId == "block");
        var full = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("V_Ed", 100, "kN")).With(CalcInputValue.Quantity("A_nt", 300, "mm^2")).With(CalcInputValue.Quantity("A_nv", 1200, "mm^2")));
        Assert.Equal(410 * 300 / 1.25 + 275 * 1200 / Math.Sqrt(3), full.OutputSi("V_eff_Rd"), 3);
    }

    [Theory]
    [InlineData(ConnectionCalculator.HoleOversize)]
    [InlineData(ConnectionCalculator.HoleSlot)]
    public void An_Oversize_Or_Slotted_Hole_Type_Without_The_Hole_Size_Is_Blocked(string holeType)
    {
        // C-1 verification N1: M20, e1 = e2 = 40, hole type Oversize and d_0 blank kept the normal 22 mm hole in alpha_d and k_1: F_b,Rd 79.52 kN against 72.89 kN at the real 24 mm oversize hole
        // (9 % unconservative, silent). python3: alpha_b = min(40/(3 x 22), 800/410, 1) = 0.6061 -> k_1 = min(2.8 x 40/22 - 1.7, 2.5) = 2.5, 2.5 x 0.6061 x 410 x 20 x 10/1.25 x 0.8 = 79.52 kN;
        // at d_0 = 24: alpha_b = 0.5556, k_1 = 2.5 (2.8 x 40/24 - 1.7 = 2.967), 2.5 x 0.5556 x 410 x 20 x 10/1.25 x 0.8 = 72.89 kN.
        var blank = CalcRunner.Run(Ec3, EdgeOnly().With(CalcInputValue.Choice("hole_type", holeType)));
        Assert.True(blank.IsBlocked);
        var block = Assert.Single(blank.Blocks);
        Assert.Equal("hole", block.RuleId);
        Assert.StartsWith("d_0:", block.Message, StringComparison.Ordinal);

        var entered = CalcRunner.Run(Ec3, EdgeOnly().With(CalcInputValue.Quantity("d_0", 24, "mm")).With(CalcInputValue.Choice("hole_type", holeType)));
        Assert.False(entered.IsBlocked);

        // Normal clearance with d_0 blank still takes d + 2.
        Assert.Equal(99.39, CalcRunner.Run(Ec3, EdgeOnly()).OutputSi("F_b_Rd") / 1e3, 2);
    }

    [Fact]
    public void Bearing_Is_Taken_On_The_Thinner_Of_Two_Plies()
    {
        // chain.steel-support-R1: M20 8.8, S275, e1 = e2 = 40, p1 150, p2 120: bearing on the 20 mm end plate F_b,Rd 198.79 kN; the bolts also bear on the 11 mm column flange: 109.33 kN (45 % lower).
        // python3: alpha_b = min(40/66, 150/66 - 0.25, 800/410, 1) = 0.60606, k_1 = min(2.8 x 40/22 - 1.7, 1.4 x 120/22 - 1.7, 2.5) = 2.5, F_b = 2.5 x 0.60606 x 410 x 20 x t/1.25/1e3 kN with t 20 and 11.
        var set = Defaults().With(CalcInputValue.Quantity("t_p", 20, "mm")).With(CalcInputValue.Quantity("p_1", 150, "mm")).With(CalcInputValue.Quantity("p_2", 120, "mm"));
        Assert.Equal(198.788, CalcRunner.Run(Ec3, set).OutputSi("F_b_Rd") / 1e3, 2);
        var two = CalcRunner.Run(Ec3, set.With(CalcInputValue.Quantity("t_2", 11, "mm")));
        Assert.Equal(109.333, two.OutputSi("F_b_Rd") / 1e3, 2);

        // A thicker second ply changes nothing.
        Assert.Equal(198.788, CalcRunner.Run(Ec3, set.With(CalcInputValue.Quantity("t_2", 30, "mm"))).OutputSi("F_b_Rd") / 1e3, 2);
    }

    [Fact]
    public void An_Eccentric_Block_Tearing_Group_Uses_The_Halved_Tension_Term()
    {
        // struct.connection-ec3-02: A_nt 300, A_nv 900, f_u 410, f_y 275: V_eff,1 = 98.4 + 142.89 = 241.29 kN (concentric); V_eff,2 = 0.5 * 98.4 + 142.89 = 192.09 kN (eccentric).
        // python3: 410*300/1.25/1e3 = 98.4; 275*900/sqrt(3)/1e3 = 142.894.
        var set = Defaults().With(CalcInputValue.Quantity("V_Ed", 100, "kN")).With(CalcInputValue.Quantity("A_nt", 300, "mm^2")).With(CalcInputValue.Quantity("A_nv", 900, "mm^2"));
        Assert.Equal(241.2942, CalcRunner.Run(Ec3, set).OutputSi("V_eff_Rd") / 1e3, 3);

        var eccentric = CalcRunner.Run(Ec3, set.With(CalcInputValue.Choice("block_load", ConnectionCalculator.BlockEccentric)));
        Assert.Equal(192.0942, eccentric.OutputSi("V_eff_Rd") / 1e3, 3);
        Assert.Equal(100 / 192.0942, eccentric.OutputSi("u_block"), 4);
    }

    [Fact]
    public void Bolt_Values_That_Feed_The_Base_Plate_Are_Ports()
    {
        var r = CalcRunner.Run(Ec3, Defaults());
        var ports = Ec3.Template.OutputPorts!;

        foreach (var (symbol, unit) in new[] { ("F_t_Rd", "kN"), ("A_s", "mm^2"), ("F_b_Rd", "kN") })
        {
            Assert.Contains(ports, p => p.Symbol == symbol && p.Unit == unit);
            Assert.NotNull(r.FindOutput(symbol));
        }

        // M20: A_s = 244.79 mm², F_t,Rd = 0.9 f_ub A_s / γ_M2.
        Assert.Equal(244.794, r.OutputSi("A_s") * 1e6, 3);
        Assert.Equal(0.9 * 800 * ConnectionCalculator.StressArea(20, 2.5) / 1.25, r.OutputSi("F_t_Rd"), 1e-3);
    }

    [Fact]
    public void Bolt_Strengths_Are_Ports_For_The_Anchor_Bolt_Data_Of_The_Base_Plate()
    {
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Choice("grade", "10.9")));
        Assert.Equal(1000e6, r.OutputSi("f_ub"), 1);
        Assert.Equal(900e6, r.OutputSi("f_yb"), 1);
        Assert.Contains(Ec3.Template.OutputPorts!, p => p is { Symbol: "f_ub", Unit: "MPa" });
        Assert.Contains(Ec3.Template.OutputPorts!, p => p is { Symbol: "f_yb", Unit: "MPa" });
    }

    [Theory]
    [InlineData("e_2", 13.3)]
    [InlineData("e_2", 10)]
    [InlineData("p_2", 26.7)]
    [InlineData("p_2", 20)]
    public void A_Spacing_That_Leaves_No_Bearing_Resistance_Blocks_In_The_Calculators_Own_Words(string symbol, double mm)
    {
        // Black B-A-06: M20 (d₀ = 22 mm) with e₂ ≤ 13.3 mm or p₂ ≤ 26.7 mm gave k₁ ≤ 0, and the sheet surfaced "BLOCK engine: Division by zero."
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity(symbol, mm, "mm")));
        Assert.True(r.IsBlocked);
        Assert.Equal("spacing", r.Blocks[0].RuleId);
        Assert.StartsWith(symbol + ":", r.Blocks[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Division by zero", r.Blocks[0].Message, StringComparison.Ordinal);
        Assert.Contains("Table 3.3", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Tiny_But_Positive_Bearing_Factor_Still_Computes_And_Fails_Bearing()
    {
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("e_2", 13.4, "mm")));
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Checks, c => c.Name == "Shear force against bolt bearing resistance" && !c.Passed);
    }

    [Fact]
    public void Punching_Takes_The_Across_Flats_As_d_m_Unless_The_Across_Corners_Is_Entered()
    {
        // Green G-A-09: the sharp-hexagon across-corners (34.64 mm for M20) overstated d_m by 8 %.
        var plain = CalcRunner.Run(Ec3, Defaults());
        Assert.Equal(0.6 * Math.PI * 30 * 10 * 410 / 1.25, plain.OutputSi("B_p_Rd"), 3);
        var entered = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("e_head", 34, "mm")));
        Assert.Equal(0.6 * Math.PI * 32 * 10 * 410 / 1.25, entered.OutputSi("B_p_Rd"), 3);
        Assert.True(entered.OutputSi("B_p_Rd") > plain.OutputSi("B_p_Rd"));
    }

    [Fact]
    public void An_M12_Bolt_In_Its_Normal_Clearance_Hole_Takes_0_85_On_Its_Shear_Resistance_And_Prints_The_Factor_As_A_Step()
    {
        // DC-03 and DC-21 (C-1 finding struct.connection-ec3-R1). M12 8.8, F_v,Ed 30 kN in the 14 mm normal clearance hole:
        // python3: A_s = pi/4*((12-0.649519*1.75 + 12-1.226869*1.75)/2)**2 = 84.2665 mm2; 0.6*800*84.2665/1.25/1e3 = 32.3584 kN; x 0.85 = 27.5046 kN; 30/27.5046 = 1.0907 (FAIL); 30/32.3584 = 0.9271 (passed before the factor).
        var m12 = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Choice("size", "M12")).With(CalcInputValue.Quantity("F_v_Ed", 30, "kN")));
        Assert.False(m12.IsBlocked);
        Assert.Equal(27.5046, m12.OutputSi("F_v_Rd") / 1e3, 3);
        Assert.Equal(0.85, m12.OutputSi("k_v12"), 12);
        Assert.Equal(1.0907, m12.OutputSi("u_v"), 3);
        Assert.Contains(m12.Checks, c => c.Name == "Shear force against bolt shear resistance" && !c.Passed);

        var step = Assert.Single(m12.Steps, st => st.Symbol == "k_v12");
        Assert.Contains("M12", step.Formula, StringComparison.Ordinal);
        Assert.Contains("clearance hole", step.Formula, StringComparison.Ordinal);
        Assert.Contains("Table 3.4", step.Clause ?? string.Empty, StringComparison.Ordinal);
        Assert.StartsWith("k_v12·", m12.Steps.Single(st => st.Symbol == "F_v_Rd").Formula, StringComparison.Ordinal);

        // The same bolt in a 13 mm hole, an M16 in its normal hole, and an M12 of class 4.6 or 5.6 carry no factor.
        foreach (var set in new[]
        {
            Defaults().With(CalcInputValue.Choice("size", "M12")).With(CalcInputValue.Quantity("d_0", 13, "mm")),
            Defaults().With(CalcInputValue.Choice("size", "M16")),
            Defaults().With(CalcInputValue.Choice("size", "M12")).With(CalcInputValue.Choice("grade", "4.6")),
            Defaults().With(CalcInputValue.Choice("size", "M12")).With(CalcInputValue.Choice("grade", "5.6")),
        })
        {
            var r = CalcRunner.Run(Ec3, set);
            Assert.False(r.IsBlocked);
            Assert.Null(r.FindOutput("k_v12"));
            Assert.DoesNotContain(r.Steps, st => st.Symbol == "k_v12");
            Assert.DoesNotContain("k_v12", r.Steps.Single(st => st.Symbol == "F_v_Rd").Formula, StringComparison.Ordinal);
        }

        // M12 8.8 in a 13 mm hole keeps the full 32.358 kN; a 14 mm hole entered by hand is the normal clearance hole and takes the factor.
        Assert.Equal(32.3584, CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Choice("size", "M12")).With(CalcInputValue.Quantity("d_0", 13, "mm"))).OutputSi("F_v_Rd") / 1e3, 3);
        Assert.Equal(27.5046, CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Choice("size", "M12")).With(CalcInputValue.Quantity("d_0", 14, "mm"))).OutputSi("F_v_Rd") / 1e3, 3);

        // An M12 in an oversize hole (d_0 15, type Oversize) is outside the note: no factor on shear, 0.8 on bearing.
        var oversize = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Choice("size", "M12")).With(CalcInputValue.Quantity("d_0", 15, "mm")).With(CalcInputValue.Choice("hole_type", ConnectionCalculator.HoleOversize)));
        Assert.Equal(32.3584, oversize.OutputSi("F_v_Rd") / 1e3, 3);
        Assert.Null(oversize.FindOutput("k_v12"));
    }

    [Theory]
    [InlineData("4.6", 12, 14, ConnectionCalculator.HoleNormal, 1.0)]
    [InlineData("4.8", 12, 14, ConnectionCalculator.HoleNormal, 0.85)]
    [InlineData("5.6", 12, 14, ConnectionCalculator.HoleNormal, 1.0)]
    [InlineData("5.8", 12, 14, ConnectionCalculator.HoleNormal, 0.85)]
    [InlineData("6.8", 12, 14, ConnectionCalculator.HoleNormal, 0.85)]
    [InlineData("8.8", 12, 14, ConnectionCalculator.HoleNormal, 0.85)]
    [InlineData("10.9", 12, 14, ConnectionCalculator.HoleNormal, 0.85)]
    [InlineData("8.8", 12, 13, ConnectionCalculator.HoleNormal, 1.0)]
    [InlineData("8.8", 12, 15, ConnectionCalculator.HoleNormal, 1.0)]
    [InlineData("8.8", 12, 14, ConnectionCalculator.HoleOversize, 1.0)]
    [InlineData("8.8", 12, 14, ConnectionCalculator.HoleSlot, 1.0)]
    [InlineData("8.8", 16, 18, ConnectionCalculator.HoleNormal, 1.0)]
    [InlineData("8.8", 20, 22, ConnectionCalculator.HoleNormal, 1.0)]
    public void The_Clearance_Shear_Factor_Is_0_85_Only_For_An_M12_Of_Class_4_8_To_10_9_In_Its_14_Mm_Normal_Hole(string grade, double d, double d0, string holeType, double expected) =>
        Assert.Equal(expected, ConnectionCalculator.ClearanceShearFactor(grade, d, d0, holeType));

    [Fact]
    public void An_Across_Corners_Below_The_Across_Flats_Is_Refused()
    {
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("e_head", 25, "mm")));
        Assert.True(r.IsBlocked);
        Assert.StartsWith("e_head:", r.Blocks[0].Message, StringComparison.Ordinal);
    }
}
