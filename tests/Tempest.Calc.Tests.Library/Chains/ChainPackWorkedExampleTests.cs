using System.Globalization;
using System.Text;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum A, §A7: one issued calc pack per built-in chain (drive train, motor circuit, wet heating, cooling, steel support), written
/// under docs/worked-examples/packs/&lt;chain&gt; when TEMPEST_WRITE_WORKED_EXAMPLES=1: the pack's issue sheet, each step's issued sheet,
/// every step's inputs and a README naming the chain, every link and the issue reference. The PDFs are made later.
/// </summary>
public sealed class ChainPackWorkedExampleTests
{
    public static TheoryData<string> Chains => ["drive-train", "motor-circuit", "wet-heating", "cooling", "steel-support"];

    private sealed record Example(CalcPackTemplate Template, string Slug, string Title, string Reference, string Client, string Project, string Note, Func<LibraryTestHost, Task<IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>>>> Inputs, string Remarks);

    private static IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>> Plain(Dictionary<string, IReadOnlyList<CalcInputValue>> inputs) => inputs;

    private static readonly Example[] Examples =
    [
        new(CalcPackTemplates.DriveTrain, "drive-train", "Conveyor head drive calc pack", "CNV-DRV-001", "Client Co", "Conveyor 7 head drive", "Checked against SPEC-CNV-07 and DRG-CNV-410 to 412 rev B.",
            _ => Task.FromResult(Plain(ChainInputs.DriveTrain())),
            "A 4-pole motor (about 24 kW at 1500 rpm) turns a conveyor drum at 120 rpm through a 12.5:1 gearbox against a steady 1.8 kN·m. The motor's torque and speed are the gearbox input shaft's; the shaft's reaction and speed give bearing A its load and speed; the shaft's seat diameter is the fit's nominal size; and the motor's peak torque is the torque the four-bolt coupling flange carries."),
        new(CalcPackTemplates.MotorCircuit, "motor-circuit", "Servo axis motor circuit calc pack", "LINE-ELEC-001", "Client Co", "Line 12 axis 3", "Checked against SPEC-LINE-12 and DRG-LINE-120 rev B.",
            async host => Plain(await MotorCircuitInputsAsync(host)),
            "A ball-screw servo axis (50 kg, 0.5 m in 0.5 s) is sized, its panel demand and heat found, its cable and device chosen, and the voltage drop, earth-fault disconnection and enclosure temperature checked. **The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**"),
        new(CalcPackTemplates.WetHeating, "wet-heating", "Plant room heating calc pack", "HTG-001", "Client Co", "Plant room heating", "Checked against SPEC-HTG-01 §3.",
            _ => Task.FromResult(Plain(ChainInputs.WetHeating())),
            "Two rooms' design heat losses are the two rows of the sealed system's room table (table-cell links); the sealed system's total heat, flow-to-return difference and mean water temperature give the primary pipework its flow and fluid; the pump's shaft torque and speed are the circulator motor's load."),
        new(CalcPackTemplates.Cooling, "cooling", "Drive panel cooling calc pack", "MCC-COOL-001", "Client Co", "Motor control centre 3", "Checked against SPEC-MCC-03 and DRG-MCC-210/215 rev B.",
            _ => Task.FromResult(Plain(ChainInputs.Cooling())),
            "The heat a drive panel dissipates is a linked load of its enclosure; the heat the enclosure cannot lose naturally is the required duty of a water-cooled exchanger; the exchanger's duty, cold-side temperature rise and mean cold-side temperature give the cooling-water pipework and pump their flow and fluid temperature. The enclosure step's own check, natural dissipation holding the limit, FAILS by design: that is the reason the exchanger is needed, and the duty it hands over is exactly the shortfall."),
        new(CalcPackTemplates.SteelSupport, "steel-support", "Equipment support steelwork calc pack", "STR-SUP-001", "Client Co", "Equipment support ES-4", "Checked against SPEC-STR-09 and DRG-STR-310 to 315 rev B.",
            _ => Task.FromResult(Plain(ChainInputs.SteelSupport())),
            "The equipment's dead and maintenance loads are combined (EN 1990); the ULS vertical effect is a cell of the frame's load table; the frame's column forces check the column, its arm shear loads the bolted connection and its base reactions are the base plate's actions; the connection's bolt values are the holding-down bolt data. **The anchorage (concrete and anchor design) is done in Hilti PROFIS: the file attached to the base-plate step is a PLACEHOLDER, not a PROFIS report, to be replaced by the engineer's own report before the pack is used.**"),
    ];

