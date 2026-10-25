using CsCheck;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class LtbInteractionTests
{
    private static readonly LtbInteractionCalculator Ltb = new();

    /// <summary>The first known-answer case: the 500×200×16/10 welded I, class 2, N = 1200 kN, M_y = 200 kN·m, M_z = 15 kN·m.</summary>
    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Ltb.Template);

    private static CalcRunResult Run(params CalcInputValue[] changes) => CalcRunner.Run(Ltb, changes.Aggregate(Defaults(), (set, v) => set.With(v)));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue Choice(string symbol, string value) => CalcInputValue.Choice(symbol, value);

    // Plate section properties of the 500×200×16/10 welded I, in mm, N and MPa.
    private const double Iz = 21372333.333333333;
    private const double It = 702133.3333333334;
    private const double Iw = 1251649329333.3333;

    [Fact]
    public void Uniform_Moment_Gives_The_Classical_Critical_Moment()
    {
        // M_cr = (π/L)·√(E·I_z·G·I_t)·√(1 + π²E·I_w/(L²·G·I_t)), the textbook form of the general expression with C₁ = 1.
        const double e = 210000, g = 81000, l = 6000;
        var classic = Math.PI / l * Math.Sqrt(e * Iz * g * It) * Math.Sqrt(1 + Math.PI * Math.PI * e * Iw / (l * l * g * It));
        Assert.Equal(classic, LtbInteractionMath.Mcr(e, g, Iz, It, Iw, l, 1, 1, 1.0, 0, 0), classic * 1e-12);
        var r = Run(N("psi_y", 1));
        Assert.Equal(classic / 1e3, r.OutputSi("Mcr"), classic / 1e3 * 1e-9);
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.0, 1.88)]
    [InlineData(-0.5, 2.70)]
    [InlineData(-1.0, 2.70)]
    [InlineData(0.5, 1.31)]
    public void C1_From_Psi_Follows_The_Curve_Fit_And_Is_Capped(double psi, double c1) => Assert.Equal(c1, LtbInteractionMath.C1FromPsi(psi), 12);

    [Fact]
    public void Moment_Diagram_Factors_Follow_Psi()
    {
        Assert.Equal(1.0, LtbInteractionMath.CmFromPsi(1), 12);
        Assert.Equal(0.6, LtbInteractionMath.CmFromPsi(0), 12);
        Assert.Equal(0.4, LtbInteractionMath.CmFromPsi(-1), 12);
        Assert.Equal(0.4, LtbInteractionMath.CmFromPsi(-0.8), 12);
        Assert.Equal(1.0, LtbInteractionMath.KcFromPsi(1), 12);
        Assert.Equal(1 / 1.33, LtbInteractionMath.KcFromPsi(0), 12);
    }

    [Fact]
    public void A_Load_Above_The_Shear_Centre_Lowers_Mcr_And_One_Below_Raises_It()
    {
        var neutral = Run(N("C_1", 1.35), N("C_2", 0.45), Q("z_g", 0, "mm")).OutputSi("Mcr");
        var top = Run(N("C_1", 1.35), N("C_2", 0.45), Q("z_g", 250, "mm")).OutputSi("Mcr");
        var bottom = Run(N("C_1", 1.35), N("C_2", 0.45), Q("z_g", -250, "mm")).OutputSi("Mcr");
        Assert.True(top < neutral && neutral < bottom, $"{top} < {neutral} < {bottom}");
    }

    [Theory]
    [InlineData(true, false, 0.0, "a")]
    [InlineData(true, false, 2.1, "b")]
    [InlineData(true, true, 1.5, "c")]
    [InlineData(true, true, 2.5, "d")]
    [InlineData(false, false, 1.5, "b")]
    [InlineData(false, false, 2.5, "c")]
    [InlineData(false, true, 1.5, "c")]
    [InlineData(false, true, 2.5, "d")]
    public void The_Automatic_Curve_Follows_Tables_6_4_And_6_5(bool general, bool welded, double hOverB, string curve) => Assert.Equal(curve, LtbInteractionCalculator.AutoCurve(general, welded, hOverB));

    [Fact]
    public void The_General_Case_Is_More_Conservative_Than_The_Rolled_Case_For_The_Same_Curve()
    {
        var general = Run(Choice("ltb_method", LtbInteractionCalculator.General), Choice("ltb_curve", "c")).OutputSi("Mb_Rd");
        var rolled = Run(Choice("ltb_method", LtbInteractionCalculator.Rolled), Choice("ltb_curve", "c")).OutputSi("Mb_Rd");
        Assert.True(general < rolled);
    }

    [Fact]
    public void The_F_Modification_Never_Lowers_The_Reduction_Factor_And_Never_Exceeds_One()
    {
        foreach (var psi in new[] { 1.0, 0.5, 0.0, -0.5, -1.0 })
        {
            var without = Run(N("psi_y", psi)).OutputSi("chi_LT");
            var with = Run(N("psi_y", psi), CalcInputValue.Switch("use_f", true)).OutputSi("chi_LT");
            Assert.InRange(with, without - 1e-12, 1.0);
        }
    }

    [Fact]
    public void End_Moment_Mode_Takes_Psi_And_The_Greater_Moment_From_The_Ends()
    {
        var single = Run(Choice("moment_mode", LtbInteractionCalculator.ModeEnds), Q("M_y_A", 200, "kN*m"), Q("M_y_B", 100, "kN*m"));
        Assert.Equal(0.5, single.OutputSi("psi_y"), 12);
        Assert.Equal(200e3, single.OutputSi("M_y_Ed"), 6);
        var reverse = Run(Choice("moment_mode", LtbInteractionCalculator.ModeEnds), Q("M_y_A", -100, "kN*m"), Q("M_y_B", 200, "kN*m"));
        Assert.Equal(-0.5, reverse.OutputSi("psi_y"), 12);
        Assert.Equal(200e3, reverse.OutputSi("M_y_Ed"), 6);
        Assert.Equal(LtbInteractionMath.C1FromPsi(-0.5), reverse.OutputSi("C_1"), 12);
        var missing = Run(Choice("moment_mode", LtbInteractionCalculator.ModeEnds));
        Assert.Contains(missing.Blocks, b => b.RuleId == "end-moments" && b.Message.StartsWith("M_y_A:", StringComparison.Ordinal));
    }

    [Fact]
    public void Edition_2022_Changes_Only_The_Label()
    {
        var a = Run();
        var b = Run(Choice("edition", LtbInteractionCalculator.Edition2022));
        Assert.Contains(b.Warnings, w => w.RuleId == "edition");

        foreach (var symbol in new[] { "Mcr", "chi_LT", "Mb_Rd", "chi_y", "chi_z", "k_yy", "k_zz", "u_61", "u_62", "u_member" })
            Assert.Equal(a.OutputSi(symbol), b.OutputSi(symbol), 12);

        Assert.NotEqual(((TextValue)a.FindOutput("edition_used")!.Value).Value, ((TextValue)b.FindOutput("edition_used")!.Value).Value);
    }

    [Fact]
    public void Inputs_Outside_Their_Valid_Range_Are_A_Hard_Fail()
    {
        foreach (var change in new[]
        {
            Q("f_y", 200, "MPa"), Q("f_y", 500, "MPa"), Q("E", 180, "GPa"), N("k", 0.4), N("k", 1.1), N("k_w", 0.4), Q("L", 0, "m"), Q("L", 150, "m"), Q("N_Ed", 100, "kN"),
            N("psi_y", 1.5), N("psi_z", -1.2), Q("A", 1, "cm^2"), Q("I_w", 0, "dm^6"), N("C_2", -0.1), N("C_1", 0.9), N("alpha_d", 0), N("beta_LT", 1.5), N("gamma_M1", 0.9),
        })
        {
            var notices = CalcInputValidator.Validate(Ltb.Template, Defaults().With(change));
            Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.RuleId.Contains(change.Symbol, StringComparison.Ordinal));
        }

        Assert.All(Ltb.Template.Inputs.Where(i => i.Kind is CalcInputKind.Quantity or CalcInputKind.Number), i => Assert.True(i.HardMin == i.Min && i.HardMax == i.Max, $"{i.Symbol}: the soft and hard ranges must be the same"));
    }

    [Fact]
    public void Class_4_And_Plastic_Moduli_Below_Elastic_Block_The_Sheet()
    {
        Assert.Contains(Run(Choice("section_class", "4")).Blocks, b => b.RuleId == "class-4");
        Assert.Contains(Run(Q("W_pl_y", 1500, "cm^3")).Blocks, b => b.RuleId == "section" && b.Message.StartsWith("W_pl_y:", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_Compression_The_Axial_Terms_Vanish_And_The_Sheet_Still_Checks_Bending()
    {
        var r = Run(Q("N_Ed", 0, "kN"));
        Assert.Equal(0, r.OutputSi("u_buckling"), 12);
        Assert.Equal(0, r.OutputSi("u_N_y"), 12);
        Assert.True(r.OutputSi("u_LTB") > 0);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Bending interaction", StringComparison.Ordinal));
    }

    [Fact]
    public void Axial_Force_Alone_Reduces_To_The_Flexural_Buckling_Check_In_Both_Annexes()
    {
        foreach (var annex in new[] { LtbInteractionCalculator.AnnexBMethod, LtbInteractionCalculator.AnnexAMethod })
        {
            var r = Run(Choice("annex", annex), Q("M_y_Ed", 0, "kN*m"), Q("M_z_Ed", 0, "kN*m"));
            Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
            Assert.Equal(r.OutputSi("u_N_y"), r.OutputSi("u_61"), 12);
            Assert.Equal(r.OutputSi("u_N_z"), r.OutputSi("u_62"), 12);
            Assert.Equal(Math.Max(r.OutputSi("u_N_y"), r.OutputSi("u_N_z")), r.OutputSi("u_interaction"), 12);
        }
    }

    [Fact]
    public void Annex_B_Factors_Match_A_Hand_Calculation()
    {
        // Plastic, Table B.2: n_y = 0.2, n_z = 0.3, λ̄_y = 0.5, λ̄_z = 0.8, C_my = C_mz = C_mLT = 0.9.
        var f = LtbInteractionMath.AnnexB(0.2, 0.3, 0.5, 0.8, 0.9, 0.9, 0.9, plastic: true, torsionSusceptible: true);
        Assert.Equal(0.9 * (1 + 0.3 * 0.2), f.Kyy, 12);
        Assert.Equal(0.9 * (1 + 1.0 * 0.3), f.Kzz, 12);
        Assert.Equal(0.6 * 0.9 * (1 + 1.0 * 0.3), f.Kyz, 12);
        Assert.Equal(1 - 0.1 * 0.8 / 0.65 * 0.3, f.Kzy, 12);

        // Elastic, Table B.1: k_yz = k_zz, k_zy = 0.8·k_yy.
        var e = LtbInteractionMath.AnnexB(0.2, 0.3, 0.5, 0.8, 0.9, 0.9, 0.9, plastic: false, torsionSusceptible: false);
        Assert.Equal(0.9 * (1 + 0.6 * 0.5 * 0.2), e.Kyy, 12);
        Assert.Equal(0.9 * (1 + 0.6 * 0.8 * 0.3), e.Kzz, 12);
        Assert.Equal(e.Kzz, e.Kyz, 12);
        Assert.Equal(0.8 * e.Kyy, e.Kzy, 12);

        // The upper bounds hold for a stocky member.
        var s = LtbInteractionMath.AnnexB(0.4, 0.4, 1.5, 1.5, 1.0, 1.0, 1.0, plastic: true, torsionSusceptible: false);
        Assert.Equal(1 * (1 + 0.8 * 0.4), s.Kyy, 12);
        Assert.Equal(1 * (1 + 1.4 * 0.4), s.Kzz, 12);
    }

    [Fact]
    public void Annex_A_Refuses_A_Compression_At_Or_Above_An_Elastic_Buckling_Force()
    {
        var r = Run(Choice("annex", LtbInteractionCalculator.AnnexAMethod), Q("N_Ed", -5000, "kN"));
        Assert.Contains(r.Blocks, b => b.RuleId == "n-cr" && b.Message.StartsWith("N_Ed:", StringComparison.Ordinal));
        Assert.Contains(Run(Choice("annex", LtbInteractionCalculator.AnnexAMethod)).Warnings, w => w.RuleId == "annex-a");
    }

    [Fact]
    public void Ports_Are_Outputs_And_The_Material_And_Factor_Inputs_Are_Declared_Records()
    {
        var r = Run();
        Assert.All(Ltb.Template.OutputPorts!, p => Assert.NotNull(r.FindOutput(p.Symbol)));
        Assert.Contains(Ltb.Template.OutputPorts!, p => p is { Symbol: "Mb_Rd", Unit: "kN*m" });
        Assert.Contains(Ltb.Template.OutputPorts!, p => p.Symbol == "u_interaction");

        foreach (var (symbol, property) in new[] { ("E", "E"), ("G", "G"), ("f_y", "f_y") })
            Assert.Equal(property, Ltb.Template.FindInput(symbol)!.MaterialProperty);

        var material = Ltb.Template.RecordKinds!.Single(k => k.Kind == "material.properties");
        Assert.Equal(["E", "G", "f_y"], material.Symbols);
        var factors = Ltb.Template.RecordKinds!.Single(k => k.Kind == "ec3.ltb-factors");
        Assert.Contains("beta_LT", factors.Symbols);
        Assert.Contains("lambda_LT0", factors.Symbols);
        Assert.Contains(Ltb.Template.RecordKinds!, k => k.Kind == "ec3.mcr-factors" && k.Symbols.Contains("C_1"));
        Assert.Equal(Ltb.Template.RecordKinds!.SelectMany(k => k.Symbols).Count(), Ltb.Template.RecordKinds!.SelectMany(k => k.Symbols).Distinct().Count());
    }

    [Fact]
    public void The_Governing_Check_Is_The_Greatest_Utilisation()
    {
        var r = Run();
        var parts = new[] { r.OutputSi("u_LTB"), r.OutputSi("u_buckling"), r.OutputSi("u_61"), r.OutputSi("u_62") };
        Assert.Equal(parts.Max(), r.OutputSi("u_member"), 12);
        Assert.Equal(parts.Max(), r.Governing!.Utilisation, 9);
    }

    [Fact]
    public void A_Longer_Unrestrained_Length_Never_Raises_The_Buckling_Resistance()
    {
        Gen.Double[1, 40].Select(Gen.Double[1.05, 2]).Sample((l, grow) =>
        {
            var shorter = Run(Q("L", l, "m"), Choice("ltb_method", LtbInteractionCalculator.General), N("psi_y", 1));
            var longer = Run(Q("L", Math.Min(100, l * grow), "m"), Choice("ltb_method", LtbInteractionCalculator.General), N("psi_y", 1));
            var chi = shorter.OutputSi("chi_LT");
            return chi is > 0 and <= 1 && longer.OutputSi("Mb_Rd") <= shorter.OutputSi("Mb_Rd") * (1 + 1e-9) && shorter.OutputSi("Mb_Rd") <= 2096.36e-6 * 355e6 + 1e-6;
        }, iter: 80);
    }
}
