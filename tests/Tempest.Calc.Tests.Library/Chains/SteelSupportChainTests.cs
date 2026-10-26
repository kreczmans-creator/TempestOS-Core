using System.Text;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Calc.Mechanical;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum A, §A7 steel support: S5 → S2 → S3, M3 → S4 (arm joint), S4 (holding-down bolts) → S6 from the built-in pack template, with
/// the anchorage design filed as external evidence against S6. S5's ULS effect is a cell of S2's load table; S2's column forces and base
/// reactions feed S3 and S6; S2's arm-end shear and moment load the arm joint's bolt group, whose bolt forces are S4's design forces;
/// the holding-down bolts' S4 values feed S6.
/// </summary>
public sealed class SteelSupportChainTests
{
    private static LibraryTestHost Host() => new(calculators: [.. StructuralCalculators.All, .. MechanicalCalculators.All], packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.SteelSupport, "Equipment support", ChainInputs.SteelSupport(host));

    private static Task<CalcActionResult> AttachPlaceholderAsync(LibraryTestHost host, CalcPackInstantiation made) =>
        host.Service.AttachExternalEvidenceAsync(made.Instances["s6"].Id, "PROFIS-BP1-PLACEHOLDER.txt", "text/plain",
            Encoding.UTF8.GetBytes("PLACEHOLDER. The Hilti PROFIS anchorage report is not attached yet; this file is not a PROFIS report."), "profis_ref");

    [Fact]
    public async Task Combined_Actions_Reach_The_Frame_The_Column_The_Arm_Joint_And_The_Base_Plate()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (s5, s2, s3, m3, s4a, s4b, s6) = (made.Instances["s5"], made.Instances["s2"], made.Instances["s3"], made.Instances["m3"], made.Instances["s4a"], made.Instances["s4b"], made.Instances["s6"]);

        // S5: 12 kN dead and 8 kN maintenance load, both downward (negative), EN 1990 6.10: 1.35 × 12 + 1.5 × 8 = 28.2 kN.
        var uls = -(1.35 * ChainInputs.DeadLoad + 1.5 * ChainInputs.ImposedLoad) * 1e3;
        Assert.Equal(uls, Si(s5, "E_d_min"), 6);

        // S5 → S2: a table-cell link into row 0, column F_y, of S2's load table, the only link of S2.
        Assert.Equal(Si(s5, "E_d_min"), CellSi(s2, "loads", 0, "F_y"), 9);
        Assert.Equal([(0, new CalcLink(s5.Id, "E_d_min", "A"))], s2.CellLinks.Select(l => (l.Row, l.Link)));
        Assert.All(s2.CellLinks, l => Assert.Equal(("loads", "F_y"), (l.Symbol, l.Column)));
        Assert.Empty(s2.Links);

        // S2: the 1 m arm carries 28.2 kN, so the column takes N = 28.2 kN (compression) and M = 28.2 × 1 + 5 × 3 = 43.2 kN·m at the base.
        var h = ChainInputs.HorizontalLoad * 1e3;
        Assert.Equal(-uls, Si(s2, "R1_y"), 6);
        Assert.Equal(-uls * 1.0 + h * 3.0, Si(s2, "R1_m_abs"), 6);
        Assert.Equal(h, Si(s2, "R1_x_abs"), 6);
        Assert.Equal(uls, Si(s2, "N_e1"), 6);
        Assert.Equal(-uls * 1.0 + h * 3.0, Si(s2, "M_abs_e1"), 6);
        Assert.Equal(h, Si(s2, "V_e1"), 6);
        Assert.Equal(-uls, Si(s2, "V_e2"), 6);
        Assert.Equal(-uls * 1.0, Si(s2, "M_abs_e2"), 6);

        // S2 → S3: the column's forces; the signed axial force arrives negative (compression).
        Assert.Equal(Si(s2, "N_e1"), InputSi(s3, "N_Ed"), 9);
        Assert.Equal(Si(s2, "M_abs_e1"), InputSi(s3, "M_y_Ed"), 9);
        Assert.Equal(Si(s2, "V_e1"), InputSi(s3, "V_Ed"), 9);
        Assert.True(InputSi(s3, "N_Ed") < 0);

        // S2 → M3: the arm's end shear and moment load the six-bolt pattern (two columns, rows at y = +150, 0, −150 mm).
        Assert.Equal(Si(s2, "V_e2"), InputSi(m3, "F_y"), 9);
        Assert.Equal(Si(s2, "M_abs_e2"), InputSi(m3, "M_x"), 9);
        Assert.Equal(Si(s2, "V_e2") / 6, Si(m3, "v_max"), 6);
        Assert.Equal(Si(s2, "M_abs_e2") * 0.150 / (4 * 0.150 * 0.150), Si(m3, "t_max"), 6);

