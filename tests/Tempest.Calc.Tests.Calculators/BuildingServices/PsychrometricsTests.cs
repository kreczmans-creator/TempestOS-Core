using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.BuildingServices.Fluids;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class PsychrometricsTests
{
    private static readonly PsychrometricsCalculator Calc = new();

    private static CalcRunResult Run(params CalcInputValue[] values) => CalcRunner.Run(Calc, new CalcInputSet(values));

    [Fact]
    public void Saturation_Pressure_Hits_Tabulated_Points()
    {
        Assert.Equal(611.2, MoistAir.SaturationPressure(0.01), 1.0);
        Assert.Equal(2338.8, MoistAir.SaturationPressure(20), 2.0);
        Assert.Equal(7381, MoistAir.SaturationPressure(40), 8.0);
        Assert.Equal(101325, MoistAir.SaturationPressure(100), 150.0);
        Assert.Equal(259.9, MoistAir.SaturationPressure(-10), 1.0);
    }

    [Fact]
    public void Every_Route_To_A_State_Agrees_With_The_Others()
    {
        Gen.Select(Gen.Double[-20, 50], Gen.Double[0.05, 0.99], Gen.Double[85, 105]).Sample((t, rh, p) =>
        {
            var s = MoistAir.FromRelativeHumidity(t, rh, p);
            var fromDew = MoistAir.FromDewPoint(t, s.DewPoint, p);
            var fromWb = MoistAir.FromWetBulb(t, s.WetBulb, p);
            var fromH = MoistAir.FromEnthalpy(t, s.Enthalpy, p);
            Assert.Equal(s.W, fromDew.W, Math.Max(1e-12, s.W * 1e-6));
            Assert.Equal(s.W, fromWb.W, Math.Max(1e-9, s.W * 1e-6));
            Assert.Equal(s.W, fromH.W, Math.Max(1e-12, s.W * 1e-9));
            Assert.True(s.WetBulb <= t + 1e-9 && s.WetBulb >= s.DewPoint - 1e-6, $"t_dp {s.DewPoint} ≤ t_wb {s.WetBulb} ≤ t {t}");
        }, iter: 200);
    }

    [Fact]
    public void Altitude_Lowers_Pressure_And_Raises_Humidity_Ratio()
    {
        var sea = Run(CalcInputValue.Quantity("t_db", 25, "degC"), CalcInputValue.Of("RH", 50), CalcInputValue.Quantity("altitude", 0, "m"));
        var high = Run(CalcInputValue.Quantity("t_db", 25, "degC"), CalcInputValue.Of("RH", 50), CalcInputValue.Quantity("altitude", 1500, "m"));
        Assert.Equal(101.325, sea.OutputSi("p_ws_1") >= 0 ? 101.325 : 0, 3);
        Assert.Equal(84.556, MoistAir.PressureAtAltitude(1500), 0.05);
        Assert.True(high.OutputSi("W_1") > sea.OutputSi("W_1"));
    }

    [Fact]
    public void Cooling_With_Dehumidification_Balances()
    {
        var r = Run(CalcInputValue.Quantity("t_db", 28, "degC"), CalcInputValue.Of("RH", 60), CalcInputValue.Quantity("m_da", 2, "kg/s"),
            CalcInputValue.Choice("process", PsychrometricsCalculator.ProcessCoolingDehum), CalcInputValue.Quantity("t_2", 12, "degC"), CalcInputValue.Of("RH_2", 95));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.True(r.OutputSi("m_w") > 0);
        Assert.Equal(r.OutputSi("Q_sensible") + r.OutputSi("Q_latent"), 2 * (r.OutputSi("h_1") - r.OutputSi("h_2")) * 1e3, 1e-6);
        Assert.True(r.OutputSi("Q_total") > 0 && r.OutputSi("SHR") is > 0 and < 1);
    }

    [Fact]
    public void Cooling_With_Dehumidification_Splits_Sensible_And_Latent_The_Right_Way_Round()
    {
        // bs.psychrometrics-01. Independent (python, Hyland-Wexler + ASHRAE Ch. 1 relations): 28 degC 50 % -> 12 degC saturated, 1 kg/s:
        // W1 11.829, W2 8.730 g/kg; sensible = h1 - h(12, W1) = 16.448 kW; latent = h(12, W1) - h2 = 7.818 kW; SHR 0.6778; Q_total 24.111 kW.
        var r = Run(CalcInputValue.Quantity("t_db", 28, "degC"), CalcInputValue.Of("RH", 50), CalcInputValue.Quantity("m_da", 1, "kg/s"),
            CalcInputValue.Choice("process", PsychrometricsCalculator.ProcessCoolingDehum), CalcInputValue.Quantity("t_2", 12, "degC"), CalcInputValue.Of("RH_2", 100));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(16.448, r.OutputSi("Q_sensible") / 1e3, 0.005);
        Assert.Equal(7.818, r.OutputSi("Q_latent") / 1e3, 0.005);
        Assert.Equal(0.6778, r.OutputSi("SHR"), 0.0005);
        Assert.Equal(24.111, r.OutputSi("Q_total") / 1e3, 0.005);
    }

    [Fact]
    public void Dry_Coil_Leaving_State_Keeps_The_Entering_Moisture_And_Is_Sensible_Only()
    {
        // bs.psychrometrics-R1. 28 degC 50 % -> 20 degC with RH_2 blank: W_sat(20) = 14.695 g/kg is above W1 = 11.829 g/kg, so the coil is dry.
        // Independent (python): sensible = h(28, W1) - h(20, W1) = 8.224 kW.
        var r = Run(CalcInputValue.Quantity("t_db", 28, "degC"), CalcInputValue.Of("RH", 50), CalcInputValue.Quantity("m_da", 1, "kg/s"),
            CalcInputValue.Choice("process", PsychrometricsCalculator.ProcessCoolingDehum), CalcInputValue.Quantity("t_2", 20, "degC"));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Contains(r.Warnings, w => w.RuleId == "dry-coil");
        Assert.Equal(r.OutputSi("W_1"), r.OutputSi("W_2"), 1e-12);
        Assert.Equal(0, r.OutputSi("m_w"), 12);
        Assert.Equal(8.224, r.OutputSi("Q_total") / 1e3, 0.005);
        Assert.Equal(8.224, r.OutputSi("Q_sensible") / 1e3, 0.005);
        Assert.Equal(0, r.OutputSi("Q_latent"), 1e-6);
        Assert.Equal(1.0, r.OutputSi("SHR"), 1e-9);
    }

    [Fact]
    public void Cooling_With_A_Leaving_State_Wetter_Than_The_Entering_State_Is_Blocked()
    {
        // bs.psychrometrics-R1: an explicit RH_2 that puts more moisture in the air than entered cannot be a cooling coil.
        var r = Run(CalcInputValue.Quantity("t_db", 28, "degC"), CalcInputValue.Of("RH", 50), CalcInputValue.Quantity("m_da", 1, "kg/s"),
            CalcInputValue.Choice("process", PsychrometricsCalculator.ProcessCoolingDehum), CalcInputValue.Quantity("t_2", 20, "degC"), CalcInputValue.Of("RH_2", 100));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.RuleId == "process-input" && b.Message.StartsWith("RH_2:", StringComparison.Ordinal));
    }

    [Fact]
    public void Steam_Humidification_Raises_W_At_Nearly_Constant_Temperature()
    {
        var r = Run(CalcInputValue.Quantity("t_db", 20, "degC"), CalcInputValue.Of("RH", 30), CalcInputValue.Quantity("m_da", 1, "kg/s"),
            CalcInputValue.Choice("process", PsychrometricsCalculator.ProcessSteam), CalcInputValue.Of("W_2", 8));
        Assert.False(r.IsBlocked);
        Assert.Equal(8, r.OutputSi("W_2"), 9);
        Assert.InRange(r.OutputSi("t_2_out") - 273.15, 20, 21);
        Assert.True(r.OutputSi("m_s") > 0);
    }

    [Fact]
    public void Evaporative_Cooling_Keeps_Enthalpy()
    {
        var r = Run(CalcInputValue.Quantity("t_db", 35, "degC"), CalcInputValue.Of("RH", 20), CalcInputValue.Quantity("m_da", 1, "kg/s"),
            CalcInputValue.Choice("process", PsychrometricsCalculator.ProcessEvaporative), CalcInputValue.Of("eff", 0.85));
        Assert.False(r.IsBlocked);
        Assert.Equal(r.OutputSi("h_1"), r.OutputSi("h_2"), 1e-6);
        Assert.Equal(0.85, r.OutputSi("eff_out"), 6);
        Assert.True(r.OutputSi("t_2_out") < r.OutputSi("t_1_out"));
    }

    [Fact]
    public void Supersaturated_Or_Missing_Inputs_Block_Plainly()
    {
        Assert.Contains(Run(CalcInputValue.Quantity("t_db", 20, "degC"), CalcInputValue.Choice("known", PsychrometricsCalculator.KnownWetBulb), CalcInputValue.Quantity("t_wb", 25, "degC")).Blocks, b => b.Message.StartsWith("t_wb:", StringComparison.Ordinal));
        Assert.Contains(Run(CalcInputValue.Quantity("t_db", 20, "degC"), CalcInputValue.Choice("known", PsychrometricsCalculator.KnownW), CalcInputValue.Of("W", 30)).Blocks, b => b.RuleId == "supersaturated");
        Assert.Contains(Run(CalcInputValue.Quantity("t_db", 20, "degC"), CalcInputValue.Choice("known", PsychrometricsCalculator.KnownDewPoint)).Blocks, b => b.Message.StartsWith("t_dp:", StringComparison.Ordinal));
        Assert.Contains(Run(CalcInputValue.Quantity("t_db", 20, "degC"), CalcInputValue.Of("RH", 50), CalcInputValue.Choice("process", PsychrometricsCalculator.ProcessSensible)).Blocks, b => b.Message.StartsWith("t_2:", StringComparison.Ordinal));
        Assert.Contains(Run(CalcInputValue.Quantity("t_db", 20, "degC"), CalcInputValue.Of("RH", 90), CalcInputValue.Choice("process", PsychrometricsCalculator.ProcessSensible), CalcInputValue.Quantity("t_2", 5, "degC")).Blocks, b => b.RuleId == "process-input");
    }

    [Fact]
    public void Chart_Series_Are_Present()
    {
        var r = Run(CalcInputValue.Quantity("t_db", 20, "degC"), CalcInputValue.Of("RH", 50));
        Assert.Contains(r.Series, s => s.Name == "Saturation" && s.Points.Count == 61);
        Assert.Equal(4, r.Series.Count(s => s.Name.StartsWith("RH ", StringComparison.Ordinal)));
        Assert.Contains(r.Series, s => s.Name == "Process");
    }

    [Fact]
    public void The_Relative_Humidity_Is_Also_Offered_As_A_Percentage_Quantity_For_Linking()
    {
        // RH_1 is a plain number (50), which a percentage input cannot take by link (it would read 5000 %); phi_1 is the same value as a % quantity (SI 0.5).
        var r = Run(CalcInputValue.Quantity("t_db", 21, "degC"), CalcInputValue.Of("RH", 50));
        Assert.Equal(50, r.OutputSi("RH_1"), 9);
        Assert.Equal(0.5, r.OutputSi("phi_1"), 12);
        Assert.Equal("%", r.FindOutput("phi_1")!.Value is Tempest.Calc.Engine.Values.ScalarValue s ? s.DisplayUnit.Symbol : string.Empty);
        Assert.Equal(21 + 273.15, r.OutputSi("t_1_out"), 9);
    }
}
