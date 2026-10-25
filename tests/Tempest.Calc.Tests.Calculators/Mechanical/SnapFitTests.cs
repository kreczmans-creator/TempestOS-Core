using CsCheck;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class SnapFitTests
{
    private static readonly SnapFitCalculator Snap = new();

    /// <summary>The first known-answer case (cantilever, constant section) with the template defaults under it.</summary>
    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Snap.Template);

    private static CalcRunResult Run(params CalcInputValue[] changes) => CalcRunner.Run(Snap, changes.Aggregate(Defaults(), (set, v) => set.With(v)));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static string Text(CalcRunResult r, string symbol) => ((TextValue)r.FindOutput(symbol)!.Value).Value;

    private static CalcInputValue Mode(string mode) => CalcInputValue.Choice("mode", mode);

    private static double Tan(double degrees) => Math.Tan(degrees * Math.PI / 180);

    /// <summary>
    /// An independent check of the geometry factors: the tip deflection and the largest surface strain of a tapered cantilever of
    /// unit length, unit root thickness and width, by trapezoid integration of the curvature twice. No closed form is used.
    /// </summary>
    private static double NumericKy(char kind, double ratio)
    {
        const int n = 20000;
        double H(double x) => kind == 'T' ? 1 - (1 - ratio) * x : 1;
        double B(double x) => kind == 'W' ? 1 - (1 - ratio) * x : 1;
        double I(double x) => B(x) * Math.Pow(H(x), 3) / 12;

        double slope = 0, deflection = 0, previousCurvature = 1 / I(0), previousSlope = 0, maxStrain = 0;

        for (var i = 1; i <= n; i++)
        {
            var x = i / (double)n;
            var curvature = (1 - x) / I(x);
            slope += 0.5 * (curvature + previousCurvature) / n;
            deflection += 0.5 * (slope + previousSlope) / n;
            (previousCurvature, previousSlope) = (curvature, slope);
        }

        for (var i = 0; i <= n; i++)
        {
            var x = i / (double)n;
            maxStrain = Math.Max(maxStrain, (1 - x) * (H(x) / 2) / I(x));
        }

        return deflection / maxStrain;
    }

    // ------------------------------------------------------------------ geometry

    [Theory]
    [InlineData('T', 0.5)]
    [InlineData('T', 0.3)]
    [InlineData('T', 0.8)]
    [InlineData('T', 0.99)]
    [InlineData('W', 0.5)]
    [InlineData('W', 0.25)]
    [InlineData('W', 0.9)]
    public void The_Closed_Form_Deflection_Factors_Agree_With_Numerical_Integration_Of_The_Beam(char kind, double ratio)
    {
        var closed = kind == 'T' ? SnapFitGeometry.ThicknessKy(ratio) : SnapFitGeometry.WidthKy(ratio);
        Assert.Equal(NumericKy(kind, ratio), closed, 6);
    }

    [Fact]
    public void The_Constant_Section_Factor_Is_Two_Thirds_Which_The_Guides_Print_As_0_67_And_The_Tapers_Join_It_At_A_Ratio_Of_One()
    {
        Assert.Equal(2.0 / 3.0, SnapFitGeometry.ConstantKy, 15);
        Assert.InRange(SnapFitGeometry.ConstantKy / 0.67, 0.995, 1.0);
        Assert.Equal(2.0 / 3.0, SnapFitGeometry.ThicknessKy(1), 9);
        Assert.Equal(2.0 / 3.0, SnapFitGeometry.WidthKy(1), 9);

        // The switch between the closed form and the numerical integral (taper 0.02) is seamless.
        Assert.Equal(SnapFitGeometry.ThicknessJ(0.98 + 1e-9), SnapFitGeometry.ThicknessJ(0.98 - 1e-9), 8);
        Assert.Equal(SnapFitGeometry.WidthJ(0.98 + 1e-9), SnapFitGeometry.WidthJ(0.98 - 1e-9), 8);
    }

    [Fact]
    public void A_Thickness_Taper_Steeper_Than_Half_Moves_The_Strain_Peak_Inside_The_Beam()
    {
        Assert.Equal((1.0, 0.0), SnapFitGeometry.ThicknessPhiMax(0.5));
        Assert.Equal((1.0, 0.0), SnapFitGeometry.ThicknessPhiMax(0.8));

        Gen.Double[0.05, 0.49].Sample(ratio =>
        {
            var a = 1 - ratio;
            var (phi, t) = SnapFitGeometry.ThicknessPhiMax(ratio);
            double Shape(double x) => (1 - x) / Math.Pow(1 - a * x, 2);
            var scanned = Enumerable.Range(0, 100001).Max(i => Shape(i / 100000.0));
            return t is > 0 and < 1 && phi > 1 && Math.Abs(phi - scanned) <= 1e-8 * phi && Math.Abs(Shape(t) - phi) <= 1e-12;
        }, iter: 40);

        var r = Run(Mode(SnapFitCalculator.TaperedThickness), CalcInputValue.Of("h_ratio", 0.3));
        Assert.True(r.OutputSi("t_crit") > 0.5);
        Assert.True(r.OutputSi("phi_max") > 1);
    }

    // ------------------------------------------------------------------ cantilever

    [Fact]
    public void The_Constant_Section_Strain_And_Force_Follow_The_Beam_Relations()
    {
        Gen.Select(Gen.Double[5, 40], Gen.Double[0.5, 4], Gen.Double[2, 20], Gen.Double[0.05, 3], Gen.Double[500, 5000]).Sample((l, h, b, y, es) =>
        {
            var r = Run(Q("l", l, "mm"), Q("h", h, "mm"), Q("b", b, "mm"), Q("y", y, "mm"), Q("Es", es, "MPa"));
            var eps = 1.5 * h * y / (l * l);
            var p = b * h * h / 6 * (es * eps / l);
            return !r.IsBlocked && Math.Abs(r.OutputSi("eps") - eps) <= 1e-9 * eps && Math.Abs(r.OutputSi("P") - p) <= 1e-9 * p
                && Math.Abs(r.OutputSi("y_perm") * 1e3 - 2.0 / 3.0 * 0.04 * l * l / h) <= 1e-9 * l * l / h;
        }, iter: 60);
    }

    [Fact]
    public void A_Tapered_Beam_At_A_Ratio_Of_One_Is_The_Constant_Beam()
    {
        var constant = Run();

        foreach (var (mode, symbol) in new[] { (SnapFitCalculator.TaperedThickness, "h_ratio"), (SnapFitCalculator.TaperedWidth, "b_ratio") })
        {
            var tapered = Run(Mode(mode), CalcInputValue.Of(symbol, 1));

            foreach (var output in new[] { "eps", "P", "assembly_force", "retention_force" })
                Assert.Equal(constant.OutputSi(output), tapered.OutputSi(output), constant.OutputSi(output) * 1e-8);
        }
    }

    [Fact]
    public void Working_At_The_Permissible_Deflection_Gives_A_Utilisation_Of_One_And_Beyond_It_Fails()
    {
        foreach (var mode in new[] { SnapFitCalculator.Constant, SnapFitCalculator.TaperedThickness, SnapFitCalculator.TaperedWidth })
        {
            var first = Run(Mode(mode));
            var yPerm = first.OutputSi("y_perm") * 1e3;
            var at = Run(Mode(mode), Q("y", yPerm, "mm"));
            Assert.Equal(1.0, at.OutputSi("strain_util"), 9);

            var beyond = Run(Mode(mode), Q("y", yPerm * 1.2, "mm"));
            Assert.Contains(beyond.Checks, c => c.Name.StartsWith("Strain at the critical section", StringComparison.Ordinal) && !c.Passed);
            Assert.False(beyond.Passed);
            Assert.True(Run(Mode(mode)).Passed);
        }
    }

    [Fact]
    public void A_Beam_Shorter_Than_Five_Thicknesses_Warns()
    {
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "slender");
        Assert.DoesNotContain(Run(Q("l", 10, "mm")).Warnings, w => w.RuleId == "slender");
        var short1 = Run(Q("l", 9.9, "mm"));
        Assert.Contains(short1.Warnings, w => w.RuleId == "slender" && w.Message.StartsWith("l:", StringComparison.Ordinal));
        Assert.Contains(Run(Mode(SnapFitCalculator.TaperedWidth), Q("l", 6, "mm")).Warnings, w => w.RuleId == "slender");
    }

    // ------------------------------------------------------------------ assembly and separation

    [Fact]
    public void Assembly_And_Separation_Forces_Follow_The_Inclined_Plane_With_Friction()
    {
        Gen.Select(Gen.Double[0.05, 0.6], Gen.Double[5, 55], Gen.Double[10, 60]).Sample((mu, alpha, alphaR) =>
        {
            var r = Run(CalcInputValue.Of("mu", mu), Q("alpha", alpha, "deg"), Q("alpha_r", alphaR, "deg"));
            var p = r.OutputSi("P");
            var wa = p * (mu + Tan(alpha)) / (1 - mu * Tan(alpha));

            if (Math.Abs(r.OutputSi("assembly_force") - wa) > 1e-9 * wa)
                return false;

            var limit = Math.Atan(1 / mu) * 180 / Math.PI;

            if (alphaR >= limit)
                return r.FindOutput("retention_force")!.Value is TextValue;

            var wr = p * (mu + Tan(alphaR)) / (1 - mu * Tan(alphaR));
            return Math.Abs(r.OutputSi("retention_force") - wr) <= 1e-9 * wr;
        }, iter: 80);
    }

    [Fact]
    public void A_Return_Angle_At_Or_Above_The_Friction_Limit_Is_Self_Locking_And_Has_No_Separation_Number()
    {
        var limit = Math.Atan(1 / 0.5) * 180 / Math.PI;
        var below = Run(CalcInputValue.Of("mu", 0.5), Q("alpha_r", limit - 0.01, "deg"));
        Assert.IsType<ScalarValue>(below.FindOutput("retention_force")!.Value);
        Assert.Equal("no", Text(below, "self_locking"));
        Assert.DoesNotContain(below.Warnings, w => w.RuleId == "self-locking");

        foreach (var angle in new[] { limit, limit + 0.01, 90 })
        {
            var locked = Run(CalcInputValue.Of("mu", 0.5), Q("alpha_r", angle, "deg"));
            Assert.Equal("self-locking (infinite)", Text(locked, "retention_force"));
            Assert.StartsWith("YES", Text(locked, "self_locking"), StringComparison.Ordinal);
            Assert.Contains(locked.Warnings, w => w.RuleId == "self-locking" && w.Message.StartsWith("alpha_r:", StringComparison.Ordinal));
            Assert.DoesNotContain("Infinity", Text(locked, "retention_force"), StringComparison.Ordinal);
        }

        // Without friction nothing locks short of a face square to the joining direction.
        var smooth = Run(CalcInputValue.Of("mu", 0), Q("alpha_r", 89, "deg"));
        Assert.IsType<ScalarValue>(smooth.FindOutput("retention_force")!.Value);
        Assert.StartsWith("YES", Text(Run(CalcInputValue.Of("mu", 0), Q("alpha_r", 90, "deg")), "self_locking"), StringComparison.Ordinal);
    }

    [Fact]
    public void An_Entry_Face_That_Jams_Fails_The_Assembly_Check_And_Gives_No_Assembly_Number()
    {
        var jam = Run(CalcInputValue.Of("mu", 0.9), Q("alpha", 50, "deg"));
        Assert.Equal("jams (μ·tan α ≥ 1)", Text(jam, "assembly_force"));
        Assert.Contains(jam.Warnings, w => w.RuleId == "assembly-jam");
        Assert.Contains(jam.Checks, c => c.Name.StartsWith("Assembly", StringComparison.Ordinal) && !c.Passed);

        var free = Run(CalcInputValue.Of("mu", 0.9), Q("alpha", 40, "deg"));
        Assert.IsType<ScalarValue>(free.FindOutput("assembly_force")!.Value);
        Assert.DoesNotContain(free.Warnings, w => w.RuleId == "assembly-jam");
    }

    // ------------------------------------------------------------------ annular and torsion

    [Fact]
    public void The_Annular_Strain_Is_The_Interference_Over_The_Diameter_Times_The_Factor_And_The_Force_Is_The_Hoop_Force_Round_The_Ring()
    {
        Gen.Select(Gen.Double[10, 100], Gen.Double[0.05, 1], Gen.Double[0.5, 2], Gen.Double[0.8, 4], Gen.Double[2, 20], Gen.Double[800, 4000]).Sample((d, dd, k, h, b, es) =>
        {
            var r = Run(Mode(SnapFitCalculator.Annular), Q("D_ring", d, "mm"), Q("dD", dd, "mm"), CalcInputValue.Of("K_ann", k), Q("h", h, "mm"), Q("b", b, "mm"), Q("Es", es, "MPa"));
            var eps = k * dd / d;
            var p = 2 * Math.PI * es * eps * h * b;
            return !r.IsBlocked && Math.Abs(r.OutputSi("eps") - eps) <= 1e-9 * eps && Math.Abs(r.OutputSi("P") - p) <= 1e-9 * p && Math.Abs(r.OutputSi("y_perm") * 1e3 - 0.04 * d / k) <= 1e-9 * d;
        }, iter: 60);
    }

    [Fact]
    public void The_Torsion_Bar_Needs_Poissons_Ratio_And_Uses_The_Shear_Strain_Limit_Times_One_Plus_Nu()
    {
        var torsion = new[] { Mode(SnapFitCalculator.Torsion), Q("l", 20, "mm"), Q("r_bar", 2, "mm"), Q("a_arm", 10, "mm"), Q("y", 1.5, "mm"), CalcInputValue.Of("nu", 0.38) };
        var r = Run(torsion);
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(0.04 * 1.38, r.OutputSi("gamma_perm"), 12);
        Assert.Equal(0.015 / 0.04 / 1.38, r.OutputSi("strain_util"), 12);

        // The utilisation is the same ratio from either strain measure.
        Assert.Equal(r.OutputSi("gamma") / r.OutputSi("gamma_perm"), r.OutputSi("strain_util"), 12);

        var blank = Run([.. torsion, new CalcInputValue("nu")]);
        Assert.True(blank.IsBlocked);
        Assert.Contains(blank.Blocks, b => b.RuleId == "missing" && b.Message.StartsWith("nu: enter", StringComparison.Ordinal));
        Assert.Contains(Run(Mode(SnapFitCalculator.Torsion), Q("y", 5, "mm"), Q("a_arm", 10, "mm"), CalcInputValue.Of("nu", 0.38)).Warnings, w => w.RuleId == "small-angle");
    }

    // ------------------------------------------------------------------ material: nothing invented

    [Fact]
    public void No_Plastics_Data_Is_Shipped_And_A_New_Sheet_Says_What_To_Enter()
    {
        foreach (var symbol in new[] { "mu", "Es", "eps_perm_once", "eps_perm_repeated", "nu" })
            Assert.Null(Snap.Template.FindInput(symbol)!.Default);

        var table = Snap.Template.FindInput("Es_pts")!;
        Assert.Equal(CalcInputKind.Table, table.Kind);
        Assert.True(table.IsOptional);
        Assert.Equal("Es_curve", table.MaterialProperty);

        var fresh = CalcRunner.Run(Snap, CalcTemplateHasher.NewSheetInputs(Snap.Template));
        Assert.True(fresh.IsBlocked);
        Assert.All(fresh.Blocks, b => Assert.Contains("enter", b.Message, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fresh.Blocks, b => b.RuleId == "input.mu");

        var noEs = Run(new CalcInputValue("Es"));
        Assert.Contains(noEs.Blocks, b => b.RuleId == "missing" && b.Message.StartsWith("Es: enter", StringComparison.Ordinal) && b.Message.Contains("no default", StringComparison.Ordinal));
        var noEps = Run(new CalcInputValue("eps_perm_once"));
        Assert.Contains(noEps.Blocks, b => b.RuleId == "missing" && b.Message.StartsWith("eps_perm_once: enter", StringComparison.Ordinal));
        Assert.Null(noEps.FindOutput("strain_util"));
    }

    [Fact]
    public void The_Loading_Choice_Selects_The_One_Time_Or_The_Repeated_Permissible_Strain()
    {
        var once = Run(CalcInputValue.Of("eps_perm_repeated", 0.02));
        Assert.Equal(0.04, once.OutputSi("eps_perm"), 12);

        var repeated = Run(CalcInputValue.Choice("loading", SnapFitCalculator.Repeated), CalcInputValue.Of("eps_perm_repeated", 0.02));
        Assert.Equal(0.02, repeated.OutputSi("eps_perm"), 12);
        Assert.Equal(2 * once.OutputSi("strain_util"), repeated.OutputSi("strain_util"), 12);

        var missing = Run(CalcInputValue.Choice("loading", SnapFitCalculator.Repeated));
        Assert.Contains(missing.Blocks, b => b.RuleId == "missing" && b.Message.StartsWith("eps_perm_repeated:", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Secant_Modulus_Is_Interpolated_From_The_W3_Points_Holds_Its_End_Values_And_Ignores_The_Row_Order()
    {
        var points = new[] { (0.005, 2600.0), (0.01, 2400.0), (0.03, 1800.0) };
        var curve = CalcInputValue.Choice("Es_source", SnapFitCalculator.EsCurve);

        foreach (var (strain, expected) in new[] { (0.0075, 2500.0), (0.01, 2400.0), (0.02, 2100.0), (0.03, 1800.0) })
        {
            // eps = 1.5·h·y/l² with l = 15, h = 2: y = eps·225/3 mm.
            var r = Run(curve, SnapFitCalculator.SecantPoints(points), Q("y", strain * 75, "mm"));
            Assert.Equal(strain, r.OutputSi("eps"), 12);
            Assert.Equal(expected * 1e6, r.OutputSi("Es"), 1e-3);
            Assert.DoesNotContain(r.Warnings, w => w.RuleId == "es-range");
        }

        var shuffled = Run(curve, SnapFitCalculator.SecantPoints(points[2], points[0], points[1]), Q("y", 0.02 * 75, "mm"));
        Assert.Equal(2100e6, shuffled.OutputSi("Es"), 1e-3);

        var above = Run(curve, SnapFitCalculator.SecantPoints(points), Q("y", 0.04 * 75, "mm"));
        Assert.Equal(1800e6, above.OutputSi("Es"), 1e-3);
        Assert.Contains(above.Warnings, w => w.RuleId == "es-range" && w.Message.StartsWith("Es_pts:", StringComparison.Ordinal));
        var below = Run(curve, SnapFitCalculator.SecantPoints(points), Q("y", 0.002 * 75, "mm"));
        Assert.Equal(2600e6, below.OutputSi("Es"), 1e-3);
        Assert.Contains(below.Warnings, w => w.RuleId == "es-range");

        var empty = Run(curve, CalcInputValue.Table("Es_pts", []));
        Assert.True(empty.IsBlocked);
        Assert.Contains(empty.Blocks, b => b.RuleId == "es-curve" && b.Message.StartsWith("Es_pts: enter", StringComparison.Ordinal));
        var useless = Run(curve, SnapFitCalculator.SecantPoints((0, 2400)));
        Assert.Contains(useless.Blocks, b => b.RuleId == "es-curve");
    }

    // ------------------------------------------------------------------ sheet content

    [Fact]
    public void The_Root_Fillet_Advisory_And_The_Temperature_Note_Are_Always_On_The_Sheet()
    {
        var torsion = new[] { Mode(SnapFitCalculator.Torsion), Q("l", 20, "mm"), Q("r_bar", 2, "mm"), Q("a_arm", 10, "mm"), CalcInputValue.Of("nu", 0.38) };
        var ring = new[] { Mode(SnapFitCalculator.Annular) };

        foreach (var changes in new[] { Array.Empty<CalcInputValue>(), [Mode(SnapFitCalculator.TaperedThickness)], [Mode(SnapFitCalculator.TaperedWidth)], ring, torsion })
        {
            var r = Run(changes);
            Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
            Assert.Contains("fillet", Text(r, "fillet_advisory"), StringComparison.OrdinalIgnoreCase);
            Assert.Contains(r.RunAssumptions, a => a.Statement.Contains("temperature of use", StringComparison.Ordinal));
        }

        Assert.Contains("23", Run().RunAssumptions.Single(a => a.Statement.Contains("temperature of use", StringComparison.Ordinal)).Statement, StringComparison.Ordinal);
        Assert.Contains("60", Run(Q("T_use", 60, "degC")).RunAssumptions.Single(a => a.Statement.Contains("temperature of use", StringComparison.Ordinal)).Statement, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Declared_Ports_Are_Outputs_Of_A_Default_Run_And_The_Record_Kinds_Name_Real_Inputs()
    {
        var r = Run();

        foreach (var port in Snap.Template.OutputPorts!)
        {
            var scalar = Assert.IsType<ScalarValue>(r.FindOutput(port.Symbol)!.Value);
            Assert.Equal(Engine.Units.UnitCatalog.Parse(port.Unit.Length > 0 ? port.Unit : "1").Dimension, scalar.Dimension);
        }

        Assert.Equal(["assembly_force", "retention_force", "strain_util"], Snap.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.All(Snap.Template.RecordKinds!, kind => Assert.All(kind.Symbols, symbol => Assert.NotNull(Snap.Template.FindInput(symbol))));
        Assert.Contains(Snap.Template.RecordKinds!, k => k.Kind == "snapfit.annular-factor" && k.Symbols.SequenceEqual(["K_ann"]));
        Assert.Contains(Snap.Template.RecordKinds!, k => k.Kind == "snapfit.friction" && k.Symbols.SequenceEqual(["mu"]));
        var material = Snap.Template.RecordKinds!.Single(k => k.Kind == SnapFitCalculator.MaterialKind);
        Assert.All(Snap.Template.Inputs.Where(i => i.MaterialProperty is not null), i => Assert.Contains(i.Symbol, material.Symbols));
    }

    [Fact]
    public void Inputs_Outside_Their_Hard_Range_Are_Refused()
    {
        foreach (var (symbol, value, unit) in new[] { ("l", 0.01, "mm"), ("h", 0.001, "mm"), ("b", 0.0, "mm"), ("y", -1.0, "mm"), ("Es", 0.001, "MPa"), ("alpha", 90.0, "deg"), ("alpha_r", 91.0, "deg"), ("mu", 3.0, ""), ("eps_perm_once", 0.0, ""), ("h_ratio", 0.01, ""), ("b_ratio", 1.5, ""), ("T_use", 1000.0, "degC") })
        {
            var change = unit.Length > 0 ? Q(symbol, value, unit) : CalcInputValue.Of(symbol, value);
            Assert.Contains(CalcInputValidator.Validate(Snap.Template, Defaults().With(change)), n => n.Severity == CalcNoticeSeverity.Block);
        }
    }

    [Fact]
    public void Every_Rule_The_Run_Raises_Is_Declared_On_The_Template()
    {
        var declared = Snap.Template.ApplicabilityRules.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var runs = new[]
        {
            Run(Q("l", 5, "mm")), Run(CalcInputValue.Of("mu", 0.9), Q("alpha", 50, "deg")), Run(CalcInputValue.Of("mu", 0.2), Q("alpha_r", 90, "deg")),
            Run(Mode(SnapFitCalculator.Torsion), Q("y", 5, "mm"), Q("a_arm", 10, "mm"), CalcInputValue.Of("nu", 0.38)),
            Run(CalcInputValue.Choice("Es_source", SnapFitCalculator.EsCurve), SnapFitCalculator.SecantPoints((0.1, 1000))),
            Run(CalcInputValue.Choice("Es_source", SnapFitCalculator.EsCurve), CalcInputValue.Table("Es_pts", [])),
            Run(new CalcInputValue("Es")), Run(Mode(SnapFitCalculator.TaperedWidth), new CalcInputValue("b_ratio")),
        };

        foreach (var notice in runs.SelectMany(r => r.Notices))
            Assert.True(declared.Contains(notice.RuleId) || notice.RuleId.StartsWith("input.", StringComparison.Ordinal) || notice.RuleId.StartsWith("record.", StringComparison.Ordinal), $"rule '{notice.RuleId}' is not declared");
    }
}
