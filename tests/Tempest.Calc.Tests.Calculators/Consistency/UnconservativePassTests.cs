using Tempest.Calc.Electrical;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// C-2 phase A fix, item M1: five checks that passed while their own warning said the result was not safe now fail (or are computed on the safe side).
/// Each test fails on the code before the fix: it asserts the verdict the warning had always implied. The expected numbers come from python3
/// (each comment shows the arithmetic); they change results, so the five templates went up one minor version.
/// </summary>
public sealed class UnconservativePassTests
{
    // ---- struct.en1993-member k-default -----------------------------------------------------------------------------------------------------

    internal static CalcInputSet ShsMember(params CalcInputValue[] more)
    {
        var calc = new MemberChecksCalculator();
        var baseline = calc.Template.TestCases[0].Inputs.ToDictionary(v => v.Symbol);

        // A new sheet opens with the declared defaults: k_yy and k_zz at 1.0 with no provenance, which is what "left at the default" means.
        baseline["k_yy"] = CalcInputValue.Of("k_yy", 1.0);
        baseline["k_zz"] = CalcInputValue.Of("k_zz", 1.0);
        baseline["ltb"] = CalcInputValue.Choice("ltb", MemberChecksCalculator.Restrained);

        foreach (var v in new[]
        {
            CalcInputValue.Quantity("N_Ed", -700, "kN"), CalcInputValue.Quantity("M_y_Ed", 20, "kN*m"), CalcInputValue.Quantity("M_z_Ed", 0, "kN*m"), CalcInputValue.Quantity("V_Ed", 0, "kN"),
            CalcInputValue.Quantity("A", 40, "cm^2"), CalcInputValue.Quantity("W_pl_y", 150, "cm^3"), CalcInputValue.Quantity("W_pl_z", 150, "cm^3"),
            CalcInputValue.Quantity("I_y", 1200, "cm^4"), CalcInputValue.Quantity("I_z", 1200, "cm^4"), CalcInputValue.Quantity("A_v", 20, "cm^2"),
            CalcInputValue.Quantity("L_cr_y", 3, "m"), CalcInputValue.Quantity("L_cr_z", 3, "m"), CalcInputValue.Choice("curve_y", "a"), CalcInputValue.Choice("curve_z", "a"),
        }.Concat(more))
            baseline[v.Symbol] = v;

        return new CalcInputSet([.. baseline.Values]);
    }