    /// <summary>The motor circuit's inputs with the synthetic E4 tables saved as (unlicensed, synthetic) records and pinned.</summary>
    internal static async Task<Dictionary<string, IReadOnlyList<CalcInputValue>>> MotorCircuitInputsAsync(LibraryTestHost host)
    {
        var (ampacity, r1) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "cable.ampacity", "SYNTHETIC cable capacities (test values, not a regulation table)", 0, "Synthetic test data, not from any standard", "n/a", "invented values for the worked example", "Tempest test data", "tempest.tests",
            false, CalcDataOrigin.UserEntered, [new CalcDataValue("ampacity", Rows: ConductorSizingCalculator.SyntheticAmpacity().Rows)], DateTimeOffset.UnixEpoch));
        var (ratings, r2) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "device.ratings", "SYNTHETIC device ratings (test values, not a manufacturer table)", 0, "Synthetic test data, not from any catalogue", "n/a", "invented values for the worked example", "Tempest test data", "tempest.tests",
            false, CalcDataOrigin.UserEntered, [new CalcDataValue("ratings", Rows: ConductorSizingCalculator.TypicalRatings().Rows)], DateTimeOffset.UnixEpoch));
        Assert.Null(r1);
        Assert.Null(r2);
        return ChainInputs.MotorCircuit(CalcInstanceService.RecordProvenance(ampacity!), CalcInstanceService.RecordProvenance(ratings!));
    }

    private static async Task<Guid?> ProjectAsync(LibraryTestHost host, string name)
    {
        var factory = new EngineeringObjectFactory<Project>(
            "Project", host.Context, (doc, rev) => new Project(doc, rev, host.Context, "EX-" + name.Split(' ')[0].ToUpperInvariant(), name, EngineeringObjectMetadata.Empty));
        return (await factory.CreateAsync("Worked example project.")).Id;
    }

    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All, .. MechanicalCalculators.All, .. BuildingServicesCalculators.All, .. StructuralCalculators.All],
        packTemplates: CalcPackTemplates.All);

    [Theory]
    [MemberData(nameof(Chains))]
    public async Task Each_Chain_Is_Issued_As_One_Pack_And_Written_As_A_Worked_Example(string slug)
    {
        var example = Examples.Single(e => e.Slug == slug);
        var host = Host();
        var inputs = await example.Inputs(host);
        var made = await ChainKit.InstantiateAsync(host, example.Template, example.Title, inputs, await ProjectAsync(host, example.Project));

        // The steel support's anchorage is designed elsewhere: file a clearly marked placeholder against the base plate.
        if (slug == "steel-support")
        {
            var filed = await host.Service.AttachExternalEvidenceAsync(made.Instances["s6"].Id, "PROFIS-BP1-PLACEHOLDER.txt", "text/plain",
                Encoding.UTF8.GetBytes("PLACEHOLDER. The Hilti PROFIS anchorage report for base plate BP1 is not attached yet; the engineer replaces this file with the real report. This file is not a PROFIS report."), "profis_ref");
            Assert.True(filed.Succeeded, filed.Reason);
        }

        var sheet = await ChainKit.CheckAndIssueAsync(host, made, example.Template, example.Reference, example.Client, example.Note);

        if (Environment.GetEnvironmentVariable("TEMPEST_WRITE_WORKED_EXAMPLES") != "1")
            return;

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "VERSION"))) root = root.Parent;
        var folder = Path.Combine(root!.FullName, "docs", "worked-examples", "packs", slug);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "pack-issue-sheet.txt"), sheet);

        var issued = new Dictionary<string, CalcInstance>(StringComparer.Ordinal);

        foreach (var step in example.Template.Steps)
        {
            var member = (await host.Store.FindInstanceAsync(made.Instances[step.Key].Id))!;
            issued[step.Key] = member;
            File.WriteAllText(Path.Combine(folder, $"{step.Key}-{step.TemplateId.Replace('.', '-')}.sheet.txt"), CalcSheetTextRenderer.Render(await host.Service.SheetAsync(member)));
        }

        File.WriteAllText(Path.Combine(folder, "inputs.json"), InputsJson(example, issued));
        File.WriteAllText(Path.Combine(folder, "README.md"), Readme(example, issued));
    }

    private static string InputsJson(Example example, IReadOnlyDictionary<string, CalcInstance> issued)
    {
        var sb = new StringBuilder("{\n");
        var steps = example.Template.Steps;

        for (var i = 0; i < steps.Count; i++)
        {
            sb.Append("  \"").Append(steps[i].Key).Append("\": ");
            var json = CalcInputJson.Write(new CalcInputSet(issued[steps[i].Key].Inputs)).TrimEnd().Replace("\n", "\n  ", StringComparison.Ordinal);
            sb.Append(json).Append(i < steps.Count - 1 ? ",\n" : "\n");
        }

        return sb.Append("}\n").ToString();
    }

    private static string Number(double? n) => (n ?? 0).ToString("G6", CultureInfo.InvariantCulture);

    private static string Readme(Example example, IReadOnlyDictionary<string, CalcInstance> issued)
    {
        var template = example.Template;
        var name = template.Steps.ToDictionary(s => s.Key, s => s.Name, StringComparer.Ordinal);
        var sb = new StringBuilder();
        sb.Append("# Calc pack — ").Append(template.Title).Append(" (`").Append(template.Id).Append("`)\n\n");
        sb.Append("**Chain.** ").Append(string.Join(" → ", template.Steps.Select(s => $"{s.Name} (`{s.TemplateId}`)"))).Append(".\n\n");
        sb.Append("**Purpose.** ").Append(template.Description).Append("\n\n");
        sb.Append(example.Remarks).Append("\n\n");
        sb.Append("**Issue.** Project “").Append(example.Project).Append("”, pack “").Append(example.Title).Append("”, checked once as a bundle and issued under issue reference **`").Append(example.Reference).Append("`** to ").Append(example.Client)
            .Append(" in one transaction. After issue every step is read-only and carries that reference. ").Append(example.Note).Append("\n\n");

        sb.Append("## Steps\n\n| Step | Calculator | Revision | Checks |\n|---|---|---|---|\n");

        foreach (var step in template.Steps)
        {
            var i = issued[step.Key];
            var passed = i.Checks.Count(c => c.Passed);
            sb.Append("| ").Append(step.Name).Append(" | `").Append(step.TemplateId).Append("` | ").Append(i.Revision).Append(" | ")
                .Append(i.Checks.Count == 0 ? "none" : $"{passed} of {i.Checks.Count} PASS").Append(" |\n");
        }

        sb.Append("\n## Links\n\nEvery link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an upstream input makes every step below it stale, and the pack cannot be checked or issued until each is updated in order.\n\n");
        sb.Append("| From | Output | To | Input | Value carried |\n|---|---|---|---|---|\n");

        foreach (var b in template.Bindings)
        {
            var target = issued[b.ToStep];
            string value;
            string input;

            if (b.Row is { } row && b.Column is { } column)
            {
                var cell = target.Inputs.Single(x => x.Symbol == b.Input).Rows![row].Get(column)!;
                value = $"{Number(cell.Number)} {cell.Unit}".Trim();
                input = $"`{b.Input}` row {row + 1} (`{column}`), a table-cell link";
            }
            else
            {
                var v = target.Inputs.Single(x => x.Symbol == b.Input);
                value = $"{Number(v.Number)} {v.Unit}".Trim();
                input = $"`{b.Input}`";
            }

            sb.Append("| ").Append(name[b.FromStep]).Append(" | `").Append(b.Output).Append("` | ").Append(name[b.ToStep]).Append(" | ").Append(input).Append(" | ").Append(value).Append(" |\n");
        }

        sb.Append("\n## Inputs and provenance\n\nEvery input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.\n");

        if (template.Id == CalcPackTemplates.MotorCircuit.Id)
            sb.Append("\nThe cable-capacity and device-rating tables are the library's synthetic test tables (`ConductorSizingCalculator.SyntheticAmpacity()` and `TypicalRatings()`), saved as unlicensed records titled “SYNTHETIC … (test values, not a regulation table)” and pinned to the cable step. They are not published values.\n");

        if (template.Id == CalcPackTemplates.SteelSupport.Id)
            sb.Append("\nThe base-plate step cites `PROFIS-BP1-PLACEHOLDER.txt`, filed against its Evidence; that file only says it is a placeholder.\n");

        sb.Append("\n## Files\n\n- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.\n");

        foreach (var step in template.Steps)
            sb.Append("- `").Append(step.Key).Append('-').Append(step.TemplateId.Replace('.', '-')).Append(".sheet.txt` — ").Append(step.Name).Append(", the issued sheet.\n");

        sb.Append("- `inputs.json` — every step's inputs as issued (links included), in the exchange form the Calc API reads.\n");
        sb.Append("\nGenerated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.\n");
        return sb.ToString();
    }
}