        // M3 → S4 (arm joint): the bolt group's governing-bolt shear and tension are the connection's design forces per bolt.
        Assert.Equal(Si(m3, "v_max"), InputSi(s4a, "F_v_Ed"), 9);
        Assert.Equal(Si(m3, "t_max"), InputSi(s4a, "F_t_Ed"), 9);
        Assert.True(InputSi(s4a, "F_t_Ed") > 0, "the joint carries a moment, so its bolts are in tension");

        // S2 → S4 (arm joint): the arm's shear is the group force for block tearing.
        Assert.Equal(Si(s2, "V_e2"), InputSi(s4a, "V_Ed"), 9);

        // The bolt group's resistances are those M20 8.8 gives S4 (typed into the bolt group, so guarded here).
        Assert.Equal(Si(s4a, "F_v_Rd"), InputSi(m3, "V_R"), 1e-3 * Si(s4a, "F_v_Rd"));
        Assert.Equal(Si(s4a, "F_t_Rd"), InputSi(m3, "T_R"), 1e-3 * Si(s4a, "F_t_Rd"));

        // S2 → S6: the base reactions are the base plate's actions (compression positive, moment and shear unsigned).
        Assert.Equal(Si(s2, "R1_y"), InputSi(s6, "N_Ed"), 9);
        Assert.Equal(Si(s2, "R1_m_abs"), InputSi(s6, "M_Ed"), 9);
        Assert.Equal(Si(s2, "R1_x_abs"), InputSi(s6, "V_Ed"), 9);

        // S4 (holding-down bolts) → S6: resistances, stress area, bearing and strengths of the bolt in the 25 mm base plate, not the arm joint's 20 mm plate.
        Assert.Equal(0.9 * 800 * Si(s4b, "A_s") * 1e6 / 1.25, Si(s4b, "F_t_Rd"), 3);
        Assert.Equal(Si(s4b, "F_t_Rd"), InputSi(s6, "F_t_Rd"), 9);
        Assert.Equal(Si(s4b, "A_s"), InputSi(s6, "A_s"), 9);
        Assert.Equal(Si(s4b, "F_b_Rd"), InputSi(s6, "F_1_vb_Rd"), 9);
        Assert.Equal(Si(s4b, "f_ub"), InputSi(s6, "f_ub"), 9);
        Assert.Equal(Si(s4b, "f_yb"), InputSi(s6, "f_yb"), 9);
        Assert.Equal(800, Si(s4b, "f_ub") / 1e6, 9);
        Assert.Equal(640, Si(s4b, "f_yb") / 1e6, 9);
        Assert.Equal(244.794, Si(s4b, "A_s") * 1e6, 3);
        Assert.Equal(141.0016, Si(s4b, "F_t_Rd") / 1e3, 3);

        // The base plate's bearing value is the 25 mm plate's: 25/11 times the arm joint's, which bears on the thinner 11 mm column flange behind the 20 mm end plate (chain.steel-support-R1; k₁ and α_b are alike).
        Assert.Equal(25.0 / 11.0, Si(s4b, "F_b_Rd") / Si(s4a, "F_b_Rd"), 9);
        Assert.NotEqual(Si(s4a, "F_b_Rd"), InputSi(s6, "F_1_vb_Rd"));

        // Every design check of the column, both connections, the bolt group and the base plate passes.
        Assert.All(new[] { s3, m3, s4a, s4b, s6 }, i => Assert.All(i.Checks, check => Assert.True(check.Passed, $"{i.Name}: {check.Name}")));