    [Fact]
    public void A_Member_Interaction_With_The_Default_K_Under_Heavy_Compression_Is_Computed_With_The_Annex_Bound_And_Fails()
    {
        // python3: A = 40 cm2, I = 1200 cm4, L = 3 m, E = 210 GPa, f_y = 355 MPa, curve a (alpha 0.21), N = 700 kN, M_y = 20 kN·m, W_pl,y = 150 cm3:
        //   lambda = 0.71683, chi = 0.83970, N_b = 1192.37 kN, n = 0.58707, M_y,b = 53.25 kN·m.
        //   k = 1 gave u = 0.58707 + 1 × 20/53.25 = 0.96265 (a pass, with a warning that the bound is higher);
        //   the bound 1 + 0.8 n = 1.46965 gives u = 0.58707 + 1.46965 × 20/53.25 = 1.13905.
        var calc = new MemberChecksCalculator();
        var r = CalcRunner.Run(calc, ShsMember());
        Assert.False(r.IsBlocked);
        Assert.Equal(1.1390474, r.OutputSi("u_member"), 6);
        Assert.Equal(1.4696523, r.OutputSi("k_yy_eff"), 6);

        var member = Assert.Single(r.Checks, c => c.Name.StartsWith("Member interaction", StringComparison.Ordinal));
        Assert.False(member.Passed);
        Assert.False(r.Passed);

        // The warning stays, and now says the bound was used.
        var warning = Assert.Single(r.Notices, n => n.RuleId == "k-default");
        Assert.StartsWith("k_yy:", warning.Message, StringComparison.Ordinal);
        Assert.Contains("1.470", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// C-2 verification F1: the bound C_m(1 + 0.8n) replaced a k left at 1.0 only above N_Ed/N_b,Rd = 0.3, so a member at 0.297 passed on k = 1 and failed on the bound it warned about.
    /// python3 (SHS A 40 cm2, I 1200 cm4, W_pl,y 150 cm3, S355, E 210 GPa, curve a, L_cr 4.2 m, N 280 kN, M_y 37 kN·m): N_cr = 1409.94 kN, lambda = 1.00356, chi = 0.66311,
    /// N_b = 941.62 kN, n = 0.297359, M_y,b = 53.25 kN·m; u with k = 1: 0.297359 + 37/53.25 = 0.992195 (PASS); bound 1 + 0.8n = 1.237888, u = 0.297359 + 1.237888 × 37/53.25 = 1.157488 (FAIL).
    /// </summary>
    [Fact]
    public void A_Member_Below_The_Old_Threshold_With_The_Default_K_Is_Computed_With_The_Annex_Bound_And_Fails()
    {
        var calc = new MemberChecksCalculator();
        var inputs = ShsMember(CalcInputValue.Quantity("N_Ed", -280, "kN"), CalcInputValue.Quantity("M_y_Ed", 37, "kN*m"), CalcInputValue.Quantity("L_cr_y", 4.2, "m"), CalcInputValue.Quantity("L_cr_z", 4.2, "m"));
        var r = CalcRunner.Run(calc, inputs);

        Assert.False(r.IsBlocked);
        Assert.True(r.OutputSi("u_buckling") < 0.3);
        Assert.Equal(0.2973593, r.OutputSi("u_buckling"), 5);
        Assert.Equal(1.2378875, r.OutputSi("k_yy_eff"), 5);
        Assert.Equal(1.1574877, r.OutputSi("u_member"), 5);
        Assert.False(Assert.Single(r.Checks, c => c.Name.StartsWith("Member interaction", StringComparison.Ordinal)).Passed);
        Assert.False(r.Passed);
        Assert.StartsWith("k_yy:", Assert.Single(r.Notices, n => n.RuleId == "k-default").Message, StringComparison.Ordinal);

        // A k = 1 the person recorded a basis for is theirs, as before: 0.992195 passes and the sheet still says the bound is higher.
        var assumed = CalcRunner.Run(calc, inputs.With(CalcInputValue.Of("k_yy", 1.0, CalcProvenance.Assumed("Annex B, C_my = 0.4: k_yy is near 1 for this moment diagram."))));
        Assert.Equal(0.9921950, assumed.OutputSi("u_member"), 5);
        Assert.True(assumed.Passed);
        Assert.Contains(assumed.Notices, n => n.RuleId == "k-default");

        // Any compression with a moment: n = 0.05 still takes the bound 1.04 (it was k = 1 below 0.3).
        var light = CalcRunner.Run(calc, ShsMember(CalcInputValue.Quantity("N_Ed", -50, "kN"), CalcInputValue.Quantity("M_y_Ed", 20, "kN*m"), CalcInputValue.Quantity("L_cr_y", 2, "m"), CalcInputValue.Quantity("L_cr_z", 2, "m")));
        Assert.True(light.OutputSi("u_buckling") < 0.1);
        Assert.Equal(1 + 0.8 * light.OutputSi("u_buckling"), light.OutputSi("k_yy_eff"), 9);
        Assert.Contains(light.Notices, n => n.RuleId == "k-default");
    }

    [Fact]
    public void A_K_Recorded_As_An_Assumption_Or_Entered_Other_Than_One_Is_Used_As_Entered()
    {
        var calc = new MemberChecksCalculator();

        // Entered by the user with a recorded basis: the user's k = 1.0 is kept (python3: 0.58707 + 20/53.25 = 0.96265) and the sheet still says the bound is higher.
        var assumed = CalcRunner.Run(calc, ShsMember(CalcInputValue.Of("k_yy", 1.0, CalcProvenance.Assumed("Annex B, C_my = 0.4: k_yy is near 1 for this moment diagram."))));
        Assert.Equal(0.9626522, assumed.OutputSi("u_member"), 6);
        Assert.Contains(assumed.Notices, n => n.RuleId == "k-default");

        // Entered as 1.3034: python3 0.58707 + 1.3034 × 20/53.25 = 1.07661.
        var entered = CalcRunner.Run(calc, ShsMember(CalcInputValue.Of("k_yy", 1.3034)));
        Assert.Equal(1.0766052, entered.OutputSi("u_member"), 6);
        Assert.DoesNotContain(entered.Notices, n => n.RuleId == "k-default");
    }

    // ---- elec.voltage-drop temperature ------------------------------------------------------------------------------------------------------

    private static CalcInputSet ThreePhase(params CalcInputValue[] more)
    {
        var calc = new VoltageDropCalculator();
        var d = calc.Template.TestCases[1].Inputs.ToDictionary(v => v.Symbol);

        foreach (var v in more)
            d[v.Symbol] = v;

        return new CalcInputSet([.. d.Values]);
    }

    [Fact]
    public void The_Voltage_Drop_Is_Computed_At_The_Estimated_Conductor_Temperature_When_That_Is_Above_The_Assumed_One()
    {
        // python3: 3 phase 400 V, 100 A, 80 m, 35 mm2, rho_20 0.01724, alpha 0.00393, X' 0.08 ohm/km, cos 0.85, I_z 119 A, rated 70 degC, ambient 30 degC:
        //   T_est = 30 + 40 (100/119)^2 = 58.2466 degC.   With T_c = 50 degC entered: rho = 0.019273, dV = 7.06942 V (1.7674 %), loss 1321.55 W (before).
        //   At the estimated temperature: rho = 0.019831, R = 0.045329 ohm, dV = 7.25744 V (1.8144 %), loss 1359.86 W.
        var calc = new VoltageDropCalculator();
        var r = CalcRunner.Run(calc, ThreePhase(CalcInputValue.Quantity("T_c", 50, "degC"), CalcInputValue.Of("dv_limit", 1.8)));
        Assert.False(r.IsBlocked);
        Assert.Equal(58.24659, r.OutputSi("T_c_est") - 273.15, 4);
        Assert.Equal(7.257440, r.OutputSi("dV"), 5);
        Assert.Equal(1.814360, r.OutputSi("dV_pct"), 5);
        Assert.Equal(1359.8626, r.OutputSi("P_loss"), 3);

        // 1.7674 % passed a 1.8 % limit at the assumed 50 degC; at the temperature the cable reaches it is 1.8144 %, a fail.
        Assert.False(r.Checks.Single(c => c.Name.StartsWith("Voltage drop", StringComparison.Ordinal)).Passed);
        Assert.False(r.Passed);
        Assert.Contains(r.Notices, n => n.RuleId == "temperature");
    }

    [Fact]
    public void A_Conductor_Temperature_At_Or_Above_The_Estimate_Keeps_Its_Drop()
    {
        // T_c = 70 degC entered is above the estimate (58.25): the drop stays at 70 degC, 7.52542 V (python3: R = 0.047150, X = 0.0064, sqrt(3) × 100 × (R cos + X sin)).
        var calc = new VoltageDropCalculator();
        var r = CalcRunner.Run(calc, ThreePhase());
        Assert.Equal(7.5254153, r.OutputSi("dV"), 5);
        Assert.DoesNotContain(r.Notices, n => n.RuleId == "temperature");
    }

    // ---- mech.spring buckling ---------------------------------------------------------------------------------------------------------------

    internal static CalcInputSet SlenderSpringInputs(double f2) => CalcTemplateHasher.DefaultInputs(new SpringCalculator().Template)
        .With(CalcInputValue.Quantity("D", 20, "mm")).With(CalcInputValue.Of("n_active", 10)).With(CalcInputValue.Choice("ends", SpringCalculator.PlainGround))
        .With(CalcInputValue.Quantity("L0", 80, "mm")).With(CalcInputValue.Of("nu_seat", 1)).With(CalcInputValue.Quantity("F_1", 8, "N")).With(CalcInputValue.Quantity("F_2", f2, "N"));

    private static CalcRunResult SlenderSpring(double f2) => CalcRunner.Run(new SpringCalculator(), SlenderSpringInputs(f2));

    [Fact]
    public void A_Compression_Spring_That_Reaches_Its_Buckling_Deflection_Fails_A_Check()
    {
        // python3: d 2 mm, D 20 mm, n 10, L0 80 mm, nu 1, E 206, G 80 GPa: k = 2.000 N/mm; lambda = 4; C1' = 0.81746, C2' = 6.7959;
        //   y_cr = 80 × 0.81746 × (1 − sqrt(1 − 6.7959/16)) = 15.795 mm.  F_2 = 60 N: s_2 = 30.000 mm, u_b = 1.8993 (the warning always said "reaches").
        //   F_2 = 24 N: s_2 = 12 mm, u_b = 0.7597, a pass.
        var over = SlenderSpring(60);
        Assert.False(over.IsBlocked);
        Assert.Equal(1.899334, over.OutputSi("u_b"), 5);
        var check = Assert.Single(over.Checks, c => c.Name.StartsWith("Working deflection against the buckling", StringComparison.Ordinal));
        Assert.False(check.Passed);
        Assert.False(over.Passed);
        Assert.Contains(over.Notices, n => n.RuleId == "buckling");

        var ok = SlenderSpring(24);
        Assert.Equal(0.759734, ok.OutputSi("u_b"), 5);
        Assert.True(Assert.Single(ok.Checks, c => c.Name.StartsWith("Working deflection against the buckling", StringComparison.Ordinal)).Passed);
    }

    [Fact]
    public void A_Short_Spring_That_Is_Stable_At_Any_Deflection_Passes_The_Buckling_Check_With_No_Utilisation()
    {
        var spring = new SpringCalculator();
        var r = CalcRunner.Run(spring, CalcTemplateHasher.DefaultInputs(spring.Template));
        Assert.Equal(0, r.OutputSi("u_b"));
        Assert.True(Assert.Single(r.Checks, c => c.Name.StartsWith("Working deflection against the buckling", StringComparison.Ordinal)).Passed);
    }

    // ---- mech.shaft critical-speed ----------------------------------------------------------------------------------------------------------

    /// <summary>The default shaft with room in its slope and deflection limits, so that only the critical speed can fail.</summary>
    internal static CalcInputSet RoomyShaft() => CalcTemplateHasher.DefaultInputs(new ShaftCalculator().Template).With(CalcInputValue.Quantity("y_max", 1, "mm")).With(CalcInputValue.Quantity("theta_max", 0.01, "rad"));

    [Fact]
    public void A_Running_Speed_Above_Half_The_First_Critical_Speed_Fails_A_Check()
    {
        // python3: d 30 mm, L 400 mm, E 207 GPa, one 20 kg mass at mid-span: y = m g L^3/(48 E I) = 0.031776 mm; omega = sqrt(g/y) = 555.6 rad/s = 5305.17 rpm.
        //   At 4000 rpm the critical speed is 5305.17/4000 = 1.3263 times the operating speed, below the required 2: the sheet only warned and passed.
        //   At 1500 rpm it is 3.5368 (a pass).
        var shaft = new ShaftCalculator();
        var roomy = RoomyShaft();
        var fast = CalcRunner.Run(shaft, roomy.With(CalcInputValue.Quantity("n", 4000, "rpm")));
        Assert.False(fast.IsBlocked);
        Assert.Equal(1.3263, fast.OutputSi("r_c"), 3);
        var check = Assert.Single(fast.Checks, c => c.Name.StartsWith("Critical speed", StringComparison.Ordinal));
        Assert.False(check.Passed);
        Assert.False(fast.Passed);
        Assert.Contains(fast.Notices, n => n.RuleId == "critical-speed");

        var slow = CalcRunner.Run(shaft, roomy);
        Assert.Equal(3.5368, slow.OutputSi("r_c"), 3);
        Assert.True(Assert.Single(slow.Checks, c => c.Name.StartsWith("Critical speed", StringComparison.Ordinal)).Passed);
        Assert.True(slow.Passed);
    }

    // ---- elec.motor-sizing speed-above-rated ------------------------------------------------------------------------------------------------

    /// <summary>The continuous-duty case at 3000 rpm against its 2850 rpm nameplate.</summary>
    internal static CalcInputSet MotorAboveRated()
    {
        var d = new MotorSizingCalculator().Template.TestCases[2].Inputs.ToDictionary(v => v.Symbol);
        d["n_L"] = CalcInputValue.Quantity("n_L", 750, "rpm");
        return new CalcInputSet([.. d.Values]);
    }

    [Fact]
    public void A_Motor_Speed_Above_The_Rated_Speed_Fails_A_Check()
    {
        // python3: n_L 750 rpm at i = 4 gives n_pk = 3000 rpm against the 2850 rpm nameplate: u = 3000/2850 = 1.05263, a fail (the sheet only warned).
        var motor = new MotorSizingCalculator();
        var d = motor.Template.TestCases[2].Inputs.ToDictionary(v => v.Symbol);
        var above = CalcRunner.Run(motor, MotorAboveRated());
        Assert.False(above.IsBlocked);
        Assert.Equal(1.0526316, above.OutputSi("u_rated_speed"), 6);
        var check = Assert.Single(above.Checks, c => c.Name.StartsWith("Motor speed against the rated speed", StringComparison.Ordinal));
        Assert.False(check.Passed);
        Assert.False(above.Passed);
        Assert.Contains(above.Notices, n => n.RuleId == "speed-above-rated");

        // At the nameplate (n_L · i = n_rated, which floating point may land a hair above) the check passes.
        d["n_L"] = CalcInputValue.Quantity("n_L", 360, "rpm");
        d["n_rated"] = CalcInputValue.Quantity("n_rated", 1440, "rpm");
        var at = CalcRunner.Run(motor, new CalcInputSet([.. d.Values]));
        Assert.True(Assert.Single(at.Checks, c => c.Name.StartsWith("Motor speed against the rated speed", StringComparison.Ordinal)).Passed);
    }
}
