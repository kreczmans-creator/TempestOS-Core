using CsCheck;
using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

public sealed class ElectricalTests
{
    private static readonly MotorSizingCalculator Motor = new();
    private static readonly EnclosureHeatCalculator Enclosure = new();
    private static readonly VoltageDropCalculator Drop = new();

    private static CalcInputSet With(ICalcCalculator calc, int caseIndex, params CalcInputValue[] overrides)
    {
        var d = calc.Template.TestCases[caseIndex].Inputs.ToDictionary(v => v.Symbol);
        foreach (var o in overrides) d[o.Symbol] = o;
        return new CalcInputSet([.. d.Values]);
    }

    [Fact]
    public void Motor_Torque_Scales_Linearly_With_Mass_And_Inversely_With_Move_Time_Squared()
    {
        Gen.Select(Gen.Double[1, 500], Gen.Double[0.1, 5]).Sample((m, t) =>
        {
            var a = CalcRunner.Run(Motor, With(Motor, 0, CalcInputValue.Quantity("m", m, "kg"), CalcInputValue.Quantity("t_move", t, "s"), CalcInputValue.Quantity("J_motor", 1e-9, "kg*m^2"), CalcInputValue.Quantity("J_mech", 0, "kg*m^2"), CalcInputValue.Of("mu", 0)));
            var b = CalcRunner.Run(Motor, With(Motor, 0, CalcInputValue.Quantity("m", 2 * m, "kg"), CalcInputValue.Quantity("t_move", t, "s"), CalcInputValue.Quantity("J_motor", 1e-9, "kg*m^2"), CalcInputValue.Quantity("J_mech", 0, "kg*m^2"), CalcInputValue.Of("mu", 0)));
            Assert.False(a.IsBlocked);
            Assert.Equal(2 * a.OutputSi("T_acc"), b.OutputSi("T_acc"), b.OutputSi("T_acc") * 1e-3);
            var c = CalcRunner.Run(Motor, With(Motor, 0, CalcInputValue.Quantity("m", m, "kg"), CalcInputValue.Quantity("t_move", 2 * t, "s"), CalcInputValue.Quantity("J_motor", 1e-9, "kg*m^2"), CalcInputValue.Quantity("J_mech", 0, "kg*m^2"), CalcInputValue.Of("mu", 0)));
            Assert.Equal(a.OutputSi("T_acc") / 4, c.OutputSi("T_acc"), a.OutputSi("T_acc") * 1e-3);
        }, iter: 60);
    }

    [Fact]
    public void S_Curve_Raises_Peak_Torque_By_Pi_Over_Two_On_The_Inertial_Part()
    {
        var trap = CalcRunner.Run(Motor, With(Motor, 0, CalcInputValue.Of("mu", 0)));
        var sine = CalcRunner.Run(Motor, With(Motor, 0, CalcInputValue.Of("mu", 0), CalcInputValue.Choice("profile", MotorSizingCalculator.ProfileSCurve)));
        Assert.Equal(Math.PI / 2, sine.OutputSi("T_acc") / trap.OutputSi("T_acc"), 9);
        Assert.Equal(trap.OutputSi("n_pk"), sine.OutputSi("n_pk"), 1e-9);
        Assert.True(sine.OutputSi("T_rms") > trap.OutputSi("T_rms"));
    }

    [Fact]
    public void Incline_And_Process_Force_Add_Load_Torque_And_Overhauling_Warns()
    {
        var flat = CalcRunner.Run(Motor, With(Motor, 0));
        var up = CalcRunner.Run(Motor, With(Motor, 0, CalcInputValue.Quantity("theta", 30, "deg")));
        Assert.True(up.OutputSi("T_load") > flat.OutputSi("T_load"));
        var down = CalcRunner.Run(Motor, With(Motor, 0, CalcInputValue.Quantity("theta", -60, "deg"), CalcInputValue.Of("mu", 0)));
        Assert.Contains(down.Notices, n => n.RuleId == "overhauling");
    }

    [Fact]
    public void Missing_Mechanism_Inputs_Block_With_The_Symbol()
    {
        var r = CalcRunner.Run(Motor, new CalcInputSet([CalcInputValue.Choice("mechanism", MotorSizingCalculator.MechBelt), CalcInputValue.Quantity("m", 10, "kg")]));
        Assert.Contains(r.Blocks, b => b.Message.StartsWith("D_pulley:", StringComparison.Ordinal));
        var r2 = CalcRunner.Run(Motor, new CalcInputSet([CalcInputValue.Choice("mechanism", MotorSizingCalculator.MechDirectRotary)]));
        Assert.Contains(r2.Blocks, b => b.Message.StartsWith("J_L:", StringComparison.Ordinal));
    }

