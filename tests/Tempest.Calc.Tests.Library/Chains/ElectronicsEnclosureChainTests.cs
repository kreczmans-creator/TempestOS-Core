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
/// Addendum B, §B7 chain 10: the PCB power trace E7 → the board's load budget E6 → the enclosure's heat E2 → the lid latch M8, with the lid plate S9 an independent member, from the built-in pack template.
/// The trace's I²R loss is a row of the load table (a table-cell link, W into kW); the load budget's heat is the enclosure's additional load; the enclosure's internal temperature is the latch's temperature of use.
/// S9 takes nothing: the pressure on a lid is not an output of any step here and the plate step has no temperature input. The plastic's values and the trace's board ambient are typed, and guarded against the linked
/// temperature. Every expected number was recomputed in python3 (IPC-2221 legacy fit and the resistivity relation; the load-budget heat sum; the effective-surface relation; the cantilever beam relations; the Navier series).
/// </summary>
public sealed class ElectronicsEnclosureChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All, .. MechanicalCalculators.All, .. BuildingServicesCalculators.All, .. StructuralCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.ElectronicsEnclosure, "Controller box 7", ChainInputs.ElectronicsEnclosure(host));

    /// <summary>The temperature (°C) the example types the latch's and the lid's plastic values for.</summary>
    private const double TypedForCelsius = 55;

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

        // E6: the heat released inside, Φ = Σ h·P·d·k = 1.507335 + 25 + (1 − 0.96) × 200 + 12 W = 46.507335 W (the trace's whole loss, the drivers' and display's whole input, the power stage's loss).
        Assert.Equal(1.5073351085105653 + 25 + 0.04 * 200 + 12, Si(e6, "Phi"), 9);
        Assert.Equal(46.50733510851057, Si(e6, "Phi"), 9);

        // E6 → E2, kW into W: the board's heat is the enclosure's additional load.
        Assert.Equal(Si(e6, "Phi"), InputSi(e2, "P_add"), 9);
        Assert.Equal(46.50733510851057, e2.Inputs.Single(i => i.Symbol == "P_add").Number!.Value, 9);
        Assert.Equal("W", e2.Inputs.Single(i => i.Symbol == "P_add").Unit);

        // E2: 400 × 500 × 200 against a wall: A_eff = top 0.08 + front 0.2 + two sides 0.2 = 0.48 m²; P = 4 + 46.507335 = 50.507335 W; ΔT = P/(5.5 × 0.48) = 19.1316 K; 54.1316 °C inside, 60 °C limit.
        Assert.Equal(0.48, Si(e2, "A_eff"), 12);
        Assert.Equal(50.507335108510574, Si(e2, "P_total"), 9);
        Assert.Equal(19.13156632898128, Si(e2, "dT_nat"), 9);
        Assert.Equal(54.13156632898128 + 273.15, Si(e2, "T_int"), 9);
        Assert.Equal(66, Si(e2, "P_nat_max"), 9);
        Assert.All(e2.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.DoesNotContain(e2.Warnings, w => w.RuleId == "cooling-unit");

        // E2 → M8, degC to degC: the enclosure's internal temperature is the latch's temperature of use.
        Assert.Equal(Si(e2, "T_int"), InputSi(m8, "T_use"), 9);
        Assert.Equal(54.13156632898128, m8.Inputs.Single(i => i.Symbol == "T_use").Number!.Value, 9);
        Assert.Equal("degC", m8.Inputs.Single(i => i.Symbol == "T_use").Unit);

        // M8: ε = y·h/((2/3)·l²) = 1.6667 %, P = b·h²·Es·ε/(6·l) = 4.4444 N, u = 0.5556; W = 4.7162 N; W′ = 77.063 N (α′ 70°, below the 73.3° that would lock).
        Assert.Equal(0.016666666666666666, Si(m8, "eps"), 12);
        Assert.Equal(4.444444444444445, Si(m8, "P"), 9);
        Assert.Equal(0.5555555555555556, Si(m8, "strain_util"), 12);
        Assert.Equal(4.716205239022644, Si(m8, "assembly_force"), 9);
        Assert.Equal(77.06299885691139, Si(m8, "retention_force"), 9);
        Assert.Equal(Math.Atan(1 / 0.3), Si(m8, "alpha_lock"), 12);
        Assert.All(m8.Checks, c => Assert.True(c.Passed, c.Name));

        // S9: independent — 400 × 200 × 5 mm, 1 kPa, E 2.1 GPa, ν 0.38: D = 25.5669 N·m, w_max = 0.633862 mm (w/t 0.127, limit span/250 = 0.8 mm), f₁₁ = 101.33 Hz (Navier series, python3).
        Assert.Equal(25.566853669939228, Si(s9, "D"), 9);
        Assert.Equal(6.338621520475308e-4, Si(s9, "w_max"), 1e-9);
        Assert.Equal(857302.3008882519, Si(s9, "sigma_max"), 1.0);
        Assert.Equal(101.32880394891876, Si(s9, "f1"), 1e-6);
        Assert.All(s9.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.Empty(s9.Links);
        Assert.Empty(s9.CellLinks);
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
            [("e7", "trace_Ploss", "e6", "loads"), ("e6", "Phi", "e2", "P_add"), ("e2", "T_int", "m8", "T_use")],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));
        Assert.Equal((0, "P"), (template.Bindings[0].Row, template.Bindings[0].Column));

        foreach (var b in template.Bindings.Where(b => b.Row is null))
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked: the plastic's values, the trace's board ambient and the lid's pressure are typed; the lid takes no link at all.
        Assert.Equal(2, made.Instances.Values.Sum(i => i.Links.Count));
        Assert.Equal(1, made.Instances.Values.Sum(i => i.CellLinks.Count));
        Assert.Empty(made.Instances["s9"].Links);
        Assert.Empty(made.Instances["s9"].CellLinks);
        Assert.DoesNotContain(template.Bindings, b => b.FromStep == "s9" || b.ToStep == "s9");
        Assert.DoesNotContain(template.Bindings, b => b.ToStep == "e7");
    }

    [Fact]
    public async Task The_Typed_Temperatures_Are_Guarded_Against_The_Linked_One()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (e7, e2, m8) = (made.Instances["e7"], made.Instances["e2"], made.Instances["m8"]);
        var internalC = Si(e2, "T_int") - 273.15;

        // The trace's board ambient is typed (a link from the enclosure would be a cycle) at the enclosure's ceiling, and the enclosure settles below it: the trace was judged at least as hot as it runs.
        Assert.Equal(60, e7.Inputs.Single(i => i.Symbol == "T_amb").Number!.Value, 9);
        Assert.Equal(e2.Inputs.Single(i => i.Symbol == "T_max").Number!.Value, e7.Inputs.Single(i => i.Symbol == "T_amb").Number!.Value, 9);
        Assert.True(internalC <= e7.Inputs.Single(i => i.Symbol == "T_amb").Number!.Value);

        // The latch's secant modulus and permissible strain do not follow the linked temperature: they are typed for 55 °C, which the enclosure stays below (the temperature of use is a record, not a conversion).
        Assert.True(internalC <= TypedForCelsius, $"{internalC} °C is above the {TypedForCelsius} °C the latch's plastic values are typed for");
        Assert.Equal(1500, InputSi(m8, "Es") / 1e6, 9);
        Assert.Equal(0.03, m8.Inputs.Single(i => i.Symbol == "eps_perm_once").Number!.Value, 12);
        Assert.False(m8.Inputs.Single(i => i.Symbol == "Es").Provenance is { Kind: CalcProvenanceKind.Link });
        Assert.False(m8.Inputs.Single(i => i.Symbol == "eps_perm_once").Provenance is { Kind: CalcProvenanceKind.Link });
        Assert.Contains("55 C", m8.Inputs.Single(i => i.Symbol == "Es").Provenance!.Reference, StringComparison.Ordinal);

        // The lid's modulus is typed for the same 55 °C.
        Assert.Contains("55 C", made.Instances["s9"].Inputs.Single(i => i.Symbol == "E").Provenance!.Reference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Higher_Trace_Current_Makes_Every_Downstream_Step_Stale_Refuses_The_Check_And_The_Issue_And_Updating_In_Order_Carries_The_Values_Through_While_The_Lid_Stays_Current()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var m8Before = made.Instances["m8"];

        await ChainKit.ReviseAndUpdateAsync(host, made, "e7", "I", 1.2, ["e6", "e2", "m8"]);

        var (e7, e6, e2, m8, s9) = (await Fresh(host, made, "e7"), await Fresh(host, made, "e6"), await Fresh(host, made, "e2"), await Fresh(host, made, "m8"), await Fresh(host, made, "s9"));

        // 8 A to 9.6 A: the same rise, so the same resistance; the loss goes with I²: 2.170563 W; the minimum width 1.7455 mm (u 0.8728).
        Assert.Equal(2.1705625562552138, Si(e7, "trace_Ploss"), 12);
        Assert.Equal(0.023552111070477583, Si(e7, "trace_R"), 12);
        Assert.Equal(0.8727687516920095, Si(e7, "u_w"), 12);

        // ... into the load table (row 0) ..., the budget's heat 47.170563 W, the enclosure's 51.170563 W and 54.382789 °C, and the latch's temperature of use.
        Assert.Equal(Si(e7, "trace_Ploss"), CellSi(e6, "loads", 0, "P"), 9);
        Assert.Equal(47.17056255625522, Si(e6, "Phi"), 9);
        Assert.Equal(Si(e6, "Phi"), InputSi(e2, "P_add"), 9);
        Assert.Equal(51.17056255625522, Si(e2, "P_total"), 9);
        Assert.Equal(54.382788847066365 + 273.15, Si(e2, "T_int"), 9);
        Assert.Equal(Si(e2, "T_int"), InputSi(m8, "T_use"), 9);
        Assert.NotEqual(InputSi(m8Before, "T_use"), InputSi(m8, "T_use"));

        // The typed plastic values did not follow: the strain, force and utilisation are those of the 55 °C values, unchanged, and still inside the temperature they were typed for.
        Assert.Equal(1500, InputSi(m8, "Es") / 1e6, 9);
        Assert.Equal(0.5555555555555556, Si(m8, "strain_util"), 12);
        Assert.True(Si(e2, "T_int") - 273.15 <= TypedForCelsius);

        // The lid takes nothing from the chain and is untouched: it is current, with the same result.
        Assert.False((await host.Service.StalenessAsync(s9)).IsStale);
        Assert.Equal(6.338621520475308e-4, Si(s9, "w_max"), 1e-9);
    }

    [Fact]
    public async Task Revising_The_Lid_Leaves_The_Rest_Of_The_Chain_Current_And_Revising_The_Trace_Leaves_The_Lid_Current()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var s9 = made.Instances["s9"];

        // A thinner lid: nothing is downstream of it, so nothing goes stale.
        var thinner = await host.Service.UpdateInputsAsync(s9.Id, Scale("t", 0.8)(s9.Inputs));
        Assert.True(thinner.Succeeded, thinner.Reason);
        foreach (var key in new[] { "e7", "e6", "e2", "m8", "s9" })
            Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, key);

        // The revised lid (4 mm) deflects more: 0.633862 mm × (5/4)³ = 1.238 mm against the 0.8 mm limit, so its check FAILS and the sheet says so; the other steps are unchanged.
        var lid = await Fresh(host, made, "s9");
        Assert.Equal(6.338621520475308e-4 * Math.Pow(5.0 / 4.0, 3), Si(lid, "w_max"), 1e-8);
        Assert.Contains(lid.Checks, c => !c.Passed);

        // And revising the trace: the chain goes stale (not the lid).
        var trace = made.Instances["e7"];
        var revised = await host.Service.UpdateInputsAsync(trace.Id, Scale("I", 1.2)(trace.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, "e6"))).IsStale);
        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "s9"))).IsStale);
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

        foreach (var key in new[] { "e6", "e2", "m8" })
            Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be stale");

        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "s9"))).IsStale);

        // The load budget takes the loss by a cell link and says which cell; the others are stale only because the step they are linked to is.
        var e6Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e6"))).Reasons;
        Assert.Contains(e6Reasons, r => r.Contains("loads", StringComparison.Ordinal) && r.Contains("trace_Ploss", StringComparison.Ordinal));
        var e2Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e2"))).Reasons;
        Assert.Contains(e2Reasons, r => r.Contains("'Board load budget', which 'P_add' is linked to, is itself stale", StringComparison.Ordinal));
        var m8Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "m8"))).Reasons;
        Assert.Contains(m8Reasons, r => r.Contains("'Enclosure temperature', which 'T_use' is linked to, is itself stale", StringComparison.Ordinal));

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
        Assert.Contains("Lid plate", sheet, StringComparison.Ordinal);
    }
}
