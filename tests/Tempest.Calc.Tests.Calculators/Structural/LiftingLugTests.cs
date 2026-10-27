using CsCheck;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

/// <summary>S8 (struct.lifting): the lug and spreader-beam arithmetic. The gate, the attestation and the second check are tested in the Library project.</summary>
public sealed class LiftingLugTests
{
    private static readonly LiftingLugCalculator Lug = new();

    /// <summary>The first known-answer case: BTH-1 mode, two lugs at 30°, 150 kN.</summary>
    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Lug.Template);

    private static CalcRunResult Run(params CalcInputValue[] changes) => CalcRunner.Run(Lug, changes.Aggregate(Defaults(), (set, v) => set.With(v)));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue Choice(string symbol, string value) => CalcInputValue.Choice(symbol, value);

    private static string Text(CalcRunResult r, string symbol) => r.FindOutput(symbol)?.Value is TextValue t ? t.Value : throw new InvalidOperationException($"{symbol} is not a text output");

    private static double Kn(CalcRunResult r, string symbol) => r.OutputSi(symbol) / 1e3;

    [Fact]
    public void The_Template_Is_Gated_And_Demands_The_Extra_Governance()
    {
        var t = Lug.Template;
        Assert.Equal(CalcFeatures.Lifting, t.FeatureFlag);
        Assert.True(t.RequiresAttestation);
        Assert.True(t.RequiresSecondCheck);
        Assert.True(t.IndependentCheck);
        Assert.Equal("struct.lifting", t.Id);

        foreach (var symbol in CalcAttestation.Symbols)
            Assert.Equal(CalcInputKind.Text, t.FindInput(symbol)!.Kind);
    }

    [Fact]
    public void The_Sheet_States_That_It_Is_A_Design_Calculation_And_Not_A_Thorough_Examination()
    {
        var t = Lug.Template;
        Assert.Contains("design calculation, not a thorough examination", t.Scope, StringComparison.Ordinal);
        Assert.Contains("LOLER", t.Scope, StringComparison.Ordinal);
        Assert.Contains("PUWER", t.Scope, StringComparison.Ordinal);

        var r = Run();
        Assert.Contains("design calculation, not a thorough examination", Text(r, "sheet_notice"), StringComparison.Ordinal);
        Assert.Contains("thorough examination", Text(r, "checklist"), StringComparison.Ordinal);
        Assert.Contains("LOLER reg 9", Text(r, "checklist"), StringComparison.Ordinal);
    }

    [Fact]
    public void No_Standard_Factor_Has_A_Default()
    {
        foreach (var symbol in new[] { "Nd", "k_svc", "n_y", "n_u", "k_b", "dish_max", "k_proof" })
        {
            var spec = Lug.Template.FindInput(symbol)!;
            Assert.Null(spec.Default);
            Assert.True(spec.IsOptional, symbol);
            Assert.Contains(Lug.Template.RecordKinds!, k => k.Symbols.Contains(symbol));
        }

        Assert.Contains(Lug.Template.RecordKinds!, k => k.Kind == LiftingLugCalculator.MaterialKind && k.Symbols.Contains("f_y") && k.Symbols.Contains("f_u"));
        Assert.Equal("f_y", Lug.Template.FindInput("f_y")!.MaterialProperty);
        Assert.Equal("f_u", Lug.Template.FindInput("f_u")!.MaterialProperty);
    }

    [Theory]
    [InlineData("WLL", 0.4, "kN")]
    [InlineData("WLL", 10001, "kN")]
    [InlineData("theta", -1, "deg")]
    [InlineData("theta", 61, "deg")]
    [InlineData("beta", 31, "deg")]
    [InlineData("n_cycles", 16001, "")]
    [InlineData("n_cycles", 0, "")]
    [InlineData("t_p", 4, "mm")]
    [InlineData("t_p", 151, "mm")]
    [InlineData("Dh", 9, "mm")]
    [InlineData("Dp", 7, "mm")]
    [InlineData("r_lug", 14, "mm")]
    [InlineData("be", 4, "mm")]
    [InlineData("a", 4, "mm")]
    [InlineData("f_y", 99, "MPa")]
    [InlineData("f_u", 1401, "MPa")]
    [InlineData("Nd", 0.9, "")]
    [InlineData("Nd", 5.1, "")]
    [InlineData("n_y", 4.1, "")]
    [InlineData("k_b", 0.4, "")]
    [InlineData("k_proof", 3.5, "")]
    [InlineData("A_beam", 0.5, "cm^2")]
    public void Inputs_Outside_Their_Range_Are_A_Hard_Fail(string symbol, double value, string unit)
    {
        var changed = unit.Length > 0 ? Q(symbol, value, unit) : N(symbol, value);
        Assert.Contains(CalcInputValidator.Validate(Lug.Template, Defaults().With(changed)), n => n.Severity == CalcNoticeSeverity.Block);
        Assert.True(CalcRunner.Run(Lug, Defaults().With(changed)).IsBlocked);
    }

    [Fact]
    public void Every_Numeric_Input_Has_A_Usual_Range_Inside_Its_Hard_Range() =>
        Assert.All(Lug.Template.Inputs.Where(i => i.Kind is CalcInputKind.Number or CalcInputKind.Quantity), i =>
        {
            Assert.True(i.HardMin <= i.Min, i.Symbol);
            Assert.True(i.HardMax >= i.Max, i.Symbol);
        });

    [Fact]
    public void The_Cycle_Count_Is_Limited_To_16000_By_A_Hard_Limit()
    {
        Assert.False(CalcRunner.Run(Lug, Defaults().With(N("n_cycles", 16000))).IsBlocked);
        Assert.True(CalcRunner.Run(Lug, Defaults().With(N("n_cycles", 16000.5))).IsBlocked);
    }

    [Fact]
    public void The_Pin_Clearance_Factor_And_The_Shear_Plane_Angle_Follow_The_Stated_Equations()
    {
        foreach (var ratio in new[] { 0.6, 0.75, 0.9, 1.0 })
        {
            var r = Run(Q("Dh", 50, "mm"), Q("Dp", 50 * ratio, "mm"));
            Assert.Equal(1 - 0.275 * Math.Sqrt(1 - ratio * ratio), r.OutputSi("Cr"), 12);
            Assert.Equal(55 * ratio, r.OutputSi("phi") * 180 / Math.PI, 9);
        }

        // The S106 ratio: C_r = 0.78 and φ = 33° from D_p/D_h = 0.6.
        var s106 = Run(Q("Dp", 30, "mm"));
        Assert.Equal(0.78, s106.OutputSi("Cr"), 12);
        Assert.Equal(33, s106.OutputSi("phi") * 180 / Math.PI, 9);
    }

    [Fact]
    public void The_S106_Case_Is_Secondary_Confirmed_And_Pins_All_Six_Published_Values()
    {
        var s106 = Lug.Template.TestCases.Single(c => c.Id == "ka-pdhonline-s106");
        Assert.True(s106.Source.Secondary);
        Assert.Equal(CalcReferenceStatus.Confirmed, s106.Status);
        Assert.Contains("checked against the owner's copy", s106.Source.Location, StringComparison.Ordinal);
        Assert.Equal(["A_v", "Cr", "P_b", "P_sp", "P_t", "P_v", "phi"], s106.Expected.Select(e => e.Symbol).Order().ToArray());

        // The six published values, to the rounding of the published three decimals.
        var published = new Dictionary<string, double> { ["Cr"] = 0.78, ["phi"] = 33, ["A_v"] = 2.964, ["P_t"] = 25.448, ["P_b"] = 10.875, ["P_v"] = 19.849, ["P_sp"] = 20.539 };

        foreach (var e in s106.Expected)
            Assert.Equal(published[e.Symbol], e.Value);

        // Run on the published inputs: every value matches without the test-case tolerance helping more than the rounding of the course notes.
        var r = CalcRunner.Run(Lug, new CalcInputSet(s106.Inputs));
        Assert.False(r.IsBlocked);
        Assert.Equal(11.6, r.OutputSi("Fa") / 6.894757293168361e6, 1e-9);
        Assert.Equal(25.448, r.OutputSi("P_t") / 4448.2216152605, 6e-4);
        Assert.Equal(20.539, r.OutputSi("P_sp") / 4448.2216152605, 6e-4);
        Assert.Equal(19.849, r.OutputSi("P_v") / 4448.2216152605, 6e-4);
        Assert.Equal(10.875, r.OutputSi("P_b") / 4448.2216152605, 6e-4);

        // The other analytic cases are confirmed and are not secondary.
        Assert.All(Lug.Template.TestCases.Where(c => c.Id != s106.Id), c =>
        {
            Assert.Equal(CalcReferenceStatus.Confirmed, c.Status);
            Assert.False(c.Source.Secondary);
        });
    }

    [Fact]
    public void Shear_Out_Uses_The_Pin_Radius_And_The_Two_Planes_At_Phi_And_Grows_With_The_Pin_Ratio()
    {
        // A_v = 2·t·[e + (D_p/2)(1 − cos φ)], φ = 55·D_p/D_h, e the end distance (here R − D_h/2 = 40 mm): a larger pin opens the planes and adds area.
        var small = Run(Q("Dp", 30, "mm")).OutputSi("A_v");
        var large = Run(Q("Dp", 50, "mm")).OutputSi("A_v");
        Assert.True(large > small);
        Assert.Equal(2 * 20 * (40 + 25 * (1 - Math.Cos(Math.PI * 55 / 180))), large * 1e6, 1e-6);
        Assert.Equal(2 * 20 * (40 + 15 * (1 - Math.Cos(Math.PI * 33 / 180))), small * 1e6, 1e-6);
    }

    [Fact]
    public void The_Single_Plane_Fracture_Is_The_Section_Beyond_The_Hole_Not_The_Net_Section_Across_It()
    {
        var r = Run();
        Assert.Null(r.FindOutput("A_net"));
        var side = 35.0;
        Assert.Equal((1.13 * 40 + 0.92 * side / (1 + side / 50)) * 20, r.OutputSi("A_fr") * 1e6, 1e-6);
        var cr = r.OutputSi("Cr");
        Assert.Equal(cr * r.OutputSi("Fa") * r.OutputSi("A_fr"), r.OutputSi("P_sp"), r.OutputSi("P_sp") * 1e-12);
    }

    [Fact]
    public void The_Effective_Width_Is_Limited_By_4t_And_By_The_Strength_Ratio()
    {
        // t = 10 mm: 4t = 40 mm caps a 60 mm edge distance (the plate is wide enough: R = 100).
        var thin = Run(Q("t_p", 10, "mm"), Q("r_lug", 100, "mm"), Q("be", 60, "mm"), Q("a", 50, "mm"));
        Assert.Equal(40, thin.OutputSi("be_eff") * 1e3, 9);
        Assert.Equal(2 * 10 * 40, thin.OutputSi("A_t") * 1e6, 6);
        Assert.Contains(thin.Warnings, w => w.RuleId == "be-eff");

        // The third limit, 0.6·(f_u/f_y)·√(D_h/b)·b, is below b here (f_u = f_y, a wide edge distance).
        var strength = Run(Q("t_p", 40, "mm"), Q("r_lug", 200, "mm"), Q("be", 120, "mm"), Q("f_y", 355, "MPa"), Q("f_u", 355, "MPa"));
        Assert.Equal(0.6 * Math.Sqrt(50.0 / 120) * 120, strength.OutputSi("be_eff") * 1e3, 9);
    }

    [Fact]
    public void BTH1_Mode_Blocks_Until_The_User_Records_Are_Entered_And_Says_What_To_Enter()
    {
        foreach (var symbol in new[] { "Nd", "n_y", "n_u", "k_b" })
        {
            var inputs = new CalcInputSet(Defaults().Values.Where(v => v.Symbol != symbol));
            var r = CalcRunner.Run(Lug, inputs);
            Assert.True(r.IsBlocked, symbol);
            Assert.StartsWith(symbol + ": ", r.Blocks[0].Message, StringComparison.Ordinal);
            Assert.Contains("enter", r.Blocks[0].Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("own copy of the code", r.Blocks[0].Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EN13155_Mode_Needs_None_Of_The_BTH1_Records()
    {
        var inputs = new CalcInputSet(Defaults().Values.Where(v => v.Symbol is not ("Nd" or "n_y" or "n_u" or "k_b" or "dish_max" or "k_proof")).Select(v => v.Symbol == "mode" ? Choice("mode", LiftingLugCalculator.ModeEn) : v));
        var r = CalcRunner.Run(Lug, inputs);
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.NotNull(r.FindOutput("cap_en"));
        Assert.Null(r.FindOutput("cap_bth"));
        Assert.Contains(r.Warnings, w => w.RuleId == "proof");
    }

    [Fact]
    public void Both_Mode_Reports_Each_Mode_And_The_Smaller_Capacity_Governs()
    {
        var r = Run(Choice("mode", LiftingLugCalculator.ModeBoth));
        var bth = Kn(r, "cap_bth");
        var en = Kn(r, "cap_en");
        Assert.Equal(Math.Min(bth, en), Kn(r, "capacity"), 9);
        Assert.Equal(150 / Math.Max(r.OutputSi("u_bth"), r.OutputSi("u_en")), Kn(r, "capacity"), 9);
        Assert.Equal(8, r.Checks.Count(c => c.Name.EndsWith("(BTH-1)", StringComparison.Ordinal) || c.Name.StartsWith("Sling force against", StringComparison.Ordinal)) - 1 /* dishing */);
    }

    [Fact]
    public void The_Governing_Mode_Names_The_Section_And_For_EN13155_The_Route()
    {
        var shear = Run(Choice("mode", LiftingLugCalculator.ModeBoth), Q("t_p", 30, "mm"), Q("Dh", 40, "mm"), Q("Dp", 38, "mm"), Q("r_lug", 40, "mm"), Q("be", 30, "mm"), Q("f_y", 235, "MPa"), Q("f_u", 360, "MPa"), Q("WLL", 100, "kN"), Q("theta", 20, "deg"), Q("beta", 0, "deg"));
        Assert.Equal("BTH-1: double-plane shear", Text(shear, "governing_mode"));

        var en = Run(Choice("mode", LiftingLugCalculator.ModeEn), Q("t_p", 30, "mm"), Q("Dh", 40, "mm"), Q("Dp", 38, "mm"), Q("r_lug", 40, "mm"), Q("be", 30, "mm"), Q("f_y", 235, "MPa"), Q("f_u", 360, "MPa"), Q("WLL", 100, "kN"), Q("theta", 20, "deg"), Q("beta", 0, "deg"));
        Assert.Equal("EN 13155 (2×WLL elastic): double-plane shear", Text(en, "governing_mode"));

        // Higher-strength steel: the 3× route governs.
        var release = Run(Choice("mode", LiftingLugCalculator.ModeEn), Q("f_y", 355, "MPa"), Q("f_u", 470, "MPa"));
        Assert.StartsWith("EN 13155 (3×WLL no release)", Text(release, "governing_mode"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_Sling_Force_Includes_Both_Angles_And_Is_Not_Understated_By_The_Out_Of_Plane_Angle()
    {
        var r = Run(Choice("mode", LiftingLugCalculator.ModeEn), Q("theta", 30, "deg"), Q("beta", 30, "deg"));
        var expected = 75 / (Math.Cos(30 * Math.PI / 180) * Math.Cos(30 * Math.PI / 180));
        Assert.Equal(expected, Kn(r, "F_s"), 9);
        Assert.Equal(expected * Math.Cos(30 * Math.PI / 180), Kn(r, "F_ip"), 9);
        Assert.Equal(expected * Math.Sin(30 * Math.PI / 180), Kn(r, "F_op"), 9);
        Assert.Equal(75 * Math.Tan(30 * Math.PI / 180), Kn(r, "N_beam"), 9);
        // The vertical component is the load on the lug.
        Assert.Equal(75, Kn(r, "F_s") * Math.Cos(30 * Math.PI / 180) * Math.Cos(30 * Math.PI / 180), 9);
    }

    [Fact]
    public void A_Horizontal_Beam_Adds_The_Automatic_Pitch_And_Roll_Tilt_Cases()
    {
        var plain = Run(Choice("mode", LiftingLugCalculator.ModeEn), Q("beta", 0, "deg"));
        var beam = Run(Choice("mode", LiftingLugCalculator.ModeEn), CalcInputValue.Switch("beam", true), Q("beta", 0, "deg"));
        const double fv = 75.0;
        var th = 30 * Math.PI / 180;
        var tau = 6 * Math.PI / 180;
        Assert.Equal(fv / Math.Cos(th), Kn(plain, "F_s"), 9);

        // Pitch: the slings at θ ± 6° meet at the hook, so the sling at θ − 6° carries 2·F_v·sin(θ + 6°)/sin 2θ.
        var pitch = 2 * fv * Math.Sin(th + tau) / Math.Sin(2 * th);
        Assert.Equal(pitch, Kn(beam, "F_s_pitch"), 9);
        Assert.Equal(pitch, Kn(beam, "F_s"), 9);
        Assert.Equal(fv / (Math.Cos(th) * Math.Cos(tau)), Kn(beam, "F_s_roll"), 9);
        Assert.True(Kn(beam, "F_s_pitch") > Kn(beam, "F_s_roll"));
        // The force along the tilted beam: the triangle is rigid with the beam, so each sling stays at θ to the beam's normal and the more loaded lug sends F_v·sin(θ + 6°)/cos θ along it.
        Assert.Equal(Math.Max(fv * Math.Tan(th), fv * Math.Sin(th + tau) / Math.Cos(th)), Kn(beam, "N_beam"), 9);
        Assert.Equal(pitch * Math.Sin(th), Kn(beam, "N_beam"), 9);
        Assert.True(beam.OutputSi("u_en") > plain.OutputSi("u_en"));
        Assert.Contains(beam.Warnings, w => w.RuleId == "tilt");
        Assert.DoesNotContain(plain.Warnings, w => w.RuleId == "tilt");
    }

    [Fact]
    public void The_Pitch_Redistribution_Matches_Statics_Of_Two_Slings_Meeting_At_The_Hook()
    {
        // Slings at θ + 6° and θ − 6°: equal and opposite horizontal components, vertical components carrying the whole load.
        var beam = Run(Choice("mode", LiftingLugCalculator.ModeEn), CalcInputValue.Switch("beam", true), Q("WLL", 200, "kN"), Q("beta", 0, "deg"));
        const double th = 30, tau = 6;
        var heavy = Kn(beam, "F_s_pitch");
        var lightSling = heavy * Math.Sin((th - tau) * Math.PI / 180) / Math.Sin((th + tau) * Math.PI / 180);
        Assert.Equal(200, heavy * Math.Cos((th - tau) * Math.PI / 180) + lightSling * Math.Cos((th + tau) * Math.PI / 180), 8);
        // The beam compression is the envelope. Along the tilted beam each sling sends F·sin θ (the triangle is rigid with the beam), the more loaded lug
        // the larger of the two, and the difference of the two is the component of the load along the beam: (F1 − F2)·sin θ = W·sin 6°.
        var thr = th * Math.PI / 180;
        Assert.Equal((heavy - lightSling) * Math.Sin(thr), 200 * Math.Sin(tau * Math.PI / 180), 8);
        Assert.Equal(Math.Max(heavy * Math.Sin(thr), 100 * Math.Tan(thr)), Kn(beam, "N_beam"), 8);
    }

    [Fact]
    public void A_Sling_Angle_No_More_Than_The_Tilt_Puts_The_Whole_Load_On_One_Lug_With_A_Warning()
    {
        var r = Run(Choice("mode", LiftingLugCalculator.ModeEn), CalcInputValue.Switch("beam", true), Q("theta", 5, "deg"), Q("beta", 0, "deg"));
        Assert.Equal(150, Kn(r, "F_s_pitch"), 9);
        Assert.Contains(r.Warnings, w => w.RuleId == "tilt" && w.Message.Contains("whole load", StringComparison.Ordinal));

        // The whole load on one vertical sling, along a beam tilted 6°: W·sin 6° (the pitch formula gives the same at θ = 6°, so there is no step).
        Assert.Equal(150 * Math.Sin(6 * Math.PI / 180), Kn(r, "N_beam"), 9);
        var atTilt = Run(Choice("mode", LiftingLugCalculator.ModeEn), CalcInputValue.Switch("beam", true), Q("theta", 6.000001, "deg"), Q("beta", 0, "deg"));
        Assert.Equal(Kn(r, "N_beam"), Kn(atTilt, "N_beam"), 4);
    }

    [Fact]
    public void Four_Lugs_Are_Taken_As_Three_Sharing_And_Say_So()
    {
        var three = Run(Choice("n_lugs", "3"));
        var four = Run(Choice("n_lugs", "4"));
        Assert.Equal(Kn(three, "F_s"), Kn(four, "F_s"), 12);
        Assert.Contains(four.Warnings, w => w.RuleId == "sharing");
        Assert.True(Kn(Run(Choice("n_lugs", "1")), "F_s") > Kn(Run(Choice("n_lugs", "2")), "F_s"));
    }

    [Fact]
    public void A_Beam_Area_Gives_The_Axial_Utilisation_And_Without_It_The_Sheet_Says_So()
    {
        var withArea = Run(CalcInputValue.Switch("beam", true), Q("A_beam", 120, "cm^2"));
        Assert.NotNull(withArea.FindOutput("u_beam"));
        Assert.Contains(withArea.Checks, c => c.Name == "Beam axial utilisation against its limit");

        var without = Run(CalcInputValue.Switch("beam", true));
        Assert.Null(without.FindOutput("u_beam"));
        Assert.Contains(without.Warnings, w => w.RuleId == "beam-area");
    }

    [Fact]
    public void The_Dishing_Ratio_Is_A_Limit_And_Its_Failure_Is_Named()
    {
        var pass = Run();
        Assert.True(pass.Passed);

        var fail = Run(N("dish_max", 2));
        Assert.False(fail.Passed);
        Assert.StartsWith("dishing ratio limit exceeded", Text(fail, "governing_mode"), StringComparison.Ordinal);
        Assert.True(fail.OutputSi("util") > 1);

        var none = new CalcInputSet(Defaults().Values.Where(v => v.Symbol != "dish_max"));
        var r = CalcRunner.Run(Lug, none);
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Warnings, w => w.RuleId == "dishing");
        Assert.Null(r.FindOutput("u_dish"));
    }

    [Fact]
    public void The_Proof_Load_Is_The_User_Factor_Times_The_Rated_Load()
    {
        Assert.Equal(225, Kn(Run(), "proof_load"), 9);
        Assert.Equal(300, Kn(Run(N("k_proof", 2)), "proof_load"), 9);
    }

    [Fact]
    public void Impossible_Geometry_Blocks_With_The_Input_Named()
    {
        var pin = Run(Q("Dh", 40, "mm"), Q("Dp", 45, "mm"));
        Assert.True(pin.IsBlocked);
        Assert.StartsWith("Dp: ", pin.Blocks[0].Message, StringComparison.Ordinal);

        var radius = Run(Q("Dh", 140, "mm"), Q("Dp", 120, "mm"), Q("r_lug", 65, "mm"));
        Assert.True(radius.IsBlocked);
        Assert.StartsWith("r_lug: ", radius.Blocks[0].Message, StringComparison.Ordinal);

        var strength = Run(Q("f_y", 500, "MPa"), Q("f_u", 450, "MPa"));
        Assert.True(strength.IsBlocked);
        Assert.StartsWith("f_u: ", strength.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_Edge_Distance_Beyond_The_Lug_Is_Limited_And_Warned()
    {
        var r = Run(Q("be", 60, "mm"));
        Assert.Contains(r.Warnings, w => w.RuleId == "be");
        Assert.Equal(40, r.OutputSi("side") * 1e3, 9);
        Assert.Equal(40, r.OutputSi("a_eff") * 1e3, 9);
    }

    [Fact]
    public void A_Close_Fitting_Pin_Needs_No_Clearance_Warning_And_A_Loose_One_Gets_It()
    {
        Assert.DoesNotContain(Run(Q("Dp", 48, "mm")).Warnings, w => w.RuleId == "clearance");
        Assert.Contains(Run(Q("Dp", 30, "mm")).Warnings, w => w.RuleId == "clearance");
    }

    [Fact]
    public void Governance_Inputs_Are_Printed_On_The_Sheet_And_Do_Not_Change_The_Numbers()
    {
        var plain = Run();
        var attested = Run(CalcInputValue.Choice(CalcAttestation.NameSymbol, "Dr Pat Competent"), CalcInputValue.Choice(CalcAttestation.QualificationSymbol, "CEng"), CalcInputValue.Choice(CalcAttestation.StatementSymbol, "Reviewed the lug geometry and the calculation."));
        Assert.Equal(plain.OutputSi("capacity"), attested.OutputSi("capacity"), 12);

        var inputs = Defaults().With(CalcInputValue.Choice(CalcAttestation.NameSymbol, "Dr Pat Competent"));
        var sheet = CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(Lug.Template, inputs, CalcRunner.Run(Lug, inputs), new CalcSheetHeader("P", "C", "T", "A", CalcSheetStatus.Draft, "a", null, null, null, null),
            new CalcSheetFooter(Lug.Template.Reference, "0", "e", 0, 0, null)));
        Assert.Contains("Dr Pat Competent", sheet, StringComparison.Ordinal);
        Assert.Contains("design calculation, not a thorough examination", sheet, StringComparison.Ordinal);
        Assert.Contains("LOLER", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Declared_Ports_Are_Outputs_Of_A_Run_And_Match_The_Spec_Names()
    {
        var ports = Lug.Template.OutputPorts!.Select(p => p.Symbol).ToList();
        Assert.Contains("capacity", ports);
        Assert.Contains("governing_mode", ports);
        Assert.Contains("util", ports);

        var r = Run(CalcInputValue.Switch("beam", true));

        foreach (var port in Lug.Template.OutputPorts!)
            Assert.NotNull(r.FindOutput(port.Symbol));
    }

    [Fact]
    public void Property_The_Capacity_Is_Independent_Of_The_Rated_Load_And_The_Utilisation_Scales_With_It()
    {
        Gen.Double[10, 400].Select(Gen.Double[1.1, 3.0]).Sample((wll, k) =>
        {
            var one = Run(Q("WLL", wll, "kN"));
            var scaled = Run(Q("WLL", Math.Min(wll * k, 10000), "kN"));
            var factor = Math.Min(wll * k, 10000) / wll;

            if (one.IsBlocked || scaled.IsBlocked)
                return;

            Assert.Equal(Kn(one, "capacity"), Kn(scaled, "capacity"), Kn(one, "capacity") * 1e-9);
            Assert.Equal(one.OutputSi("lug_util") * factor, scaled.OutputSi("lug_util"), one.OutputSi("lug_util") * factor * 1e-9);
            Assert.Equal(wll, Kn(one, "capacity") * one.OutputSi("lug_util"), wll * 1e-9);
        }, iter: 200);
    }

    [Fact]
    public void Property_A_Thicker_Lug_And_A_Stronger_Steel_Never_Lower_The_Capacity()
    {
        Gen.Double[12, 40].Select(Gen.Double[250, 700]).Sample((t, fy) =>
        {
            var weak = Run(Q("t_p", t, "mm"), Q("f_y", fy, "MPa"), Q("f_u", fy * 1.3, "MPa"));
            var thick = Run(Q("t_p", t + 3, "mm"), Q("f_y", fy, "MPa"), Q("f_u", fy * 1.3, "MPa"));
            var strong = Run(Q("t_p", t, "mm"), Q("f_y", fy + 50, "MPa"), Q("f_u", fy * 1.3 + 65, "MPa"));

            if (weak.IsBlocked || thick.IsBlocked || strong.IsBlocked)
                return;

            Assert.True(thick.OutputSi("capacity") >= weak.OutputSi("capacity") * (1 - 1e-12));
            Assert.True(strong.OutputSi("capacity") >= weak.OutputSi("capacity") * (1 - 1e-12));
        }, iter: 100);
    }

    [Fact]
    public void Property_A_Steeper_Sling_Never_Lowers_The_Utilisation()
    {
        Gen.Double[0, 59].Sample(theta =>
        {
            var low = Run(Q("theta", theta, "deg"));
            var high = Run(Q("theta", theta + 1, "deg"));
            Assert.True(high.OutputSi("util") >= low.OutputSi("util") * (1 - 1e-12));
        }, iter: 100);
    }
}
