using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// The motor circuit links of C-1 batch F: chain.motor-circuit-04 (the circuit is sized on the motor's rating, not only on the duty), -07 (the supply voltage and power factor reach the voltage-drop step),
/// -R1 (the cable's insulation temperature reaches the loop-impedance and voltage-drop steps) Expected values from python3.
/// </summary>
public sealed class MotorCircuitSupplyLinksTests
{
    private static LibraryTestHost Host() => new(calculators: [.. ElectricalCalculators.All], packTemplates: CalcPackTemplates.All);

    [Fact]
    public async Task A_Bigger_Motor_On_The_Same_Duty_Gets_A_Bigger_Cable_And_Device()
    {
        // chain.motor-circuit-04: T_rated 2 N·m → 20 N·m at the 3000 rpm nameplate speed: P_rated 628.3 W → 6283.2 W, while the duty (P_rms 784.14 W) is unchanged. The linked machine is carried at the larger of the two:
        // before, 784.14 W (I_3φ 1.3727 A, 6 A device, 1.5 mm²); now 6283.185/0.97/0.85/(√3·400) = 10.9994 A, a 16 A device and 2.5 mm² on the synthetic table (python3).
        var host = Host();
        var made = await InstantiateAsync(host, CalcPackTemplates.MotorCircuit, "Conveyor motor circuit", ChainInputs.MotorCircuit(host));
        var (e1, e6, e4) = (made.Instances["e1"], made.Instances["e6"], made.Instances["e4"]);

        Assert.Equal(Si(e1, "P_rated"), InputSi(e6, "P_rated_add"), 9);
        Assert.Equal(628.3185307179587, Si(e1, "P_rated"), 9);
        Assert.Equal(1.3727254367736512, Si(e6, "I_3ph"), 6);
        Assert.Equal(6, Si(e4, "I_n"), 9);
        Assert.Equal(1.5e-6, Si(e4, "CSA"), 12);

        await ReviseAndUpdateAsync(host, made, "e1", "T_rated", 10, ["e6", "e4", "e3", "e5", "e2"]);
        var (e1Big, e6Big, e4Big) = (await Fresh(host, made, "e1"), await Fresh(host, made, "e6"), await Fresh(host, made, "e4"));

        Assert.Equal(6283.185307179587, Si(e1Big, "P_rated"), 9);
        Assert.Equal(Si(e1, "P_rms") < Si(e1Big, "P_rated") ? Si(e1Big, "P_rated") : 0, InputSi(e6Big, "P_rated_add"), 9);
        Assert.Equal(10.999389716084753, Si(e6Big, "I_3ph"), 6);
        Assert.Equal(16, Si(e4Big, "I_n"), 9);
        Assert.Equal(2.5e-6, Si(e4Big, "CSA"), 12);
    }

    [Fact]
    public async Task The_Supply_Voltage_And_Power_Factor_Of_The_Load_Budget_Are_The_Voltage_Drop_Steps()
    {
        // chain.motor-circuit-07: V_n 230 V in E3 against 400 V gave a drop 1.74 times too small (0.466 % against 0.810 %); the line voltage and the linked machine's power factor now follow E6.
        var host = Host();
        var made = await InstantiateAsync(host, CalcPackTemplates.MotorCircuit, "Conveyor motor circuit", ChainInputs.MotorCircuit(host));
        var (e6, e3) = (made.Instances["e6"], made.Instances["e3"]);

        Assert.Equal(InputSi(e6, "U_3ph"), InputSi(e3, "V_n"), 9);
        Assert.Equal(400, InputSi(e3, "V_n"), 9);
        Assert.Equal(InputSi(e6, "cosphi_add"), InputSi(e3, "cos_phi"), 9);
        Assert.Contains(e3.Links, l => l.Input.Symbol == "V_n" && l.Link.OutputSymbol == "U_L");
        Assert.Contains(e3.Links, l => l.Input.Symbol == "cos_phi" && l.Link.OutputSymbol == "pf_link");

        await ReviseAndUpdateAsync(host, made, "e6", "U_3ph", 230.0 / 400, ["e4", "e3", "e5"]);
        var (e6Low, e3Low) = (await Fresh(host, made, "e6"), await Fresh(host, made, "e3"));

        Assert.Equal(230, InputSi(e6Low, "U_3ph"), 9);
        Assert.Equal(230, InputSi(e3Low, "V_n"), 9);
        Assert.Equal(100 * Si(e3Low, "dV") / 230, Si(e3Low, "dV_pct"), 9);
    }

    [Fact]
    public async Task An_XLPE_Cable_Takes_Its_90_Degrees_Into_The_Fault_Loop_And_The_Voltage_Drop()
    {
        // chain.motor-circuit-R1: E4's insulation did not reach E5 θ or E3 T_c, which kept 70 °C: Z_s 5.4 % low for an XLPE cable (2.000 against 2.109 Ω at 100 m of 2.5/2.5 mm², type C 16 A).
        var host = Host();
        var made = await InstantiateAsync(host, CalcPackTemplates.MotorCircuit, "Conveyor motor circuit", ChainInputs.MotorCircuit(host));
        var (e4, e3, e5) = (made.Instances["e4"], made.Instances["e3"], made.Instances["e5"]);

        Assert.Equal(70 + 273.15, Si(e4, "T_op"), 9);
        Assert.Equal(70 + 273.15, InputSi(e5, "T_op"), 9);
        Assert.Equal(70 + 273.15, InputSi(e3, "T_c"), 9);

        var changed = await host.Service.UpdateInputsAsync(e4.Id, [.. e4.Inputs.Select(v => v.Symbol == "insulation" ? CalcInputValue.Choice("insulation", ConductorSizingCalculator.Xlpe) with { Provenance = v.Provenance } : v)]);
        Assert.True(changed.Succeeded, changed.Reason);

        foreach (var key in new[] { "e3", "e5" })
            Assert.True((await host.Service.UpdateLinksAsync(made.Instances[key].Id)).Succeeded, key);

        var (e3New, e5New, e4New) = (await Fresh(host, made, "e3"), await Fresh(host, made, "e5"), await Fresh(host, made, "e4"));

        Assert.Equal(90 + 273.15, Si(e4New, "T_op"), 9);
        Assert.Equal(90 + 273.15, InputSi(e5New, "T_op"), 9);
        Assert.Equal(90 + 273.15, InputSi(e3New, "T_c"), 9);

        // R_12 follows (1 + 0.00393 (θ − 20)): at the same cross-sections and length, 90 °C against 70 °C is 1.27510/1.19650 = 1.06569 times the resistance.
        Assert.Equal(Si(e5, "R_12") * (1 + 0.00393 * 70) / (1 + 0.00393 * 50), Si(e5New, "R_12"), 6);
    }
}
