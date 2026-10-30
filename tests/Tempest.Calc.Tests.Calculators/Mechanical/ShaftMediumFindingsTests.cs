using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// M4 1.3.0: mech.shaft-01 (n_c output with an overhung mass), -02 (a shoulder is checked on both sides), -03 (x_T2 defaulted to 400 mm), -04 (carried masses add no weight, stated and optional)
/// and -05 (key bearing on the weakest yield strength). Expected values from python3 (see each test).
/// </summary>
public sealed class ShaftMediumFindingsTests
{
    private static readonly ShaftCalculator Shaft = new();

    private static CalcInputSet Base(params CalcInputValue[] overrides) => overrides.Aggregate(new CalcInputSet([.. Shaft.Template.TestCases[0].Inputs]), (set, v) => set.With(v));

    private static CalcRunResult Run(CalcInputSet inputs) => CalcRunner.Run(Shaft, inputs);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    [Fact]
    public void An_Overhung_Mass_Gives_A_Critical_Speed_Near_The_Exact_One_Not_11_Percent_High()
    {
        // Bearings at 100 and 500 mm on a 40 mm shaft of 700 mm, 5 kg at the tip, rho 7850, E 207 GPa. The consistent-mass finite-element eigenvalue (700 Euler-Bernoulli elements, python3) is
        // 7189.74 rpm; the Rayleigh quotient with the span's upward deflection counted with its sign is within a few per cent of it, where the absolute-value quotient gave 7976.6 rpm (+10.9 %).
        var r = Run(Base(ShaftCalculator.Segments((700, 40, 0)), Q("x_A", 100, "mm"), Q("x_B", 500, "mm"), ShaftCalculator.Loads((700, 0, 0, 5)), Q("rho", 7850, "kg/m^3"), Q("n", 3800, "rpm")));

        var n = r.OutputSi("n_c") * 60 / (2 * Math.PI);
        // The sheet's own lumped-node Rayleigh value is pinned at 0.1 % (mech.shaft-01 verification); it is 0.8 % above the finite-element 7189.74 rpm (a Rayleigh quotient is an upper bound) and
        // 0.24 % below a continuous-beam quadrature of the same quotient (python3, 70 000 stations, signed y: 7263.8 rpm; the absolute-value quotient gives 7999.7).
        Assert.InRange(n, 7246.6 * 0.999, 7246.6 * 1.001);
        Assert.InRange(n, 7189.742, 7189.742 * 1.01);
        Assert.True(r.OutputSi("n_c_D") * 60 / (2 * Math.PI) < n, "Dunkerley stays the lower bound");
    }

    [Fact]
    public void A_Shoulder_Is_Checked_On_Both_Sides_And_The_Lower_Fatigue_Factor_Governs()
    {
        // 50 mm with a 46 mm bore for 0-200 mm, 40 mm solid for 200-400 mm, 2 kN at the shoulder, bearings at 0 and 400, M = 200 N.m, no torque, S_ut 600 MPa, machined, 99 %:
        // S_e 168.910 (d 40) and 164.925 MPa (d 50); sigma_a 31.831 MPa on the solid side, 57.465 MPa on the hollow side: n_f 5.30647 and 2.87001 (python3).
        var shoulder = Base(ShaftCalculator.Segments((200, 50, 46), (400, 40, 0)), ShaftCalculator.Loads((200, -2, 0, 0)), Q("rho", 0, "kg/m^3"));
        var r = Run(shoulder);

        Assert.Equal(2.87001, r.OutputSi("n_f_min"), 4);

        var mirrored = Run(Base(ShaftCalculator.Segments((200, 40, 0), (400, 50, 46)), ShaftCalculator.Loads((200, -2, 0, 0)), Q("rho", 0, "kg/m^3")));
        Assert.Equal(2.87001, mirrored.OutputSi("n_f_min"), 4);
    }

    [Fact]
    public void Each_Section_Line_Prints_Its_Yield_Safety_Factor_Again()
    {
        // mech.shaft-02 (verification): the per-section record lost "n_y = ..." when the shoulder was checked on both sides; only n_y_min was printed. The Shigley Ch. 7 example (ka-shigley-7-1)
        // has one section with n_y 4.489 (S_y 565.370 MPa over the von Mises stress of sigma_a 104.91 MPa and tau_pk 40.23 MPa x 3^0.5: 565.370/sqrt(104.91^2 + 3 x 40.23^2) = 4.489, python3).
        var r = Run(new CalcInputSet([.. Shaft.Template.TestCases.Single(t => t.Id == "ka-shigley-7-1").Inputs]));

        var section = Assert.Single(r.Steps, st => st.Symbol == "X1");
        Assert.Contains("n_y = 4.489", section.Substitution, StringComparison.Ordinal);
        Assert.Equal(4.488973, r.OutputSi("n_y_min"), 5);

        // A shoulder section prints it as well (the governing side's value).
        var shoulder = Run(Base(ShaftCalculator.Segments((200, 50, 46), (400, 40, 0)), ShaftCalculator.Loads((200, -2, 0, 0)), Q("rho", 0, "kg/m^3")));
        Assert.Contains("n_y = ", Assert.Single(shoulder.Steps, st => st.Symbol == "X1").Substitution, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Section_That_Is_Not_A_Shoulder_Is_Unchanged()
    {
        // The ka-simply-supported case: uniform 30 mm shaft, no torque, M 200 N.m: sigma_a 75.46 MPa.
        var r = Run(Base());

        Assert.False(r.IsBlocked);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "torque-span");
    }

    [Fact]
    public void A_Blank_Torque_Exit_Is_The_Shaft_Length_So_A_Section_Beyond_400_mm_Carries_The_Torque()
    {
        // 40 mm shaft of 800 mm on bearings at 0 and 800, 2 kN at 400, T 1 kN.m, section at 600 (M 200 N.m): sigma_a 31.831 MPa, tau_m 79.577 MPa, S_e 168.910 MPa,
        // 1/n_f = 31.831/168.910 + sqrt(3) 79.577/600 = 0.418148, n_f 2.39137 (python3). With the torque leaving at 400 mm the 600 mm section carried none: n_f 5.30647.
        var inputs = new CalcInputSet([.. Base(ShaftCalculator.Segments((800, 40, 0)), Q("x_B", 800, "mm"), ShaftCalculator.Loads((400, -2, 0, 0)), Q("rho", 0, "kg/m^3"), Q("T", 1, "kN*m"), ShaftCalculator.Sections((600, 1, 1, 0, 0))).Values.Where(v => v.Symbol != "x_T2")]);
        var r = Run(inputs);

        Assert.Equal(2.39137, r.OutputSi("n_f_min"), 4);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "torque-span");
    }