        // The anchorage is designed outside the chain: S6 says so until the report is filed.
        Assert.Contains(s6.Warnings, w => w.RuleId == "anchorage");
        Assert.DoesNotContain(s6.Checks, c => c.Name.StartsWith("Anchorage", StringComparison.Ordinal));
        Assert.Equal(7, made.Pack.InstanceIds.Count);
    }

    [Fact]
    public async Task The_Pack_States_What_It_Does_Not_Check_And_Bearing_Is_On_The_Thinner_Ply()
    {
        // chain.steel-support-03, -04, -05, -13, -R1 (and the limits of -01, -02): the text says what the joint, the column and the anchorage checks leave out, S4a bears on the 11 mm column
        // flange (F_b,Rd 198.79 -> 109.33 kN, u_b 0.024 -> 0.043), and S6 warns that the anchorage is not assessed.
        var text = CalcPackTemplates.SteelSupport.Description;
        Assert.Contains("bolts of the bracket's joint", text, StringComparison.Ordinal);
        Assert.Contains("end plate in bending", text, StringComparison.Ordinal);
        Assert.Contains("symmetric", text, StringComparison.Ordinal);
        Assert.Contains("lateral-torsional buckling", text, StringComparison.Ordinal);
        Assert.Contains("not assessed", text, StringComparison.Ordinal);
        Assert.DoesNotContain("designs the bracket's bolted joint", text, StringComparison.Ordinal);

        var host = Host();
        var made = await InstantiateAsync(host);
        var s4a = made.Instances["s4a"];
        Assert.Equal(109.3333, Si(s4a, "F_b_Rd") / 1e3, 3);
        Assert.Contains(made.Instances["s6"].Warnings, w => w.RuleId == "anchorage-result");
    }

    [Fact]
    public async Task A_Heavier_Load_Makes_Every_Step_Below_S5_Stale_Blocks_The_Pack_And_Updating_Carries_It_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var before = InputSi(made.Instances["s6"], "N_Ed");
        var jointBefore = InputSi(made.Instances["s4a"], "F_t_Ed");

        // A higher variable-action factor raises the ULS effect, which is the cell the frame reads.
        await ReviseAndUpdateAsync(host, made, "s5", "gamma_Q", 1.2, ["s2", "s3", "m3", "s4a", "s6"]);

        var (s5, s2, s3, m3, s4a, s6) = (await Fresh(host, made, "s5"), await Fresh(host, made, "s2"), await Fresh(host, made, "s3"), await Fresh(host, made, "m3"), await Fresh(host, made, "s4a"), await Fresh(host, made, "s6"));
        Assert.Equal(-(1.35 * ChainInputs.DeadLoad + 1.8 * ChainInputs.ImposedLoad) * 1e3, Si(s5, "E_d_min"), 6);
        Assert.Equal(Si(s5, "E_d_min"), CellSi(s2, "loads", 0, "F_y"), 9);
        Assert.Equal(Si(s2, "N_e1"), InputSi(s3, "N_Ed"), 9);
        Assert.Equal(Si(s2, "M_abs_e2"), InputSi(m3, "M_x"), 9);
        Assert.Equal(Si(m3, "t_max"), InputSi(s4a, "F_t_Ed"), 9);
        Assert.True(InputSi(s4a, "F_t_Ed") > jointBefore);
        Assert.Equal(Si(s2, "R1_y"), InputSi(s6, "N_Ed"), 9);
        Assert.True(InputSi(s6, "N_Ed") > before);
    }

    [Fact]
    public async Task Revising_The_Holding_Down_Bolts_Makes_The_Base_Plate_Stale_And_Updating_Carries_The_New_Bolt_Data_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var tBefore = InputSi(made.Instances["s6"], "F_t_Rd");
        var bearingBefore = InputSi(made.Instances["s6"], "F_1_vb_Rd");

        // A thicker base plate for the bolts' bearing: the bearing value rises, the tension resistance does not.
        await ReviseAndUpdateAsync(host, made, "s4b", "t", 1.2, ["s6"]);

        var (s4b, s6) = (await Fresh(host, made, "s4b"), await Fresh(host, made, "s6"));
        Assert.Equal(Si(s4b, "F_b_Rd"), InputSi(s6, "F_1_vb_Rd"), 9);
        Assert.True(InputSi(s6, "F_1_vb_Rd") > bearingBefore);
        Assert.Equal(tBefore, InputSi(s6, "F_t_Rd"), 9);

        // The arm joint is on its own step: it is untouched by the bolts' revision.
        Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, "s4a"))).IsStale);
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

        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.SteelSupport, "STR-SUP-001", "Client Co", "Checked against SPEC-STR-09.");

        // The cell link is named under its row, the top-level ones by input, and the placeholder is cited by the base-plate sheet.
        Assert.Contains("F_y: link: Load combinations rev A · E_d_min", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Support frame rev A · R1_m_abs", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Arm joint bolt group rev A · t_max", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Holding-down bolts rev A · F_t_Rd", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Holding-down bolts rev A · f_ub", sheet, StringComparison.Ordinal);
        Assert.Contains("PROFIS-BP1-PLACEHOLDER.txt", sheet, StringComparison.Ordinal);
        Assert.Contains("external report attached: PROFIS-BP1-PLACEHOLDER.txt", sheet, StringComparison.Ordinal);
    }
}
