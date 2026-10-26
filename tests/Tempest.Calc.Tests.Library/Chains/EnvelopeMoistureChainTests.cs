using Tempest.Calc.BuildingServices;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B, §B7 chain 7: the room heat loss B4 and the room air state B2 → the Glaser condensation check B7, from the built-in pack template.
/// B4 offers no per-layer output, so the wall's layers are typed in both steps (guarded by a test) and the one value they share, the wall's U-value (B4 <c>U_1</c>),
/// is linked to B7's <c>U_ref</c> for a consistency check; B2 gives B7 the internal temperature (<c>t_1_out</c>) and relative humidity (<c>phi_1</c>). Every
/// expected number was recomputed in python3 (an independent Glaser: Magnus saturation, series resistance, lower-convex-hull tangent construction, twelve months
/// carried from the first condensing month).
/// </summary>
public sealed class EnvelopeMoistureChainTests
{
    private static LibraryTestHost Host() => new(calculators: BuildingServicesCalculators.All, packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.EnvelopeMoisture, "Studio 3 external wall", ChainInputs.EnvelopeMoisture(host));

    private static double Gm2(CalcInstance instance, string symbol) => Si(instance, symbol) * 1000;

    [Fact]
    public async Task Values_Flow_Down_Every_Link_Of_The_Chain_With_Their_Units()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.EnvelopeMoisture));
        var made = await InstantiateAsync(host);
        var (b4, b2, b7) = (made.Instances["b4"], made.Instances["b2"], made.Instances["b7"]);
        Assert.Equal(3, made.Pack.InstanceIds.Count);

        // B4: R = 0.13 + 0.0125/0.25 + 0.08/0.04 + 0.215/0.8 + 0.015/0.9 + 0.04 = 2.5054167 m²K/W, U = 0.39913521 W/m²K.
        var r4 = 0.13 + 0.0125 / 0.25 + 0.08 / 0.04 + 0.215 / 0.8 + 0.015 / 0.9 + 0.04;
        Assert.Equal(2.5054166666666666, r4, 12);
        Assert.Equal(r4, Si(b4, "R_1"), 12);
        Assert.Equal(1 / r4, Si(b4, "U_1"), 12);
        Assert.Equal(0.39913520705138866, Si(b4, "U_1"), 12);
        Assert.Equal(1.4, Si(b4, "U_2"), 12);

        // Φ = 18 m² × U × 24 K + 3 m² × 1.4 × 24 K + 0.34 × (0.5 × 48 m³/h) × 24 K = 172.4264 + 100.8 + 195.84 = 469.0664 W, 19.5444 W/K.
        Assert.Equal(172.4264094461999, Si(b4, "Phi_1"), 9);
        Assert.Equal(100.8, Si(b4, "Phi_2"), 9);
        Assert.Equal(273.2264094461999, Si(b4, "Phi_T"), 9);
        Assert.Equal(195.84, Si(b4, "Phi_V"), 9);
        Assert.Equal(469.06640944619994, Si(b4, "Phi_HL"), 9);
        Assert.Equal(19.544433726924996, Si(b4, "HLC"), 9);

        // The wall's U against its 0.45 target: 0.887 (the window has a U override, so only the wall is held to the target).
        var wallCheck = Assert.Single(b4.Checks);
        Assert.True(wallCheck.Passed);
        Assert.Equal(0.39913520705138866 / 0.45, Si(b4, "u_target_1"), 12);

        // B2: 21 °C, 50 % at sea level: the state's dry-bulb and its relative humidity as a percentage quantity (SI 0.5).
        Assert.Equal(21 + 273.15, Si(b2, "T_1_out"), 9);
        Assert.Equal(0.5, Si(b2, "phi_1"), 12);
        Assert.Equal(50, Si(b2, "RH_1"), 9);

        // B4 → B7, U-value (W/(m²·K)): B7's U_ref; B2 → B7, dry-bulb (K, shown in °C) and relative humidity (SI 0.5, shown in %).
        Assert.Equal(Si(b4, "U_1"), InputSi(b7, "U_ref"), 12);
        Assert.Equal(Si(b2, "T_1_out"), InputSi(b7, "T_int"), 9);
        Assert.Equal(294.15, InputSi(b7, "T_int"), 9);
        Assert.Equal(Si(b2, "phi_1"), InputSi(b7, "RH_int"), 12);
        Assert.Equal(0.5, InputSi(b7, "RH_int"), 12);
        Assert.Equal("degC", b7.Inputs.Single(i => i.Symbol == "T_int").Unit);
        Assert.Equal("%", b7.Inputs.Single(i => i.Symbol == "RH_int").Unit);
        Assert.Equal(21, b7.Inputs.Single(i => i.Symbol == "T_int").Number!.Value, 9);
        Assert.Equal(50, b7.Inputs.Single(i => i.Symbol == "RH_int").Number!.Value, 9);

        // B7: R_T = 0.13 + 0.05 + 0.0002/0.35 + 2 + 0.26875 + 0.015/0.9 + 0.04 = 2.5059881 m²K/W, U = 0.39904419 W/m²K, 0.0228 % from B4's: no warning.
        var rT = r4 + 0.0002 / 0.35;
        Assert.Equal(2.505988095238095, rT, 12);
        Assert.Equal(rT, Si(b7, "R_T"), 12);
        Assert.Equal(1 / rT, Si(b7, "U_wall"), 12);
        Assert.Equal(0.39904419414450154, Si(b7, "U_wall"), 12);
        Assert.Equal(0.00022802525379677786, Si(b7, "dU_ref"), 12);
        Assert.Equal(Math.Abs(1 / rT - 1 / r4) / (1 / r4), Si(b7, "dU_ref"), 12);
        Assert.DoesNotContain(b7.Warnings, w => w.RuleId == "u-ref-mismatch");
        Assert.Equal(6.705, Si(b7, "S_d"), 12);
        Assert.Equal(610.5 * Math.Exp(17.269 * 21 / (237.3 + 21)), Si(b7, "psat_i"), 9);
        Assert.Equal(2485.5817938155224, Si(b7, "psat_i"), 9);

        // B7: condensate on the mineral wool / brick interface (interface 3) from December: 1.7268 g/m² (Dec), 9.8141 (Jan, the peak), 7.0199 (Feb), dry from March.
        Assert.Equal(12, Si(b7, "n_start"), 9);
        Assert.Equal(1.7267954735188895, Gm2(b7, "Mc_12"), 9);
        Assert.Equal(9.81408664390367, Gm2(b7, "Mc_1"), 9);
        Assert.Equal(7.019875744442285, Gm2(b7, "Mc_2"), 9);
        Assert.All(new[] { "Mc_3", "Mc_4", "Mc_5", "Mc_6", "Mc_7", "Mc_8", "Mc_9", "Mc_10", "Mc_11" }, s => Assert.Equal(0, Si(b7, s), 12));
        Assert.Equal(9.81408664390367, Gm2(b7, "Mc_if_3"), 9);
        Assert.All(new[] { "Mc_if_1", "Mc_if_2", "Mc_if_4" }, s => Assert.Equal(0, Si(b7, s), 12));
        Assert.Equal(9.81408664390367, Gm2(b7, "Mc_max"), 9);
        Assert.Equal(0, Si(b7, "Mc_end"), 12);

        // B7: the surface factor f_Rsi = 1 − 0.25/2.625988 = 0.904798 against f_Rsi,min 0.608842 (January, p_i/0.8 = 1553.5 Pa), and all three checks pass.
        Assert.Equal(0.904797740533042, Si(b7, "fRsi"), 12);
        Assert.Equal(0.6088418809594959, Si(b7, "fRsi_min"), 12);
        Assert.Equal(1, Si(b7, "n_fcrit"), 9);
        Assert.Equal(0.6729038476608152, Si(b7, "u_f"), 12);
        Assert.Equal(9.81408664390367 / 500, Si(b7, "u_Mc"), 12);
        Assert.Equal(3, b7.Checks.Count);
        Assert.All(b7.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.Equal(1, Si(b7, "pass"), 9);
        Assert.All(made.Instances.Values, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task Every_Binding_Of_The_Template_Is_A_Link_From_The_Output_It_Names()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var template = CalcPackTemplates.EnvelopeMoisture;

        Assert.Equal(["b4", "b2", "b7"], template.Steps.Select(s => s.Key));
        Assert.Equal(["bs.room-heat-loss", "bs.psychrometrics", "bs.glaser"], template.Steps.Select(s => s.TemplateId));
        Assert.Equal(
            [("b4", "U_1", "b7", "U_ref"), ("b2", "T_1_out", "b7", "T_int"), ("b2", "phi_1", "b7", "RH_int")],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));
        Assert.All(template.Bindings, b => Assert.Null(b.Row));

        foreach (var b in template.Bindings)
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked: the layers, the surface resistances, the climate and the limits are entered (the heat-loss step offers no per-layer output).
        Assert.Equal(3, made.Instances.Values.Sum(i => i.Links.Count));
        Assert.Equal(0, made.Instances.Values.Sum(i => i.CellLinks.Count));
        Assert.Equal(["RH_int", "T_int", "U_ref"], made.Instances["b7"].Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));
        Assert.Empty(made.Instances["b4"].Links);
        Assert.Empty(made.Instances["b2"].Links);
    }

    [Fact]
    public async Task The_Typed_Layers_Of_The_Two_Steps_Agree_And_So_Do_The_Internal_Temperatures_Which_Are_Not_Linked()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (b4, b2, b7) = (made.Instances["b4"], made.Instances["b2"], made.Instances["b7"]);

        // B4 offers no per-layer output, so the wall is typed twice: every B4 layer is a B7 layer of the same name with the same thickness and conductivity; the only extra B7 layer
        // is the 0.2 mm vapour control layer, below B4's 1 mm layer minimum and worth 0.0006 m²K/W of the 2.5 m²K/W.
        var wall4 = b4.Inputs.Single(i => i.Symbol == "layers").Rows!;
        var wall7 = b7.Inputs.Single(i => i.Symbol == "layers").Rows!;
        Assert.Equal(4, wall4.Count);
        Assert.Equal(5, wall7.Count);

        foreach (var row in wall4)
        {
            var name = row.Get("name")!.Text;
            var twin = Assert.Single(wall7, r => r.Get("name")!.Text == name);
            Assert.Equal(row.Get("d")!.Number!.Value, twin.Get("d")!.Number!.Value, 12);
            Assert.Equal(row.Get("d")!.Unit, twin.Get("d")!.Unit);
            Assert.Equal(row.Get("lambda")!.Number!.Value, twin.Get("lambda")!.Number!.Value, 12);
            Assert.Equal(row.Get("element")!.Text, twin.Get("element")!.Text);
        }

        var extra = Assert.Single(wall7, r => wall4.All(w => w.Get("name")!.Text != r.Get("name")!.Text));
        Assert.Equal("Vapour control layer", extra.Get("name")!.Text);
        Assert.Equal(0.2, extra.Get("d")!.Number!.Value, 12);
        Assert.True(0.0002 / 0.35 < 0.001 * Si(b4, "R_1"));

        // The surface resistances agree (B4's conventional horizontal 0.13 and outside 0.04 are B7's Rsi and Rse), and B4's typed internal design temperature is the air state's.
        Assert.Equal(0.13, InputSi(b7, "Rsi"), 12);
        Assert.Equal(0.04, InputSi(b7, "Rse"), 12);
        Assert.Equal(InputSi(b4, "T_int"), InputSi(b2, "T_db"), 9);
        Assert.Equal(InputSi(b4, "T_int"), InputSi(b7, "T_int"), 9);
        Assert.Equal("External wall", b4.Inputs.Single(i => i.Symbol == "elements").Rows![0].Get("name")!.Text);
        Assert.Equal("External wall", b7.Inputs.Single(i => i.Symbol == "element").Text);
    }

    [Fact]
    public async Task A_Thicker_Insulation_In_The_Heat_Loss_Step_Makes_The_Glaser_Step_Stale_And_Once_Updated_It_Warns_Until_Its_Own_Layers_Are_Retyped()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var b7Before = made.Instances["b7"];

        // 80 mm to 100 mm of mineral wool in the heat-loss step: R = 3.0054167, U = 0.3327326 W/m²K. Only the Glaser step takes from it, so only it goes stale.
        var b4Before = made.Instances["b4"];
        var revised = await host.Service.UpdateInputsAsync(b4Before.Id, ScaleCell("layers", 1, "d", 1.25)(b4Before.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, "b7"))).IsStale);
        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "b2"))).IsStale);
        Assert.True((await host.Service.UpdateLinksAsync(made.Instances["b7"].Id)).Succeeded);
        var (b4, b7) = (await Fresh(host, made, "b4"), await Fresh(host, made, "b7"));
        Assert.False((await host.Service.StalenessAsync(b7)).IsStale);
        Assert.Equal(1 / (0.13 + 0.0125 / 0.25 + 0.1 / 0.04 + 0.215 / 0.8 + 0.015 / 0.9 + 0.04), Si(b4, "U_1"), 12);
        Assert.Equal(0.3327325661999168, Si(b4, "U_1"), 12);
        Assert.Equal(Si(b4, "U_1"), InputSi(b7, "U_ref"), 12);

        // The Glaser step still holds the old 80 mm layers: its U-value is unchanged and 19.9 % above the heat-loss step's, so the two no longer describe one wall.
        Assert.Equal(Si(b7Before, "U_wall"), Si(b7, "U_wall"), 12);
        Assert.Equal(Math.Abs(0.39904419414450154 - 0.3327325661999168) / 0.3327325661999168, Si(b7, "dU_ref"), 12);
        Assert.Equal(0.19929407181845382, Si(b7, "dU_ref"), 12);
        var warning = Assert.Single(b7.Warnings, w => w.RuleId == "u-ref-mismatch");
        Assert.StartsWith("U_ref:", warning.Message, StringComparison.Ordinal);

        // Retyping 100 mm in the Glaser step clears the warning: U = 0.332669, 0.019 % from the heat-loss step's; Mc rises to 25.569 (Jan) and 29.906 g/m² (Feb, the peak), dry from March.
        var retyped = await host.Service.UpdateInputsAsync(b7.Id, [.. b7.Inputs.Select(i => i.Symbol != "layers" ? i : i with
        {
            Rows = [.. i.Rows!.Select(r => r.Get("name")!.Text == "Mineral wool" ? new CalcRow(r.Cells.ToDictionary(c => c.Key, c => c.Key == "d" ? c.Value with { Number = 100 } : c.Value, StringComparer.Ordinal)) : r)],
        })]);
        Assert.True(retyped.Succeeded, retyped.Reason);
        var fixedB7 = await Fresh(host, made, "b7");
        Assert.DoesNotContain(fixedB7.Warnings, w => w.RuleId == "u-ref-mismatch");
        Assert.Equal(0.33266931482002193, Si(fixedB7, "U_wall"), 12);
        Assert.Equal(Math.Abs(0.33266931482002193 - 0.3327325661999168) / 0.3327325661999168, Si(fixedB7, "dU_ref"), 12);
        Assert.Equal(9.621570844376974, Gm2(fixedB7, "Mc_12"), 9);
        Assert.Equal(25.569150421231537, Gm2(fixedB7, "Mc_1"), 9);
        Assert.Equal(29.905704372867774, Gm2(fixedB7, "Mc_2"), 9);
        Assert.Equal(29.905704372867774, Gm2(fixedB7, "Mc_max"), 9);
        Assert.Equal(0, Si(fixedB7, "Mc_end"), 12);
        Assert.Equal(0.92002528724251, Si(fixedB7, "fRsi"), 12);
        Assert.All(fixedB7.Checks, c => Assert.True(c.Passed, c.Name));
    }

    [Fact]
    public async Task Revising_The_Heat_Loss_Step_Makes_The_Glaser_Step_Stale_Refuses_The_Check_And_The_Issue_And_Updating_Carries_The_U_Value_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        Assert.Equal(0.39913520705138866, InputSi(made.Instances["b7"], "U_ref"), 12);

        await ChainKit.ReviseAndUpdateAsync(host, made, "b4", ScaleCell("layers", 1, "d", 1.25), ["b7"]);

        var b7 = await Fresh(host, made, "b7");
        Assert.Equal(0.3327325661999168, InputSi(b7, "U_ref"), 12);
        Assert.Equal(Si(await Fresh(host, made, "b4"), "U_1"), InputSi(b7, "U_ref"), 12);

        // The pack was checked once the step was current: the warning is a notice, not a block.
        Assert.Contains(b7.Warnings, w => w.RuleId == "u-ref-mismatch");
        Assert.Empty(b7.Blocks);
    }

    [Fact]
    public async Task A_Wetter_Room_In_The_Air_State_Step_Makes_The_Glaser_Step_Stale_Through_Its_Humidity_Alone_And_Updating_Carries_The_New_Condensate_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var b2Before = made.Instances["b2"];
        await AuthoredByAnotherAsync(host, made);

        // 50 % to 60 %: only the humidity link moves; the temperature and the U-value links name unchanged values.
        var revised = await host.Service.UpdateInputsAsync(b2Before.Id, Scale("RH", 1.2)(b2Before.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);
        var staleness = await host.Service.StalenessAsync(await Fresh(host, made, "b7"));
        Assert.True(staleness.IsStale);
        Assert.Contains(staleness.Reasons, r => r.Contains("'RH_int'", StringComparison.Ordinal) && r.Contains("different 'phi_1'", StringComparison.Ordinal));
        Assert.DoesNotContain(staleness.Reasons, r => r.Contains("'T_int'", StringComparison.Ordinal));
        Assert.DoesNotContain(staleness.Reasons, r => r.Contains("'U_ref'", StringComparison.Ordinal));
        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "b4"))).IsStale);

        Assert.True((await host.Service.UpdateLinksAsync(made.Instances["b7"].Id)).Succeeded);
        var b7 = await Fresh(host, made, "b7");
        Assert.Equal(0.6, InputSi(b7, "RH_int"), 12);
        Assert.False((await host.Service.StalenessAsync(b7)).IsStale);

        // 60 %: p_i = 1491.349 Pa; condensation from November, 5.7995 g/m² (Nov), 39.380 (Dec), 79.321 (Jan), 105.297 (Feb, the peak), dry from May; f_Rsi,min rises to 0.757876.
        Assert.Equal(11, Si(b7, "n_start"), 9);
        Assert.Equal(5.799502275933421, Gm2(b7, "Mc_11"), 9);
        Assert.Equal(39.37980146502858, Gm2(b7, "Mc_12"), 9);
        Assert.Equal(79.32059635098886, Gm2(b7, "Mc_1"), 9);
        Assert.Equal(105.29729203333831, Gm2(b7, "Mc_2"), 9);
        Assert.Equal(99.39321535696152, Gm2(b7, "Mc_3"), 9);
        Assert.Equal(53.25765545779224, Gm2(b7, "Mc_4"), 9);
        Assert.Equal(0, Si(b7, "Mc_5"), 12);
        Assert.Equal(105.29729203333831, Gm2(b7, "Mc_max"), 9);
        Assert.Equal(0.7578756199961533, Si(b7, "fRsi_min"), 12);
        Assert.Equal(0.83761882467751, Si(b7, "u_f"), 12);
        Assert.All(b7.Checks, c => Assert.True(c.Passed, c.Name));

        // The same revision through the shared helper proves the whole chain order and the pack check once it is current.
        var other = Host();
        var again = await InstantiateAsync(other);
        await ChainKit.ReviseAndUpdateAsync(other, again, "b2", "RH", 1.2, ["b7"]);
        Assert.Equal(0.6, InputSi(await Fresh(other, again, "b7"), "RH_int"), 12);
    }

    [Fact]
    public async Task While_Stale_The_Pack_Check_And_Issue_Are_Refused_With_Reasons_That_Name_The_Stale_Step()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var (b4, b2) = (made.Instances["b4"], made.Instances["b2"]);

        var wall = await host.Service.UpdateInputsAsync(b4.Id, ScaleCell("layers", 1, "d", 1.25)(b4.Inputs));
        Assert.True(wall.Succeeded, wall.Reason);
        var air = await host.Service.UpdateInputsAsync(b2.Id, Scale("T_db", 1.1)(b2.Inputs));
        Assert.True(air.Succeeded, air.Reason);

        // Neither root is stale; the Glaser step takes from both, so it is, and its reasons say which values moved.
        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "b4"))).IsStale);
        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "b2"))).IsStale);
        var staleness = await host.Service.StalenessAsync(await Fresh(host, made, "b7"));
        Assert.True(staleness.IsStale);
        Assert.Contains(staleness.Reasons, r => r.Contains("'U_ref'", StringComparison.Ordinal) && r.Contains("different 'U_1'", StringComparison.Ordinal));
        Assert.Contains(staleness.Reasons, r => r.Contains("'T_int'", StringComparison.Ordinal) && r.Contains("different 'T_1_out'", StringComparison.Ordinal));
        Assert.DoesNotContain(staleness.Reasons, r => r.Contains("'RH_int'", StringComparison.Ordinal));

        host.Principal = "j.reviewer";
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("Pack member 'Wall condensation' is stale", check.Reason, StringComparison.Ordinal);
        Assert.Contains("Update it before checking the pack", check.Reason, StringComparison.Ordinal);

        var issue = await host.Service.IssuePackAsync(made.Pack.Id, "ENV-STALE", "Client Co");
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
        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.EnvelopeMoisture, "ENV-001", "Client Co", "Checked against SPEC-ENV-02.");

        Assert.Contains("link: Room heat loss rev A · U_1", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Room air state rev A · T_1_out", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Room air state rev A · phi_1", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_Is_Refused_At_The_Staleness_Gate_When_The_Heat_Loss_Step_Moves_After_The_Pack_Was_Checked()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await IssueIsRefusedAtTheStalenessGateWhenTheFirstStepMovesAfterTheCheckAsync(host, made, CalcPackTemplates.EnvelopeMoisture, "b4", "b7",
            (id, outside) => host.Service.BindAsync(id, "U_ref", outside, "U_1"), ["b7"]);
    }
}
