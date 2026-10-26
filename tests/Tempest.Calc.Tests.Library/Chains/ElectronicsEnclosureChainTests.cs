using System.Text.RegularExpressions;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B, §B7 chain 10: the PCB power trace E7 → the board's load budget E6 → the sealed enclosure's heat E2 → the cover latches M8 and the cover plate S9, from the built-in pack template.
/// The trace's I²R loss is a row of the load table (a table-cell link, W into kW); the load budget's heat is the enclosure's additional load; the enclosure's internal temperature is the latch's temperature of use;
/// and the enclosure, sealed at a stated temperature with a pressure-compensation breather, would put p0·(T_int/T_seal − 1) across its walls, limited to the breather's cracking pressure: that is the pressure on the cover plate and,
/// times the cover area, the force the latches must hold. The plastic's and the alloy's values and the trace's board ambient are typed for the
/// enclosure's limit (60 °C), and the enclosure step's own check guards them at run time. Every expected number was recomputed in python3 (IPC-2221 legacy fit and the resistivity relation; the load-budget heat sum; the
/// effective-surface relation; the constant-volume gas relation; the cantilever beam relations; the Navier series).
/// </summary>
public sealed class ElectronicsEnclosureChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All, .. MechanicalCalculators.All, .. BuildingServicesCalculators.All, .. StructuralCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.ElectronicsEnclosure, "Controller box 7", ChainInputs.ElectronicsEnclosure(host));

    [Fact]
    public async Task Values_Flow_Down_Every_Link_Of_The_Chain_With_Their_Units()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.ElectronicsEnclosure));
        var made = await InstantiateAsync(host);
        var (e7, e6, e2, m8, s9) = (made.Instances["e7"], made.Instances["e6"], made.Instances["e2"], made.Instances["m8"], made.Instances["s9"]);
        Assert.Equal(5, made.Pack.InstanceIds.Count);

        // E7: A = [8/(0.048·30^0.44)]^(1/0.725) = 147.285 mil², 2 oz = 2.756 mil, minimum width 1.35742 mm against 2 mm drawn (u 0.6787); R at 90 °C, ρ = 0.01724 (1 + 0.00393 × 70) = 0.0219827 Ω·mm²/m:
        // R = ρ·L/(w·t) = 23.5521 mΩ, 0.188417 V, P = 64 × R = 1.507335 W (python3).
        Assert.Equal(147.28522041733675 * 25.4e-6 * 25.4e-6, Si(e7, "A_req"), 15);
        Assert.Equal(1.3574182142962097e-3, Si(e7, "w_min"), 12);
        Assert.Equal(0.6787091071481048, Si(e7, "u_w"), 12);
        Assert.Equal(0.023552111070477583, Si(e7, "trace_R"), 12);
        Assert.Equal(0.18841688856382066, Si(e7, "vdrop"), 12);
        Assert.Equal(1.5073351085105653, Si(e7, "trace_Ploss"), 12);
        Assert.Equal(8 * 8 * Si(e7, "trace_R"), Si(e7, "trace_Ploss"), 12);
        Assert.All(e7.Checks, c => Assert.True(c.Passed, c.Name));

        // E7 → E6, a table-cell link: the loss (W) is row 0, column P (kW) of the load table, the only link of E6.
        Assert.Equal(Si(e7, "trace_Ploss"), CellSi(e6, "loads", 0, "P"), 9);
        Assert.Equal(1.5073351085105653e-3, e6.Inputs.Single(i => i.Symbol == "loads").Rows![0].Get("P")!.Number!.Value, 12);
        Assert.Equal("kW", e6.Inputs.Single(i => i.Symbol == "loads").Rows![0].Get("P")!.Unit);
        Assert.Equal([(0, new CalcLink(e7.Id, "trace_Ploss", "A"))], e6.CellLinks.Select(l => (l.Row, l.Link)));
        Assert.All(e6.CellLinks, l => Assert.Equal(("loads", "P"), (l.Symbol, l.Column)));
        Assert.Empty(e6.Links);

        // E6: the heat released inside, Φ = Σ h·P·d·k + P_DC (1/η_PSU − 1) = 1.507335 + 25 + (1 − 0.96) × 200 + 12 + 238.507335 × (1/0.9 − 1) W = 73.008150 W (the trace's whole loss, the drivers' and display's whole input,
        // the power stage's loss, and the supply's own loss, which the sheet adds itself since 1.4.0: elec.load-budget-05; the typed 26.5 W row it replaces was 0.0008 W low) (python3).
        var dcDraw = 1.5073351085105653 + 25 + 200 + 12;
        Assert.Equal(1.5073351085105653 + 25 + 0.04 * 200 + 12 + dcDraw * (1 / 0.9 - 1), Si(e6, "Phi"), 9);
        Assert.Equal(73.0081501205673, Si(e6, "Phi"), 9);

        // E6 → E2, kW into W: the board's heat is the enclosure's additional load.
        Assert.Equal(Si(e6, "Phi"), InputSi(e2, "P_add"), 9);
        Assert.Equal(0.0730081501205673, e2.Inputs.Single(i => i.Symbol == "P_add").Number!.Value, 12);
        Assert.Equal("kW", e2.Inputs.Single(i => i.Symbol == "P_add").Unit);

        // E2: 500 × 600 × 200 against a wall: A_eff = top 0.1 + front 0.3 + two sides 0.24 = 0.64 m²; P = 4 + 73.008150 = 77.008150 W; ΔT = P/(5.5 × 0.64) = 21.8773 K; 56.8773 °C inside, 60 °C limit.
        Assert.Equal(0.64, Si(e2, "A_eff"), 12);
        Assert.Equal(77.0081501205673, Si(e2, "P_total"), 9);
        Assert.Equal(21.877315375161164, Si(e2, "dT_nat"), 9);
        Assert.Equal(56.87731537516116 + 273.15, Si(e2, "T_int"), 9);
        Assert.Equal(88, Si(e2, "P_nat_max"), 9);
        Assert.All(e2.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.DoesNotContain(e2.Warnings, w => w.RuleId == "cooling-unit");

        // E2, sealed at 25 °C: the gas-law difference is 101.325 kPa × (T_int/T_seal − 1) = 101.325 × (330.027315/298.15 − 1) = 10.83337 kPa, but the 1 kPa breather opens first: dp_seal = 1 kPa (notice), and on the 0.3 m² cover it is 300 N.
        Assert.Equal(101325 * ((56.87731537516116 + 273.15) / 298.15 - 1), Si(e2, "dp_gas"), 6);
        Assert.Equal(10833.369043730354, Si(e2, "dp_gas"), 6);
        Assert.Equal(1000, Si(e2, "dp_seal"), 9);
        Assert.Contains(e2.Warnings, w => w.RuleId == "seal-relief");
        Assert.Equal(300, Si(e2, "F_seal"), 9);
        Assert.Equal(Si(e2, "dp_seal") * InputSi(e2, "A_cover"), Si(e2, "F_seal"), 9);

        // E2 → M8, degC to degC: the enclosure's internal temperature is the latch's temperature of use.
        Assert.Equal(Si(e2, "T_int"), InputSi(m8, "T_use"), 9);
        Assert.Equal(56.87731537516116, m8.Inputs.Single(i => i.Symbol == "T_use").Number!.Value, 9);
        Assert.Equal("degC", m8.Inputs.Single(i => i.Symbol == "T_use").Unit);

        // M8: ε = y·h/((2/3)·l²) = 1.6667 %, P = b·h²·Es·ε/(6·l) = 4.1481 N, u = 0.5556; W = 4.4018 N; W′ = 71.925 N (α′ 70°, below the 73.3° that would lock).
        // Six latches hold 6 × 71.925 = 431.55 N against the 300 N on the cover (E2 F_seal → M8 F_req, N into N): utilisation 0.6952.
        Assert.Equal(0.016666666666666666, Si(m8, "eps"), 12);
        Assert.Equal(4.148148148148149, Si(m8, "P"), 9);
        Assert.Equal(0.5555555555555556, Si(m8, "strain_util"), 12);
        Assert.Equal(4.401791556421135, Si(m8, "assembly_force"), 9);
        Assert.Equal(71.92546559978396, Si(m8, "retention_force"), 9);
        Assert.Equal(Math.Atan(1 / 0.3), Si(m8, "alpha_lock"), 12);
        Assert.Equal(Si(e2, "F_seal"), InputSi(m8, "F_req"), 9);
        Assert.Equal(300, m8.Inputs.Single(i => i.Symbol == "F_req").Number!.Value, 9);
        Assert.Equal("N", m8.Inputs.Single(i => i.Symbol == "F_req").Unit);
        Assert.Equal(6, InputSi(m8, "n_latch"), 12);
        Assert.Equal(6 * 71.92546559978396, Si(m8, "F_hold"), 8);
        Assert.Equal(431.5527935987038, Si(m8, "F_hold"), 8);
        Assert.Equal(300 / 431.5527935987038, Si(m8, "u_hold"), 12);
        Assert.True(6 * Si(m8, "retention_force") >= Si(e2, "dp_seal") * InputSi(e2, "A_cover"));
        Assert.Contains(m8.Checks, c => c.Name.StartsWith("Latches hold the load", StringComparison.Ordinal));
        Assert.All(m8.Checks, c => Assert.True(c.Passed, c.Name));

        // E2 → S9, kPa into kPa: the sealed enclosure's pressure difference, limited by the breather, is the uniform pressure on the cover.
        Assert.Equal(Si(e2, "dp_seal"), InputSi(s9, "q"), 6);
        Assert.Equal(1, s9.Inputs.Single(i => i.Symbol == "q").Number!.Value, 9);
        Assert.Equal("kPa", s9.Inputs.Single(i => i.Symbol == "q").Unit);

        // S9: the 600 × 500 × 4 mm aluminium front cover, E 69 GPa, ν 0.33: D = 412.973 N·m; w_max = 0.855161 mm (w/t 0.214; limit span/250 = 2 mm, u 0.428); σ_vM 5.8992 MPa (u 0.049 of 120); f₁₁ = 65.835 Hz (Navier series, python3).
        Assert.Equal(412.9727303332959, Si(s9, "D"), 8);
        Assert.Equal(8.551608852897289e-4, Si(s9, "w_max"), 1e-9);
        // The corner twisting stress converges slowly: python3 sums 5899242.0 Pa to 401 odd terms, 5899263.5 to 2401; the sheet sums further.
        Assert.Equal(5899264.0, Si(s9, "sigma_max"), 5.0);
        Assert.Equal(65.8348745528009, Si(s9, "f1"), 1e-6);
        Assert.Equal(0.42758044264486444, Si(s9, "u_defl"), 1e-6);
        Assert.All(s9.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.Empty(s9.CellLinks);
        Assert.Equal(["q"], s9.Links.Select(l => l.Input.Symbol));
        Assert.All(made.Instances.Values, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task Every_Binding_Of_The_Template_Is_A_Link_From_The_Output_It_Names()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var template = CalcPackTemplates.ElectronicsEnclosure;

        Assert.Equal(["e7", "e6", "e2", "m8", "s9"], template.Steps.Select(s => s.Key));
        Assert.Equal(["elec.pcb-trace", "elec.load-budget", "elec.enclosure-heat", "mech.snap-fit", "struct.plate-bending"], template.Steps.Select(s => s.TemplateId));
        Assert.Equal(
            [("e7", "trace_Ploss", "e6", "loads"), ("e6", "Phi", "e2", "P_add"), ("e2", "T_int", "m8", "T_use"), ("e2", "F_seal", "m8", "F_req"), ("e2", "dp_seal", "s9", "q")],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));
        Assert.Equal((0, "P"), (template.Bindings[0].Row, template.Bindings[0].Column));

        foreach (var b in template.Bindings.Where(b => b.Row is null))
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked: the plastic's and the alloy's values, the latch count, the cover area and the trace's board ambient are typed.
        Assert.Equal(4, made.Instances.Values.Sum(i => i.Links.Count));
        Assert.Equal(1, made.Instances.Values.Sum(i => i.CellLinks.Count));
        Assert.DoesNotContain(template.Bindings, b => b.ToStep == "e7");
    }

    [Fact]
    public async Task The_Typed_Values_Carry_The_Temperature_They_Are_For_And_The_Enclosure_Steps_Own_Check_Guards_It_At_Run_Time()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (e7, e2, m8, s9) = (made.Instances["e7"], made.Instances["e2"], made.Instances["m8"], made.Instances["s9"]);
        var limit = e2.Inputs.Single(i => i.Symbol == "T_max").Number!.Value;

        // Exactly the material values carry "at NN C", and NN is the enclosure's limit; the geometry inputs carry no temperature.
        static double TemperatureOf(CalcInstance i, string symbol) => double.Parse(Regex.Match(i.Inputs.Single(x => x.Symbol == symbol).Provenance!.Reference!, @"at (\d+) C").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        foreach (var (instance, symbol) in new[] { (m8, "Es"), (m8, "eps_perm_once"), (s9, "E"), (s9, "nu"), (s9, "rho"), (s9, "f_y") })
        {
            Assert.Equal(limit, TemperatureOf(instance, symbol), 9);
            Assert.False(instance.Inputs.Single(i => i.Symbol == symbol).Provenance is { Kind: CalcProvenanceKind.Link });
        }

        foreach (var symbol in new[] { "l", "h", "b", "y" })
            Assert.DoesNotMatch(@"at \d+ C", m8.Inputs.Single(i => i.Symbol == symbol).Provenance!.Reference);

        Assert.Equal(ChainInputs.ElectronicsTypedForCelsius, limit, 9);

        // The trace's board ambient is typed (a link from the enclosure would be a cycle) at the same limit.
        Assert.Equal(limit, e7.Inputs.Single(i => i.Symbol == "T_amb").Number!.Value, 9);

        // The run-time guard: the enclosure step checks that natural dissipation holds T_max, so the typed values are good while it passes.
        var holds = Assert.Single(e2.Checks, c => c.Name.StartsWith("Natural temperature rise against the allowed rise", StringComparison.Ordinal));
        Assert.True(holds.Passed);
        Assert.True(Si(e2, "T_int") - 273.15 <= limit);
    }

    [Fact]
    public async Task The_Typed_Cover_Area_Matches_The_Cover_Plate_And_The_Latch_Count_Is_A_Typed_Whole_Number()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (e2, m8, s9) = (made.Instances["e2"], made.Instances["m8"], made.Instances["s9"]);

        // The pressure acts on the area of the cover plate: 0.6 m × 0.5 m = 0.3 m², typed in E2 and in S9's sides, and the cover is the enclosure's largest face (the 500 × 600 front, against the 500 × 200 top and the 600 × 200 sides).
        Assert.Equal(InputSi(s9, "a") * InputSi(s9, "b"), InputSi(e2, "A_cover"), 12);
        Assert.Equal(InputSi(e2, "W") * InputSi(e2, "H"), InputSi(e2, "A_cover"), 12);
        Assert.True(InputSi(e2, "A_cover") > InputSi(e2, "W") * InputSi(e2, "D"));
        Assert.True(InputSi(e2, "A_cover") > InputSi(e2, "H") * InputSi(e2, "D"));

        // The latch count is typed, not linked.
        Assert.Equal(6, InputSi(m8, "n_latch"), 12);
        Assert.False(m8.Inputs.Single(i => i.Symbol == "n_latch").Provenance is { Kind: CalcProvenanceKind.Link });
    }

    [Fact]
    public async Task Revising_The_Breather_Rating_Makes_The_Latches_And_The_Cover_Stale_And_The_Latch_Check_Fails_Above_431_N()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // 1 kPa to 1.5 kPa: still below the 10.83 kPa the gas law would give, so the breather governs; the force on the cover is 450 N against the six latches' 431.55 N: they fail; the cover deflects 1.5 times as much (linear).
        await ChainKit.ReviseAndUpdateAsync(host, made, "e2", "dp_relief", 1.5, ["m8", "s9"]);

        var (e2, m8, s9) = (await Fresh(host, made, "e2"), await Fresh(host, made, "m8"), await Fresh(host, made, "s9"));
        Assert.Equal(1500, Si(e2, "dp_seal"), 9);
        Assert.Equal(450, Si(e2, "F_seal"), 9);
        Assert.Equal(Si(e2, "dp_seal"), InputSi(s9, "q"), 6);
        Assert.Equal(Si(e2, "F_seal"), InputSi(m8, "F_req"), 9);
        Assert.Equal(8.551608852897289e-4 * 1.5, Si(s9, "w_max"), 1e-9);
        Assert.Equal(450 / 431.5527935987038, Si(m8, "u_hold"), 12);
        Assert.Contains(m8.Checks, c => !c.Passed && c.Name.StartsWith("Latches hold the load", StringComparison.Ordinal));
        Assert.All(s9.Checks, c => Assert.True(c.Passed, c.Name));
    }

    [Fact]
    public async Task A_Trace_Current_That_Takes_The_Enclosure_Past_Its_Limit_Fails_The_Enclosure_Check_Which_Is_What_Guards_The_Typed_Material_Values()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // 8 A to 24 A: the 2 mm trace is now 3.09 times too narrow (u_w 3.0887), so it runs at the rise the curve-fit gives for its drawn width, 192.37 K (elec.pcb-trace-02; it was held at the allowed 30 K before), R 35.339 mΩ, and loses 20.355 W;
        // the budget's heat is 20.355 + 45 + (20.355 + 237)/9 = 93.950 W, and the enclosure reaches 62.827 °C against the 60 °C the plastic and the alloy were typed for, and its own check fails (python3).
        var e7 = made.Instances["e7"];
        Assert.True((await host.Service.UpdateInputsAsync(e7.Id, Scale("I", 3)(e7.Inputs))).Succeeded);

        foreach (var key in new[] { "e6", "e2" })
            Assert.True((await host.Service.UpdateLinksAsync(made.Instances[key].Id)).Succeeded, key);

        var e2 = await Fresh(host, made, "e2");
        Assert.Equal(62.82677691940542 + 273.15, Si(e2, "T_int"), 6);
        Assert.Contains(e2.Checks, c => !c.Passed && c.Name.StartsWith("Natural temperature rise against the allowed rise", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_Supply_Loss_Follows_The_Loads_It_Serves_Because_The_Sheet_Adds_It_Itself()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var e6 = made.Instances["e6"];
        var rows = e6.Inputs.Single(i => i.Symbol == "loads").Rows!;

        // The 24 V supply (90 %) sits inside the enclosure: its loss is the DC loads' draw × (1/0.9 − 1), added by E6 (1.4.0) when the supply's bus and efficiency are entered; there is no typed row to keep in step.
        var dc = rows.Where(r => r.Get("supply")!.Text == "DC").Sum(r => r.Get("P")!.Number!.Value * (r.Get("d")?.Number ?? 1));
        Assert.Equal(0.23850733510851058, dc, 12);
        Assert.DoesNotContain(rows, r => r.Get("name")!.Text == "24 V supply loss");
        Assert.Equal(0.9, e6.Inputs.Single(i => i.Symbol == "eta_psu").Number!.Value, 12);
        Assert.Equal("AC single-phase", e6.Inputs.Single(i => i.Symbol == "psu_bus").Text);

        // The supply's loss in Φ is exactly the DC draw × (1/0.9 − 1): Φ less the other rows' heat (the trace's whole loss, 25 W, 8 W, 12 W).
        Assert.Equal(1.0 / 0.9 * dc * 1000 - dc * 1000, Si(e6, "Phi") - (1.5073351085105653 + 25 + 8 + 12), 9);
    }

    [Fact]
    public async Task A_Higher_Trace_Current_Makes_Every_Downstream_Step_Stale_Including_The_Lid_Refuses_The_Check_And_The_Issue_And_Updating_In_Order_Carries_The_Values_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var m8Before = made.Instances["m8"];
        var s9Before = made.Instances["s9"];

        await ChainKit.ReviseAndUpdateAsync(host, made, "e7", "I", 1.2, ["e6", "e2", "m8", "s9"]);

        var (e7, e6, e2, m8, s9) = (await Fresh(host, made, "e7"), await Fresh(host, made, "e6"), await Fresh(host, made, "e2"), await Fresh(host, made, "m8"), await Fresh(host, made, "s9"));

        // 8 A to 9.6 A: the same rise, so the same resistance; the loss goes with I²: 2.170563 W; the minimum width 1.7455 mm (u 0.8728).
        Assert.Equal(2.1705625562552138, Si(e7, "trace_Ploss"), 12);
        Assert.Equal(0.023552111070477583, Si(e7, "trace_R"), 12);
        Assert.Equal(0.8727687516920095, Si(e7, "u_w"), 12);

        // ... into the load table (row 0) ..., the budget's heat 73.745070 W, the enclosure's 77.745070 W and 57.086667 °C, and the latch's temperature of use.
        Assert.Equal(Si(e7, "trace_Ploss"), CellSi(e6, "loads", 0, "P"), 9);
        Assert.Equal(73.74506950695024, Si(e6, "Phi"), 9);
        Assert.Equal(Si(e6, "Phi"), InputSi(e2, "P_add"), 9);
        Assert.Equal(77.74506950695024, Si(e2, "P_total"), 9);
        Assert.Equal(57.08666747356541 + 273.15, Si(e2, "T_int"), 9);
        Assert.Equal(Si(e2, "T_int"), InputSi(m8, "T_use"), 9);
        Assert.NotEqual(InputSi(m8Before, "T_use"), InputSi(m8, "T_use"));

        // ... and the gas-law pressure rises to 10.904516 kPa (10.833369 before), but the 1 kPa breather governs both times, so the cover's pressure and the force on the latches do not move and the cover plate is current again after the update.
        Assert.Equal(10904.516457350379, Si(e2, "dp_gas"), 6);
        Assert.Equal(1000, Si(e2, "dp_seal"), 9);
        Assert.Equal(Si(e2, "dp_seal"), InputSi(s9, "q"), 6);
        Assert.Equal(InputSi(s9Before, "q"), InputSi(s9, "q"), 9);
        Assert.Equal(300, InputSi(m8, "F_req"), 9);
        Assert.Equal(8.551608852897289e-4, Si(s9, "w_max"), 1e-9);

        // The typed material values did not follow: the latch's strain, force and utilisation are those of its 60 °C values, unchanged, and the enclosure is still inside that temperature.
        Assert.Equal(1400, InputSi(m8, "Es") / 1e6, 9);
        Assert.Equal(0.5555555555555556, Si(m8, "strain_util"), 12);
        Assert.True(Si(e2, "T_int") - 273.15 <= 60);
        Assert.All(new[] { m8, s9 }, i => Assert.All(i.Checks, c => Assert.True(c.Passed, c.Name)));
    }

    [Fact]
    public async Task Revising_The_Cover_Leaves_The_Rest_Of_The_Chain_Current()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var s9 = made.Instances["s9"];

        // A thinner cover: nothing is downstream of it, so nothing goes stale.
        var thinner = await host.Service.UpdateInputsAsync(s9.Id, Scale("t", 0.75)(s9.Inputs));
        Assert.True(thinner.Succeeded, thinner.Reason);
        foreach (var key in new[] { "e7", "e6", "e2", "m8", "s9" })
            Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, key);

        // The revised cover (3 mm) deflects (4/3)³ = 2.370 times as much: 2.0270 mm against the 2 mm limit, so its check FAILS and the sheet says so (4 mm is the thinnest that passes).
        var lid = await Fresh(host, made, "s9");
        Assert.True(Si(lid, "w_max") > 2e-3);
        Assert.Equal(8.551608852897289e-4 * Math.Pow(4.0 / 3.0, 3), Si(lid, "w_max"), 1e-8);
        Assert.Contains(lid.Checks, c => !c.Passed);
    }

    [Fact]
    public async Task While_Stale_The_Pack_Check_And_Issue_Are_Refused_With_Reasons_That_Name_The_Stale_Step()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var e7 = made.Instances["e7"];

        var revised = await host.Service.UpdateInputsAsync(e7.Id, Scale("I", 1.2)(e7.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);

        foreach (var key in new[] { "e6", "e2", "m8", "s9" })
            Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be stale");

        // The load budget takes the loss by a cell link and says which cell; the others are stale only because the step they are linked to is.
        var e6Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e6"))).Reasons;
        Assert.Contains(e6Reasons, r => r.Contains("loads", StringComparison.Ordinal) && r.Contains("trace_Ploss", StringComparison.Ordinal));
        var e2Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e2"))).Reasons;
        Assert.Contains(e2Reasons, r => r.Contains("'Board load budget', which 'P_add' is linked to, is itself stale", StringComparison.Ordinal));
        var m8Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "m8"))).Reasons;
        Assert.Contains(m8Reasons, r => r.Contains("'Enclosure temperature', which 'T_use' is linked to, is itself stale", StringComparison.Ordinal));
        var s9Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "s9"))).Reasons;
        Assert.Contains(s9Reasons, r => r.Contains("'Enclosure temperature', which 'q' is linked to, is itself stale", StringComparison.Ordinal));
        Assert.Contains(m8Reasons, r => r.Contains("'Enclosure temperature', which 'F_req' is linked to, is itself stale", StringComparison.Ordinal));

        host.Principal = "j.reviewer";
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("Pack member 'Board load budget' is stale", check.Reason, StringComparison.Ordinal);
        Assert.Contains("Update it before checking the pack", check.Reason, StringComparison.Ordinal);

        var issue = await host.Service.IssuePackAsync(made.Pack.Id, "ELX-STALE", "Client Co");
        Assert.False(issue.Succeeded);
        Assert.Contains("check it before issuing", issue.Reason, StringComparison.Ordinal);
        Assert.NotEqual(CalcInstanceStatus.Issued, (await host.Store.FindPackAsync(made.Pack.Id))!.Status);
    }

    [Fact]
    public async Task Issue_Is_Refused_At_The_Staleness_Gate_When_The_Trace_Step_Moves_After_The_Pack_Was_Checked()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await IssueIsRefusedAtTheStalenessGateWhenTheFirstStepMovesAfterTheCheckAsync(host, made, CalcPackTemplates.ElectronicsEnclosure, "e7", "e6",
            (id, outside) => host.Service.BindCellAsync(id, "loads", 0, "P", outside, "trace_Ploss"), ["e6", "e2", "m8", "s9"]);
    }

    [Fact]
    public async Task The_Chain_Checks_And_Issues_As_One_Bundle_With_Every_Link_On_The_Sheet()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // Another engineer is recorded as the author of every step; full provenance, one check by an independent checker, one issue.
        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.ElectronicsEnclosure, "ELX-ENC-001", "Client Co", "Checked against SPEC-ELX-02.");

        // The cell link is named under its row, the top-level ones by input.
        Assert.Contains("P: link: Power trace rev A · trace_Ploss", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Board load budget rev A · Phi", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Enclosure temperature rev A · T_int", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Enclosure temperature rev A · dp_seal", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Enclosure temperature rev A · F_seal", sheet, StringComparison.Ordinal);
    }
}
