using CsCheck;
using Tempest.Calc.Engine.Analysis;
using Tempest.Calc.Engine.Analysis.ToleranceStack;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.FluidPower;

/// <summary>F3 — O-ring and seal gland: behaviour beyond the known-answer cases.</summary>
public sealed class ORingGlandTests
{
    private static readonly ORingGlandCalculator Gland = new();

    /// <summary>The first known-answer case (the defaults and a permitted gap of 0.15 mm) with overrides applied in order.</summary>
    private static CalcInputSet With(params CalcInputValue[] overrides) => overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Gland.Template), (set, value) => set.With(value));

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Gland, With(overrides));

    private static CalcInputValue Mm(string symbol, double value) => CalcInputValue.Quantity(symbol, value, "mm");

    private static CalcInputValue Percent(string symbol, double value) => CalcInputValue.Quantity(symbol, value, "%");

    private static double Pct(CalcRunResult r, string symbol) => r.OutputSi(symbol) * 100.0;

    /// <summary>No reduction and no thermal change: every figure is a plain hand calculation.</summary>
    private static CalcInputValue[] Plain(params CalcInputValue[] more) =>
    [
        CalcInputValue.Choice("red", ORingGlandCalculator.RelationNone),
        CalcInputValue.Quantity("T_min", 20, "degC"), CalcInputValue.Quantity("T_max", 20, "degC"),
        Percent("s_min", 0), Percent("s_max", 0),
        .. more,
    ];

    [Fact]
    public void The_Defaults_Pass_Every_Check_And_The_Four_Ports_Are_Outputs()
    {
        var r = Run();
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.True(r.Passed);
        Assert.Equal(["squeeze_min", "squeeze_max", "fill_max", "stretch"], Gland.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.All(Gland.Template.OutputPorts!, p => Assert.NotNull(r.FindOutput(p.Symbol)));
        Assert.Equal(16.9560030299, Pct(r, "squeeze_min"), 7);
        Assert.Equal(26.9574115556, Pct(r, "squeeze_max"), 7);
        Assert.Equal(84.6429192000, Pct(r, "fill_max"), 7);
        Assert.Equal(3.8306451613, Pct(r, "stretch"), 7);
    }

    [Fact]
    public void Without_Reduction_Or_Thermal_Change_The_Worst_Case_Is_The_Hand_Calculation()
    {
        var r = Run(Plain());

        // Depth from 2.700 to 2.748 mm, W from 3.4 to 3.6 mm.
        Assert.Equal((1 - 2.748 / 3.4) * 100, Pct(r, "squeeze_min"), 9);
        Assert.Equal((1 - 2.700 / 3.6) * 100, Pct(r, "squeeze_max"), 9);
        Assert.Equal((51.5 / 49.6 - 1) * 100, Pct(r, "stretch"), 9);
        Assert.Equal((51.45 / 50.4 - 1) * 100, Pct(r, "stretch_min"), 9);
        Assert.Equal(0.106e-3, r.OutputSi("gap_max"), 12);
        var corner = 2 * (1 - Math.PI / 4) * 0.2 * 0.2;
        Assert.Equal(Math.PI / 4 * 3.6 * 3.6 / (4.8 * 2.700 - corner) * 100, Pct(r, "fill_max"), 9);
        Assert.Equal(Math.PI / 4 * 3.4 * 3.4 / (4.9 * 2.748 - corner) * 100, Pct(r, "fill_min"), 9);
    }

    [Fact]
    public void The_Duty_Selects_The_Squeeze_Band_And_Each_Criterion_Passes_Or_Fails_On_Its_Own()
    {
        Assert.True(Run().Passed);

        var dynamicDuty = Run(CalcInputValue.Choice("duty", ORingGlandCalculator.DutyDynamic));
        Assert.Contains(dynamicDuty.Checks, c => c.Name.StartsWith("Squeeze maximum", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(dynamicDuty.Checks, c => c.Name.StartsWith("Squeeze minimum", StringComparison.Ordinal) && c.Passed);

        // The bands are inputs a record fills: widening the dynamic band passes it.
        Assert.True(Run(CalcInputValue.Choice("duty", ORingGlandCalculator.DutyDynamic), Percent("q_hi_d", 30)).Passed);
        Assert.Contains(Run(Percent("q_lo_s", 18)).Checks, c => c.Name.StartsWith("Squeeze minimum", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(Run(Percent("fill_hi", 80)).Checks, c => c.Name.StartsWith("Gland fill maximum", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(Run(Percent("fill_lo", 65)).Checks, c => c.Name.StartsWith("Gland fill minimum", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(Gland.Template.RecordKinds!, k => k.Kind == "oring.criteria" && k.Symbols.Contains("q_lo_s") && k.Symbols.Contains("fill_hi") && k.Symbols.Contains("x_hi"));
    }

    [Fact]
    public void The_Installed_Stretch_Rule_Passes_At_5_8_Percent_And_Fails_At_6_5()
    {
        CalcInputValue[] At(double seat) => [Mm("d_r_tol", 0), Mm("D_g", seat), Mm("D_g_u", 0), Mm("D_g_l", 0), Mm("D_s", seat + 4.7), Mm("D_c", seat + 4.6)];

        var ok = Run(At(52.9));
        Assert.Equal(5.8, Pct(ok, "stretch"), 9);
        Assert.Equal(5.8 / 6, ok.OutputSi("u_stretch"), 9);
        Assert.Contains(ok.Checks, c => c.Name.StartsWith("Installed stretch not above", StringComparison.Ordinal) && c.Passed);

        var tooMuch = Run(At(53.25));
        Assert.Equal(6.5, Pct(tooMuch, "stretch"), 9);
        Assert.Contains(tooMuch.Checks, c => c.Name.StartsWith("Installed stretch not above", StringComparison.Ordinal) && !c.Passed);
        Assert.False(tooMuch.Passed);

        // A ring larger than its seat is not stretched at all: the lower limit catches it.
        var loose = Run(At(49.5));
        Assert.Contains(loose.Checks, c => c.Name.StartsWith("Installed stretch not below", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void The_Reduction_Relations_Give_Their_Formulas_And_More_Reduction_Means_Less_Squeeze()
    {
        double Red(string relation, double nu = 0.49) => Pct(Run(Mm("d_r_tol", 0), Mm("D_s", 57), Mm("D_g", 52), Mm("D_g_u", 0), Mm("D_g_l", 0), CalcInputValue.Choice("red", relation), CalcInputValue.Of("nu", nu)), "red_nom");

        Assert.Equal(0, Red(ORingGlandCalculator.RelationNone), 12);
        Assert.Equal((1 - 1 / Math.Sqrt(1.04)) * 100, Red(ORingGlandCalculator.RelationIncompressible), 9);
        Assert.Equal(0.49 * 4, Red(ORingGlandCalculator.RelationLinear), 9);
        Assert.Equal(0.56 + 0.59 * 4 - 0.0046 * 16, Red(ORingGlandCalculator.RelationParker), 9);

        double Min(string relation) => Pct(Run(CalcInputValue.Choice("red", relation)), "squeeze_min");
        Assert.True(Min(ORingGlandCalculator.RelationNone) > Min(ORingGlandCalculator.RelationIncompressible));
        Assert.True(Min(ORingGlandCalculator.RelationIncompressible) > Min(ORingGlandCalculator.RelationParker));

        // The Parker quadratic is used for about 3 to 25 % stretch, held at its 25 % value above that, reads zero without stretch and is cited, not confirmed.
        Assert.Equal(ORingGlandModel.Parker(25) / 100, ORingGlandModel.Reduction(ReductionRelation.Parker, 3.0, 0.49), 12);
        Assert.Equal(0, ORingGlandModel.Reduction(ReductionRelation.Parker, -0.02, 0.49));
        Assert.Contains(Gland.Template.ClauseRefs, c => c.Standard.Contains("ORD 5700", StringComparison.Ordinal) && c.Clause == "Section 3.5");
        Assert.Contains(Run(CalcInputValue.Choice("red", ORingGlandCalculator.RelationParker)).RunAssumptions, a => a.Statement.Contains("not been confirmed", StringComparison.Ordinal));
    }

    [Fact]
    public void Cold_Lowers_The_Minimum_Squeeze_And_Hot_Swell_Raises_The_Maximum_Squeeze_And_Fill()
    {
        var baseline = Run();
        Assert.True(Pct(Run(CalcInputValue.Quantity("T_min", -50, "degC")), "squeeze_min") < Pct(baseline, "squeeze_min"));
        Assert.True(Pct(Run(Percent("s_min", -3)), "squeeze_min") < Pct(baseline, "squeeze_min"));
        Assert.True(Pct(Run(Percent("s_max", 12)), "squeeze_max") > Pct(baseline, "squeeze_max"));

        var swollen = Run(Percent("s_max", 12));
        Assert.Contains(swollen.Checks, c => c.Name.StartsWith("Gland fill maximum", StringComparison.Ordinal) && !c.Passed);

        // The fill uses the unstretched section, so the reduction relation does not move it.
        Assert.Equal(Pct(baseline, "fill_max"), Pct(Run(CalcInputValue.Choice("red", ORingGlandCalculator.RelationNone)), "fill_max"), 9);

        // The factors are the documented formulas.
        double Step(string symbol) => ((Tempest.Calc.Engine.Values.ScalarValue)baseline.Steps.Single(x => x.Symbol == symbol).Result).Si;
        Assert.Equal(1 + 1.5e-4 * -40, Step("f_c"), 12);
        Assert.Equal(1 + 1.2e-5 * -40, Step("g_c"), 12);
        Assert.Equal((1 + 1.5e-4 * 60) * Math.Cbrt(1.05), Step("f_h"), 12);
        Assert.Equal(1 + 1.2e-5 * 60, Step("g_h"), 12);
    }

    [Fact]
    public void A_Ring_That_Expands_Less_Than_The_Groove_Metal_Warns_That_The_Ends_Are_Not_The_Bounds()
    {
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "expansion");
        Assert.Contains(Run(CalcInputValue.Quantity("alpha", 5e-6, "1/K")).Warnings, w => w.RuleId == "expansion");
    }

    [Fact]
    public void The_Extrusion_Record_Is_Interpolated_Flagged_Outside_Its_Range_And_Combined_With_A_Value()
    {
        var points = new ExtrusionPoint[]
        {
            new(70, 0, 0.30), new(70, 100, 0.15), new(70, 200, 0.08),
            new(90, 0, 0.45), new(90, 100, 0.25), new(90, 200, 0.15),
        };

        Assert.Equal(0.1575, ExtrusionRecord.Lookup(points, 80, 150).GapMm!.Value, 12);
        Assert.Equal(0.115, ExtrusionRecord.Lookup(points, 70, 150).GapMm!.Value, 12);
        Assert.Equal(0.35, ExtrusionRecord.Lookup(points, 90, 50).GapMm!.Value, 12);
        Assert.False(ExtrusionRecord.Lookup(points, 80, 150).OutOfRange);

        var tooHigh = ExtrusionRecord.Lookup(points, 80, 250);
        Assert.True(tooHigh.OutOfRange);
        Assert.Equal((0.08 + 0.15) / 2, tooHigh.GapMm!.Value, 12);
        Assert.True(ExtrusionRecord.Lookup(points, 60, 100).OutOfRange);
        Assert.False(ExtrusionRecord.Lookup(points, 95, 100).OutOfRange);
        Assert.Equal(0.25, ExtrusionRecord.Lookup(points, 95, 100).GapMm!.Value, 12);
        Assert.Null(ExtrusionRecord.Lookup([], 70, 100).GapMm);

        CalcInputValue Record(params (double H, double P, double Gap)[] rows) =>
            CalcInputValue.Table("ext", rows.Select(x => CalcRow.Of(("H", new CalcCell(x.H)), ("p", new CalcCell(x.P, "bar")), ("gap", new CalcCell(x.Gap, "mm")))).ToList());

        // The sheet: the record's gap is the limit, the largest gap 0.106 mm is checked against it.
        var rows = points.Select(p => (p.Hardness, p.PressureBar, p.GapMm)).ToArray();
        var noValue = new CalcInputValue("gap_allow");
        var inRange = Run(noValue, Record(rows), CalcInputValue.Of("H_A", 80), CalcInputValue.Quantity("p_op", 150, "bar"));
        Assert.Equal(0.1575e-3, inRange.OutputSi("gap_allow"), 12);
        Assert.Contains(inRange.Checks, c => c.Name.StartsWith("Extrusion gap", StringComparison.Ordinal) && c.Passed);

        var strict = Run(Record(rows), CalcInputValue.Of("H_A", 80), CalcInputValue.Quantity("p_op", 150, "bar"), Mm("gap_allow", 0.10));
        Assert.Equal(0.10e-3, strict.OutputSi("gap_allow"), 12);
        Assert.Contains(strict.Checks, c => c.Name.StartsWith("Extrusion gap", StringComparison.Ordinal) && !c.Passed);

        var outside = Run(noValue, Record(rows), CalcInputValue.Of("H_A", 80), CalcInputValue.Quantity("p_op", 250, "bar"));
        Assert.Contains(outside.Warnings, w => w.RuleId == "extrusion-range");
        Assert.Contains(outside.Checks, c => c.Name.StartsWith("Extrusion gap", StringComparison.Ordinal) && !c.Passed);

        // Without a record or a value the check is not made and the sheet says so.
        var none = CalcRunner.Run(Gland, CalcTemplateHasher.DefaultInputs(Gland.Template).With(new CalcInputValue("gap_allow")));
        Assert.Contains(none.Warnings, w => w.RuleId == "extrusion");
        Assert.DoesNotContain(none.Checks, c => c.Name.StartsWith("Extrusion gap", StringComparison.Ordinal));
        Assert.Contains(Gland.Template.RecordKinds!, k => k.Kind == "oring.extrusion" && k.Symbols.Contains("ext"));
    }

    [Fact]
    public void A_Measured_Distribution_Replaces_The_Tolerance_And_Is_Checked()
    {
        CalcInputValue Measured(params (string Dim, double Mean, double Sigma, string? Dist)[] rows) =>
            CalcInputValue.Table("measured", rows.Select(x => CalcRow.Of(("dim", new CalcCell(Text: x.Dim)), ("mean", new CalcCell(x.Mean, "mm")), ("sigma", new CalcCell(x.Sigma, "mm")), ("dist", new CalcCell(Text: x.Dist ?? ORingGlandCalculator.DistributionNormal)))).ToList());

        // A perfectly repeatable groove (σ = 0) at its mean collapses the groove part of the spread.
        var plain = Run(Plain());
        var measured = Run(Plain(Measured(("D_g", 51.475, 0, null), ("D_s", 56.923, 0, null), ("b", 4.85, 0, null))));
        Assert.False(measured.IsBlocked, string.Join("; ", measured.Blocks.Select(b => b.Message)));
        Assert.True(Pct(measured, "squeeze_min") > Pct(plain, "squeeze_min"));
        Assert.True(Pct(measured, "squeeze_max") < Pct(plain, "squeeze_max"));

        // A measured normal about 51.48 mm with σ 0.012 mm has a 3σ half-band of 0.036 mm.
        var r = Run(Plain(Measured(("D_g", 51.48, 0.012, null))));
        Assert.Equal((51.48 + 0.036) / (50 - 0.4) * 100 - 100, Pct(r, "stretch"), 9);

        // A dimension twice, or one the arrangement does not use, or a mean below the spread.
        Assert.StartsWith("measured:", Run(Measured(("D_g", 51.5, 0.01, null), ("D_g", 51.5, 0.01, null))).Blocks[0].Message, StringComparison.Ordinal);
        Assert.Contains(Run(Measured(("h_f", 2.7, 0.01, null))).Warnings, w => w.RuleId == "measured");
        Assert.True(Run(Measured(("D_g", 0.05, 0.1, null))).IsBlocked);
    }

    [Fact]
    public void Inconsistent_Geometry_Blocks_And_Names_The_Input()
    {
        void Blocks(string symbol, params CalcInputValue[] overrides)
        {
            var r = Run(overrides);
            Assert.True(r.IsBlocked, $"{symbol}: expected a block");
            Assert.StartsWith(symbol + ":", r.Blocks[0].Message, StringComparison.Ordinal);
        }

        Blocks("D_s_l", Mm("D_s_u", 0.01), Mm("D_s_l", 0.02));
        Blocks("D_g", Mm("D_g", 57.5));
        Blocks("D_c", Mm("D_c", 57.5));
        Blocks("W", Mm("W", 0.5), Mm("W_tol", 0.6));
        Blocks("d_r", Mm("d_r", 1), Mm("d_r_tol", 1.5));
        Blocks("T_max", CalcInputValue.Quantity("T_max", -40, "degC"));
        Blocks("s_max", Percent("s_max", -5), Percent("s_min", 0));
        Blocks("q_hi_s", Percent("q_hi_s", 10), Percent("q_lo_s", 20));
        Blocks("fill_hi", Percent("fill_hi", 50));
        Blocks("x_hi", Percent("x_hi", 0.5), Percent("x_lo", 1));
        Blocks("r_g", Mm("r_g", 5), Mm("b", 1), Mm("b_u", 0), Mm("b_l", 0));
        Blocks("h_f", CalcInputValue.Choice("type", ORingGlandCalculator.TypeFace), Mm("h_f", 0.1), Mm("h_f_u", 0), Mm("h_f_l", -0.2));
    }

    [Fact]
    public void A_Rod_Seal_Stretches_On_The_Rod_Checks_Its_Outside_Diameter_And_A_Face_Seal_Takes_Its_Depth_Directly()
    {
        CalcInputValue[] Rod =
        [
            CalcInputValue.Choice("type", ORingGlandCalculator.TypeRod),
            Mm("d_r", 39.5), Mm("d_r_tol", 0.35), Mm("W", 3.0), Mm("W_tol", 0.1),
            Mm("D_s", 40), Mm("D_s_u", -0.025), Mm("D_s_l", -0.05), Mm("D_g", 44.8), Mm("D_g_u", 0.062), Mm("D_g_l", 0),
            Mm("D_c", 40), Mm("D_c_u", 0.039), Mm("D_c_l", 0), Mm("b", 4.1),
        ];
        var rod = Run(Rod);
        Assert.False(rod.IsBlocked, string.Join("; ", rod.Blocks.Select(b => b.Message)));
        Assert.Equal((39.9625 / 39.5 - 1) * 100, Pct(rod, "stretch_nom"), 9);
        Assert.NotNull(rod.FindOutput("od_comp"));
        Assert.Contains(rod.Checks, c => c.Name.StartsWith("Ring OD compression", StringComparison.Ordinal));
        Assert.Contains(rod.Checks, c => c.Name.StartsWith("Squeeze minimum", StringComparison.Ordinal) && !c.Passed);
        Assert.Null(Run().FindOutput("od_comp"));

        CalcInputValue[] Face = [CalcInputValue.Choice("type", ORingGlandCalculator.TypeFace)];
        var face = Run(Face);
        Assert.False(face.IsBlocked, string.Join("; ", face.Blocks.Select(b => b.Message)));
        // Face depth 2.7 to 2.75 mm, ring section 3.4 to 3.6 mm, before the thermal and reduction effects.
        var plain = Run(Plain(Face));
        Assert.Equal((1 - 2.75 / 3.4) * 100, Pct(plain, "squeeze_min"), 9);
        Assert.Equal((1 - 2.70 / 3.6) * 100, Pct(plain, "squeeze_max"), 9);
        Assert.Equal(0.1e-3, plain.OutputSi("gap_max"), 12);
        Assert.Contains(Run(CalcInputValue.Choice("type", ORingGlandCalculator.TypeFace), CalcInputValue.Choice("duty", ORingGlandCalculator.DutyDynamic)).Warnings, w => w.RuleId == "face");
    }

    [Fact]
    public void The_Monte_Carlo_Is_Repeatable_By_Seed_And_Counts_The_Fraction_Outside_Each_Limit()
    {
        CalcInputValue[] Tight = [Percent("x_hi", 3.5), Percent("q_lo_s", 19), Mm("gap_allow", 0.09)];
        var a = Run([.. Tight, CalcInputValue.Of("seed", 5)]);
        var b = Run([.. Tight, CalcInputValue.Of("seed", 5)]);
        var c = Run([.. Tight, CalcInputValue.Of("seed", 6)]);

        foreach (var symbol in new[] { "p_sq_min", "p_stretch", "p_gap", "p_fill_max" })
            Assert.Equal(a.OutputSi(symbol), b.OutputSi(symbol));

        Assert.NotEqual(a.OutputSi("p_stretch"), c.OutputSi("p_stretch"));

        // The count is a multiple of 1/n; the stretch spread (σ 0.28 points about 2.95 %) puts about 6 % over a 3.5 % limit.
        Assert.Equal(0, Math.IEEERemainder(a.OutputSi("p_stretch") * 10_000, 1), 6);
        Assert.InRange(Pct(a, "p_stretch"), 1, 10);
        Assert.InRange(Pct(a, "p_sq_min"), 0.5, 15);

        // A wider tolerance makes more fail; a tighter one fewer.
        Assert.True(Pct(Run([.. Tight, Mm("D_g_u", 0.05), Mm("D_g_l", -0.10)]), "p_stretch") > Pct(a, "p_stretch"));
        Assert.True(Pct(Run([.. Tight, Mm("d_r_tol", 0.1)]), "p_stretch") < Pct(a, "p_stretch"));
        Assert.True(a.OutputSi("cpk_stretch") > 0);
    }

    private static (GlandEnvironment Env, GlandVar[] Vars) PistonGland(ReductionRelation relation = ReductionRelation.Parker)
    {
        var env = new GlandEnvironment(SealKind.Piston, relation, 0.49, 1.5e-4, 1.2e-5, 20, -20, 80, 0, 0.05, 0.2);
        GlandVar Var(GlandDim dim, double nominal, double upper, double lower) =>
            new(dim, dim.ToString(), nominal, nominal + (upper + lower) / 2, (upper - lower) / 2, (upper - lower) / 6, ContributorDistribution.Normal);

        return (env,
        [
            Var(GlandDim.Section, 3.5, 0.1, -0.1), Var(GlandDim.RingId, 50, 0.4, -0.4), Var(GlandDim.Surface, 56.9, 0.046, 0), Var(GlandDim.Groove, 51.5, 0, -0.05),
            Var(GlandDim.Clearance, 56.9, -0.03, -0.06), Var(GlandDim.Width, 4.8, 0.1, 0),
        ]);
    }

    [Fact]
    public void The_Monte_Carlo_Runs_The_Exact_Model_And_Sees_The_Tail_The_Linearised_Model_Misses()
    {
        // L1: the engine's Monte Carlo is run on ORingGlandModel.Evaluate itself; the linearised stack stays for C_pk. The review found the
        // linear model under-predicts the minimum-squeeze tail by about 9 % (1.78 % against 1.95 to 2.01 %).
        var (env, vars) = PistonGland();
        var analysis = ORingGlandModel.Analyse(env, GlandCriterion.SqueezeMin, vars, 0.18, null, 200_000, 7);
        var exact = analysis.Simulation!.FractionBelowLower!.Value;
        var linear = ToleranceStackEngine.Simulate(analysis.Linear.Contributors(), 200_000, 7, new StackRequirement(0.18, null)).FractionBelowLower!.Value;

        Assert.InRange(exact, 0.0185, 0.0235);
        Assert.True(exact > linear + 0.0015, $"exact {exact:P3} against linearised {linear:P3}");

        // The same seed and the same sampling order reproduce the count exactly with an independent call of the engine on the model.
        var nominal = vars.ToDictionary(v => v.Dim.ToString(), v => v.Mid);
        var distributions = vars.ToDictionary(v => v.Dim.ToString(), v => Distribution.Normal(v.Mid, v.Sigma));
        var independent = MonteCarlo.Run(m =>
        {
            var x = new double[8];

            foreach (var v in vars)
                x[(int)v.Dim] = m[v.Dim.ToString()];

            return ORingGlandModel.Evaluate(env, GlandCriterion.SqueezeMin, x);
        }, nominal, distributions, 200_000, 7, 0.18, null);
        Assert.Equal(independent.FractionBelowLower, exact);

        foreach (var (criterion, lower, upper) in new (GlandCriterion, double?, double?)[]
        {
            (GlandCriterion.Stretch, null, 0.032), (GlandCriterion.FillMax, null, 0.80), (GlandCriterion.Gap, null, 0.075), (GlandCriterion.FillMin, 0.65, null),
        })
        {
            var a = ORingGlandModel.Analyse(env, criterion, vars, lower, upper, 100_000, 3);
            var sim = (a.Simulation!.FractionBelowLower ?? 0) + (a.Simulation.FractionAboveUpper ?? 0);
            var lin = ToleranceStackEngine.Simulate(a.Linear.Contributors(), 100_000, 3, new StackRequirement(lower, upper));
            var linFraction = (lin.FractionBelowLower ?? 0) + (lin.FractionAboveUpper ?? 0);

            Assert.True(sim is > 0.001 and < 0.9, $"{criterion}: fraction {sim:P3} is not in the testable range");
            Assert.True(Math.Abs(sim - linFraction) < 0.2 * Math.Max(sim, linFraction) + 0.003, $"{criterion}: exact {sim:P2} against linearised {linFraction:P2}");
        }

        // C_pk is the linearised one, and a run with no limit skips the simulation.
        Assert.NotNull(analysis.Stack.Capability);
        Assert.Null(ORingGlandModel.Analyse(env, GlandCriterion.SqueezeMin, vars, null, null, 1_000, 1).Simulation);
    }

    [Fact]
    public void A_Worst_Case_Pass_Leaves_Only_A_Small_Monte_Carlo_Tail_And_The_Stack_Is_Never_Less_Conservative_Than_The_Exact_Corner()
    {
        Gen.Select(Gen.Double[0.3, 2.0], Gen.Double[0.3, 2.0], Gen.Double[0.0, 0.02]).Sample((wScale, dScale, grooveShift) =>
        {
            var r = Run(Mm("W_tol", 0.1 * wScale), Mm("d_r_tol", 0.4 * dScale), Mm("D_g", 51.5 + grooveShift), CalcInputValue.Of("n_mc", 4_000));

            if (r.IsBlocked)
                return true;

            var bandsHold = Pct(r, "squeeze_min") >= 15 && Pct(r, "squeeze_max") <= 30 && Pct(r, "stretch") <= 6 && Pct(r, "fill_max") <= 85 && Pct(r, "fill_min") >= 60;
            var ordered = Pct(r, "squeeze_min") <= Pct(r, "squeeze_nom") + 1e-9 && Pct(r, "squeeze_nom") <= Pct(r, "squeeze_max") + 1e-9 && Pct(r, "stretch_min") <= Pct(r, "stretch") + 1e-9;
            var inRange = new[] { "p_sq_min", "p_sq_max", "p_fill_max", "p_fill_min", "p_stretch", "p_gap" }.All(s => Pct(r, s) is >= 0 and <= 100);
            var exact = r.Warnings.All(w => w.RuleId != "worst-case");
            var tail = !bandsHold || new[] { "p_sq_min", "p_sq_max", "p_fill_max", "p_fill_min", "p_stretch" }.All(s => Pct(r, s) <= 1.0);
            return ordered && inRange && exact && tail;
        }, iter: 60);
    }

    [Fact]
    public void Material_Properties_Ring_Size_And_Bands_Are_Records_And_No_Size_Table_Is_Shipped()
    {
        var kinds = Gland.Template.RecordKinds!;
        var material = Assert.Single(kinds, k => k.Kind == "material.properties");
        Assert.Equal(["alpha", "nu"], material.Symbols);
        Assert.All(material.Symbols, s => Assert.NotNull(Gland.Template.FindInput(s)!.MaterialProperty));
        Assert.Equal("alpha", Gland.Template.FindInput("alpha")!.MaterialProperty);
        Assert.Equal("nu", Gland.Template.FindInput("nu")!.MaterialProperty);

        // L5: the library applies one material record to a sheet, so the groove metal's alpha_m (a second material) stays an input the user enters from the metal's data sheet; the input says so.
        Assert.Null(Gland.Template.FindInput("alpha_m")!.MaterialProperty);
        Assert.Contains("one material record per sheet", Gland.Template.FindInput("alpha_m")!.Description, StringComparison.Ordinal);
        Assert.Contains(kinds, k => k.Kind == "oring.ring" && k.Symbols.Contains("W") && k.Symbols.Contains("d_r"));

        // Only the two optional tables exist, and both are the user's data (measured scan results, the extrusion record).
        Assert.Equal(["ext", "measured"], Gland.Template.Inputs.Where(i => i.Kind == CalcInputKind.Table).Select(i => i.Symbol).Order());
        Assert.All(Gland.Template.Inputs.Where(i => i.Kind == CalcInputKind.Table), i => Assert.True(i.IsOptional));
        Assert.DoesNotContain(Gland.Template.Inputs, i => i.Kind == CalcInputKind.Table && i.Symbol is "sizes" or "glands");
    }

    [Fact]
    public void Every_Numeric_Input_Has_Hard_Limits_And_The_Clause_References_Carry_Numbers_And_Titles_Only()
    {
        Assert.All(Gland.Template.Inputs.Where(i => i.Kind is CalcInputKind.Quantity or CalcInputKind.Number && i.Symbol is not "seed"), spec =>
        {
            Assert.True(spec.HardMin is not null && spec.HardMax is not null, $"{spec.Symbol} needs hard limits");
        });

        foreach (var (symbol, value, unit) in new[] { ("W", 0.1, "mm"), ("W", 50.0, "mm"), ("d_r", 5000.0, "mm"), ("alpha", -1e-5, "1/K"), ("nu", 0.6, ""), ("x_hi", 0.0, "%"), ("fill_hi", 150.0, "%"), ("n_mc", 0.0, "") })
        {
            var v = unit.Length > 0 ? CalcInputValue.Quantity(symbol, value, unit) : CalcInputValue.Of(symbol, value);
            Assert.Contains(CalcInputValidator.Validate(Gland.Template, With(v)), n => n.Severity == CalcNoticeSeverity.Block);
        }

        Assert.All(Gland.Template.ClauseRefs, c => Assert.True(c.Title.Length < 120));
    }

    private static readonly CalcInputValue[] RodGland =
    [
        CalcInputValue.Choice("type", ORingGlandCalculator.TypeRod),
        Mm("d_r", 39.5), Mm("d_r_tol", 0.35), Mm("W", 3.0), Mm("W_tol", 0.1),
        Mm("D_s", 40), Mm("D_s_u", -0.025), Mm("D_s_l", -0.05), Mm("D_g", 44.8), Mm("D_g_u", 0.062), Mm("D_g_l", 0),
        Mm("D_c", 40), Mm("D_c_u", 0.039), Mm("D_c_l", 0), Mm("b", 4.1),
    ];

    private static readonly CalcInputValue[] InternalFace =
    [
        CalcInputValue.Choice("type", ORingGlandCalculator.TypeFace), CalcInputValue.Choice("face_p", ORingGlandCalculator.PressureInternal),
        Mm("d_r", 60), Mm("d_r_tol", 0.5), Mm("W", 3.5), Mm("W_tol", 0.1),
        Mm("D_g", 66.7), Mm("D_g_u", 0.05), Mm("D_g_l", 0), Mm("h_f", 2.75), Mm("h_f_u", 0.05), Mm("h_f_l", 0),
        Mm("e_f", 0.05), Mm("e_f_u", 0.05), Mm("e_f_l", 0), Mm("b", 4.9), Mm("b_u", 0.1), Mm("b_l", 0),
    ];

    private static double Step(CalcRunResult r, string symbol) => ((Tempest.Calc.Engine.Values.ScalarValue)r.Steps.Single(x => x.Symbol == symbol).Result).Si;

    [Fact]
    public void The_Hot_Fill_Is_On_A_Volume_Basis_Not_An_Isotropic_Section_Area()
    {
        // M2: fill_max = (pi/4)·W_max²·(1 + alpha·dT)³(1 + s)/(A_g,min·g³) with the unscaled groove area; checked by hand, independent of the model.
        double Hand(double swell, double alphaRing = 1.5e-4, double tMax = 80)
        {
            double g = 1 + 1.2e-5 * (tMax - 20), unscaledGroove = 4.8 * 2.700 - 2 * (1 - Math.PI / 4) * 0.2 * 0.2;
            return Math.PI / 4 * 3.6 * 3.6 * Math.Pow(1 + alphaRing * (tMax - 20), 3) * (1 + swell) / (unscaledGroove * Math.Pow(g, 3)) * 100;
        }

        Assert.Equal(Hand(0.05), Pct(Run(), "fill_max"), 8);
        Assert.Equal(84.64, Pct(Run(), "fill_max"), 2);
        Assert.Equal(Hand(0.15), Pct(Run(Percent("s_max", 15)), "fill_max"), 8);
        Assert.InRange(Pct(Run(Percent("s_max", 15)), "fill_max"), 92.6, 92.8);
        Assert.Equal(Hand(0.0, 3e-4, 100), Pct(Run(CalcInputValue.Quantity("T_max", 100, "degC"), Percent("s_max", 0), CalcInputValue.Quantity("alpha", 3e-4, "1/K")), "fill_max"), 8);

        // The isotropic scaling it replaces would read lower (82.59 % on the defaults): the model's ring area carries f³/g, not f².
        var (env, vars) = PistonGland(ReductionRelation.Incompressible);
        var mid = new double[8];

        foreach (var v in vars)
            mid[(int)v.Dim] = v.Mid;

        var (f, g) = ORingGlandModel.Factors(env, GlandCase.Hot);
        Assert.Equal(Math.PI / 4 * Math.Pow(f * 3.5, 2) * f / g, ORingGlandModel.HotRingArea(env, mid), 9);
    }

    [Fact]
    public void The_Parker_Quadratic_Is_Used_For_About_3_To_25_Percent_And_The_Sheet_Warns_Outside_That()
    {
        // M3: no jump from nothing to 0.56 % at the first trace of stretch; held at the 25 % value above the range.
        double Y(double x) => ORingGlandModel.Reduction(ReductionRelation.Parker, x, 0.49);
        Assert.Equal(0, Y(0));
        Assert.Equal(ORingGlandModel.Parker(3) / 100 / 3, Y(0.01), 12);
        Assert.True(Y(1e-9) < 1e-8);
        Assert.Equal(ORingGlandModel.Parker(3) / 100, Y(0.03), 12);
        Assert.Equal(0.56 / 100 + 0.59 * 0.04 - 0.0046 * 16 / 100, Y(0.04), 12);
        Assert.Equal(ORingGlandModel.Parker(25) / 100, Y(0.25), 12);
        Assert.Equal(Y(0.25), Y(0.30), 12);
        Assert.Equal(Y(0.25), Y(0.64), 12);
        Assert.Equal(0.12435, Y(0.40), 12);

        // Continuous at 3 % and increasing through the range.
        Assert.Equal(Y(0.03), Y(0.0300001), 6);
        Assert.True(Enumerable.Range(1, 60).Select(i => Y(i * 0.005)).Zip(Enumerable.Range(2, 59).Select(i => Y(i * 0.005)), (a, b) => b >= a - 1e-12).All(ok => ok));

        // Sheet: the defaults (stretch 2.1 to 4.4 %) reach below 3 %, so the Parker relation warns; a ring at 4 % throughout does not.
        Assert.Contains(Run(CalcInputValue.Choice("red", ORingGlandCalculator.RelationParker)).Warnings, w => w.RuleId == "stretch" && w.Message.Contains("Parker", StringComparison.Ordinal));
        Assert.DoesNotContain(Run(Plain(Mm("d_r_tol", 0), Mm("D_s", 57), Mm("D_g", 52), Mm("D_g_u", 0), Mm("D_g_l", 0), CalcInputValue.Choice("red", ORingGlandCalculator.RelationParker))).Warnings, w => w.RuleId == "stretch");
        Assert.Contains(Run(Plain(Mm("d_r_tol", 0), Mm("D_s", 70), Mm("D_g", 65), Mm("D_g_u", 0), Mm("D_g_l", 0), CalcInputValue.Choice("red", ORingGlandCalculator.RelationParker))).Warnings, w => w.RuleId == "stretch" && w.Message.Contains("25", StringComparison.Ordinal));
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "stretch");
    }

    [Fact]
    public void A_Rod_Seal_Outside_Diameter_Has_Its_Own_Limit_And_Thickens_The_Section()
    {
        // M4: od_hi is a record limit of its own (it used to borrow x_hi, the 6 % ID-stretch rule).
        Assert.Contains(Gland.Template.RecordKinds!, k => k.Kind == "oring.criteria" && k.Symbols.Contains("od_hi"));
        var rod = Run(RodGland);
        Assert.Equal(2.7144408252, Pct(rod, "od_comp"), 9);

        bool Od(CalcRunResult r) => r.Checks.Single(c => c.Name == "Ring OD compression not above the limit").Passed;
        bool StretchOk(CalcRunResult r) => r.Checks.Single(c => c.Name == "Installed stretch not above the limit").Passed;
        Assert.True(Od(rod));
        Assert.False(Od(Run([.. RodGland, Percent("od_hi", 2.5)])));
        Assert.True(Od(Run([.. RodGland, Percent("od_hi", 3)])));

        // x_hi no longer governs the OD compression, and od_hi no longer governs the stretch.
        Assert.True(Od(Run([.. RodGland, Percent("x_hi", 2)])));
        Assert.False(StretchOk(Run([.. RodGland, Percent("x_hi", 2)])));
        Assert.True(StretchOk(Run([.. RodGland, Percent("od_hi", 2.5)])));

        // Constant-volume thickening: W_eff = f·W·(1 − r)/sqrt(1 − c). In ka-rod-seal the hot end compresses the OD by 5.069 % and the section by 2.6 %.
        Assert.Equal(0.0506929787, Step(rod, "c_h"), 9);
        Assert.Equal(26.8202916965, Pct(rod, "squeeze_max"), 7);
        Assert.Equal(84.5655009226, Pct(rod, "fill_max"), 7);
        Assert.True(Pct(rod, "squeeze_max") > 24.9 + 1.5);

        // A piston seal has no outside-diameter compression to add: the same section reads as before.
        Assert.DoesNotContain(Run().Steps, x => x.Symbol is "c_h" or "c_c");
        Assert.Equal(0.0, ORingGlandModel.OdCompression(PistonGland().Env, GlandCase.Hot, new double[8]));
        Assert.Equal(1.0, ORingGlandModel.Thickening(0.0));
        Assert.Equal(1 / Math.Sqrt(1 - 0.05), ORingGlandModel.Thickening(0.05), 12);
    }

    [Fact]
    public void A_Face_Seal_With_Internal_Pressure_Seats_Its_Outside_Diameter_And_Is_Judged_On_Its_Compression()
    {
        // M5: with the pressure inside, the ring OD seats on the groove outer wall; the ID is free, so no stretch lower limit applies and the OD compression is checked.
        var external = Run(Plain(CalcInputValue.Choice("type", ORingGlandCalculator.TypeFace)));
        var internalFace = Run([.. InternalFace, CalcInputValue.Of("n_mc", 2_000)]);
        Assert.False(internalFace.IsBlocked, string.Join("; ", internalFace.Blocks.Select(b => b.Message)));

        // Hand: ring OD 66.3 to 67.7 mm on a 66.70 to 66.75 mm outer wall.
        Assert.Equal((66.75 / 66.3 - 1) * 100, Pct(internalFace, "stretch"), 9);
        Assert.Equal((66.70 / 67.7 - 1) * 100, Pct(internalFace, "stretch_min"), 9);
        Assert.Equal((67.7 - 66.70) / 67.7 * 100, Pct(internalFace, "od_comp"), 9);
        Assert.Equal((67.0 - 66.725) / 67.0 * 100, Pct(internalFace, "od_comp_nom"), 9);
        Assert.NotNull(internalFace.FindOutput("od_comp"));
        Assert.Contains(internalFace.Checks, c => c.Name.StartsWith("Ring OD compression", StringComparison.Ordinal) && c.Passed);
        Assert.DoesNotContain(internalFace.Checks, c => c.Name.StartsWith("Installed stretch not below", StringComparison.Ordinal));

        // The external-pressure face seal keeps its ID stretch and its lower stretch limit, and has no OD output.
        Assert.Null(external.FindOutput("od_comp"));
        Assert.Contains(external.Checks, c => c.Name.StartsWith("Installed stretch not below", StringComparison.Ordinal));
        Assert.Equal(ORingGlandCalculator.PressureExternal, Gland.Template.FindInput("face_p")!.DefaultChoice);

        // The old external-only model gave this ring a negative "stretch" that failed x_lo = 0; here the compression is judged against od_hi, a limit of its own.
        Assert.True(Pct(internalFace, "stretch_min") < 0);
        Assert.Contains(Run([.. InternalFace, Percent("od_hi", 1)]).Checks, c => c.Name.StartsWith("Ring OD compression", StringComparison.Ordinal) && !c.Passed);

        // The direction is ignored by the radial seals.
        Assert.Equal(Pct(Run(), "squeeze_min"), Pct(Run(CalcInputValue.Choice("face_p", ORingGlandCalculator.PressureInternal)), "squeeze_min"), 12);
        Assert.Null(Run(CalcInputValue.Choice("face_p", ORingGlandCalculator.PressureInternal)).FindOutput("od_comp"));

        // The compressed ring thickens (at the hot end the OD compression is 3.86 %): squeeze_max and fill_max exceed those of the same ring without the correction.
        Assert.Equal(26.9136545944, Pct(internalFace, "squeeze_max"), 7);
        Assert.Equal(82.6242783141, Pct(internalFace, "fill_max"), 7);
        Assert.Equal(0.0386191240, Step(internalFace, "c_h"), 9);
    }

    [Fact]
    public void A_Missing_Extrusion_Limit_Fails_The_Sheet_Until_It_Is_Entered()
    {
        // L2: with 160 bar and no permitted gap the sheet used to pass with a warning.
        var none = CalcRunner.Run(Gland, CalcTemplateHasher.DefaultInputs(Gland.Template).With(new CalcInputValue("gap_allow")));
        Assert.Contains(none.Warnings, w => w.RuleId == "extrusion");
        Assert.Contains(none.Checks, c => c.Name.StartsWith("Extrusion verified", StringComparison.Ordinal) && !c.Passed);
        Assert.False(none.Passed);

        // The same sheet at no pressure has nothing to extrude, and passes; the limit entered passes it at 160 bar.
        Assert.True(CalcRunner.Run(Gland, CalcTemplateHasher.DefaultInputs(Gland.Template).With(new CalcInputValue("gap_allow")).With(CalcInputValue.Quantity("p_op", 0, "bar"))).Passed);
        Assert.True(Run().Passed);

        // A new sheet opens with no permitted gap: it runs, and fails until the limit is entered.
        var fresh = CalcRunner.Run(Gland, CalcTemplateHasher.NewSheetInputs(Gland.Template));
        Assert.False(fresh.IsBlocked);
        Assert.False(fresh.Passed);
    }

    [Fact]
    public void The_Gap_Is_The_Largest_Over_The_Temperature_Range_With_Each_Part_Expanding_By_Its_Own_Metal_And_The_Bore_Dilation()
    {
        // L3: gap = g_s·D_s − g_m·D_c over the installed, cold and hot temperatures (+ dilation under pressure).
        var same = Run();
        Assert.Equal(0.10607632e-3, same.OutputSi("gap_max"), 12);
        Assert.Equal(0.106e-3 * (1 + 1.2e-5 * 60), same.OutputSi("gap_max"), 12);
        Assert.Equal(0.0299856e-3, same.OutputSi("gap_min"), 12);

        // An aluminium bore round a steel piston opens up when hot: (1 + 2.3e-5·60)·56.946 − (1 + 1.2e-5·60)·56.84 mm.
        var al = Run(CalcInputValue.Quantity("alpha_s", 2.3e-5, "1/K"));
        Assert.Equal(1.00138 * 56.946 - 1.00072 * 56.84, al.OutputSi("gap_max") * 1e3, 9);
        Assert.True(al.OutputSi("gap_max") > same.OutputSi("gap_max"));

        // Bore dilation under pressure adds to the gap and to nothing else.
        var dilated = Run(CalcInputValue.Quantity("alpha_s", 2.3e-5, "1/K"), Mm("dD_p", 0.02));
        Assert.Equal(al.OutputSi("gap_max") + 0.02e-3, dilated.OutputSi("gap_max"), 12);
        Assert.Equal(al.OutputSi("gap_min"), dilated.OutputSi("gap_min"), 12);
        Assert.Equal(Pct(al, "squeeze_min"), Pct(dilated, "squeeze_min"), 12);
        Assert.Equal(0.16366068e-3, Run(CalcInputValue.Quantity("alpha_s", 2.3e-5, "1/K"), Mm("dD_p", 0.02)).OutputSi("gap_max"), 11);

        // Against a permitted gap the dilation can fail the extrusion check.
        Assert.Contains(Run(Mm("gap_allow", 0.12), Mm("dD_p", 0.02)).Checks, c => c.Name.StartsWith("Extrusion gap", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(Run(Mm("gap_allow", 0.12)).Checks, c => c.Name.StartsWith("Extrusion gap", StringComparison.Ordinal) && c.Passed);

        // A rod seal: the rod (sealing surface) expands with alpha_s, the housing with alpha_m.
        var rod = Run([.. RodGland, CalcInputValue.Quantity("alpha_s", 2.3e-5, "1/K")]);
        Assert.True(rod.OutputSi("gap_max") > Run(RodGland).OutputSi("gap_max"));
        Assert.Equal(0.99952 * 40.039 - 0.99908 * 39.95, rod.OutputSi("gap_max") * 1e3, 9);

        // A face seal's axial gap is as dimensioned.
        Assert.Equal(0.1e-3, Run(Plain(CalcInputValue.Choice("type", ORingGlandCalculator.TypeFace), Mm("dD_p", 0.3))).OutputSi("gap_max"), 12);

        // The model agrees: the exact Gap criterion is the same maximum.
        var (env, vars) = PistonGland();
        var withMetals = env with { AlphaSurface = 2.3e-5, BoreDilation = 0.02 };
        var x = new double[8];

        foreach (var v in vars)
            x[(int)v.Dim] = v.Mid;

        Assert.Equal(Math.Max(Math.Max(ORingGlandModel.GapAt(withMetals, GlandCase.Installed, x), ORingGlandModel.GapAt(withMetals, GlandCase.Cold, x)), ORingGlandModel.GapAt(withMetals, GlandCase.Hot, x)) + 0.02, ORingGlandModel.Evaluate(withMetals, GlandCriterion.Gap, x), 12);
    }

    [Fact]
    public void The_Stretch_Is_Also_Checked_At_The_Cold_End()
    {
        // L4: the cold ring is shorter against the same groove, so the stretch is larger there (4.41 % against 3.83 % installed on the defaults).
        var r = Run();
        Assert.Equal(4.4072499513, Pct(r, "X_c"), 8);
        Assert.True(Pct(r, "X_c") > Pct(r, "stretch"));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Installed stretch not above the limit at the cold end", StringComparison.Ordinal) && c.Passed);

        // A limit between the two: the installed stretch passes, the cold-end stretch fails.
        var between = Run(Percent("x_hi", 4.2));
        Assert.Contains(between.Checks, c => c.Name == "Installed stretch not above the limit" && c.Passed);
        Assert.Contains(between.Checks, c => c.Name.StartsWith("Installed stretch not above the limit at the cold end", StringComparison.Ordinal) && !c.Passed);
        Assert.False(between.Passed);
    }

    [Fact]
    public void A_Measured_Row_That_Is_Not_A_Dimension_Blocks_And_A_Face_Gap_Row_Replaces_The_Gap()
    {
        // L6: an unknown key used to fall through to the face gap and silently replace e_f.
        CalcInputValue Measured(string dim, double mean, double sigma) =>
            CalcInputValue.Table("measured", [CalcRow.Of(("dim", new CalcCell(Text: dim)), ("mean", new CalcCell(mean, "mm")), ("sigma", new CalcCell(sigma, "mm")), ("dist", new CalcCell(Text: ORingGlandCalculator.DistributionNormal)))]);

        var face = CalcInputValue.Choice("type", ORingGlandCalculator.TypeFace);
        var unknown = CalcRunner.Run(Gland, With(face, Measured("zz", 0.5, 0.01)));
        Assert.True(unknown.IsBlocked);
        Assert.True(CalcRunner.Run(Gland, With(face, Measured("", 0.5, 0.01))).IsBlocked);

        // e_f is a dimension of a face seal: the measured mean 0.04 mm, no spread, is its gap.
        var gap = Run(face, Measured("e_f", 0.04, 0));
        Assert.False(gap.IsBlocked, string.Join("; ", gap.Blocks.Select(b => b.Message)));
        Assert.Equal(0.04e-3, gap.OutputSi("gap_max"), 12);

        // On a radial seal e_f is not used: the row is ignored with a warning, not applied.
        var radial = Run(Measured("e_f", 0.5, 0));
        Assert.Contains(radial.Warnings, w => w.RuleId == "measured");
        Assert.Equal(Run().OutputSi("gap_max"), radial.OutputSi("gap_max"), 12);
    }

    [Fact]
    public void A_Held_Ring_Takes_The_Larger_Of_The_Held_Circumference_Factor_And_The_Compression_Factor_In_The_Hot_Fill()
    {
        // M4 residual: the OD-held fill is max(f/g, 1/(1 − c)). A ring compressed little at the hot end (c_h = 1.87 %, 1/(1 − c) = 1.0191) still takes f/g = 1.0248.
        var loose = Run([.. RodGland, Mm("d_r", 38.0)]);
        Assert.Equal(0.018729779285, Step(loose, "c_h"), 9);
        Assert.Equal(82.2700204416, Pct(loose, "fill_max"), 7);

        // ... isotropic (f W)² × f/g over the groove area, with f/g = 1.025544/1.00072 and no compression term.
        var (env, _) = PistonGland();
        var (f, g) = ORingGlandModel.Factors(env with { Kind = SealKind.Rod }, GlandCase.Hot);
        Assert.Equal(1.0248060637, f / g, 9);

        // A strongly compressed ring (ka-rod-seal, c_h = 5.07 %) takes 1/(1 − c) = 1.0534 instead; unchanged from 1.1.0.
        Assert.Equal(84.5655009226, Pct(Run(RodGland), "fill_max"), 7);

        // The model agrees at the mean dimensions.
        var rodEnv = new GlandEnvironment(SealKind.Rod, ReductionRelation.Incompressible, 0.49, 1.5e-4, 1.2e-5, 20, -20, 80, 0, 0.05, 0.2);
        var x = new double[8];
        x[(int)GlandDim.Section] = 3.0; x[(int)GlandDim.RingId] = 38.0; x[(int)GlandDim.Surface] = 39.9625; x[(int)GlandDim.Groove] = 44.831; x[(int)GlandDim.Clearance] = 40.0195; x[(int)GlandDim.Width] = 4.15;
        var c = ORingGlandModel.OdCompression(rodEnv, GlandCase.Hot, x);
        var (fr, gr) = ORingGlandModel.Factors(rodEnv, GlandCase.Hot);
        var expected = Math.PI / 4 * Math.Pow(fr * 3.0, 2) * Math.Max(fr / gr, 1 / (1 - c));
        Assert.Equal(expected, ORingGlandModel.HotRingArea(rodEnv, x), 9);
        Assert.True(fr / gr > 1 / (1 - c));

        // A rod seal is held on the rod: the f/g term follows the rod's metal (alpha_s), not the groove's. Python3: the loose rod with an aluminium rod reads 82.6683 %.
        var alRod = rodEnv with { AlphaSurface = 2.3e-5 };
        Assert.Equal(gr / ((1 + 2.3e-5 * 60)), ORingGlandModel.HotRingArea(alRod, x) / ORingGlandModel.HotRingArea(rodEnv, x), 9);
        Assert.Equal(82.6682943055, Pct(Run([.. RodGland, Mm("d_r", 38.0), CalcInputValue.Quantity("alpha_s", 2.3e-5, "1/K")]), "fill_max"), 7);

        // A piston seal is held on the groove: alpha_s does not enter its f/g term.
        var (pistonEnv, pistonVars) = PistonGland(ReductionRelation.Incompressible);
        var mid = new double[8];

        foreach (var v in pistonVars)
            mid[(int)v.Dim] = v.Mid;

        Assert.Equal(ORingGlandModel.HotRingArea(pistonEnv, mid), ORingGlandModel.HotRingArea(pistonEnv with { AlphaSurface = 2.3e-5 }, mid), 12);
    }

    [Fact]
    public void The_Ring_OD_Compression_Is_Also_Checked_At_The_Hot_End()
    {
        // L-d: ka-rod-seal is compressed 2.71 % as installed and 5.07 % at the hot end (the ring grows more than the housing).
        var rod = Run(RodGland);
        Assert.Contains(rod.Checks, c => c.Name == "Ring OD compression at the hot end not above the limit" && c.Passed);

        // An od_hi between the two: the installed check passes and the hot-end check fails.
        var between = Run([.. RodGland, Percent("od_hi", 4)]);
        Assert.Contains(between.Checks, c => c.Name == "Ring OD compression not above the limit" && c.Passed);
        Assert.Contains(between.Checks, c => c.Name == "Ring OD compression at the hot end not above the limit" && !c.Passed);
        Assert.False(between.Passed);

        // The internal-pressure face seal has it too, and the radial piston seal neither.
        Assert.Contains(Run([.. InternalFace, Percent("od_hi", 3)]).Checks, c => c.Name == "Ring OD compression at the hot end not above the limit" && !c.Passed);
        Assert.DoesNotContain(Run().Checks, c => c.Name.StartsWith("Ring OD compression", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Depth_And_The_Stretch_Follow_Each_Part_Metal_When_The_Sealing_Surface_Part_Differs_From_The_Groove_Metal()
    {
        // L-f: the bore (piston seal) or rod (rod seal) expands with alpha_s, the groove with alpha_m; with alpha_s = alpha_m nothing moves.
        var same = Run(CalcInputValue.Quantity("alpha_s", 1.2e-5, "1/K"));
        Assert.Equal(Pct(Run(), "squeeze_min"), Pct(same, "squeeze_min"), 12);
        Assert.Equal(Pct(Run(), "squeeze_max"), Pct(same, "squeeze_max"), 12);
        Assert.Equal(Pct(Run(), "fill_max"), Pct(same, "fill_max"), 12);

        // Aluminium bore round a steel piston: h = (g_s·D_s − g_m·D_g)/2 at each end (python3 recompute of the worst case).
        var al = Run(CalcInputValue.Quantity("alpha_s", 2.3e-5, "1/K"));
        Assert.Equal(17.3347820634, Pct(al, "squeeze_min"), 7);
        Assert.Equal(26.4498064079, Pct(al, "squeeze_max"), 7);
        Assert.Equal(64.2445202775, Pct(al, "fill_min"), 7);
        Assert.Equal(84.0579883229, Pct(al, "fill_max"), 7);
        Assert.Equal(Pct(Run(), "stretch"), Pct(al, "stretch"), 12);
        Assert.Equal(Pct(Run(), "X_c"), Pct(al, "X_c"), 12);

        // A rod seal: the rod is the reference diameter, so the stretch at the ends follows the rod's metal (gs), the depth too, and the gap.
        var rod = Run([.. RodGland, CalcInputValue.Quantity("alpha_s", 2.3e-5, "1/K")]);
        Assert.Equal(2.6291156903, Pct(rod, "X_c"), 7);
        Assert.Equal(13.4189415951, Pct(rod, "squeeze_min"), 7);
        Assert.Equal(27.1961603931, Pct(rod, "squeeze_max"), 7);
        Assert.Equal(61.5880054912, Pct(rod, "fill_min"), 7);
        Assert.Equal(85.0309304132, Pct(rod, "fill_max"), 7);
        Assert.Equal(0.10653528e-3, rod.OutputSi("gap_max"), 12);
        Assert.Equal(Pct(Run(RodGland), "stretch"), Pct(rod, "stretch"), 12);

        // The model's depth is the same expression, and the stack never reads below the exact corner (the worst-case check stays silent).
        var (env, vars) = PistonGland(ReductionRelation.Incompressible);
        var x = new double[8];

        foreach (var v in vars)
            x[(int)v.Dim] = v.Mid;

        var metals = env with { AlphaSurface = 2.3e-5 };
        Assert.Equal((1 + 2.3e-5 * 60) * x[(int)GlandDim.Surface] / 2 - (1 + 1.2e-5 * 60) * x[(int)GlandDim.Groove] / 2, ORingGlandModel.DepthAt(metals, GlandCase.Hot, x), 9);
        Assert.DoesNotContain(al.Warnings, w => w.RuleId == "worst-case");
        Assert.DoesNotContain(rod.Warnings, w => w.RuleId == "worst-case");
    }

    [Fact]
    public void The_Template_Version_Is_Bumped_For_The_Changed_Model()
    {
        Assert.Equal("1.2.0", Gland.Template.Version);
    }

    [Fact]
    public void The_Pareto_Names_The_Largest_Contributor_To_The_Squeeze_Spread()
    {
        var r = Run(Plain());
        var series = Assert.Single(r.Series, s => s.Name.StartsWith("Contribution to the minimum squeeze", StringComparison.Ordinal));
        Assert.Equal(100.0, series.Points.Sum(p => p.Y), 9);
        Assert.NotNull(r.Steps.SingleOrDefault(s => s.Symbol == "pareto"));
    }

    [Fact]
    public void The_Stack_Engine_Is_What_Produces_The_Ranges()
    {
        // Depth is half the diameter difference: the engine's worst case of the two half-diameters (0.023 and 0.025 mm half-bands) is 0.048/2 either side.
        var surface = new GlandVar(GlandDim.Surface, "D_s", 56.9, 56.923, 0.023, 0.0077, ContributorDistribution.Normal);
        var groove = new GlandVar(GlandDim.Groove, "D_g", 51.5, 51.475, 0.025, 0.0083, ContributorDistribution.Normal);
        var stack = ToleranceStackEngine.Analyse([surface.Scaled(0.5), groove.Scaled(-0.5)]);
        Assert.Equal(2.724, stack.Mean, 9);
        Assert.Equal(0.024, stack.WorstCase, 9);
        Assert.Equal(2.700, stack.WorstCaseMinimum, 9);
        Assert.Equal(2.748, stack.WorstCaseMaximum, 9);
    }
}
