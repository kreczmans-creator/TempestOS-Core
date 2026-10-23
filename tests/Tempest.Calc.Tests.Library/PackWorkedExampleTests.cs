using System.Text;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>§9: one calc pack combining M2 + M3 + S1 under a single issue sheet, proving the pack path end to end; writes docs/worked-examples/packs when TEMPEST_WRITE_WORKED_EXAMPLES=1.</summary>
public sealed class PackWorkedExampleTests
{
    private static CalcProvenance Drg(string n) => CalcProvenance.Drawing(n, "B");

    [Fact]
    public async Task Bracket_Pack_M2_M3_S1_Issues_Under_One_Sheet()
    {
        var host = new LibraryTestHost(calculators: [new PressFitCalculator(), new BoltedJointCalculator(), new SectionPropertiesCalculator()]);
        var project = await host.ProjectAsync();

        var pressFit = await host.Service.CreateAsync(PressFitCalculator.Id, project, "Bearing bush press fit",
            new PressFitCalculator().Template.TestCases[0].Inputs.Select(v => v with { Provenance = Drg("DRG-BRK-010") }));
        var joint = await host.Service.CreateAsync(BoltedJointCalculator.Id, project, "Bracket flange bolts",
            new BoltedJointCalculator().Template.TestCases[0].Inputs.Select(v => v with { Provenance = CalcProvenance.Specification("SPEC-BRK-2 §3") }));
        var section = await host.Service.CreateAsync(SectionPropertiesCalculator.Id, project, "Bracket arm section",
            new SectionPropertiesCalculator().Template.TestCases[2].Inputs.Select(v => v with { Provenance = Drg("DRG-BRK-011") }));

        foreach (var id in new[] { pressFit.Id, joint.Id, section.Id })
        {
            var run = await host.Service.RunAsync(id);
            Assert.NotNull(run);
            Assert.False(run!.Result.IsBlocked, string.Join("; ", run.Result.Blocks.Select(b => b.Message)));
        }

        var pack = await host.Service.CreatePackAsync(project, "Bracket assembly calc pack (M2 + M3 + S1)", [pressFit.Id, joint.Id, section.Id]);
        var check = await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Tempest Engineering", "Checked against DRG-BRK-010/011 rev B.", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        var issued = await host.Service.IssuePackAsync(pack.Id, "BRK-CALC-001", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);

        var stored = (await host.Store.FindPackAsync(pack.Id))!;
        Assert.Equal(CalcInstanceStatus.Issued, stored.Status);
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(stored.EvidenceId!.Value))!;
        Assert.Equal(EvidenceStatus.Issued, evidence.Status);
        var attachment = Assert.Single(await evidence.GetAttachmentsAsync());
        var sheet = Encoding.UTF8.GetString((await evidence.ReadAttachmentContentAsync(attachment.Id)).Bytes);
        Assert.Contains("PACK INDEX", sheet, StringComparison.Ordinal);
        Assert.Contains("Bearing bush press fit", sheet, StringComparison.Ordinal);
        Assert.Contains("Bracket arm section", sheet, StringComparison.Ordinal);

        foreach (var id in new[] { pressFit.Id, joint.Id, section.Id })
        {
            var member = (await host.Store.FindInstanceAsync(id))!;
            Assert.Equal(CalcInstanceStatus.Issued, member.Status);
            Assert.Equal("BRK-CALC-001", member.IssueReference);
            var blocked = await host.Service.UpdateInputsAsync(id, []);
            Assert.False(blocked.Succeeded);
        }

        if (Environment.GetEnvironmentVariable("TEMPEST_WRITE_WORKED_EXAMPLES") != "1")
            return;

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "VERSION"))) root = root.Parent;
        var folder = Path.Combine(root!.FullName, "docs", "worked-examples", "packs", "bracket-assembly-m2-m3-s1");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "pack-issue-sheet.txt"), sheet);

        foreach (var (id, slug) in new[] { (pressFit.Id, "m2-press-fit"), (joint.Id, "m3-bolted-joint"), (section.Id, "s1-section") })
        {
            var member = (await host.Store.FindInstanceAsync(id))!;
            File.WriteAllText(Path.Combine(folder, slug + ".sheet.txt"), CalcSheetTextRenderer.Render(await host.Service.SheetAsync(member)));
        }

        File.WriteAllText(Path.Combine(folder, "README.md"), """
            # Calc pack — bracket assembly (M2 + M3 + S1)

            **Purpose.** Proves the pack path (§9): three instances from three templates — `mech.press-fit` (bearing bush), `mech.bolted-joint` (flange bolts), `struct.section-properties` (bracket arm) — checked and issued together under one issue sheet `BRK-CALC-001` in one transaction.

            **Files.** `pack-issue-sheet.txt` (the pack's issue sheet: index, members, revisions, as stored on the Evidence record), `m2-press-fit.sheet.txt`, `m3-bolted-joint.sheet.txt`, `s1-section.sheet.txt` (the members' issued sheets). Generated by `PackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library`; after issue every member is read-only and carries the pack's issue reference.
            """);
    }
}
