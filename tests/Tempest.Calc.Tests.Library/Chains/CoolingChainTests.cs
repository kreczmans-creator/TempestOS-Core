using Tempest.Calc.BuildingServices;
using Tempest.Calc.BuildingServices.Fluids;
using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum A, §A7 cooling: E6 → E2 → B5 → B1 from the built-in pack template. A drive panel's heat is the enclosure's linked load;
/// the heat the enclosure cannot lose naturally is the exchanger's required duty; the exchanger's duty, cold-side rise and mean
/// cold-side temperature give the cooling-water pipework its flow and fluid.
/// </summary>
public sealed class CoolingChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All, .. BuildingServicesCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.Cooling, "Drive panel cooling", ChainInputs.Cooling(host));

    [Fact]
    public async Task Panel_Heat_Reaches_The_Exchanger_Duty_And_The_Cooling_Water_Flow()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (e6, e2, b5, b1) = (made.Instances["e6"], made.Instances["e2"], made.Instances["b5"], made.Instances["b1"]);

        // E6 → E2: the heat the panel's loads dissipate (kW into W) joins the enclosure's own 150 W schedule.
        Assert.Equal(Si(e6, "Phi"), InputSi(e2, "P_add"), 9);
        Assert.Equal(3510, Si(e6, "Phi"), 6);
        Assert.Equal(150 + Si(e6, "Phi"), Si(e2, "P_total"), 9);

        // E2: floor-standing 1.2 × 2.0 × 0.6 m with the back and right side covered: A_eff = 0.72 + 2.4 + 1.2 = 4.32 m², h = 5.5, 10 K allowed.
        Assert.Equal(4.32, Si(e2, "A_eff"), 9);
        Assert.Equal(5.5 * 4.32 * 10, Si(e2, "P_nat_max"), 9);
        Assert.Equal(Si(e2, "P_total") - Si(e2, "P_nat_max"), Si(e2, "P_cool"), 9);
        Assert.False(e2.Checks.Single().Passed, "natural dissipation cannot hold the limit, which is why the exchanger exists");
        Assert.Contains(e2.Warnings, w => w.RuleId == "cooling-unit");

        // E2 → B5: the shortfall is the duty the exchanger is sized for (W into kW), and sizing meets it exactly.
        Assert.Equal(Si(e2, "P_cool"), InputSi(b5, "Q_req"), 9);
        Assert.Equal(Si(e2, "P_cool"), Si(b5, "Phi"), 9);

        // E2 → B5: the air enters the coil at E2's temperature limit (45 °C) and has c_p 1006 J/(kg·K) (E2's own constant), 0.4 kg/s through the coil.
        Assert.Equal(Si(e2, "T_max"), InputSi(b5, "T_h_in"), 9);
        Assert.Equal(45, InputSi(b5, "T_h_in") - 273.15, 9);
        Assert.Contains(b5.Inputs, i => i.Symbol == "T_h_in" && CalcLink.From(i.Provenance) is { OutputSymbol: "T_max" });
        Assert.Equal(45 - Si(b5, "Phi") / (0.4 * 1006), Si(b5, "T_h_out") - 273.15, 9);
        Assert.True(Si(b5, "T_h_out") - 273.15 > 35, "the air leaves the coil above the ambient it must be cooled towards");

        // B5: the cold stream (0.25 kg/s, 4180 J/kg·K) takes that duty as a rise of Q/C_c from 22 °C.
        Assert.Equal(Si(b5, "Phi") / (0.25 * 4180), Si(b5, "dT_c"), 9);
        Assert.Equal(22 + Si(b5, "dT_c") / 2, Si(b5, "T_c_m") - 273.15, 9);
        Assert.Equal(22 + Si(b5, "dT_c"), Si(b5, "T_c_out") - 273.15, 9);

        // B5 → B1: duty, rise and mean temperature; B1 works the flow out as Φ/(ρ c_p ΔT), which is the cold side's mass flow over ρ.
        Assert.Equal(Si(b5, "Phi"), InputSi(b1, "Phi_load"), 9);
        Assert.Equal(Si(b5, "dT_c"), InputSi(b1, "dT_load"), 9);
        Assert.Equal(Si(b5, "T_c_m"), InputSi(b1, "T_fluid"), 9);
        Assert.Equal(4180, Si(b5, "cp_c"), 9);
        Assert.Equal(Si(b5, "cp_c"), InputSi(b1, "cp_load"), 9);
        Assert.Contains(b1.Inputs, i => i.Symbol == "cp_load" && CalcLink.From(i.Provenance) is { OutputSymbol: "cp_c" });
        Assert.Contains(b1.Inputs, i => i.Symbol == "T_fluid" && CalcLink.From(i.Provenance) is { OutputSymbol: "T_c_m" });
        Assert.Equal(0.25 / Water.Density(Si(b5, "T_c_m") - 273.15), Si(b1, "Q_design"), 12);
        Assert.DoesNotContain(b1.Warnings, w => w.RuleId == "heat-t");
        Assert.True(Si(b1, "T_shaft") > 0);

        Assert.All(new[] { b5, b1 }, i => Assert.All(i.Checks, check => Assert.True(check.Passed, $"{i.Name}: {check.Name}")));
    }

    [Fact]
    public async Task A_Panel_That_Cools_Itself_Leaves_The_Exchanger_Nothing_To_Size()
    {
        var host = Host();
        var inputs = ChainInputs.Cooling(host);

        // A panel with only its controls dissipates 20 W, which the enclosure's surface loses naturally: E2's cooling-unit duty is zero.
        inputs["e6"] = [.. inputs["e6"].Select(i => i.Symbol == "loads" ? LoadBudgetCalculator.Loads(("Controls", LoadBudgetCalculator.Dc, 0.2, 1, 0.9, 1, 1, 1, false)) with { Provenance = i.Provenance } : i)];
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.Cooling, await host.ProjectAsync(), "Drive panel cooling", inputs);

        Assert.Null(made);
        Assert.StartsWith("Step 'b5'", refusal);
        Assert.Contains("Q_req", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Bigger_Panel_Load_Makes_The_Chain_Stale_Blocks_The_Pack_And_Updating_Carries_The_Duty_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var duty = Si(made.Instances["b5"], "Phi");
        var flow = Si(made.Instances["b1"], "Q_design");

        // A bigger extruder drive: more heat in the panel, so every step below it moves.
        await ReviseAndUpdateAsync(host, made, "e6", ScaleCell("loads", 0, "P", 1.4), ["e2", "b5", "b1"]);

        var (e6, e2, b5, b1) = (await Fresh(host, made, "e6"), await Fresh(host, made, "e2"), await Fresh(host, made, "b5"), await Fresh(host, made, "b1"));
        Assert.True(Si(b5, "Phi") > duty);
        Assert.True(Si(b1, "Q_design") > flow);
        Assert.Equal(Si(e6, "Phi"), InputSi(e2, "P_add"), 9);
        Assert.Equal(Si(e2, "P_cool"), InputSi(b5, "Q_req"), 9);
        Assert.Equal(Si(b5, "Phi"), InputSi(b1, "Phi_load"), 9);
        Assert.Equal(Si(b5, "dT_c"), InputSi(b1, "dT_load"), 9);
    }

    [Fact]
    public async Task The_Chain_Checks_And_Issues_As_One_Bundle_With_Every_Link_On_The_Sheet()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.Cooling, "MCC-COOL-001", "Client Co", "Checked against SPEC-MCC-03.");

        Assert.Contains("link: Panel load budget rev A · Phi", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Enclosure heat rev A · P_cool", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cooling-water air coil rev A · Phi", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cooling-water air coil rev A · dT_c", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cooling-water air coil rev A · T_c_m", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cooling-water air coil rev A · cp_c", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Enclosure heat rev A · T_max", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Pack_Says_The_Coil_Is_Sensible_Only_And_Its_Pipework_Step_Warns_About_Plant_Loss_And_A_Glycol_Fluid()
    {
        // chain.cooling-01, -02, -03, -04: sensible-only duty, no coil or valve loss, glycol reaching B1 as c_p only, a hot ambient blocking E2. The text says so; the steps warn at run time.
        var text = CalcPackTemplates.Cooling.Description;
        Assert.Contains("sensible only", text, StringComparison.Ordinal);
        Assert.Contains("pipe friction only", text, StringComparison.Ordinal);
        Assert.Contains("custom fluid", text, StringComparison.Ordinal);
        Assert.Contains("hot ambient", text, StringComparison.Ordinal);

        var host = Host();
        var made = await InstantiateAsync(host);
        Assert.Contains(made.Instances["b1"].Warnings, w => w.RuleId == "plant-loss");
        Assert.DoesNotContain(made.Instances["b1"].Warnings, w => w.RuleId == "fluid-cp");
    }

    [Fact]
    public async Task A_Hot_Ambient_At_Or_Above_The_Limit_Gives_The_Coil_A_Duty_Instead_Of_Stopping_At_The_Enclosure()
    {
        // chain.cooling-04 and elec.enclosure-heat-R2: T_amb 46 degC against T_max 45 degC blocked E2 ('T_max must exceed the ambient'), so B5 and B1 got nothing. The enclosure gains h A_eff (T_amb - T_max)
        // through its walls, so the unit removes P_total + that gain. python3: P_total = 150 + 3510 = 3660 W; h A_eff = 5.5 x 4.32 = 23.76 W/K; P_cool = 3660 + 23.76 x 1 = 3683.76 W.
        var host = Host();
        var inputs = ChainInputs.Cooling(host);
        inputs["e2"] = [.. inputs["e2"].Select(i => i.Symbol switch { "T_amb" => i with { Number = 46 }, _ => i })];
        var made = await ChainKit.InstantiateAsync(host, CalcPackTemplates.Cooling, "Drive panel cooling", inputs);
        var (e2, b5, b1) = (made.Instances["e2"], made.Instances["b5"], made.Instances["b1"]);

        Assert.Equal(46, InputSi(e2, "T_amb") - 273.15, 9);
        Assert.Equal(45, Si(e2, "T_max") - 273.15, 9);
        Assert.Equal(3660, Si(e2, "P_total"), 6);
        Assert.Equal(-23.76, Si(e2, "P_nat_max"), 9);
        Assert.Equal(3683.76, Si(e2, "P_cool"), 6);
        Assert.Contains(e2.Warnings, w => w.RuleId == "limit");

        Assert.Equal(3683.76, InputSi(b5, "Q_req"), 6);
        Assert.Equal(3683.76, Si(b5, "Phi"), 6);
        Assert.Equal(Si(b5, "Phi"), InputSi(b1, "Phi_load"), 9);
        Assert.True(Si(b1, "Q_design") > 0);
    }

    [Fact]
    public async Task A_Glycol_Cold_Side_Changes_The_Pump_Flow_Through_The_Specific_Heat_Link()
    {
        var host = Host();
        var inputs = ChainInputs.Cooling(host);

        // A 30 % glycol mixture, c_p about 3700 J/(kg·K) (illustrative): the same duty and rise now need B1 to move less volume per unit mass flow.
        inputs["b5"] = [.. inputs["b5"].Select(i => i.Symbol == "cp_c" ? i with { Number = 3700 } : i)];
        var made = await ChainKit.InstantiateAsync(host, CalcPackTemplates.Cooling, "Drive panel cooling", inputs);
        var (b5, b1) = (made.Instances["b5"], made.Instances["b1"]);

        Assert.Equal(3700, InputSi(b1, "cp_load"), 9);
        Assert.Equal(Si(b5, "Phi") / (0.25 * 3700), Si(b5, "dT_c"), 9);
        Assert.Equal(0.25 / Water.Density(Si(b5, "T_c_m") - 273.15), Si(b1, "Q_design"), 12);
    }
}
