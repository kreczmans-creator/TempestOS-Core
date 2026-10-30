using System.Text;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B, §B7 chain 9: the pipe expansion and anchor force B8 → the anchor post frame S2 → the post's base plate S6, from the built-in pack template, with the anchorage design done elsewhere filed as
/// external evidence against S6 exactly as in the steel-support chain (DA-22). B8's anchor force is a cell of S2's first load row (a table-cell link, N into kN); S2's base reactions are S6's actions. B8's
/// anchor moment is deliberately not linked. Every expected number was recomputed in python3 (B8 beam relations; the S2 cantilever statics; S6's EN 1993-1-8 Table 6.7 regime 1 statics, bearing strips and T-stub).
/// </summary>
public sealed class PipeworkSupportsChainTests
{
    private static LibraryTestHost Host() => new(calculators: [.. BuildingServicesCalculators.All, .. StructuralCalculators.All], packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.PipeworkSupports, "Pipe rack 4 anchor A2", ChainInputs.PipeworkSupports(host));

    private static Task<CalcActionResult> AttachPlaceholderAsync(LibraryTestHost host, CalcPackInstantiation made) =>
        host.Service.AttachExternalEvidenceAsync(made.Instances["s6"].Id, "PROFIS-BP1-PLACEHOLDER.txt", "text/plain",
            Encoding.UTF8.GetBytes("PLACEHOLDER. The Hilti PROFIS anchorage report for the pipe anchor post is not attached yet; this file is not a PROFIS report."), "profis_ref");