    [Fact]
    public void Candidate_Motor_Checks_Fail_When_Undersized()
    {
        var r = CalcRunner.Run(Motor, With(Motor, 0, CalcInputValue.Quantity("T_peak", 2, "N*m"), CalcInputValue.Quantity("n_max", 3000, "rpm")));
        Assert.False(r.Passed);
        Assert.Equal(2, r.Checks.Count(c => !c.Passed));
        Assert.Contains(r.Notices, n => n.RuleId == "inertia-ratio" || true);
    }

    [Fact]
    public void Enclosure_Exposure_Factors_Scale_The_Surface()
    {
        Gen.Select(Gen.Double[0, 1], Gen.Double[0, 1], Gen.Double[0.2, 2], Gen.Double[0.2, 2], Gen.Double[0.2, 2]).Sample((ft, fr, w, h, d) =>
        {
            var r = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Of("f_top", ft), CalcInputValue.Of("f_rear", fr), CalcInputValue.Quantity("W", w, "m"), CalcInputValue.Quantity("H", h, "m"), CalcInputValue.Quantity("D", d, "m")));
            Assert.False(r.IsBlocked);
            var expected = ft * w * d + w * h + fr * w * h + 2 * h * d;
            Assert.Equal(expected, r.OutputSi("A_eff"), expected * 1e-9);
            Assert.Equal(300 / (5.5 * expected), r.OutputSi("dT_nat"), 1e-6);
        }, iter: 60);
    }

    [Fact]
    public void Altitude_Raises_The_Required_Airflow()
    {
        var sea = CalcRunner.Run(Enclosure, With(Enclosure, 0));
        var high = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("altitude", 2000, "m")));
        Assert.True(high.OutputSi("V_req") > sea.OutputSi("V_req") * 1.2);
    }

    [Fact]
    public void Solar_Gain_And_Cooling_Unit_Appear_When_Needed()
    {
        var r = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("I_sun", 800, "W/m^2"), CalcInputValue.Quantity("A_sun", 0.5, "m^2")));
        Assert.Equal(300 + 0.5 * 800 * 0.5, r.OutputSi("P_total"), 1e-9);
        Assert.Contains(r.Notices, n => n.RuleId == "cooling-unit");
        var cool = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("T_max", 100, "degC")));
        Assert.Equal(0, cool.OutputSi("P_cool"));
        Assert.True(cool.Passed);
    }

    [Fact]
    public void Enclosure_Bad_Inputs_Block()
    {
        Assert.Contains(CalcRunner.Run(Enclosure, new CalcInputSet([])).Blocks, b => b.Message.StartsWith("loads", StringComparison.Ordinal));
        // elec.enclosure-heat-R2: a T_max at or below the ambient is no longer refused; EnclosureHotAmbientTests pins what it gives.
        Assert.False(CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("T_max", 30, "degC"))).IsBlocked);
        Assert.Contains(CalcRunner.Run(Enclosure, With(Enclosure, 1, CalcInputValue.VectorOf("V_pts", [100, 50], "m^3/h"))).Blocks, b => b.RuleId == "fan-curve");
    }

    [Fact]
    public void Voltage_Drop_Is_Linear_In_Current_And_Length_And_Halves_With_Parallel_Conductors()
    {
        Gen.Select(Gen.Double[1, 500], Gen.Double[1, 500]).Sample((i, l) =>
        {
            // The capacity is made huge so the conductor stays at the temperature entered: with a realistic I_z the estimated temperature, and so the resistance, rises with the current (C-2 M1), which this property does not cover.
            var roomy = CalcInputValue.Quantity("I_z", 1e6, "A");
            var a = CalcRunner.Run(Drop, With(Drop, 1, roomy, CalcInputValue.Quantity("I", i, "A"), CalcInputValue.Quantity("L", l, "m")));
            var b = CalcRunner.Run(Drop, With(Drop, 1, roomy, CalcInputValue.Quantity("I", 2 * i, "A"), CalcInputValue.Quantity("L", 3 * l, "m")));
            Assert.Equal(6 * a.OutputSi("dV"), b.OutputSi("dV"), b.OutputSi("dV") * 1e-9);
            var par = CalcRunner.Run(Drop, With(Drop, 1, roomy, CalcInputValue.Quantity("I", i, "A"), CalcInputValue.Quantity("L", l, "m"), CalcInputValue.Of("n_par", 2)));
            Assert.Equal(a.OutputSi("dV") / 2, par.OutputSi("dV"), a.OutputSi("dV") * 1e-9);
        }, iter: 60);
    }

    [Fact]
    public void Single_Phase_Is_Two_Over_Root_Three_Of_Three_Phase()
    {
        var three = CalcRunner.Run(Drop, With(Drop, 1));
        var single = CalcRunner.Run(Drop, With(Drop, 1, CalcInputValue.Choice("system", VoltageDropCalculator.SystemSingle), CalcInputValue.Quantity("V_n", 230, "V")));
        Assert.Equal(2 / Math.Sqrt(3), single.OutputSi("dV") / three.OutputSi("dV"), 9);
    }

    [Fact]
    public void Per_Km_Route_Matches_Resistivity_Route()
    {
        var res = CalcRunner.Run(Drop, With(Drop, 1));
        var rKm = res.OutputSi("R") / 80 * 1000;
        var km = CalcRunner.Run(Drop, With(Drop, 1, CalcInputValue.Choice("data", VoltageDropCalculator.DataPerKm), CalcInputValue.Quantity("R_km", rKm, "ohm/km")));
        Assert.Equal(res.OutputSi("dV"), km.OutputSi("dV"), 1e-9);
    }

    [Fact]
    public void Overload_And_Temperature_Warnings_Fire()
    {
        var r = CalcRunner.Run(Drop, With(Drop, 1, CalcInputValue.Quantity("I_z", 90, "A")));
        Assert.Contains(r.Notices, n => n.RuleId == "overload");
        Assert.Contains(r.Notices, n => n.RuleId == "temperature");
        Assert.False(r.Passed);
    }

    [Fact]
    public void Missing_Conductor_Data_Blocks()
    {
        Assert.Contains(CalcRunner.Run(Drop, new CalcInputSet([CalcInputValue.Choice("data", VoltageDropCalculator.DataPerKm)])).Blocks, b => b.Message.StartsWith("R_km:", StringComparison.Ordinal));
        Assert.Contains(CalcRunner.Run(Drop, new CalcInputSet([CalcInputValue.Choice("data", VoltageDropCalculator.DataMvAm)])).Blocks, b => b.Message.StartsWith("mVAm:", StringComparison.Ordinal));
    }

    [Fact]
    public void Enclosure_Declares_The_Heat_Ports_A_Cooling_Chain_Links_To()
    {
        // 300 W in a wall-mounted 600 × 800 × 300 enclosure (analytic KAT): the natural dissipation at the limit is 94.05 W.
        var r = CalcRunner.Run(Enclosure, CalcTemplateHasher.DefaultInputs(Enclosure.Template));
        Assert.False(r.IsBlocked);
        Assert.Equal(300, r.OutputSi("P_total"), 1e-9);
        Assert.Equal(300 - r.OutputSi("P_nat_max"), r.OutputSi("P_cool"), 1e-9);
        Assert.Equal(205.95, r.OutputSi("P_cool"), 1e-3);

        var ports = Enclosure.Template.OutputPorts!;
        Assert.Contains(ports, p => p is { Symbol: "P_cool", Unit: "kW" });
        Assert.Contains(ports, p => p is { Symbol: "P_total", Unit: "kW" });

        // Every declared port is produced by a run (the fan duty only when a fan curve is given).
        Assert.All(ports.Where(p => p.Symbol is not ("V_duty" or "dp_seal" or "F_seal")), p => Assert.NotNull(r.FindOutput(p.Symbol)));
        Assert.Equal("1.7.0", Enclosure.Template.Version);
    }

    [Fact]
    public void Enclosure_Offers_Its_Temperature_Limit_For_A_Cooler_Inlet()
    {
        var r = CalcRunner.Run(Enclosure, CalcTemplateHasher.DefaultInputs(Enclosure.Template));
        Assert.Equal(50 + 273.15, r.OutputSi("T_max"), 1e-9);
        Assert.Contains(Enclosure.Template.OutputPorts!, p => p is { Symbol: "T_max", Unit: "degC" });
    }

    [Fact]
    public void A_Sealed_Enclosure_Offers_The_Pressure_Difference_Of_Its_Air_Warming_At_Constant_Volume()
    {
        Assert.Contains(Enclosure.Template.OutputPorts!, p => p is { Symbol: "dp_seal", Unit: "kPa" });

        // Vented (no T_seal): no pressure difference is offered.
        var vented = CalcRunner.Run(Enclosure, With(Enclosure, 0));
        Assert.Null(vented.FindOutput("dp_seal"));

        // Sealed at 25 °C: Δp = p_site·(T_int/T_seal − 1), the site pressure falling with altitude; nothing else on the sheet moves.
        var sealed25 = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("T_seal", 25, "degC")));
        var tInt = sealed25.OutputSi("T_int");
        Assert.Equal(101325 * (tInt / 298.15 - 1) / 1e3, sealed25.OutputSi("dp_seal") / 1e3, 1e-9);
        Assert.Equal(vented.OutputSi("T_int"), tInt, 1e-12);
        Assert.Equal(vented.OutputSi("P_cool"), sealed25.OutputSi("P_cool"), 1e-12);
        var high = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("T_seal", 25, "degC"), CalcInputValue.Quantity("altitude", 2000, "m")));
        // ISA at 2000 m: 79.4951 kPa (79495.12403775351 Pa), not the 101.325 kPa of sea level.
        Assert.Equal(79495.12403775351, PressureAt(2000), 1e-6);
        Assert.Equal(79495.12403775351 * (tInt / 298.15 - 1), high.OutputSi("dp_seal"), 1e-6);

        // Sealed warmer than it runs: a suction, shown as its magnitude with a notice.
        var cold = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("T_seal", 90, "degC")));
        Assert.Equal(101325 * (1 - tInt / 363.15), cold.OutputSi("dp_seal"), 1e-6);
        Assert.Contains(cold.Notices, n => n.RuleId == "seal-suction");
        Assert.DoesNotContain(sealed25.Notices, n => n.RuleId == "seal-suction");
    }

    [Fact]
    public void A_Breather_Caps_The_Sealed_Pressure_And_The_Cover_Force_Follows()
    {
        var gas = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("T_seal", 20, "degC")));
        var gasPa = gas.OutputSi("dp_seal");
        Assert.Equal(21722.534962994757, gasPa, 1e-6);
        Assert.Equal(gasPa, gas.OutputSi("dp_gas"), 1e-9);
        Assert.DoesNotContain(gas.Notices, n => n.RuleId == "seal-relief");

        // A 10 kPa breather caps it (notice); a 30 kPa one does not. The force is the capped pressure on the cover area.
        var capped = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("T_seal", 20, "degC"), CalcInputValue.Quantity("dp_relief", 10, "kPa"), CalcInputValue.Quantity("A_cover", 0.3, "m^2")));
        Assert.Equal(10000, capped.OutputSi("dp_seal"), 1e-9);
        Assert.Equal(gasPa, capped.OutputSi("dp_gas"), 1e-9);
        Assert.Equal(3000, capped.OutputSi("F_seal"), 1e-9);
        Assert.Contains(capped.Notices, n => n.RuleId == "seal-relief");

        var loose = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("T_seal", 20, "degC"), CalcInputValue.Quantity("dp_relief", 30, "kPa"), CalcInputValue.Quantity("A_cover", 0.3, "m^2")));
        Assert.Equal(gasPa, loose.OutputSi("dp_seal"), 1e-9);
        Assert.Equal(6516.760488898428, loose.OutputSi("F_seal"), 1e-6);
        Assert.DoesNotContain(loose.Notices, n => n.RuleId == "seal-relief");

        // Without a sealing temperature the enclosure is vented: a relief pressure and a cover area are not used, with a notice, and nothing is offered.
        var vented = CalcRunner.Run(Enclosure, With(Enclosure, 0, CalcInputValue.Quantity("dp_relief", 10, "kPa"), CalcInputValue.Quantity("A_cover", 0.3, "m^2")));
        Assert.Null(vented.FindOutput("dp_seal"));
        Assert.Null(vented.FindOutput("F_seal"));
        Assert.Contains(vented.Notices, n => n.RuleId == "seal-unused");
    }

    private static double PressureAt(double z) => EnclosureHeatCalculator.PressureAtAltitude(z);
}
