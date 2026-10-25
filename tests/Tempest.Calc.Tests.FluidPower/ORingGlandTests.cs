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
        Assert.Equal(82.5940850678, Pct(r, "fill_max"), 7);
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

        // The Parker quadratic is held at its vertex, reads zero without stretch and is cited, not confirmed.
        Assert.Equal(ORingGlandModel.Parker(64) / 100, ORingGlandModel.Reduction(ReductionRelation.Parker, 3.0, 0.49), 12);
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

    [Fact]
    public void The_Linearised_Monte_Carlo_Agrees_With_A_Full_Non_Linear_Monte_Carlo()
    {
        var env = new GlandEnvironment(SealKind.Piston, ReductionRelation.Incompressible, 0.49, 1.5e-4, 1.2e-5, 20, -20, 80, 0, 0.05, 0.2);
        GlandVar Var(GlandDim dim, double nominal, double upper, double lower) =>
            new(dim, dim.ToString(), nominal, nominal + (upper + lower) / 2, (upper - lower) / 2, (upper - lower) / 6, ContributorDistribution.Normal);

        GlandVar[] vars =
        [
            Var(GlandDim.Section, 3.5, 0.1, -0.1), Var(GlandDim.RingId, 50, 0.4, -0.4), Var(GlandDim.Surface, 56.9, 0.046, 0), Var(GlandDim.Groove, 51.5, 0, -0.05),
            Var(GlandDim.Clearance, 56.9, -0.03, -0.06), Var(GlandDim.Width, 4.8, 0.1, 0),
        ];

        foreach (var (criterion, lower, upper) in new (GlandCriterion, double?, double?)[]
        {
            (GlandCriterion.SqueezeMin, 0.195, null), (GlandCriterion.Stretch, null, 0.032), (GlandCriterion.FillMax, null, 0.785), (GlandCriterion.Gap, null, 0.075),
        })
        {
            var analysis = ORingGlandModel.Analyse(env, criterion, vars, lower, upper, 100_000, 3);
            var linear = (analysis.Simulation!.FractionBelowLower ?? 0) + (analysis.Simulation.FractionAboveUpper ?? 0);

            var nominal = vars.ToDictionary(v => v.Dim.ToString(), v => v.Mid);
            var distributions = vars.ToDictionary(v => v.Dim.ToString(), v => Distribution.Normal(v.Mid, v.Sigma));
            var full = MonteCarlo.Run(m =>
            {
                var x = new double[8];

                foreach (var v in vars)
                    x[(int)v.Dim] = m[v.Dim.ToString()];

                return ORingGlandModel.Evaluate(env, criterion, x);
            }, nominal, distributions, 100_000, 99, lower, upper);
            var nonLinear = (full.FractionBelowLower ?? 0) + (full.FractionAboveUpper ?? 0);

            Assert.True(linear is > 0.02 and < 0.9, $"{criterion}: linearised fraction {linear:P3} is not in the testable range");
            Assert.True(Math.Abs(linear - nonLinear) < 0.01, $"{criterion}: linearised {linear:P2} against non-linear {nonLinear:P2}");
        }
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
        Assert.Null(Gland.Template.FindInput("alpha_m")!.MaterialProperty);
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