    [Fact]
    public void A_Torque_Exit_Entered_Short_Of_A_Section_Warns_That_The_Section_Carries_No_Torque()
    {
        var r = Run(Base(ShaftCalculator.Segments((800, 40, 0)), Q("x_B", 800, "mm"), ShaftCalculator.Loads((400, -2, 0, 0)), Q("rho", 0, "kg/m^3"), Q("T", 1, "kN*m"), Q("x_T2", 400, "mm"), ShaftCalculator.Sections((600, 1, 1, 0, 0))));

        Assert.Equal(5.30647, r.OutputSi("n_f_min"), 4);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "torque-span");
        Assert.Contains("600", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Carried_Mass_Needs_A_Stated_Answer_On_Its_Weight_And_The_Option_Adds_It()
    {
        // ka-simply-supported: 2 kN and 20 kg at mid-span, massless shaft. With w_masses off (the weights entered as forces) M_max = PL/4 = 0.2 kN.m; with it on P = 2 + 20 x 9.80665/1000 = 2.196133 kN,
        // M_max 0.2196133 kN.m (python3). With it blank the sheet cannot tell: it blocked on neither (1.5.1 and before: a warning, and a pass with the weight left out).
        var off = Run(Base(CalcInputValue.Switch("w_masses", false)));
        Assert.Equal(200, off.OutputSi("M_max"), 6);
        Assert.DoesNotContain(off.Notices, n => n.RuleId == "mass-weight");

        var on = Run(Base(CalcInputValue.Switch("w_masses", true)));
        Assert.Equal(219.6133, on.OutputSi("M_max"), 4);
        Assert.Equal(1.0980665e3, on.OutputSi("R_A"), 2);
        Assert.DoesNotContain(on.Warnings, w => w.RuleId == "mass-weight");

        var blank = Run(new CalcInputSet(Base().Values.Where(v => v.Symbol != "w_masses")));
        Assert.True(blank.IsBlocked);
        var block = Assert.Single(blank.Blocks);
        Assert.Equal("mass-weight", block.RuleId);
        Assert.StartsWith("w_masses:", block.Message, StringComparison.Ordinal);
        Assert.Contains("20 kg", block.Message, StringComparison.Ordinal);
        Assert.Empty(CalcMessageShape.Problems(block.Message));

        // No carried mass, nothing to say: a blank switch is fine.
        Assert.False(Run(new CalcInputSet(Base(ShaftCalculator.Loads((200, -2, 0, 0))).Values.Where(v => v.Symbol != "w_masses"))).IsBlocked);
    }

    [Fact]
    public void The_Shafts_Own_Weight_Is_Added_As_Vertical_Load_With_The_Option()
    {
        // 30 mm x 400 mm, 7850 kg/m3: w = 7850 x pi/4 x 0.03^2 x 9.80665 = 54.4184 N/m, M_mid = wL^2/8 = 1.08837 N.m on top of PL/4 = 200 N.m (python3); no carried mass here.
        var r = Run(Base(ShaftCalculator.Loads((200, -2, 0, 0)), Q("rho", 7850, "kg/m^3"), CalcInputValue.Switch("w_masses", true)));

        Assert.Equal(201.08837, r.OutputSi("M_max"), 3);
    }

    [Fact]
    public void The_Key_Bearing_Uses_The_Weakest_Of_The_Key_The_Shaft_And_The_Hub()
    {
        // 50 mm shaft, T 1.2 kN.m, key 14 x 9, n_target 2: shear needs 2 T n/(d w 0.577 S_y,key) = 2 x 1200 x 2/(0.05 x 0.014 x 0.577 x 400e6) = 29.7 mm; bearing 4 T n/(d h S_y) with S_y = 400 MPa 53.333 mm,
        // with the shaft's 250 MPa 85.333 mm, with a 200 MPa hub 106.667 mm (python3).
        CalcInputSet Key(double shaftSy, params CalcInputValue[] more) => Base([ShaftCalculator.Segments((400, 50, 0)), ShaftCalculator.Loads((200, -1.4, 0, 0)), Q("rho", 0, "kg/m^3"), Q("T", 1.2, "kN*m"), Q("x_key", 100, "mm"), Q("w", 14, "mm"), Q("h", 9, "mm"),
            Q("L_key", 100, "mm"), Q("S_y_key", 400, "MPa"), Q("S_y", shaftSy, "MPa"), CalcInputValue.Of("n_target", 2), .. more]);

        Assert.Equal(53.3333, Run(Key(450)).OutputSi("L_key_req") * 1e3, 3);
        Assert.Equal(85.3333, Run(Key(250)).OutputSi("L_key_req") * 1e3, 3);
        Assert.Equal(106.6667, Run(Key(450, Q("S_y_hub", 200, "MPa"))).OutputSi("L_key_req") * 1e3, 3);
    }
}
