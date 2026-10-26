using CsCheck;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class SpringTests
{
    private static readonly SpringCalculator Spring = new();

    /// <summary>The first known-answer case (compression, loads, Bergsträsser, squared and ground) with the template defaults under it.</summary>
    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Spring.Template);

    private static CalcRunResult Run(params CalcInputValue[] changes) => CalcRunner.Run(Spring, changes.Aggregate(Defaults(), (set, v) => set.With(v)));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static string Text(CalcRunResult r, string symbol) => ((TextValue)r.FindOutput(symbol)!.Value).Value;

    private static CalcInputValue Mode(string mode) => CalcInputValue.Choice("mode", mode);

    [Fact]
    public void The_Rate_Is_G_d4_Over_8_D3_n_And_The_Force_Is_Rate_Times_Deflection()
    {
        Gen.Select(Gen.Double[0.5, 10], Gen.Double[5, 15], Gen.Double[2, 30], Gen.Double[40, 100], Gen.Double[0.1, 3]).Sample((d, c, n, g, s) =>
        {
            var r = Run(Q("d", d, "mm"), Q("D", d * c, "mm"), CalcInputValue.Of("n", n), Q("G", g, "GPa"), CalcInputValue.Choice("work", SpringCalculator.Deflections), Q("s_1", 0, "mm"), Q("s_2", s, "mm"), Q("L0", 5000, "mm"));
            var k = g * 1e9 * Math.Pow(d * 1e-3, 4) / (8 * Math.Pow(d * c * 1e-3, 3) * n);
            return !r.IsBlocked && Math.Abs(r.OutputSi("k") - k) <= 1e-9 * k && Math.Abs(r.OutputSi("F_2") - k * s * 1e-3) <= 1e-9 * k * s * 1e-3;
        }, iter: 60);
    }

    [Fact]
    public void Loads_And_Deflections_Describe_The_Same_State()
    {
        Gen.Double[1, 100].Sample(f =>
        {
            var byLoad = Run(Q("F_1", f / 4, "N"), Q("F_2", f, "N"));
            var byDeflection = Run(CalcInputValue.Choice("work", SpringCalculator.Deflections), Q("s_1", byLoad.OutputSi("s_1") * 1e3, "mm"), Q("s_2", byLoad.OutputSi("s_2") * 1e3, "mm"));
            return Math.Abs(byDeflection.OutputSi("F_2") - f) <= 1e-9 * f && Math.Abs(byDeflection.OutputSi("tau_2") - byLoad.OutputSi("tau_2")) <= 1e-9 * byLoad.OutputSi("tau_2");
        }, iter: 40);
    }

    [Theory]
    [InlineData(SpringCalculator.Plain, 8, 18, 18)]
    [InlineData(SpringCalculator.PlainGround, 9, 18, 18)]
    [InlineData(SpringCalculator.Squared, 10, 22, 23)]
    [InlineData(SpringCalculator.SquaredGround, 10, 20, 20)]
    public void Solid_Length_And_Pitch_Follow_The_End_Type(string ends, double coils, double shigley, double en)
    {
        // d = 2 mm, n = 8, L0 = 45 mm: coils and solid length from the end-type relations, the pitch from the free-length relation of the same end.
        // The EN 13906-1 form (the default) differs from Shigley's only for squared unground ends, by 0.5 d.
        var r = Run(CalcInputValue.Choice("ends", ends), Q("L0", 45, "mm"));
        Assert.Equal(coils, r.OutputSi("n_t"), 9);
        Assert.Equal(en * 1e-3, r.OutputSi("L_s"), 9);
        var shigleyRun = Run(CalcInputValue.Choice("ends", ends), Q("L0", 45, "mm"), CalcInputValue.Choice("solid_method", SpringCalculator.SolidShigley));
        Assert.Equal(shigley * 1e-3, shigleyRun.OutputSi("L_s"), 9);
        var (a, b) = ends switch
        {
            SpringCalculator.Plain => (1.0, 0.0),
            SpringCalculator.PlainGround => (0.0, 1.0),
            SpringCalculator.Squared => (3.0, 0.0),
            _ => (2.0, 0.0),
        };
        Assert.Equal((45 - a * 2) / (8 + b) * 1e-3, r.OutputSi("p"), 9);
    }

    [Fact]
    public void A_Free_Length_At_Or_Below_Solid_Blocks()
    {
        var r = Run(Q("L0", 20, "mm"));
        Assert.True(r.IsBlocked);
        Assert.Equal("solid", r.Blocks[0].RuleId);
        Assert.StartsWith("L0:", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Correction_Factor_Follows_The_Chosen_Formula()
    {
        var berg = Run().OutputSi("k_c");
        var wahl = Run(CalcInputValue.Choice("corr", SpringCalculator.Wahl)).OutputSi("k_c");
        var none = Run(CalcInputValue.Choice("corr", SpringCalculator.Uncorrected)).OutputSi("k_c");
        Assert.Equal(8.5 / 7.25, berg, 12);
        Assert.Equal(31.0 / 28.0 + 0.615 / 8, wahl, 12);
        Assert.Equal(1.0, none, 12);
        Assert.True(wahl > berg && berg > none);
    }

    [Fact]
    public void Choosing_No_Correction_For_The_Static_Stress_Leaves_The_Curvature_Factor_In_The_Fatigue_Stresses()
    {
        // mech.spring-01: d 4, D 16 (C = 4), F 100 to 400 N, R_m 1700, S_e 300 MPa: 'None' removed the curvature factor from the Goodman stresses too, u_f 0.458 against 0.634 with Bergstraesser.
        // python3: k_c = 4.5/3.25 = 1.384615, tau_1 = 88.147, tau_2 = 352.589 MPa, a = 132.221, m = 220.368, u_f = 132.221/300 + 220.368/(0.67 x 1700) = 0.634212.
        CalcInputValue[] spring = [Q("d", 4, "mm"), Q("D", 16, "mm"), Q("L0", 100, "mm"), Q("F_1", 100, "N"), Q("F_2", 400, "N"), Q("Rm", 1700, "MPa"), Q("S_e", 300, "MPa")];
        var berg = Run([.. spring, CalcInputValue.Choice("corr", SpringCalculator.Bergstraesser)]);
        var none = Run([.. spring, CalcInputValue.Choice("corr", SpringCalculator.Uncorrected)]);
        Assert.False(berg.IsBlocked, string.Join("; ", berg.Blocks.Select(b => b.Message)));
        Assert.False(none.IsBlocked, string.Join("; ", none.Blocks.Select(b => b.Message)));
        Assert.Equal(0.634212, berg.OutputSi("u_f"), 5);
        Assert.Equal(berg.OutputSi("u_f"), none.OutputSi("u_f"), 12);
        Assert.Equal(berg.OutputSi("n_f"), none.OutputSi("n_f"), 12);

        // The static stress still follows the choice: 'None' is the uncorrected 8 F D / (pi d^3), 63.662 and 254.648 MPa, and Bergstraesser is 1.3846 times that.
        Assert.Equal(1.0, none.OutputSi("k_c"), 12);
        Assert.Equal(254.648, none.OutputSi("tau_2") / 1e6, 3);
        Assert.Equal(352.589, berg.OutputSi("tau_2") / 1e6, 3);

        // Wahl keeps its own factor in the fatigue stresses.
        var wahl = Run([.. spring, CalcInputValue.Choice("corr", SpringCalculator.Wahl)]);
        Assert.True(wahl.OutputSi("u_f") > berg.OutputSi("u_f"));
    }

    [Fact]
    public void A_Spring_Index_Outside_4_To_20_Warns()
    {
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "index");
        Assert.Contains(Run(Q("D", 6, "mm")).Warnings, w => w.RuleId == "index");
        Assert.Contains(Run(Q("D", 50, "mm")).Warnings, w => w.RuleId == "index");
    }

    [Fact]
    public void Buckling_Passes_Below_The_Critical_Deflection_And_Fails_At_Or_Above_It()
    {
        var slender = new[] { Q("D", 20, "mm"), CalcInputValue.Of("n", 10), CalcInputValue.Choice("ends", SpringCalculator.PlainGround), Q("L0", 80, "mm"), CalcInputValue.Of("nu_seat", 1) };
        var ok = Run([.. slender, Q("F_1", 8, "N"), Q("F_2", 24, "N")]);
        Assert.Equal("PASS", Text(ok, "buckling"));
        Assert.DoesNotContain(ok.Warnings, w => w.RuleId == "buckling");

        var over = Run([.. slender, Q("F_1", 8, "N"), Q("F_2", 60, "N")]);
        // C-2 fix (M1): the buckling status reads FAIL (it read WARN), the warning stays and the buckling check is in the verdict.
        Assert.Equal("FAIL", Text(over, "buckling"));
        Assert.Contains(over.Warnings, w => w.RuleId == "buckling");
        Assert.True(over.OutputSi("u_b") > 1);
        Assert.False(over.Passed);
    }

    [Fact]
    public void A_Short_Spring_Is_Stable_At_Any_Deflection()
    {
        var r = Run();
        Assert.Equal("PASS", Text(r, "buckling"));
        Assert.Equal(0, r.OutputSi("u_b"));
        Assert.Contains("stable", Text(r, "y_cr"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_Looser_End_Support_Lowers_The_Buckling_Deflection()
    {
        Gen.Select(Gen.Double[0.5, 1.2], Gen.Double[1.3, 2]).Sample((tight, loose) =>
        {
            var a = Run(Q("D", 20, "mm"), CalcInputValue.Of("n", 10), CalcInputValue.Choice("ends", SpringCalculator.PlainGround), Q("L0", 150, "mm"), CalcInputValue.Of("nu_seat", tight));
            var b = Run(Q("D", 20, "mm"), CalcInputValue.Of("n", 10), CalcInputValue.Choice("ends", SpringCalculator.PlainGround), Q("L0", 150, "mm"), CalcInputValue.Of("nu_seat", loose));
            return b.OutputSi("u_b") >= a.OutputSi("u_b");
        }, iter: 40);
    }

    [Fact]
    public void Surge_Warns_Below_Thirteen_Times_The_Operating_Frequency_Only()
    {
        var fn = Run().OutputSi("f_n");
        Assert.Contains(Run(Q("f_op", fn / 12.9, "Hz")).Warnings, w => w.RuleId == "surge");
        Assert.DoesNotContain(Run(Q("f_op", fn / 13.1, "Hz")).Warnings, w => w.RuleId == "surge");
        var without = CalcRunner.Run(Spring, Defaults().With(new CalcInputValue("f_op")));
        Assert.Null(without.FindOutput("r_f"));
        Assert.Contains(Run(Q("f_op", fn / 12.9, "Hz")).Outputs, o => o.Symbol == "r_f");
    }

    [Fact]
    public void The_Surge_Frequency_Is_The_Fixed_Fixed_First_Mode()
    {
        // f = ½·√(k/m), m = ρ·(π d²/4)·(π D n): the closed form of the sheet and the lumped-mass form agree.
        var r = Run();
        var k = r.OutputSi("k");
        var mass = 7850 * (Math.PI * 4e-6 / 4) * (Math.PI * 16e-3 * 8);
        Assert.Equal(0.5 * Math.Sqrt(k / mass), r.OutputSi("f_n"), 6);
    }

    [Fact]
    public void Goodman_Is_The_Shared_Relation_And_The_Endurance_Strength_Has_No_Default()
    {
        var r = Run();
        var expected = FatigueRelations.GoodmanUtilisation((r.OutputSi("tau_2") - r.OutputSi("tau_1")) / 2, (r.OutputSi("tau_2") + r.OutputSi("tau_1")) / 2, 400e6, 0.67 * 1800e6);
        Assert.Equal(expected, r.OutputSi("u_f"), 9);
        Assert.Equal(1 / expected, r.OutputSi("n_f"), 6);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Goodman", StringComparison.Ordinal));

        // Review M-8: no invented endurance strength; a blank S_e blocks and says what to enter.
        Assert.Null(Spring.Template.FindInput("S_e")!.Default);
        Assert.False(Spring.Template.FindInput("S_e")!.IsOptional);
        var blank = CalcRunner.Run(Spring, Defaults().With(new CalcInputValue("S_e")));
        Assert.True(blank.IsBlocked);
        Assert.Contains(blank.Blocks, b => b.RuleId == "input.S_e" && b.Message.StartsWith("S_e: enter", StringComparison.Ordinal) && b.Message.Contains("no default", StringComparison.Ordinal));
        Assert.Null(blank.FindOutput("n_f"));
        Assert.DoesNotContain(Spring.Template.ApplicabilityRules, rule => rule.Id == "fatigue-skipped");
    }

    [Fact]
    public void Goodman_Matches_The_M4_Form_And_A_Steady_Load_Needs_The_Ultimate_Strength_Only()
    {
        Assert.Equal(1 / (30 / 200.0 + 100 / 700.0), FatigueRelations.GoodmanFactor(30, 100, 200, 700), 12);
        Assert.Equal(7.0, FatigueRelations.GoodmanFactor(0, 100, 200, 700), 12);
        Assert.True(double.IsPositiveInfinity(FatigueRelations.GoodmanFactor(0, 0, 200, 700)));
        var steady = Run(Q("F_1", 40, "N"), Q("F_2", 40, "N"));
        Assert.Equal(238.842177e6 / (0.67 * 1800e6), steady.OutputSi("u_f"), 7);
    }

    [Fact]
    public void A_Stroke_Below_10_000_Cycles_Warns_That_The_Static_Check_Governs()
    {
        Assert.Contains(Run(CalcInputValue.Of("N_c", 5000)).Warnings, w => w.RuleId == "fatigue-cycles");
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "fatigue-cycles");
    }

    [Fact]
    public void Working_Beyond_The_Travel_To_Solid_Fails_The_Travel_Check_And_The_Solid_Stress_Check()
    {
        var r = Run(Q("F_2", 150, "N"));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Working deflection", StringComparison.Ordinal) && !c.Passed);
        var solid = Run(Q("L0", 60, "mm"));
        Assert.Contains(solid.Checks, c => c.Name.StartsWith("Static shear stress", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void Working_Point_2_Below_Point_1_Blocks()
    {
        var r = Run(Q("F_1", 50, "N"), Q("F_2", 40, "N"));
        Assert.True(r.IsBlocked);
        Assert.Equal("sequence", r.Blocks[0].RuleId);
    }

    [Fact]
    public void A_Coil_Diameter_Not_Above_The_Wire_Blocks()
    {
        var r = Run(Q("D", 1.5, "mm"));
        Assert.True(r.IsBlocked);
        Assert.Equal("geometry", r.Blocks[0].RuleId);
    }

    [Fact]
    public void Inputs_Outside_Their_Hard_Range_Are_Refused()
    {
        foreach (var (symbol, value, unit) in new[] { ("d", 0.01, "mm"), ("d", 80.0, "mm"), ("D", 0.01, "mm"), ("G", 0.1, "GPa"), ("Rm", 10.0, "MPa"), ("f_tau", 1.5, ""), ("Rm_A", 0.1, "MPa"), ("Rm_m", 6.0, ""), ("d_max", 0.01, "mm"), ("S_e", 0.1, "MPa") })
        {
            var change = unit.Length > 0 ? Q(symbol, value, unit) : CalcInputValue.Of(symbol, value);
            Assert.Contains(CalcInputValidator.Validate(Spring.Template, Defaults().With(change)), n => n.Severity == CalcNoticeSeverity.Block);
        }
    }

    // ------------------------------------------------------------------ extension

    private static CalcInputValue[] Extension(double f0 = 10, double f1 = 30, double f2 = 60) =>
        [Mode(SpringCalculator.Extension), CalcInputValue.Of("n", 20), Q("F_0", f0, "N"), Q("F_1", f1, "N"), Q("F_2", f2, "N")];

    [Fact]
    public void An_Extension_Spring_Starts_Moving_At_The_Initial_Tension()
    {
        var r = Run(Extension());
        Assert.Equal((60 - 10) / r.OutputSi("k"), r.OutputSi("s_2"), 9);
        var byDeflection = Run([.. Extension(), CalcInputValue.Choice("work", SpringCalculator.Deflections), Q("s_1", 10.24, "mm"), Q("s_2", 25.6, "mm")]);
        Assert.Equal(30, byDeflection.OutputSi("F_1"), 9);
        Assert.Equal(60, byDeflection.OutputSi("F_2"), 9);
        Assert.Equal(42e-3, r.OutputSi("L_s"), 12);
    }

    [Fact]
    public void An_Extension_Force_Below_The_Initial_Tension_Blocks_And_So_Does_A_Hook_Radius_Under_Half_The_Wire()
    {
        var below = Run(Extension(f0: 40, f1: 30, f2: 60));
        Assert.True(below.IsBlocked);
        Assert.Equal("sequence", below.Blocks[0].RuleId);
        var hook = Run([.. Extension(), Q("r_h2", 0.9, "mm")]);
        Assert.True(hook.IsBlocked);
        Assert.Equal("hook", hook.Blocks[0].RuleId);
    }

    [Fact]
    public void An_Input_The_Chosen_Mode_Needs_Cannot_Be_Left_Blank()
    {
        foreach (var (mode, symbol) in new[] { (SpringCalculator.Compression, "L0"), (SpringCalculator.Compression, "F_2"), (SpringCalculator.Extension, "F_0"), (SpringCalculator.Extension, "r_h1"), (SpringCalculator.Extension, "f_sigma"), (SpringCalculator.Torsion, "l_1"), (SpringCalculator.Torsion, "M_2") })
        {
            var r = CalcRunner.Run(Spring, Defaults().With(Mode(mode)).With(new CalcInputValue(symbol)));
            Assert.True(r.IsBlocked, $"{mode} without {symbol}");
            Assert.Equal("missing", r.Blocks[0].RuleId);
            Assert.StartsWith(symbol + ":", r.Blocks[0].Message, StringComparison.Ordinal);
            Assert.Contains("enter", r.Blocks[0].Message, StringComparison.Ordinal);
        }

        // An input of another mode may be blank: a compression spring needs no hook radius.
        Assert.False(CalcRunner.Run(Spring, Defaults().With(new CalcInputValue("r_h1")).With(new CalcInputValue("M_1"))).IsBlocked);
    }

    [Fact]
    public void A_Tighter_Hook_Raises_The_Hook_Stress()
    {
        Gen.Select(Gen.Double[3, 8], Gen.Double[8.5, 16]).Sample((tight, loose) =>
        {
            var a = Run([.. Extension(), Q("r_h1", tight, "mm"), Q("r_h2", tight, "mm")]);
            var b = Run([.. Extension(), Q("r_h1", loose, "mm"), Q("r_h2", loose, "mm")]);
            return a.OutputSi("sigma_A") > b.OutputSi("sigma_A") && a.OutputSi("tau_B") > b.OutputSi("tau_B");
        }, iter: 40);
    }

    [Fact]
    public void Extension_Springs_Check_The_Body_And_Both_Hook_Stresses()
    {
        var r = Run(Extension());
        foreach (var name in new[] { "Static shear stress at working point 2", "Hook bending stress", "Hook side-bend torsion stress" })
            Assert.Contains(r.Checks, c => c.Name == name);
    }

    // ------------------------------------------------------------------ torsion

    private static CalcInputValue[] Torsion(double l1 = 30, double l2 = 30) =>
        [Mode(SpringCalculator.Torsion), CalcInputValue.Of("n", 6), Q("l_1", l1, "mm"), Q("l_2", l2, "mm"), Q("M_1", 0.05, "N*m"), Q("M_2", 0.15, "N*m")];

    [Fact]
    public void The_Legs_Soften_A_Torsion_Spring()
    {
        var bare = Run(Torsion(0, 0));
        var legged = Run(Torsion(30, 30));
        Assert.Equal(206e9 * Math.Pow(2e-3, 4) / (64 * 16e-3 * 6), bare.OutputSi("k_phi"), 6);
        Assert.True(legged.OutputSi("k_phi") < bare.OutputSi("k_phi"));
        Assert.True(legged.OutputSi("phi_2") > bare.OutputSi("phi_2"));
    }

    [Fact]
    public void Winding_Up_A_Torsion_Spring_Shrinks_It_And_Lengthens_Its_Body()
    {
        var r = Run(Torsion());
        Assert.True(r.OutputSi("D_w") < 16e-3);
        Assert.True(r.OutputSi("L_w") > r.OutputSi("L_s"));
        var turns = r.OutputSi("phi_2") / (2 * Math.PI);
        Assert.Equal(16e-3 * 6 / (6 + turns), r.OutputSi("D_w"), 12);
    }

    [Fact]
    public void Torsion_By_Angles_Gives_Torque_Equal_To_Rate_Times_Angle()
    {
        var r = Run([.. Torsion(), CalcInputValue.Choice("work", SpringCalculator.Deflections), Q("phi_1", 10, "deg"), Q("phi_2", 30, "deg")]);
        Assert.Equal(r.OutputSi("k_phi") * 30 * Math.PI / 180, r.OutputSi("M_2"), 9);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Static bending stress", StringComparison.Ordinal));
        Assert.Null(r.FindOutput("k"));
    }

    // ------------------------------------------------------------------ template

    [Fact]
    public void Material_Properties_Come_From_The_Library_And_The_Wire_Strength_Does_Not()
    {
        var t = Spring.Template;
        Assert.Equal("G", t.FindInput("G")!.MaterialProperty);
        Assert.Equal("E", t.FindInput("E")!.MaterialProperty);
        Assert.Equal("rho", t.FindInput("rho")!.MaterialProperty);
        Assert.Null(t.FindInput("Rm")!.MaterialProperty);
        Assert.Equal("Rm_A", t.FindInput("Rm_A")!.MaterialProperty);
        Assert.Equal("Rm_m", t.FindInput("Rm_m")!.MaterialProperty);
        var material = t.RecordKinds!.Single(k => k.Kind == SpringCalculator.MaterialKind);
        Assert.Equal(["G", "E", "rho", "Rm_A", "Rm_m"], material.Symbols);
        Assert.Contains(t.RecordKinds!, k => k.Kind == "spring.wire-strength" && k.Symbols.SequenceEqual(["Rm"]));
        Assert.Contains(t.RecordKinds!, k => k.Kind == "spring.seating" && k.Symbols.SequenceEqual(["nu_seat"]));
    }

    [Fact]
    public void The_Declared_Ports_Are_Outputs_Of_A_Default_Run_With_The_Declared_Units()
    {
        var r = Run();

        foreach (var port in Spring.Template.OutputPorts!)
        {
            var output = r.FindOutput(port.Symbol);
            Assert.NotNull(output);
            var scalar = Assert.IsType<ScalarValue>(output.Value);
            Assert.Equal(Engine.Units.UnitCatalog.Parse(port.Unit).Dimension, scalar.Dimension);
        }
    }

    [Fact]
    public void No_Wire_Strength_Table_Or_Permissible_Stress_Diagram_Is_Shipped()
    {
        // §B5: R_m(d) and the permissible stresses are the user's inputs; the template carries no table input and no curve.
        Assert.DoesNotContain(Spring.Template.Inputs, i => i.Kind == CalcInputKind.Table);
        Assert.All(new[] { "Rm", "f_tau", "f_sigma", "f_su", "S_e", "nu_seat" }, symbol => Assert.NotNull(Spring.Template.FindInput(symbol)));
    }

    // ------------------------------------------------------------------ B-1 review fixes

    private static string[] DeclaredRules() => [.. Spring.Template.ApplicabilityRules.Select(r => r.Id)];

    [Fact]
    public void Review_M8_The_Wire_Strength_Has_No_Default_And_A_Blank_One_Blocks_With_What_To_Enter()
    {
        var spec = Spring.Template.FindInput("Rm")!;
        Assert.Null(spec.Default);
        var blank = CalcRunner.Run(Spring, Defaults().With(new CalcInputValue("Rm")));
        Assert.True(blank.IsBlocked);
        var block = Assert.Single(blank.Blocks);
        Assert.Equal("wire-strength", block.RuleId);
        Assert.StartsWith("Rm: enter", block.Message, StringComparison.Ordinal);
        Assert.Contains("no default", block.Message, StringComparison.Ordinal);
        Assert.Null(blank.FindOutput("tau_2"));

        // A new sheet: nothing invented, and what is missing is named (S_e first, from the input check; then R_m).
        var fresh = CalcRunner.Run(Spring, CalcTemplateHasher.NewSheetInputs(Spring.Template));
        Assert.True(fresh.IsBlocked);
        Assert.All(fresh.Blocks, b => Assert.Contains("enter", b.Message, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fresh.Blocks, b => b.RuleId == "input.S_e");
    }

    [Fact]
    public void Review_M6_The_Wire_Strength_Can_Come_From_The_W3_Curve_R_m_Equals_A_Over_d_To_The_m()
    {
        var curve = new[] { CalcInputValue.Choice("rm_source", SpringCalculator.RmCurve), Q("Rm_A", 1800, "MPa"), CalcInputValue.Of("Rm_m", 0.1) };
        var r = CalcRunner.Run(Spring, Defaults().With(new CalcInputValue("Rm")).With(curve[0]).With(curve[1]).With(curve[2]));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        var expected = 1800e6 / Math.Pow(2, 0.1);
        Assert.Equal(expected, r.OutputSi("Rm"), 1e-9 * expected);

        // The same R_m typed gives the same stresses and utilisations: the curve only supplies the number.
        var typed = Run(Q("Rm", expected / 1e6, "MPa"));
        foreach (var symbol in new[] { "tau_2", "u_2", "u_c", "u_f", "n_f" })
            Assert.Equal(typed.OutputSi(symbol), r.OutputSi(symbol), Math.Abs(typed.OutputSi(symbol)) * 1e-9);

        // The curve ignores a typed R_m, and a missing A or m is named; an absurd curve is refused.
        var both = CalcRunner.Run(Spring, Defaults().With(curve[0]).With(curve[1]).With(curve[2]).With(Q("Rm", 3000, "MPa")));
        Assert.Equal(expected, both.OutputSi("Rm"), 1e-9 * expected);
        var noM = CalcRunner.Run(Spring, Defaults().With(curve[0]).With(curve[1]).With(new CalcInputValue("Rm_m")));
        Assert.Contains(noM.Blocks, b => b.RuleId == "missing" && b.Message.StartsWith("Rm_m:", StringComparison.Ordinal));
        var tiny = CalcRunner.Run(Spring, Defaults().With(curve[0]).With(Q("Rm_A", 100, "MPa")).With(CalcInputValue.Of("Rm_m", 5)).With(Q("d", 20, "mm")).With(Q("D", 160, "mm")));
        Assert.Contains(tiny.Blocks, b => b.RuleId == "wire-strength" && b.Message.StartsWith("Rm_A:", StringComparison.Ordinal));
    }

    [Fact]
    public void Review_M6_Rm_A_Follows_The_Diameter_Through_The_Exponent()
    {
        Gen.Select(Gen.Double[0.5, 10], Gen.Double[0, 0.3], Gen.Double[1200, 2400]).Sample((d, m, a) =>
        {
            var r = CalcRunner.Run(Spring, Defaults().With(new CalcInputValue("Rm")).With(CalcInputValue.Choice("rm_source", SpringCalculator.RmCurve)).With(Q("Rm_A", a, "MPa")).With(CalcInputValue.Of("Rm_m", m)).With(Q("d", d, "mm")).With(Q("D", d * 8, "mm")).With(Q("L0", 5000, "mm")));
            var rm = a / Math.Pow(d, m);
            return rm is < 50 or > 5000 ? r.IsBlocked : !r.IsBlocked && Math.Abs(r.OutputSi("Rm") - rm * 1e6) <= 1e-9 * rm * 1e6;
        }, iter: 60);
    }

    [Fact]
    public void Review_Low_The_En_Solid_Length_Uses_The_Maximum_Wire_Diameter_And_Adds_Half_A_Wire_For_Unground_Squared_Ends()
    {
        var squared = new[] { CalcInputValue.Choice("ends", SpringCalculator.Squared), Q("L0", 45, "mm") };
        var nominal = Run(squared);
        Assert.Equal(23e-3, nominal.OutputSi("L_s"), 12);
        var tolerance = Run([.. squared, Q("d_max", 2.04, "mm")]);
        Assert.Equal(2.04e-3 * 11.5, tolerance.OutputSi("L_s"), 12);
        var shigley = Run([.. squared, Q("d_max", 2.04, "mm"), CalcInputValue.Choice("solid_method", SpringCalculator.SolidShigley)]);
        Assert.Equal(22e-3, shigley.OutputSi("L_s"), 12);
        Assert.Contains(Run([.. squared, Q("d_max", 1.9, "mm")]).Blocks, b => b.RuleId == "d-max" && b.Message.StartsWith("d_max:", StringComparison.Ordinal));
        Assert.Equal(SpringCalculator.SolidEn, Spring.Template.FindInput("solid_method")!.DefaultChoice);
    }

    [Fact]
    public void Review_Low_The_En_Minimum_Coil_Clearance_Is_Checked_At_The_Working_Length()
    {
        // d 2, D 16, n 8: S_a = (0.0015·16²/2 + 0.1·2)·8 = 3.136 mm; squared and ground, L_s = 20, L_n = 23.136; L0 45.
        var ok = Run();
        Assert.Equal(3.136e-3, ok.OutputSi("S_a"), 12);
        Assert.Equal(23.136e-3, ok.OutputSi("L_n"), 12);
        Assert.Contains(ok.Checks, c => c.Name.StartsWith("Minimum coil clearance", StringComparison.Ordinal) && c.Passed);

        // F₂ = 112.3 N: s₂ = 23 mm, so L₂ = 22 mm is above solid (20 mm) but inside the clearance (23.136 mm).
        var tight = Run(Q("F_2", 112.3, "N"));
        Assert.Contains(tight.Checks, c => c.Name.StartsWith("Minimum coil clearance", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(tight.Checks, c => c.Name == "Working deflection within the travel to solid" && c.Passed);

        // Shigley's form has no clearance: no S_a, no check, and a free length between L_s and L_n is accepted.
        var shigley = Run(Q("F_2", 112.3, "N"), CalcInputValue.Choice("solid_method", SpringCalculator.SolidShigley));
        Assert.Null(shigley.FindOutput("S_a"));
        Assert.DoesNotContain(shigley.Checks, c => c.Name.StartsWith("Minimum coil clearance", StringComparison.Ordinal));
        var between = Run(Q("L0", 22, "mm"), Q("F_1", 0, "N"), Q("F_2", 1, "N"));
        Assert.Contains(between.Blocks, b => b.RuleId == "solid" && b.Message.StartsWith("L0:", StringComparison.Ordinal) && b.Message.Contains("L_n", StringComparison.Ordinal));
        Assert.False(Run(Q("L0", 22, "mm"), Q("F_1", 0, "N"), Q("F_2", 1, "N"), CalcInputValue.Choice("solid_method", SpringCalculator.SolidShigley)).IsBlocked);
    }

    [Fact]
    public void Review_Low_The_Torsion_Curvature_Factor_Is_The_En_Q_By_Default_And_Shigley_K_i_On_Request()
    {
        var en = Run(Torsion());
        var shigley = Run([.. Torsion(), CalcInputValue.Choice("tors_factor", SpringCalculator.TorsionShigley)]);
        Assert.Equal(SpringCalculator.TorsionEn, Spring.Template.FindInput("tors_factor")!.DefaultChoice);
        Assert.Equal(8.07 / 7.25, en.OutputSi("q"), 12);
        Assert.Null(en.FindOutput("K_i"));
        Assert.Equal(247.0 / 224, shigley.OutputSi("K_i"), 12);
        Assert.Null(shigley.FindOutput("q"));
        Assert.Equal((8.07 / 7.25) / (247.0 / 224), en.OutputSi("sigma_2") / shigley.OutputSi("sigma_2"), 12);
        Assert.Equal(en.OutputSi("phi_2"), shigley.OutputSi("phi_2"), 12);
    }

    [Fact]
    public void Review_Low_The_Travel_Check_Is_A_Utilisation_Not_A_Zero()
    {
        var r = Run();
        var check = r.Checks.Single(c => c.Name == "Working deflection within the travel to solid");
        Assert.Equal(r.OutputSi("s_2") / r.OutputSi("s_c"), check.Utilisation, 12);
        Assert.True(check.Utilisation > 0 && check.Utilisation < 1);
        Assert.Contains("u_s = ", check.DemandText, StringComparison.Ordinal);
    }

    [Fact]
    public void Review_Low_The_Force_Ports_Say_They_Are_The_Fixed_Working_Points()
    {
        Assert.All(new[] { "F_1", "F_2" }, symbol => Assert.Contains("not a force at an arbitrary deflection", Spring.Template.OutputPorts!.Single(p => p.Symbol == symbol).Description, StringComparison.Ordinal));
    }

    [Fact]
    public void Review_Low_Every_Rule_A_Run_Raises_Is_Declared()
    {
        var declared = DeclaredRules();
        var runs = new[]
        {
            Run(), Run(Q("D", 1.5, "mm")), Run(Q("D", 6, "mm")), Run(Q("L0", 20, "mm")), Run(Q("F_1", 50, "N"), Q("F_2", 40, "N")), Run(Q("F_2", 600, "N")), Run(CalcInputValue.Of("N_c", 5000)),
            Run(Q("f_op", 200, "Hz")), Run(Mode(SpringCalculator.Extension), Q("r_h2", 0.9, "mm")), Run(Mode(SpringCalculator.Torsion), Q("l_1", 30, "mm"), Q("l_2", 30, "mm"), Q("M_1", 0.05, "N*m"), Q("M_2", 0.15, "N*m")),
            CalcRunner.Run(Spring, Defaults().With(Mode(SpringCalculator.Extension)).With(new CalcInputValue("F_0"))), CalcRunner.Run(Spring, Defaults().With(new CalcInputValue("Rm"))),
            Run(Q("L0", 22, "mm")), Run(Q("d_max", 1.5, "mm")),
            Run(Q("D", 20, "mm"), CalcInputValue.Of("n", 10), CalcInputValue.Choice("ends", SpringCalculator.PlainGround), Q("L0", 80, "mm"), CalcInputValue.Of("nu_seat", 1), Q("F_1", 8, "N"), Q("F_2", 60, "N")),
            CalcRunner.Run(Spring, Defaults().With(CalcInputValue.Choice("rm_source", SpringCalculator.RmCurve))),
        };

        foreach (var notice in runs.SelectMany(r => r.Notices))
        {
            if (notice.RuleId.StartsWith("input.", StringComparison.Ordinal) || notice.RuleId.StartsWith("record.", StringComparison.Ordinal))
                continue;

            if (Spring.Template.Inputs.Any(i => i.Symbol == notice.RuleId))
                continue;

            Assert.Contains(notice.RuleId, declared);
        }

        Assert.All(new[] { "missing", "wire-strength", "d-max" }, id => Assert.Contains(id, declared));
    }

    [Fact]
    public void An_Expected_Working_Travel_More_Than_One_Percent_From_The_Springs_Own_Warns_And_A_Match_Does_Not()
    {
        // The default case: F 10 and 40 N on a rate of 4.8828125 N/mm gives s₂ − s₁ = 30/4.8828125 = 6.144 mm.
        var travel = 30 / 4.8828125;
        var match = Run(Q("s_work", travel * 1.005, "mm"));
        Assert.False(match.IsBlocked);
        Assert.DoesNotContain(match.Warnings, w => w.RuleId == "travel-mismatch");
        Assert.Equal(travel * 1e-3, match.OutputSi("s_trav"), 12);

        var off = Run(Q("s_work", travel * 1.02, "mm"));
        var warning = Assert.Single(off.Warnings, w => w.RuleId == "travel-mismatch");
        Assert.StartsWith("s_work:", warning.Message, StringComparison.Ordinal);

        // Blank (the default) changes nothing, and the deflection form of the working points is judged the same way.
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "travel-mismatch");
        var byDeflection = Run(CalcInputValue.Choice("work", SpringCalculator.Deflections), Q("s_1", 2, "mm"), Q("s_2", 8, "mm"), Q("s_work", 5, "mm"));
        Assert.Contains(byDeflection.Warnings, w => w.RuleId == "travel-mismatch");
        Assert.DoesNotContain(Run(CalcInputValue.Choice("work", SpringCalculator.Deflections), Q("s_1", 2, "mm"), Q("s_2", 8, "mm"), Q("s_work", 6, "mm")).Warnings, w => w.RuleId == "travel-mismatch");
    }

    [Fact]
    public void An_Expected_Working_Travel_Is_Not_Used_In_Torsion_Mode_And_The_Sheet_Says_So()
    {
        // The travel check is a deflection in mm and runs for compression and extension only; a torsion spring works through an angle, so s_work (typed or linked) has nothing to be compared with.
        var typed = Run([.. Torsion(), Q("s_work", 6, "mm")]);
        Assert.False(typed.IsBlocked);
        var unused = Assert.Single(typed.Warnings, w => w.RuleId == "s-work-unused");
        Assert.StartsWith("s_work:", unused.Message, StringComparison.Ordinal);
        Assert.Contains("not used in torsion mode", unused.Message, StringComparison.Ordinal);
        Assert.Null(typed.FindOutput("s_trav"));
        Assert.DoesNotContain(typed.Warnings, w => w.RuleId == "travel-mismatch");

        // A linked s_work (the M9 lift) is the same: the provenance does not matter, the mode does.
        var link = CalcProvenance.Link(Guid.NewGuid(), "lift", "1");
        var linked = Run([.. Torsion(), Q("s_work", 6, "mm") with { Provenance = link }]);
        Assert.Single(linked.Warnings, w => w.RuleId == "s-work-unused");

        // Blank s_work is silent in torsion mode, and the warning is for torsion only: compression and extension use s_work (travel check).
        Assert.DoesNotContain(Run(Torsion()).Warnings, w => w.RuleId == "s-work-unused");
        Assert.DoesNotContain(Run(Q("s_work", 6.144, "mm")).Warnings, w => w.RuleId == "s-work-unused");
        Assert.DoesNotContain(Run(Mode(SpringCalculator.Extension), Q("F_0", 10, "N"), Q("F_1", 30, "N"), Q("F_2", 60, "N"), Q("r_h1", 8, "mm"), Q("r_h2", 4, "mm"), Q("s_work", 15, "mm")).Warnings, w => w.RuleId == "s-work-unused");
        Assert.Contains("s-work-unused", DeclaredRules());
    }

    [Fact]
    public void The_Permissible_Stress_Fractions_Default_To_Generic_Textbook_Values_The_User_Replaces_With_The_Licensed_Standard_Value()
    {
        // The defaults stay; the wording no longer says the fractions have no default.
        var defaults = new[] { ("f_tau", 0.45), ("f_sigma", 0.75), ("f_su", 0.67) };

        foreach (var (symbol, value) in defaults)
        {
            var spec = Spring.Template.Inputs.Single(i => i.Symbol == symbol);
            Assert.Equal(value, spec.Default);
            Assert.Contains("generic textbook default", spec.Description, StringComparison.Ordinal);
            Assert.Contains("EN 13906", spec.Description, StringComparison.Ordinal);
        }

        var assumption = Spring.Template.Assumptions.Single(a => a.Statement.Contains("permissible stress fractions", StringComparison.Ordinal));
        Assert.DoesNotContain("the permissible stress fractions and the endurance strength are the user's inputs", assumption.Statement, StringComparison.Ordinal);
        Assert.Contains("generic textbook fractions", assumption.Statement, StringComparison.Ordinal);
        Assert.Contains("licensed EN 13906 value", assumption.Statement, StringComparison.Ordinal);
        Assert.Contains("generic textbook fractions", Spring.Template.RecordKinds!.Single(k => k.Kind == "spring.permissible").Guidance, StringComparison.Ordinal);
    }
}
