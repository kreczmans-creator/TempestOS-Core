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
    private const double Iw = 1249365333333.3333;

    [Theory]
    [InlineData(-0.5, 12.0, 2.70)]
    [InlineData(0.0, 20.0, 1.88)]
    [InlineData(-1.0, 6.0, 2.70)]
    public void A_Derived_C1_For_Long_Or_Double_Curvature_Members_Warns_That_The_Curve_Fit_Can_Exceed_The_Exact_Value(double psi, double lengthM, double expectedC1)
    {
        // P1-struct.ltb-interaction-01. The ENV Annex F fit gives C1 = min(2.70, 1.88 - 1.40 psi + 0.52 psi^2) (python3: psi -0.5 -> 2.71 -> 2.70; psi 0 -> 1.88; psi -1 -> 3.80 -> 2.70),
        // against exact elastic values of 2.459 (psi -0.5, L 12 m, UB 457x191x98) and 1.789 (psi 0, L 20 m): the value is unchanged, the sheet now says so.
        var r = Run(N("psi_y", psi), Q("L", lengthM, "m"));
        Assert.Equal(expectedC1, r.OutputSi("C_1"), 9);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "c1-psi" && w.Message.StartsWith("C_1:", StringComparison.Ordinal));
        Assert.Contains("exceed the exact elastic value by up to about 10 %", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_B1_Chosen_For_An_Open_I_Section_That_Buckles_Laterally_Is_Checked_On_Table_B2_Unless_A_Basis_Is_Recorded()
    {
        // struct.ltb-interaction-R2: the Annex B Table B.1 choice ("not susceptible to torsional deformation") with chi_LT below 1 gave k_zy = 0.6 k_yy = 0.509 against 0.918 from Table B.2
        // (u_62 1.008 against 1.248, -19 %) and a warning only: the sheet passed what Table B.2 fails. An open I-section is susceptible unless restrained against twist, so a choice with no
        // recorded basis is checked on Table B.2 (the same pattern as the member sheet's k left at 1.0); one the person recorded a basis for is theirs and keeps the warning.
        var b2 = Run(Choice("torsion", LtbInteractionCalculator.Susceptible));
        var b1 = Run(Choice("torsion", LtbInteractionCalculator.NotSusceptible));
        Assert.True(b1.OutputSi("chi_LT") < 1);
        Assert.Equal(b2.OutputSi("u_62"), b1.OutputSi("u_62"), 12);
        Assert.Equal(b2.OutputSi("u_member"), b1.OutputSi("u_member"), 12);
        var warning = Assert.Single(b1.Warnings, w => w.RuleId == "table-b1");
        Assert.Contains("the check uses Table B.2", warning.Message, StringComparison.Ordinal);

        var basis = CalcInputValue.Choice("torsion", LtbInteractionCalculator.NotSusceptible, CalcProvenance.Assumed("The member is restrained against twist by the purlin cleats at both ends."));
        var kept = Run(basis);
        Assert.True(kept.OutputSi("u_62") < b2.OutputSi("u_62"));
        Assert.Equal(1.008, kept.OutputSi("u_62"), 2);
        Assert.Contains("recorded basis", Assert.Single(kept.Warnings, w => w.RuleId == "table-b1").Message, StringComparison.Ordinal);

        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "table-b1");
        // No lateral-torsional buckling (chi_LT = 1: a very short member): Table B.1 is not contradicted.
        Assert.DoesNotContain(Run(Choice("torsion", LtbInteractionCalculator.NotSusceptible), Q("L", 0.5, "m")).Warnings, w => w.RuleId == "table-b1");
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(0.25)]
    public void A_Derived_C1_For_A_Single_Curvature_Diagram_Does_Not_Carry_The_Curve_Fit_Warning(double psi)
    {
        Assert.DoesNotContain(Run(N("psi_y", psi)).Warnings, w => w.RuleId == "c1-psi");
    }

    [Fact]
    public void An_Entered_C1_Does_Not_Carry_The_Curve_Fit_Warning()
    {
        var r = Run(N("psi_y", -0.5), N("C_1", 2.459), N("C_m_y", 0.4), N("k_c", 0.75));
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "c1-psi" && w.Message.StartsWith("C_1:", StringComparison.Ordinal));
    }

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
        var neutral = Run(N("C_1", 1.35), N("C_m_y", 1.0), N("C_2", 0.45), Q("z_g", 0, "mm")).OutputSi("Mcr");
        var top = Run(N("C_1", 1.35), N("C_m_y", 1.0), N("C_2", 0.45), Q("z_g", 250, "mm")).OutputSi("Mcr");
        var bottom = Run(N("C_1", 1.35), N("C_m_y", 1.0), N("C_2", 0.45), Q("z_g", -250, "mm")).OutputSi("Mcr");
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
    public void Black_B_B_01_Opposite_Sign_End_Moments_Warn_That_Double_Curvature_Was_Taken()
    {
        // A frame program prints member end actions, which have opposite signs for SINGLE curvature. Typed that way the
        // diagram reads as a reversal: C1 jumps to its cap and the check can turn from FAIL to PASS with no sign of it.
        var frameProgram = Run(Choice("moment_mode", LtbInteractionCalculator.ModeEnds), Q("M_y_A", 340, "kN*m"), Q("M_y_B", -170, "kN*m"));
        var warning = Assert.Single(frameProgram.Warnings, w => w.RuleId == "double-curvature");
        Assert.Contains("double curvature", warning.Message, StringComparison.Ordinal);
        Assert.Contains("C₁ = 2.7", warning.Message, StringComparison.Ordinal);
        Assert.Contains("end actions", warning.Message, StringComparison.Ordinal);
        Assert.Contains("340", warning.Message, StringComparison.Ordinal);
        Assert.Contains("-170", warning.Message, StringComparison.Ordinal);

        // The same moments with one sign (single curvature) and ψ = 0 carry no such warning.
        var single = Run(Choice("moment_mode", LtbInteractionCalculator.ModeEnds), Q("M_y_A", 340, "kN*m"), Q("M_y_B", 170, "kN*m"));
        Assert.DoesNotContain(single.Warnings, w => w.RuleId == "double-curvature");
        var zero = Run(Choice("moment_mode", LtbInteractionCalculator.ModeEnds), Q("M_y_A", 340, "kN*m"), Q("M_y_B", 0, "kN*m"));
        Assert.DoesNotContain(zero.Warnings, w => w.RuleId == "double-curvature");

        // The ψ mode (ψ entered as a number) is not an end-moment slip and is not touched.
        Assert.DoesNotContain(Run(N("psi_y", -0.5)).Warnings, w => w.RuleId == "double-curvature");

        // The sign convention is stated where it is entered.
        Assert.Contains("bending-moment diagram", Ltb.Template.FindInput("M_y_A")!.Description, StringComparison.Ordinal);
        Assert.Contains("double-curvature", Ltb.Template.ApplicabilityRules.Select(r => r.Id));
    }

    [Fact]
    public void Black_B_B_07_The_Double_Curvature_Warning_Prints_The_C1_And_C_my_The_Run_Uses()
    {
        var ends = new[] { Choice("moment_mode", LtbInteractionCalculator.ModeEnds), Q("M_y_A", 340, "kN*m"), Q("M_y_B", -170, "kN*m") };

        // An entered C₁ (and C_my) overrides the ψ-derived value: the warning must name the one the run uses, not the ψ value.
        var entered = Run([.. ends, N("C_1", 1.5), N("C_m_y", 0.9)]);
        var warning = Assert.Single(entered.Warnings, w => w.RuleId == "double-curvature");
        Assert.Equal(1.5, entered.OutputSi("C_1"), 12);
        Assert.Contains("C₁ = 1.5 (entered, not the ψ value 2.7)", warning.Message, StringComparison.Ordinal);
        Assert.Contains("C_my = 0.9 (entered, not the ψ value 0.4)", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("C₁ = 2.7", warning.Message, StringComparison.Ordinal);

        // C₁ entered alone: C_my still comes from ψ.
        var c1Only = Assert.Single(Run([.. ends, N("C_1", 1.5)]).Warnings, w => w.RuleId == "double-curvature");
        Assert.Contains("C₁ = 1.5 (entered", c1Only.Message, StringComparison.Ordinal);
        Assert.Contains("C_my = 0.4.", c1Only.Message, StringComparison.Ordinal);

        // Nothing entered: both come from ψ, as before. Annex A takes its own C_my.
        var derived = Assert.Single(Run(ends).Warnings, w => w.RuleId == "double-curvature");
        Assert.Contains("C₁ = 2.7, C_my = 0.4.", derived.Message, StringComparison.Ordinal);
        var annexA = Assert.Single(Run([.. ends, N("C_1", 1.5), Choice("annex", LtbInteractionCalculator.AnnexAMethod)]).Warnings, w => w.RuleId == "double-curvature");
        Assert.Contains("C₁ = 1.5 (entered", annexA.Message, StringComparison.Ordinal);
        Assert.Contains("Annex A takes its C_my from ψ", annexA.Message, StringComparison.Ordinal);
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
            N("psi_y", 1.5), N("psi_z", -1.2), Q("t_f", 1, "mm"), Q("t_f", 200, "mm"), Q("A", 1, "cm^2"), Q("I_w", 0, "dm^6"), N("C_2", -0.1), N("C_1", 0.9), N("alpha_d", 0), N("beta_LT", 1.5), N("gamma_M1", 0.9),
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

    // ------------------------------------------------------------------ B-1 review fixes

    private static string[] DeclaredRules() => [.. Ltb.Template.ApplicabilityRules.Select(r => r.Id)];

    [Fact]
    public void Review_H1_Annex_A_Class_3_Uses_The_Elastic_Column_Without_The_Plastic_Factors()
    {
        // Table A.1, elastic column: k_yz = C_mz·μ_y/(1 − N/N_cr,z) and k_zy = C_my·C_mLT·μ_z/(1 − N/N_cr,y), with every C_ij = 1 (w = 1).
        var r = Run(Choice("annex", LtbInteractionCalculator.AnnexAMethod), Choice("section_class", "3"));
        var n = 1200e3;
        var ncrY = r.OutputSi("N_cr_y");
        var ncrZ = r.OutputSi("N_cr_z");
        var muY = (1 - n / ncrY) / (1 - r.OutputSi("chi_y") * n / ncrY);
        var muZ = (1 - n / ncrZ) / (1 - r.OutputSi("chi_z") * n / ncrZ);
        Assert.Equal(r.OutputSi("C_mz") * muY / (1 - n / ncrZ), r.OutputSi("k_yz"), 9);
        Assert.Equal(r.OutputSi("C_my") * r.OutputSi("C_mLT") * muZ / (1 - n / ncrY), r.OutputSi("k_zy"), 9);

        // Class 2 keeps the plastic column: its k_yz and k_zy carry the 0.6√(w_i/w_j) factors, so for the same μ they are lower than the elastic form.
        var plastic = Run(Choice("annex", LtbInteractionCalculator.AnnexAMethod), Choice("section_class", "2"));
        Assert.True(plastic.OutputSi("k_yz") > 0 && plastic.OutputSi("k_zy") > 0);
    }

    [Fact]
    public void Review_H1_The_Elastic_Annex_A_Factors_Are_Not_Lowered_By_The_Plastic_Factor_In_The_Math()
    {
        double Kzy(bool plastic) => LtbInteractionMath.AnnexA(new LtbInteractionMath.AnnexAInput(
            1000e3, 20000e3, 5000e3, 4000e3, 0.9, 0.6, 0.5, 0.4, 0.9, 1.2, 1.1, 0.5, 1.0, 100e6, 10e6, 500e6, 80e6, 3000e3, 12000, 2e6, 1.0, 1.0, 0.99, plastic)).Kzy;

        // With w = 1 (class 3: W_pl/W_el = 1) the plastic column gives exactly 0.6 times the elastic one.
        Assert.Equal(0.6, Kzy(true) / Kzy(false), 12);
    }

    [Fact]
    public void Review_M1_The_Modified_Reduction_Factor_Is_Capped_At_One_Over_Lambda_Squared()
    {
        Assert.Equal(1 / (1.2 * 1.2), LtbInteractionMath.ChiLtModified(0.7, 0.8, 1.2), 12);
        Assert.Equal(1.0, LtbInteractionMath.ChiLtModified(0.9, 0.8, 0.9), 12);
        Assert.Equal(0.6, LtbInteractionMath.ChiLtModified(0.6, 1.0, 1.1), 12);

        // Curve a chosen by hand with a reverse-symmetric moment (ψ = −1, C₁ = 2.7, k_c = 0.6024) and 12 m between restraints: χ_LT,mod = χ_LT/f = 0.7248 would exceed 1/λ̄² = 0.6720.
        var r = Run(Choice("section_class", "1"), Choice("ltb_curve", "a"), CalcInputValue.Switch("use_f", true), N("psi_y", -1), Q("L", 12, "m"), Q("N_Ed", 0, "kN"), Q("M_y_Ed", 100, "kN*m"),
            Q("I_z", 2347.0, "cm^4"), Q("I_t", 121.29, "cm^4"), Q("I_w", 1.1755, "dm^6"), Q("W_pl_y", 2232.5, "cm^3"), Q("W_el_y", 1957.5, "cm^3"), Q("W_pl_z", 378.9, "cm^3"), Q("W_el_z", 243.47, "cm^3"), Q("A", 125.27, "cm^2"), Q("I_y", 45728, "cm^4"));
        var lambda = r.OutputSi("lambda_LT");
        Assert.Equal(1 / (lambda * lambda), r.OutputSi("chi_LT"), 12);
        Assert.Equal(0.6719504886914954, r.OutputSi("chi_LT"), 4);
    }

    [Fact]
    public void Review_M1_The_Modified_Factor_Never_Exceeds_One_Over_Lambda_Squared_Whatever_The_Factor_Set()
    {
        Gen.Select(Gen.Double[0.5, 1.0], Gen.Double[0.5, 1.0], Gen.Double[0.2, 0.5], Gen.Double[3, 40]).Sample((kc, beta, lam0, l) =>
        {
            var r = Run(Choice("ltb_curve", "a"), CalcInputValue.Switch("use_f", true), N("k_c", kc), N("beta_LT", beta), N("lambda_LT0", lam0), N("psi_y", -1), Q("L", l, "m"), Q("N_Ed", 0, "kN"));
            var lambda = r.OutputSi("lambda_LT");
            var expected = Math.Min(Math.Min(1.0, r.OutputSi("chi_LT_0") / r.OutputSi("f")), 1 / (lambda * lambda));
            return !r.IsBlocked && Math.Abs(r.OutputSi("chi_LT") - expected) <= 1e-12 && r.OutputSi("chi_LT") <= 1 / (lambda * lambda) * (1 + 1e-12) + 1e-12;
        }, iter: 60);
    }

    [Fact]
    public void Review_M2_An_Entered_C1_Blocks_Until_Cmy_And_Kc_Are_Entered_Or_The_Diagram_Is_Given_As_End_Moments()
    {
        // Annex B: C_my would come from ψ_y, which says nothing about a transverse load.
        var noCm = Run(N("C_1", 1.13), N("C_2", 0.45), Q("z_g", 100, "mm"));
        var block = Assert.Single(noCm.Blocks, b => b.RuleId == "C_m_y");
        Assert.StartsWith("C_m_y:", block.Message, StringComparison.Ordinal);
        Assert.Contains("enter", block.Message, StringComparison.Ordinal);
        Assert.False(Run(N("C_1", 1.13), N("C_2", 0.45), Q("z_g", 100, "mm"), N("C_m_y", 0.95)).IsBlocked);

        // The f modification: k_c is not taken from ψ either.
        var noKc = Run(N("C_1", 1.13), N("C_m_y", 0.95), CalcInputValue.Switch("use_f", true));
        Assert.Contains(noKc.Blocks, b => b.RuleId == "k_c" && b.Message.StartsWith("k_c:", StringComparison.Ordinal));
        var withKc = Run(N("C_1", 1.13), N("C_m_y", 0.95), CalcInputValue.Switch("use_f", true), N("k_c", 0.94));
        Assert.False(withKc.IsBlocked);
        Assert.Equal(0.94, withKc.OutputSi("k_c"), 12);

        // End moments are an entered moment shape: ψ is real, so C_my and k_c are derived from it.
        var ends = Run(N("C_1", 1.13), Choice("moment_mode", LtbInteractionCalculator.ModeEnds), Q("M_y_A", 200, "kN*m"), Q("M_y_B", 100, "kN*m"), CalcInputValue.Switch("use_f", true));
        Assert.False(ends.IsBlocked, string.Join("; ", ends.Blocks.Select(b => b.Message)));
        Assert.Equal(0.8, ends.OutputSi("C_my"), 12);

        // Without C_1 entered nothing changes.
        Assert.False(Run().IsBlocked);

        // Annex A has no C_m input: it warns that C_my,0 comes from ψ.
        var annexA = Run(Choice("annex", LtbInteractionCalculator.AnnexAMethod), N("C_1", 1.13));
        Assert.False(annexA.IsBlocked);
        Assert.Contains(annexA.Warnings, w => w.RuleId == "c1-psi" && w.Message.StartsWith("C_1:", StringComparison.Ordinal));
    }

    [Fact]
    public void Review_M3_The_Default_Moment_Ratio_Is_A_Uniform_Moment()
    {
        var spec = Ltb.Template.FindInput("psi_y")!;
        Assert.Equal(1, spec.Default);
        var fresh = CalcRunner.Run(Ltb, CalcTemplateHasher.NewSheetInputs(Ltb.Template));
        Assert.False(fresh.IsBlocked, string.Join("; ", fresh.Blocks.Select(b => b.Message)));
        Assert.Equal(1.0, fresh.OutputSi("C_1"), 12);
        Assert.Equal(1.0, fresh.OutputSi("C_my"), 12);
    }

    [Fact]
    public void Review_M4_A_Load_Height_With_C2_Zero_Warns_That_It_Has_No_Effect()
    {
        var silent = Run(Q("z_g", 150, "mm"), N("C_2", 0));
        Assert.Contains(silent.Warnings, w => w.RuleId == "zg-c2" && w.Message.StartsWith("z_g:", StringComparison.Ordinal));
        Assert.Equal(Run(Q("z_g", 0, "mm"), N("C_2", 0)).OutputSi("Mcr"), silent.OutputSi("Mcr"), 6);
        Assert.DoesNotContain(Run(Q("z_g", 150, "mm"), N("C_2", 0.4), N("C_1", 1.2), N("C_m_y", 1.0)).Warnings, w => w.RuleId == "zg-c2");
        Assert.DoesNotContain(Run(Q("z_g", 0, "mm"), N("C_2", 0)).Warnings, w => w.RuleId == "zg-c2");
    }

    [Fact]
    public void Review_M5_The_End_Moment_Inputs_State_The_Diagram_Ordinate_Sign_Convention()
    {
        foreach (var symbol in new[] { "M_y_A", "M_y_B" })
        {
            var text = Ltb.Template.FindInput(symbol)!.Description;
            Assert.Contains("bending-moment", text, StringComparison.Ordinal);
            Assert.Contains("single curvature", text, StringComparison.Ordinal);
        }

        Assert.Contains("stiffness analysis", Ltb.Template.FindInput("M_y_A")!.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Review_M7_The_Welded_Section_Warping_Constant_Is_Exact_For_The_Plates()
    {
        // I_w = I_z of the two flanges × h_s²/4 (the web adds nothing); h_s = 500 − 16 = 484 mm.
        var inputs = Ltb.Template.TestCases.Single(t => t.Id == "ka-mcr-uniform-6322").Inputs;
        var iw = inputs.Single(i => i.Symbol == "I_w");
        var flanges = 2 * (16.0 * 200 * 200 * 200 / 12);
        Assert.Equal("dm^6", iw.Unit);
        Assert.Equal(flanges * 484 * 484 / 4 / 1e12, iw.Number!.Value, 12);
        Assert.Equal(Iw, flanges * 484 * 484 / 4, Iw * 1e-12);

        // The classical closed form with that I_w gives the case's M_cr to 1e-6, so the case is exact for the plates.
        var classic = Math.PI / 6000 * Math.Sqrt(210000 * Iz * 81000 * It) * Math.Sqrt(1 + Math.PI * Math.PI * 210000 * Iw / (6000.0 * 6000 * 81000 * It)) / 1e6;
        var expected = Ltb.Template.TestCases.Single(t => t.Id == "ka-mcr-uniform-6322").Expected.Single(e => e.Symbol == "Mcr");
        Assert.Equal(expected.Value, classic, expected.Value * 1e-6);
    }

    [Fact]
    public void Review_Low_The_Axial_Utilisation_Line_Prints_The_Resistance_In_Kilonewtons()
    {
        var r = Run();
        var step = r.Steps.Single(st => st.Symbol == "u_buckling");
        var nbRd = r.OutputSi("N_b_Rd") / 1e3;
        Assert.Contains(CalcValue.FormatNumber(nbRd, 5) + " kN", step.Substitution, StringComparison.Ordinal);
        Assert.DoesNotContain(CalcValue.FormatNumber(nbRd * 1e3, 5) + " kN", step.Substitution, StringComparison.Ordinal);
    }

    [Fact]
    public void Review_Low_The_Moment_Inputs_Of_The_Unused_Mode_Are_Optional_And_Raise_No_Default_Warnings()
    {
        var withEnds = CalcRunner.Run(Ltb, Defaults().With(Choice("moment_mode", LtbInteractionCalculator.ModeEnds)).With(Q("M_y_A", 200, "kN*m")).With(Q("M_y_B", 100, "kN*m")).With(new CalcInputValue("M_y_Ed")).With(new CalcInputValue("psi_y")));
        Assert.False(withEnds.IsBlocked, string.Join("; ", withEnds.Blocks.Select(b => b.Message)));
        Assert.DoesNotContain(withEnds.Warnings, w => w.RuleId is "input.M_y_Ed" or "input.psi_y");

        // ψ mode: a blank ψ_y is the uniform moment, ψ = 1, the conservative diagram: it runs on it (listed as a default used). A blank greatest moment blocks (F2): it ran on the
        // template default of 300 kN·m, a moment nobody entered, and passed a member whose real moment was higher.
        var missing = CalcRunner.Run(Ltb, Defaults().With(new CalcInputValue("psi_y")));
        Assert.False(missing.IsBlocked);
        Assert.DoesNotContain(missing.Notices, n => n.RuleId == "moment");
        Assert.Equal(1.0, missing.OutputSi("C_1"), 12);

        var noMoment = CalcRunner.Run(Ltb, Defaults().With(new CalcInputValue("M_y_Ed")));
        Assert.True(noMoment.IsBlocked);
        var block = Assert.Single(noMoment.Blocks);
        Assert.Equal("moment", block.RuleId);
        Assert.Equal("M_y_Ed: enter the greatest design moment about y-y, or give the moment diagram as end moments.", block.Message);
        Assert.Empty(CalcMessageShape.Problems(block.Message));
        Assert.Null(noMoment.FindOutput("u_member"));
    }

    [Fact]
    public void Review_Low_Dead_Tension_Code_Is_Gone_And_Every_Raised_Rule_Is_Declared()
    {
        var declared = DeclaredRules();
        Assert.DoesNotContain("tension", declared);
        var runs = new[]
        {
            Run(), Run(Choice("edition", LtbInteractionCalculator.Edition2022)), Run(Choice("section_class", "4")), Run(Q("W_pl_y", 1500, "cm^3")), Run(Choice("moment_mode", LtbInteractionCalculator.ModeEnds)),
            Run(Choice("annex", LtbInteractionCalculator.AnnexAMethod), Q("N_Ed", -5000, "kN")), Run(Choice("annex", LtbInteractionCalculator.AnnexAMethod), N("C_1", 1.2)), Run(N("C_1", 1.2)), Run(N("C_1", 1.2), N("C_m_y", 1.0), CalcInputValue.Switch("use_f", true)),
            Run(Q("z_g", 100, "mm")), Run(Choice("ltb_method", LtbInteractionCalculator.General), CalcInputValue.Switch("use_f", true)), Run(Q("f_y", 460, "MPa")), Run(N("C_m_LT", 0.25)), Run(N("C_1", 1.2), N("psi_y", 0.3), N("C_2", 0.2), Q("z_g", 50, "mm"), N("C_m_y", 0.9)),
            CalcRunner.Run(Ltb, Defaults().With(new CalcInputValue("M_y_Ed"))), Run(Q("L_cr_z", 60, "m")),
        };

        foreach (var notice in runs.SelectMany(r => r.Notices))
        {
            if (notice.RuleId.StartsWith("input.", StringComparison.Ordinal) || notice.RuleId.StartsWith("record.", StringComparison.Ordinal))
                continue;

            Assert.Contains(notice.RuleId, declared);
        }

        // ... including the ones that were raised before without a declaration.
        Assert.All(new[] { "use-f", "engine", "C_m_LT", "moment", "k_c", "C_m_y", "zg-c2", "curve" }, id => Assert.Contains(id, declared));
    }

    [Theory]
    [InlineData(false, 2.42, 19.6, "a", "b")]
    [InlineData(false, 2.42, 40, "a", "b")]
    [InlineData(false, 2.42, 60, "b", "c")]
    [InlineData(false, 2.42, 100, "b", "c")]
    [InlineData(false, 1.0, 60, "b", "c")]
    [InlineData(false, 1.2, 100, "b", "c")]
    [InlineData(false, 1.0, 120, "d", "d")]
    [InlineData(true, 2.5, 16, "b", "c")]
    [InlineData(true, 1.0, 40, "b", "c")]
    [InlineData(true, 2.5, 50, "c", "d")]
    public void Review_Low_The_Flexural_Curves_Follow_Table_6_2(bool welded, double hOverB, double tf, string y, string z) => Assert.Equal((y, z), LtbInteractionCalculator.AutoFlexuralCurves(welded, hOverB, tf));

    [Fact]
    public void Review_Low_A_Rolled_I_Deeper_Than_1_2_With_Flanges_Over_100_Mm_Has_No_Table_6_2_Row()
    {
        Assert.Null(LtbInteractionCalculator.AutoFlexuralCurves(false, 2.4, 110));
        var r = Run(Choice("section_type", LtbInteractionCalculator.RolledI), Choice("curve_y", LtbInteractionCalculator.FlexuralAuto), Choice("curve_z", LtbInteractionCalculator.FlexuralAuto), Q("t_f", 110, "mm"));
        Assert.Contains(r.Blocks, b => b.RuleId == "curve" && b.Message.StartsWith("curve_y:", StringComparison.Ordinal));
    }

    [Fact]
    public void Review_Low_The_Flexural_Curves_Follow_The_Section_Type_Like_The_Ltb_Curve()
    {
        var auto = Choice("curve_y", LtbInteractionCalculator.FlexuralAuto);
        var autoZ = Choice("curve_z", LtbInteractionCalculator.FlexuralAuto);
        var rolled = Run(auto, autoZ, Choice("section_type", LtbInteractionCalculator.RolledI), Q("t_f", 19.6, "mm"), Q("h", 467.2, "mm"), Q("b", 192.8, "mm"));
        var welded = Run(auto, autoZ, Choice("section_type", LtbInteractionCalculator.WeldedI), Q("t_f", 19.6, "mm"), Q("h", 467.2, "mm"), Q("b", 192.8, "mm"));
        Assert.Equal(0.21, rolled.OutputSi("alpha_y"), 12);
        Assert.Equal(0.34, rolled.OutputSi("alpha_z"), 12);
        Assert.Equal(0.34, welded.OutputSi("alpha_y"), 12);
        Assert.Equal(0.49, welded.OutputSi("alpha_z"), 12);
        Assert.True(welded.OutputSi("chi_z") < rolled.OutputSi("chi_z"));

        // A curve chosen by hand is kept, and the automatic choice is declared on the sheet.
        var byHand = Run(Choice("curve_y", "c"), autoZ, Choice("section_type", LtbInteractionCalculator.RolledI));
        Assert.Equal(0.49, byHand.OutputSi("alpha_y"), 12);
        Assert.Contains(rolled.RunAssumptions, a => a.Statement.Contains("Table 6.2", StringComparison.Ordinal));

        // The default for both axes is the automatic choice.
        Assert.Equal(LtbInteractionCalculator.FlexuralAuto, Ltb.Template.FindInput("curve_y")!.DefaultChoice);
        Assert.Equal(LtbInteractionCalculator.FlexuralAuto, Ltb.Template.FindInput("curve_z")!.DefaultChoice);
    }

    [Fact]
    public void Review_Low_Above_420_Mpa_The_Flexural_Curves_Must_Be_Chosen_By_Hand()
    {
        var blocked = Run(Q("f_y", 460, "MPa"), Choice("curve_y", LtbInteractionCalculator.FlexuralAuto), Choice("curve_z", LtbInteractionCalculator.FlexuralAuto));
        Assert.Contains(blocked.Blocks, b => b.RuleId == "curve" && b.Message.Contains("choose", StringComparison.Ordinal));
        Assert.False(Run(Q("f_y", 460, "MPa"), Choice("curve_y", "a0"), Choice("curve_z", "a")).IsBlocked);
        Assert.False(Run(Q("f_y", 420, "MPa"), Choice("curve_y", LtbInteractionCalculator.FlexuralAuto), Choice("curve_z", LtbInteractionCalculator.FlexuralAuto)).IsBlocked);
    }

    [Fact]
    public void Review_Low_The_Edition_2022_Label_Only_Decision_Is_Stated_On_The_Template_And_The_Sheet()
    {
        Assert.Contains(Ltb.Template.Assumptions, a => a.Statement.Contains("label printed on the sheet only, by decision", StringComparison.Ordinal));
        var warning = Assert.Single(Run(Choice("edition", LtbInteractionCalculator.Edition2022)).Warnings, w => w.RuleId == "edition");
        Assert.Contains("label only", warning.Message, StringComparison.Ordinal);
    }
}
