using Tempest.Calc.BuildingServices;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B, §B7 chain 8: the chilled-water pipe B1 and the plant room air state B2 → the pipe insulation B9, from the built-in pack template. B1 offers the design temperature of the line (<c>T_line</c>, 1.5.0: the 6 °C flow, not the 9 °C mean the properties are taken at) and the
/// outside diameter of its first segment (<c>OD_1</c>, from the new optional <c>OD</c> column); B2 the dry-bulb (<c>t_1_out</c>) and the dew point (<c>t_dp_1</c>). Every expected number was recomputed in python3
/// (Kell density, Vogel viscosity, Colebrook; Hyland–Wexler saturation pressure inverted for the dew point; the cylinder resistance series and a scan-and-bisect for the thickness).
/// </summary>
public sealed class ColdPipeChainTests
{
    private static LibraryTestHost Host() => new(calculators: BuildingServicesCalculators.All, packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.ColdPipe, "Plant room 2 chilled water", ChainInputs.ColdPipe(host));

    /// <summary>A revision of the pipe step: the water 9 °C instead of 6 °C and the tube 59.4 mm outside diameter instead of 54 mm.</summary>
    private static IReadOnlyList<CalcInputValue> WarmerAndWider(IReadOnlyList<CalcInputValue> inputs) => ScaleCell("segments", 0, "OD", 1.1)(Scale("T_line", 1.5)(inputs));