    [Fact]
    public async Task Values_Flow_Down_Every_Link_Of_The_Chain_With_Their_Units()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.PipeworkSupports));
        var made = await InstantiateAsync(host);
        var (b8, s2, s6) = (made.Instances["b8"], made.Instances["s2"], made.Instances["s6"]);
        Assert.Equal(3, made.Pack.InstanceIds.Count);

        // B8: 219.1 × 6.3 steel, 60 m, 10 → 150 °C: ΔL = 12e-6 × 60 × 140 = 100.8 mm; I = π/64 (OD⁴ − d_i⁴) = 2.38614e-5 m⁴; the 9.5 m offset leg needs √(3·E·OD·ΔL/Sa) = 9.32528 m (u 0.98161)
        // and puts F = 12·E·I·ΔL/H³ = 7069.457 N along the run on the anchor (the run's own 4.443 N along the leg adds 0.0014 N to the resultant).
        Assert.Equal(0.1008, Si(b8, "dL"), 12);
        Assert.Equal(9.32527693958737, Si(b8, "L_req"), 9);
        Assert.Equal(0.9816080989039337, Si(b8, "u_leg"), 9);
        Assert.Equal(7069.4567703256, Si(b8, "F_leg"), 6);
        Assert.Equal(4.442991299023933, Si(b8, "F_run"), 9);
        Assert.Equal(7069.458166484483, Si(b8, "anchor_force"), 6);
        Assert.Equal(Math.Sqrt(7069.4567703256 * 7069.4567703256 + 4.442991299023933 * 4.442991299023933), Si(b8, "anchor_force"), 6);
        Assert.Equal(33579.9196590466, Si(b8, "anchor_moment"), 5);
        Assert.Equal(291.8825707609589, Si(b8, "scr") * 1e6, 6);
        Assert.Equal(1, Si(b8, "pass"), 9);
        Assert.All(b8.Checks, c => Assert.True(c.Passed, c.Name));

        // B8 → S2, a table-cell link: the anchor force (N) is row 0, column F_x (kN) of the load table, the only link of S2.
        Assert.Equal(Si(b8, "anchor_force"), CellSi(s2, "loads", 0, "F_x"), 6);
        Assert.Equal(7.069458166484483, s2.Inputs.Single(i => i.Symbol == "loads").Rows![0].Get("F_x")!.Number!.Value, 9);
        Assert.Equal("kN", s2.Inputs.Single(i => i.Symbol == "loads").Rows![0].Get("F_x")!.Unit);
        Assert.Equal([(0, new CalcLink(b8.Id, "anchor_force", "A"))], s2.CellLinks.Select(l => (l.Row, l.Link)));
        Assert.All(s2.CellLinks, l => Assert.Equal(("loads", "F_x"), (l.Symbol, l.Column)));
        Assert.Empty(s2.Links);

        // S2: the 2.5 m post carries 1.5 × 7.069458 = 10.604187 kN along the pipe at the top and 1.35 × 6 = 8.1 kN down: M = 10.604187 × 2.5 = 26.510468 kN·m at the base.
        var fh = 1.5 * 7069.458166484483;
        Assert.Equal(10604.187249726724, fh, 6);
        Assert.Equal(8100, Si(s2, "R1_y"), 6);
        Assert.Equal(fh, Si(s2, "R1_x_abs"), 6);
        Assert.Equal(fh * 2.5, Si(s2, "R1_m_abs"), 5);
        Assert.Equal(26510.46812431681, Si(s2, "R1_m_abs"), 5);
        Assert.Equal(-8100, Si(s2, "N_e1"), 6);
        Assert.Equal(fh * 2.5, Si(s2, "M_abs_e1"), 5);
        Assert.Equal(fh, Si(s2, "V_e1"), 6);

        // S2 → S6: the base reactions are the plate's actions (compression positive; moment and shear unsigned), as in the steel-support pack.
        Assert.Equal(Si(s2, "R1_y"), InputSi(s6, "N_Ed"), 9);
        Assert.Equal(Si(s2, "R1_m_abs"), InputSi(s6, "M_Ed"), 9);
        Assert.Equal(Si(s2, "R1_x_abs"), InputSi(s6, "V_Ed"), 9);
        Assert.Equal(8.1, InputSi(s6, "N_Ed") / 1e3, 9);
        Assert.True(InputSi(s6, "N_Ed") > 0, "S6 takes compression positive: an upward reaction");
        Assert.Equal(["M_Ed", "N_Ed", "V_Ed"], s6.Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));

        // S6, EN 1993-1-8 regime 1: f_jd = 9.4444 MPa, c = 77.886 mm (cut to 73.4 mm at the plate edge), z_C = 93.857 mm, C = (M + N·x_b)/(z_C + x_b) = 120.617 kN, T = C − N = 112.517 kN, 56.258 kN a bolt (python3).
        Assert.Equal(9.444444444444443, Si(s6, "f_jd") / 1e6, 9);
        Assert.Equal(77.88566280509036, Si(s6, "c") * 1e3, 8);
        Assert.Equal(56.25828812024331, Si(s6, "F_t_Ed") / 1e3, 6);
        Assert.Equal(958.7579009815755, Si(s6, "N_j_Rd") / 1e3, 5);
        Assert.Equal(0.00844843102905043, Si(s6, "u_N"), 9);
        Assert.Equal(0.26231865208758576, Si(s6, "u_C"), 9);
        Assert.Equal(0.39899495120739936, Si(s6, "u_T"), 9);
        Assert.Equal(0.39899495120739936, Si(s6, "u_bt"), 9);
        Assert.Equal(2.246046812431681, Si(s6, "V_b_Ed") / 1e3, 6);
        Assert.Equal(0.06752871176464188, Si(s6, "u_V"), 9);
        Assert.Equal(0.39899495120739936, Si(s6, "u_max"), 9);
        Assert.All(s6.Checks, c => Assert.True(c.Passed, c.Name));

        // The anchorage is designed outside the chain: S6 says so until the report is filed.
        Assert.Contains(s6.Warnings, w => w.RuleId == "anchorage");
        Assert.DoesNotContain(s6.Checks, c => c.Name.StartsWith("Anchorage", StringComparison.Ordinal));
        Assert.All(made.Instances.Values, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task The_Anchor_Moment_Is_Not_Linked_Because_It_Is_A_Guided_Cantilever_Root_Moment_Not_An_Applied_Moment()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (b8, s2) = (made.Instances["b8"], made.Instances["s2"]);

        // B8's anchor moment (33.58 kN·m) is the root moment of its leg model; it appears nowhere in the post frame, whose loads are the force and the weight only.
        Assert.Equal(33579.9196590466, Si(b8, "anchor_moment"), 5);
        Assert.DoesNotContain(CalcPackTemplates.PipeworkSupports.Bindings, b => b.Output == "anchor_moment");
        var loads = s2.Inputs.Single(i => i.Symbol == "loads").Rows!;
        Assert.Equal(2, loads.Count);
        Assert.All(loads, r => Assert.Equal(0, r.Get("M")!.Number!.Value, 12));
    }

    [Fact]
    public async Task The_Holding_Down_Bolt_Data_Typed_Into_The_Base_Plate_Match_The_Connection_Step_For_The_Same_Bolt_And_Plate()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var s6 = made.Instances["s6"];

        // This chain has no bolt-check step, so S6's bolt data are typed (M20 8.8, a 25 mm S275 plate): guard them against S4 run on the steel-support example's holding-down bolt inputs.
        var support = CalcPackTemplates.SteelSupport;
        var s4Inputs = host.Service.ExampleInputs(support, support.Steps.Single(s => s.Key == "s4b"))!;
        var s4 = await host.Service.CreateAsync("struct.connection-ec3", await host.ProjectAsync(), "Guard holding-down bolts", s4Inputs);
        Assert.Empty(s4.Blocks);
        Assert.Equal(Si(s4, "F_t_Rd"), InputSi(s6, "F_t_Rd"), 1e-3 * Si(s4, "F_t_Rd"));
        Assert.Equal(Si(s4, "A_s"), InputSi(s6, "A_s"), 1e-3 * Si(s4, "A_s"));
        Assert.Equal(Si(s4, "F_b_Rd"), InputSi(s6, "F_1_vb_Rd"), 1e-3 * Si(s4, "F_b_Rd"));
        Assert.Equal(Si(s4, "f_ub"), InputSi(s6, "f_ub"), 9);
        Assert.Equal(Si(s4, "f_yb"), InputSi(s6, "f_yb"), 9);
        Assert.Equal(25e-3, InputSi(s6, "t_p"), 12);

        // Geometry, not just values: the S4 run is for S6's own plate and edge (thickness 25 mm, the 40 mm edge distance S6 uses as e_min, S275 plate steel), and for the same M20 8.8 bolt.
        Assert.Equal(InputSi(s6, "t_p"), InputSi(s4, "t_p"), 12);
        Assert.Equal(InputSi(s6, "e_min"), InputSi(s4, "e_1"), 12);
        Assert.Equal(40e-3, InputSi(s4, "e_1"), 12);
        Assert.Equal("S275", s4.Inputs.Single(i => i.Symbol == "steel").Text);
        Assert.Equal("M20", s4.Inputs.Single(i => i.Symbol == "size").Text);
        Assert.Equal("8.8", s4.Inputs.Single(i => i.Symbol == "grade").Text);
        Assert.Equal(275e6, InputSi(s6, "f_y"), 3);
    }

    [Fact]
    public async Task Every_Binding_Of_The_Template_Is_A_Link_From_The_Output_It_Names()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var template = CalcPackTemplates.PipeworkSupports;

        Assert.Equal(["b8", "s2", "s6"], template.Steps.Select(s => s.Key));
        Assert.Equal(["bs.pipe-expansion", "struct.frame-2d", "struct.base-plate"], template.Steps.Select(s => s.TemplateId));
        Assert.Equal(
            [("b8", "anchor_force", "s2", "loads"), ("s2", "R1_y", "s6", "N_Ed"), ("s2", "R1_m_abs", "s6", "M_Ed"), ("s2", "R1_x_abs", "s6", "V_Ed")],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));
        Assert.Equal((0, "F_x"), (template.Bindings[0].Row, template.Bindings[0].Column));

        foreach (var b in template.Bindings.Where(b => b.Row is null))
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked: the weight, the load factors, the plate and the bolt data are entered.
        Assert.Equal(template.Bindings.Count(b => b.Row is null), made.Instances.Values.Sum(i => i.Links.Count));
        Assert.Equal(1, made.Instances.Values.Sum(i => i.CellLinks.Count));
        Assert.Equal("anchor_force", Assert.Single(made.Instances["s2"].CellLinks).Link.OutputSymbol);
    }

    [Fact]
    public async Task A_Shorter_Anchor_Spacing_Makes_The_Frame_And_The_Base_Plate_Stale_Refuses_The_Check_And_The_Issue_And_Updating_Carries_The_Values_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var s6Before = made.Instances["s6"];
        Assert.Equal(7.069458166484483, CellSi(made.Instances["s2"], "loads", 0, "F_x") / 1e3, 6 + 3);

        await ChainKit.ReviseAndUpdateAsync(host, made, "b8", "L", 0.9, ["s2", "s6"]);

        var (b8, s2, s6) = (await Fresh(host, made, "b8"), await Fresh(host, made, "s2"), await Fresh(host, made, "s6"));

        // 60 m to 54 m: the growth falls to 90.72 mm and the leg force to 6362.511 N (resultant 6362.514 N: the run, now stiffer, adds 6.09 N along the leg).
        Assert.Equal(0.09072, Si(b8, "dL"), 12);
        Assert.Equal(6362.514012314052, Si(b8, "anchor_force"), 6);
        Assert.Equal(8.84673448228215, Si(b8, "L_req"), 9);
        Assert.NotEqual(Si(made.Instances["b8"], "anchor_force"), Si(b8, "anchor_force"));
        Assert.Equal(Si(b8, "anchor_force"), CellSi(s2, "loads", 0, "F_x"), 6);

        // S2 → S6: 1.5 × 6.362514 = 9.543771 kN along the pipe, M = 23.859428 kN·m, the weight unchanged; bolt tension falls to 50.466 kN.
        Assert.Equal(9543.771018471078, Si(s2, "R1_x_abs"), 6);
        Assert.Equal(23859.427546177695, Si(s2, "R1_m_abs"), 5);
        Assert.Equal(8100, Si(s2, "R1_y"), 6);
        Assert.Equal(Si(s2, "R1_m_abs"), InputSi(s6, "M_Ed"), 9);
        Assert.Equal(Si(s2, "R1_x_abs"), InputSi(s6, "V_Ed"), 9);
        Assert.Equal(50.46637739621849, Si(s6, "F_t_Ed") / 1e3, 6);
        Assert.Equal(0.3579175701859467, Si(s6, "u_T"), 9);
        Assert.Equal(1.9809427546177696, Si(s6, "V_b_Ed") / 1e3, 6);
        Assert.True(Si(s6, "F_t_Ed") < Si(s6Before, "F_t_Ed"));
        Assert.All(s6.Checks, c => Assert.True(c.Passed, c.Name));
    }

    [Fact]
    public async Task While_Stale_The_Pack_Check_And_Issue_Are_Refused_With_Reasons_That_Name_The_Stale_Step()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var b8 = made.Instances["b8"];

        var revised = await host.Service.UpdateInputsAsync(b8.Id, Scale("L", 0.9)(b8.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);

        foreach (var key in new[] { "s2", "s6" })
            Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be stale");

        // The frame takes the anchor force by a cell link and says which cell; the base plate takes nothing from B8 and is stale only because the frame, which it is linked to, is.
        var s2Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "s2"))).Reasons;
        Assert.Contains(s2Reasons, r => r.Contains("loads", StringComparison.Ordinal) && r.Contains("anchor_force", StringComparison.Ordinal));
        var s6Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "s6"))).Reasons;
        Assert.Contains(s6Reasons, r => r.Contains("'Anchor post frame', which", StringComparison.Ordinal) && r.Contains("is itself stale", StringComparison.Ordinal));

        host.Principal = "j.reviewer";
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("Pack member 'Anchor post frame' is stale", check.Reason, StringComparison.Ordinal);
        Assert.Contains("Update it before checking the pack", check.Reason, StringComparison.Ordinal);

        var issue = await host.Service.IssuePackAsync(made.Pack.Id, "PIP-STALE", "Client Co");
        Assert.False(issue.Succeeded);
        Assert.Contains("check it before issuing", issue.Reason, StringComparison.Ordinal);
        Assert.NotEqual(CalcInstanceStatus.Issued, (await host.Store.FindPackAsync(made.Pack.Id))!.Status);
    }

    [Fact]
    public async Task The_Chain_Checks_And_Issues_As_One_Bundle_With_The_Anchorage_Report_Filed_As_Evidence()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        var filed = await AttachPlaceholderAsync(host, made);
        Assert.True(filed.Succeeded, filed.Reason);
        var s6 = await Fresh(host, made, "s6");
        Assert.DoesNotContain(s6.Warnings, w => w.RuleId == "anchorage");
        Assert.Equal(CalcProvenanceKind.Evidence, s6.Inputs.Single(i => i.Symbol == "profis_ref").Provenance!.Kind);
        var anchorage = Assert.Single(s6.Checks, c => c.Name.StartsWith("Anchorage designed externally", StringComparison.Ordinal));
        Assert.True(anchorage.Passed);
        Assert.Equal("external report attached: PROFIS-BP1-PLACEHOLDER.txt", anchorage.Demand);

        // The attachment sits on the base plate's own Evidence.
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(s6.EvidenceId!.Value))!;
        Assert.Contains(await evidence.GetAttachmentsAsync(), a => a.FileName == "PROFIS-BP1-PLACEHOLDER.txt");

        // Another engineer is recorded as the author of every step; full provenance, one check by an independent checker, one issue.
        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.PipeworkSupports, "PIP-SUP-001", "Client Co", "Checked against SPEC-PIP-11.");

        // The cell link is named under its row, the top-level ones by input, and the placeholder is cited by the base-plate sheet.
        Assert.Contains("F_x: link: Pipe expansion and anchor force rev A · anchor_force", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Anchor post frame rev A · R1_y", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Anchor post frame rev A · R1_m_abs", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Anchor post frame rev A · R1_x_abs", sheet, StringComparison.Ordinal);
        Assert.Contains("PROFIS-BP1-PLACEHOLDER.txt", sheet, StringComparison.Ordinal);
        Assert.Contains("external report attached: PROFIS-BP1-PLACEHOLDER.txt", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_Is_Refused_At_The_Staleness_Gate_When_The_Pipe_Step_Moves_After_The_Pack_Was_Checked()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await IssueIsRefusedAtTheStalenessGateWhenTheFirstStepMovesAfterTheCheckAsync(host, made, CalcPackTemplates.PipeworkSupports, "b8", "s2",
            (id, outside) => host.Service.BindCellAsync(id, "loads", 0, "F_x", outside, "anchor_force"), ["s2", "s6"]);
    }
}
