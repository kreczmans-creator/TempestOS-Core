using System.Text;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum A, §A7 steel support: S5 → S2 → S3 → S4 → S6 from the built-in pack template, with the anchorage design filed as external
/// evidence against S6. S5's ULS effect is a cell of S2's load table; S2's column forces and base reactions feed S3 and S6; S4's bolt
/// values feed S6.
/// </summary>
public sealed class SteelSupportChainTests
{
    private static LibraryTestHost Host() => new(calculators: [.. StructuralCalculators.All], packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.SteelSupport, "Equipment support", ChainInputs.SteelSupport());

    private static Task<CalcActionResult> AttachPlaceholderAsync(LibraryTestHost host, CalcPackInstantiation made) =>
        host.Service.AttachExternalEvidenceAsync(made.Instances["s6"].Id, "PROFIS-BP1-PLACEHOLDER.txt", "text/plain",
            Encoding.UTF8.GetBytes("PLACEHOLDER. The Hilti PROFIS anchorage report is not attached yet; this file is not a PROFIS report."), "profis_ref");

    [Fact]
    public async Task Combined_Actions_Reach_The_Frame_The_Column_The_Connection_And_The_Base_Plate()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (s5, s2, s3, s4, s6) = (made.Instances["s5"], made.Instances["s2"], made.Instances["s3"], made.Instances["s4"], made.Instances["s6"]);

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

        // S2 → S3: the column's forces; the signed axial force arrives negative (compression).
        Assert.Equal(Si(s2, "N_e1"), InputSi(s3, "N_Ed"), 9);
        Assert.Equal(Si(s2, "M_abs_e1"), InputSi(s3, "M_y_Ed"), 9);
        Assert.Equal(Si(s2, "V_e1"), InputSi(s3, "V_Ed"), 9);
        Assert.True(InputSi(s3, "N_Ed") < 0);

        // S2 → S4: the arm's shear is the connection's group force.
        Assert.Equal(Si(s2, "V_e2"), InputSi(s4, "V_Ed"), 9);

        // S2 → S6: the base reactions are the base plate's actions (compression positive, moment and shear unsigned).
        Assert.Equal(Si(s2, "R1_y"), InputSi(s6, "N_Ed"), 9);
        Assert.Equal(Si(s2, "R1_m_abs"), InputSi(s6, "M_Ed"), 9);
        Assert.Equal(Si(s2, "R1_x_abs"), InputSi(s6, "V_Ed"), 9);

        // S4 → S6: the bolt's tension resistance, stress area and bearing resistance are the holding-down bolt data.
        Assert.Equal(0.9 * 800 * Si(s4, "A_s") * 1e6 / 1.25, Si(s4, "F_t_Rd"), 3);
        Assert.Equal(Si(s4, "F_t_Rd"), InputSi(s6, "F_t_Rd"), 9);
        Assert.Equal(Si(s4, "A_s"), InputSi(s6, "A_s"), 9);
        Assert.Equal(Si(s4, "F_b_Rd"), InputSi(s6, "F_1_vb_Rd"), 9);
        Assert.Equal(244.794, Si(s4, "A_s") * 1e6, 3);
        Assert.Equal(141.0016, Si(s4, "F_t_Rd") / 1e3, 3);

        // Every design check of the column, connection and base plate passes.
        Assert.All(new[] { s3, s4, s6 }, i => Assert.All(i.Checks, check => Assert.True(check.Passed, $"{i.Name}: {check.Name}")));

        // The anchorage is designed outside the chain: S6 says so until the report is filed.
        Assert.Contains(s6.Warnings, w => w.RuleId == "anchorage");
        Assert.Equal(5, made.Pack.InstanceIds.Count);
    }

    [Fact]
    public async Task A_Heavier_Load_Makes_Every_Step_Below_S5_Stale_Blocks_The_Pack_And_Updating_Carries_It_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var before = InputSi(made.Instances["s6"], "N_Ed");

        // A higher variable-action factor raises the ULS effect, which is the cell the frame reads.
        await ReviseAndUpdateAsync(host, made, "s5", "gamma_Q", 1.2, ["s2", "s3", "s4", "s6"]);

        var (s5, s2, s3, s4, s6) = (await Fresh(host, made, "s5"), await Fresh(host, made, "s2"), await Fresh(host, made, "s3"), await Fresh(host, made, "s4"), await Fresh(host, made, "s6"));
        Assert.Equal(-(1.35 * ChainInputs.DeadLoad + 1.8 * ChainInputs.ImposedLoad) * 1e3, Si(s5, "E_d_min"), 6);
        Assert.Equal(Si(s5, "E_d_min"), CellSi(s2, "loads", 0, "F_y"), 9);
        Assert.Equal(Si(s2, "N_e1"), InputSi(s3, "N_Ed"), 9);
        Assert.Equal(Si(s2, "V_e2"), InputSi(s4, "V_Ed"), 9);
        Assert.Equal(Si(s2, "R1_y"), InputSi(s6, "N_Ed"), 9);
        Assert.True(InputSi(s6, "N_Ed") > before);
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
        Assert.Contains(s6.Checks, c => c.Name.StartsWith("Anchorage designed externally", StringComparison.Ordinal) && c.Passed);

        // The attachment sits on the base plate's own Evidence.
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(s6.EvidenceId!.Value))!;
        Assert.Contains(await evidence.GetAttachmentsAsync(), a => a.FileName == "PROFIS-BP1-PLACEHOLDER.txt");

        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.SteelSupport, "STR-SUP-001", "Client Co", "Checked against SPEC-STR-09.");

        // The cell link is named under its row, the top-level ones by input, and the placeholder is cited by the base-plate sheet.
        Assert.Contains("F_y: link: Load combinations rev A · E_d_min", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Support frame rev A · R1_m_abs", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Arm connection and bolts rev A · F_t_Rd", sheet, StringComparison.Ordinal);
        Assert.Contains("PROFIS-BP1-PLACEHOLDER.txt", sheet, StringComparison.Ordinal);
    }
}
