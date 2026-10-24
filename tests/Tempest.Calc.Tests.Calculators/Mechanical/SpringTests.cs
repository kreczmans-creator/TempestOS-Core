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
    [InlineData(SpringCalculator.Plain, 8, 18)]
    [InlineData(SpringCalculator.PlainGround, 9, 18)]
    [InlineData(SpringCalculator.Squared, 10, 22)]
    [InlineData(SpringCalculator.SquaredGround, 10, 20)]
    public void Solid_Length_And_Pitch_Follow_The_End_Type(string ends, double coils, double solid)
    {
        // d = 2 mm, n = 8, L0 = 45 mm: coils and solid length from the end-type relations, the pitch from the free-length relation of the same end.
        var r = Run(CalcInputValue.Choice("ends", ends), Q("L0", 45, "mm"));
        Assert.Equal(coils, r.OutputSi("n_t"), 9);
        Assert.Equal(solid * 1e-3, r.OutputSi("L_s"), 9);
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
    public void A_Spring_Index_Outside_4_To_20_Warns()
    {
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "index");
        Assert.Contains(Run(Q("D", 6, "mm")).Warnings, w => w.RuleId == "index");
        Assert.Contains(Run(Q("D", 50, "mm")).Warnings, w => w.RuleId == "index");
    }

    [Fact]
    public void Buckling_Passes_Below_The_Critical_Deflection_And_Warns_At_Or_Above_It()
    {
        var slender = new[] { Q("D", 20, "mm"), CalcInputValue.Of("n", 10), CalcInputValue.Choice("ends", SpringCalculator.PlainGround), Q("L0", 80, "mm"), CalcInputValue.Of("nu_seat", 1) };
        var ok = Run([.. slender, Q("F_1", 8, "N"), Q("F_2", 24, "N")]);
        Assert.Equal("PASS", Text(ok, "buckling"));
        Assert.DoesNotContain(ok.Warnings, w => w.RuleId == "buckling");

        var over = Run([.. slender, Q("F_1", 8, "N"), Q("F_2", 60, "N")]);
        Assert.Equal("WARN", Text(over, "buckling"));
        Assert.Contains(over.Warnings, w => w.RuleId == "buckling");
        Assert.True(over.OutputSi("u_b") > 1);
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
    public void Goodman_Is_The_Shared_Relation_And_Is_Skipped_Without_An_Endurance_Strength()
    {
        var r = Run();
        var expected = FatigueRelations.GoodmanUtilisation((r.OutputSi("tau_2") - r.OutputSi("tau_1")) / 2, (r.OutputSi("tau_2") + r.OutputSi("tau_1")) / 2, 400e6, 0.67 * 1800e6);
        Assert.Equal(expected, r.OutputSi("u_f"), 9);
        Assert.Equal(1 / expected, r.OutputSi("n_f"), 6);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Goodman", StringComparison.Ordinal));

        var skipped = CalcRunner.Run(Spring, Defaults().With(new CalcInputValue("S_e")));
        Assert.Contains(skipped.Warnings, w => w.RuleId == "fatigue-skipped");
        Assert.Null(skipped.FindOutput("n_f"));
        Assert.DoesNotContain(skipped.Checks, c => c.Name.StartsWith("Goodman", StringComparison.Ordinal));
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
        foreach (var (symbol, value, unit) in new[] { ("d", 0.01, "mm"), ("d", 80.0, "mm"), ("D", 0.01, "mm"), ("G", 0.1, "GPa"), ("Rm", 10.0, "MPa"), ("f_tau", 1.5, "") })
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
        var material = t.RecordKinds!.Single(k => k.Kind == SpringCalculator.MaterialKind);
        Assert.Equal(["G", "E", "rho"], material.Symbols);
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
}