    [Fact]
    public async Task Values_Flow_Down_Every_Link_Of_The_Chain_With_Their_Units()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.ColdPipe));
        var made = await InstantiateAsync(host);
        var (b1, b2, b9) = (made.Instances["b1"], made.Instances["b2"], made.Instances["b9"]);
        Assert.Equal(3, made.Pack.InstanceIds.Count);

        // B1: 12 m³/h in 51.6 mm at the 9 °C MEAN of the 6/12 °C line (Kell ρ 999.7808 kg/m³, Vogel μ 1.34428 mPa·s): 1.594004 m/s, Re 61172, Colebrook f 0.0200988, 4.32308 m of head, 42.386 kPa.
        var area = Math.PI * 0.0516 * 0.0516 / 4;
        Assert.Equal(12.0 / 3600 / area, Si(b1, "v_s1"), 12);
        Assert.Equal(1.594004197382923, Si(b1, "v_s1"), 12);
        Assert.Equal(61172.28675951049, Si(b1, "Re_s1"), 6);
        Assert.Equal(0.020098794721890557, Si(b1, "f_s1"), 9);
        Assert.Equal(4.3230818345048, Si(b1, "h_f_total"), 8);
        Assert.Equal(42385.6583120926, Si(b1, "dp_total"), 4);
        Assert.Equal(125.47018403613059e-3, Si(b1, "V_pipe"), 12);
        Assert.Single(b1.Checks);
        Assert.True(b1.Checks[0].Passed);

        // B1's two new ports: the design temperature of the line as entered, the 6 °C flow and NOT the 9 °C mean (K, shown in °C) and the first segment's OUTSIDE diameter (m, shown in mm) — not the 51.6 mm bore it judges the flow in.
        Assert.Equal(6 + 273.15, Si(b1, "T_line"), 9);
        Assert.Equal(9 + 273.15, InputSi(b1, "T"), 9);
        Assert.Equal(0.054, Si(b1, "OD_1"), 12);
        Assert.Equal(["OD_1", "T_line"], CalcPackTemplates.ColdPipe.Bindings.Where(b => b.FromStep == "b1").Select(b => b.Output).Order(StringComparer.Ordinal));
        Assert.Equal(0.0516, CellSi(b1, "segments", 0, "D"), 12);
        Assert.Equal(0.054, CellSi(b1, "segments", 0, "OD"), 12);

        // B2: 28 °C at 60 %, sea level: p_ws 3.7818 kPa, p_w 2.26932 kPa, dew point 19.514065 °C (inverted Hyland–Wexler).
        Assert.Equal(28 + 273.15, Si(b2, "t_1_out"), 9);
        Assert.Equal(19.51406457315946 + 273.15, Si(b2, "t_dp_1"), 7);
        Assert.Equal(2.269324180757383e3, Si(b2, "p_w_1"), 5);

        // B1 → B9: the insulation covers the OUTSIDE of the tube (54 mm), and the process temperature is the water's.
        Assert.Equal(Si(b1, "OD_1"), InputSi(b9, "D_i"), 12);
        Assert.Equal(54, b9.Inputs.Single(i => i.Symbol == "D_i").Number!.Value, 9);
        Assert.Equal("mm", b9.Inputs.Single(i => i.Symbol == "D_i").Unit);
        Assert.Equal(Si(b1, "T_line"), InputSi(b9, "theta_i"), 9);
        Assert.Equal(6, b9.Inputs.Single(i => i.Symbol == "theta_i").Number!.Value, 9);

        // B2 → B9: the ambient dry-bulb and the dew point (degC to degC).
        Assert.Equal(Si(b2, "t_1_out"), InputSi(b9, "theta_a"), 9);
        Assert.Equal(28, b9.Inputs.Single(i => i.Symbol == "theta_a").Number!.Value, 9);
        Assert.Equal(Si(b2, "t_dp_1"), InputSi(b9, "theta_dew"), 9);
        Assert.Equal(19.51406457315946, b9.Inputs.Single(i => i.Symbol == "theta_dew").Number!.Value, 7);

        // B2 → B9: the humidity (a % quantity) is linked too, so the sheet prints the air state's 60 %, not a default; the entered dew point wins, so it does not enter the result.
        Assert.Equal(Si(b2, "phi_1"), InputSi(b9, "RH_a"), 12);
        Assert.Equal(60, b9.Inputs.Single(i => i.Symbol == "RH_a").Number!.Value, 9);
        Assert.Equal("%", b9.Inputs.Single(i => i.Symbol == "RH_a").Unit);

        // B9: R = ln(104/54)/(2π·0.035) + 1/(8·π·0.104) = 2.98475 + 0.38257 = 3.362905 K·m/W at 25 mm; q = 22 K / R = 6.541963 W/m gained; θ_se = 28 − 6.541963/(8π·0.104) = 25.49715 °C.
        var de = 0.054 + 2 * 0.025;
        var r = Math.Log(de / 0.054) / (2 * Math.PI * 0.035) + 1 / (8 * Math.PI * de);
        Assert.Equal(3.3629051499298077, r, 12);
        Assert.Equal(r, Si(b9, "R"), 12);
        Assert.Equal(22 / r, Si(b9, "q_per_m"), 12);
        Assert.Equal(6.541962683800105, Si(b9, "q_per_m"), 12);
        Assert.Equal(28 - 22 / r / (8 * Math.PI * de), Si(b9, "theta_se") - 273.15, 9);
        Assert.Equal(25.49714976286176, Si(b9, "theta_se") - 273.15, 9);

        // The surface must stay 1 K above the dew point (20.514065 °C): 7.52065 mm does it, 25 mm chosen is at 33.4 % of the available surface-to-air range.
        Assert.Equal(7.5206514741039125e-3, Si(b9, "thickness"), 9);
        Assert.Equal(Si(b9, "thickness"), Si(b9, "t_dew"), 12);
        Assert.Equal(0.3343403455183923, Si(b9, "u_dew"), 8);
        Assert.Single(b9.Checks);
        Assert.True(b9.Checks[0].Passed);
        Assert.DoesNotContain(b9.Inputs, i => i.Symbol == "q_max_m");
        Assert.All(made.Instances.Values, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task Linking_The_Bore_Instead_Of_The_Outside_Diameter_Would_Have_Understated_The_Insulation_Which_Is_Why_The_Outside_Diameter_Is_A_Port()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var b9 = made.Instances["b9"];

        // A 51.6 mm bore as the insulation's inside diameter gives 7.4879 mm required, against 7.5207 mm on the real 54 mm outside diameter (the python3 scan-and-bisect).
        Assert.NotEqual(7.487931376692451e-3, Si(b9, "thickness"), 5);
        Assert.Equal(7.5206514741039125e-3, Si(b9, "thickness"), 9);
        Assert.Equal(0.054, InputSi(b9, "D_i"), 12);
        Assert.NotEqual(0.0516, InputSi(b9, "D_i"), 6);
    }

    [Fact]
    public async Task Every_Binding_Of_The_Template_Is_A_Link_From_The_Output_It_Names()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var template = CalcPackTemplates.ColdPipe;

        Assert.Equal(["b1", "b2", "b9"], template.Steps.Select(s => s.Key));
        Assert.Equal(["bs.pipe-pump", "bs.psychrometrics", "bs.insulation"], template.Steps.Select(s => s.TemplateId));
        Assert.Equal(
            [("b1", "OD_1", "b9", "D_i"), ("b1", "T_line", "b9", "theta_i"), ("b2", "t_1_out", "b9", "theta_a"), ("b2", "t_dp_1", "b9", "theta_dew"), ("b2", "phi_1", "b9", "RH_a")],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));
        Assert.All(template.Bindings, b => Assert.Null(b.Row));

        foreach (var b in template.Bindings)
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked: the conductivity, the surface coefficient, the margin and the chosen thickness are the user's; the conductivity and the margin are the pipe's own.
        Assert.Equal(5, made.Instances.Values.Sum(i => i.Links.Count));
        Assert.Equal(0, made.Instances.Values.Sum(i => i.CellLinks.Count));
        Assert.Equal(["D_i", "RH_a", "theta_a", "theta_dew", "theta_i"], made.Instances["b9"].Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));
        Assert.Empty(made.Instances["b1"].Links);
        Assert.Empty(made.Instances["b2"].Links);
    }

    [Fact]
    public async Task A_Warmer_Wider_Pipe_Makes_The_Insulation_Stale_Refuses_The_Check_And_The_Issue_And_Updating_Carries_The_Values_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var b9Before = made.Instances["b9"];

        await ChainKit.ReviseAndUpdateAsync(host, made, "b1", WarmerAndWider, ["b9"]);

        var (b1, b2, b9) = (await Fresh(host, made, "b1"), await Fresh(host, made, "b2"), await Fresh(host, made, "b9"));
        Assert.Equal(9 + 273.15, Si(b1, "T_line"), 9);
        Assert.Equal(0.0594, Si(b1, "OD_1"), 12);
        Assert.Equal(Si(b1, "T_line"), InputSi(b9, "theta_i"), 9);
        Assert.Equal(9, b9.Inputs.Single(i => i.Symbol == "theta_i").Number!.Value, 9);
        Assert.Equal(Si(b1, "OD_1"), InputSi(b9, "D_i"), 12);
        Assert.Equal(59.4, b9.Inputs.Single(i => i.Symbol == "D_i").Number!.Value, 9);

        // The room air did not change: its links name the same values.
        Assert.Equal(Si(b2, "t_dp_1"), InputSi(b9, "theta_dew"), 9);
        Assert.Equal(28, b9.Inputs.Single(i => i.Symbol == "theta_a").Number!.Value, 9);

        // Warmer water on a wider tube: required 6.13511 mm (7.52065 before); at 25 mm the gain is 6.049411 W/m and the surface 25.79983 °C (python3).
        Assert.NotEqual(Si(b9Before, "thickness"), Si(b9, "thickness"));
        Assert.Equal(6.1351104746437475e-3, Si(b9, "thickness"), 9);
        Assert.Equal(6.04941076692503, Si(b9, "q_per_m"), 12);
        Assert.Equal(25.799831749658473, Si(b9, "theta_se") - 273.15, 9);
        Assert.All(b9.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.Empty(b9.Blocks);
    }

    [Fact]
    public async Task A_Damper_Room_Makes_The_Insulation_Stale_Through_The_Dew_Point_Alone_And_Updating_Carries_The_New_Thickness_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var b2Before = made.Instances["b2"];
        await AuthoredByAnotherAsync(host, made);

        // 60 % to 66 %: the dew point rises to 21.056088 °C and the humidity to 66 %; the dry-bulb does not move, so only theta_dew and RH_a are stale.
        var revised = await host.Service.UpdateInputsAsync(b2Before.Id, Scale("RH", 1.1)(b2Before.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);
        var staleness = await host.Service.StalenessAsync(await Fresh(host, made, "b9"));
        Assert.True(staleness.IsStale);
        Assert.Contains(staleness.Reasons, r => r.Contains("'theta_dew'", StringComparison.Ordinal) && r.Contains("different 't_dp_1'", StringComparison.Ordinal));
        Assert.Contains(staleness.Reasons, r => r.Contains("'RH_a'", StringComparison.Ordinal) && r.Contains("different 'phi_1'", StringComparison.Ordinal));
        Assert.DoesNotContain(staleness.Reasons, r => r.Contains("'theta_a'", StringComparison.Ordinal));
        Assert.DoesNotContain(staleness.Reasons, r => r.Contains("'D_i'", StringComparison.Ordinal) || r.Contains("'theta_i'", StringComparison.Ordinal));
        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "b1"))).IsStale);

        Assert.True((await host.Service.UpdateLinksAsync(made.Instances["b9"].Id)).Succeeded);
        var b9 = await Fresh(host, made, "b9");
        Assert.False((await host.Service.StalenessAsync(b9)).IsStale);
        Assert.Equal(21.056087900134088, b9.Inputs.Single(i => i.Symbol == "theta_dew").Number!.Value, 6);
        Assert.Equal(66, b9.Inputs.Single(i => i.Symbol == "RH_a").Number!.Value, 9);
        Assert.Equal(10.12154443422767e-3, Si(b9, "thickness"), 8);
        Assert.Equal(0.42107793572430213, Si(b9, "u_dew"), 7);
        Assert.All(b9.Checks, c => Assert.True(c.Passed, c.Name));

        // The same revision through the shared helper proves the order and the pack check once it is current.
        var other = Host();
        var again = await InstantiateAsync(other);
        await ChainKit.ReviseAndUpdateAsync(other, again, "b2", "RH", 1.1, ["b9"]);
        Assert.Equal(10.12154443422767e-3, Si(await Fresh(other, again, "b9"), "thickness"), 8);
    }

    [Fact]
    public async Task While_Stale_The_Pack_Check_And_Issue_Are_Refused_With_Reasons_That_Name_The_Stale_Step()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var (b1, b2) = (made.Instances["b1"], made.Instances["b2"]);

        var pipe = await host.Service.UpdateInputsAsync(b1.Id, WarmerAndWider(b1.Inputs));
        Assert.True(pipe.Succeeded, pipe.Reason);
        var air = await host.Service.UpdateInputsAsync(b2.Id, Scale("t_db", 1.1)(b2.Inputs));
        Assert.True(air.Succeeded, air.Reason);

        // Neither root is stale; the insulation takes from both, so it is, and its reasons say which values moved: the temperature and diameter (B1), the dry-bulb and dew point (B2).
        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "b1"))).IsStale);
        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "b2"))).IsStale);
        var staleness = await host.Service.StalenessAsync(await Fresh(host, made, "b9"));
        Assert.True(staleness.IsStale);
        Assert.Contains(staleness.Reasons, r => r.Contains("'D_i'", StringComparison.Ordinal) && r.Contains("different 'OD_1'", StringComparison.Ordinal));
        Assert.Contains(staleness.Reasons, r => r.Contains("'theta_i'", StringComparison.Ordinal) && r.Contains("different 'T_line'", StringComparison.Ordinal));
        Assert.Contains(staleness.Reasons, r => r.Contains("'theta_a'", StringComparison.Ordinal) && r.Contains("different 't_1_out'", StringComparison.Ordinal));
        Assert.Contains(staleness.Reasons, r => r.Contains("'theta_dew'", StringComparison.Ordinal) && r.Contains("different 't_dp_1'", StringComparison.Ordinal));

        host.Principal = "j.reviewer";
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("Pack member 'Pipe insulation' is stale", check.Reason, StringComparison.Ordinal);
        Assert.Contains("Update it before checking the pack", check.Reason, StringComparison.Ordinal);

        var issue = await host.Service.IssuePackAsync(made.Pack.Id, "CHW-STALE", "Client Co");
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
        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.ColdPipe, "CHW-INS-001", "Client Co", "Checked against SPEC-CHW-04.");

        Assert.Contains("link: Chilled water pipe rev A · OD_1", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Chilled water pipe rev A · T_line", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Plant room air rev A · t_1_out", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Plant room air rev A · t_dp_1", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Plant room air rev A · phi_1", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Linking_The_Mean_Temperature_Instead_Of_The_Flow_Temperature_Would_Have_Understated_The_Insulation_Which_Is_Why_T_Line_Is_A_Separate_Input()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (b1, b9) = (made.Instances["b1"], made.Instances["b9"]);

        // The 6/12 °C line has a mean of 9 °C, but anti-condensation is sized on the 6 °C flow: the link carries the flow temperature, the pipe step still works at the mean.
        Assert.Equal(9 + 273.15, InputSi(b1, "T"), 9);
        Assert.Equal(6 + 273.15, InputSi(b9, "theta_i"), 9);
        Assert.Equal(7.5206514741039125e-3, Si(b9, "thickness"), 9);

        // The same insulation run at the 9 °C mean needs 6.0890 mm (python3), 19 % less: unsafe at the cold end.
        var copy = (IReadOnlyList<CalcInputValue>)[.. b9.Inputs.Select(i => i.Symbol == "theta_i" ? i with { Number = 9, Provenance = CalcProvenance.Assumed("test: the mean temperature") } : i with { Provenance = CalcProvenance.Assumed("test") })];
        var mean = await host.Service.CreateAsync("bs.insulation", made.Pack.ProjectId, "Insulation at the mean temperature", copy);
        Assert.Empty(mean.Blocks);
        Assert.Equal(6.089036686315434e-3, Si(mean, "thickness"), 9);
        Assert.True(Si(mean, "thickness") < 0.82 * Si(b9, "thickness"));
    }

    [Fact]
    public async Task Without_A_Line_Temperature_Or_A_Fluid_Temperature_The_Pipe_Step_Offers_No_Port_And_The_Pack_Is_Refused_Rather_Than_Take_The_20_C_Default()
    {
        var host = Host();
        var inputs = ChainInputs.ColdPipe(host);
        inputs["b1"] = [.. inputs["b1"].Where(v => v.Symbol is not ("T" or "T_line"))];
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.ColdPipe, await host.ProjectAsync(), "Plant room 3 chilled water", inputs);
        Assert.Null(made);
        Assert.NotNull(refusal);
        Assert.Contains("T_line", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_Is_Refused_At_The_Staleness_Gate_When_The_Pipe_Step_Moves_After_The_Pack_Was_Checked()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await IssueIsRefusedAtTheStalenessGateWhenTheFirstStepMovesAfterTheCheckAsync(host, made, CalcPackTemplates.ColdPipe, "b1", "b9",
            (id, outside) => host.Service.BindAsync(id, "theta_i", outside, "T_line"), ["b9"]);
    }
}
