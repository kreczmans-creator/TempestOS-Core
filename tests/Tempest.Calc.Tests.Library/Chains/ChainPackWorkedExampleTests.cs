using System.Globalization;
using System.Text;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum A, §A7: one issued calc pack per built-in chain (drive train, motor circuit, wet heating, cooling, steel support, hydraulic power unit), written
/// under docs/worked-examples/packs/&lt;chain&gt; when TEMPEST_WRITE_WORKED_EXAMPLES=1: the pack's issue sheet, each step's issued sheet,
/// every step's inputs and a README naming the chain, every link and the issue reference. The PDFs are made later.
/// </summary>
public sealed class ChainPackWorkedExampleTests
{
    public static TheoryData<string> Chains => ["drive-train", "motor-circuit", "wet-heating", "cooling", "steel-support", "hydraulic-power-unit"];

    private sealed record Example(CalcPackTemplate Template, string Slug, string Title, string Reference, string Client, string Project, string Note, Func<LibraryTestHost, Task<IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>>>> Inputs, string Remarks);

    private static IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>> Plain(Dictionary<string, IReadOnlyList<CalcInputValue>> inputs) => inputs;

    private static readonly Example[] Examples =
    [
        new(CalcPackTemplates.DriveTrain, "drive-train", "Conveyor head drive calc pack", "CNV-DRV-001", "Client Co", "Conveyor 7 head drive", "Checked against SPEC-CNV-07 and DRG-CNV-410 to 412 rev B.",
            host => Task.FromResult(Plain(ChainInputs.DriveTrain(host))),
            "A 4-pole motor (about 24 kW at 1500 rpm) turns a conveyor drum at 120 rpm through a 12.5:1 gearbox against a steady 1.8 kN·m. The motor's torque and speed are the gearbox input shaft's; the shaft's reaction and speed give bearing A its load and speed; the shaft's seat diameter is the nominal size of the bearing-bore fit (the bore has its own tolerance, entered from the bearing record, against a k6 seat); and the peak torque the load demands at the motor shaft (T_pk, run-up included) is the torque the four-bolt coupling flange carries and the torque the shaft's yield check sees. The gear mesh forces on the shaft are entered for the motor's torque (a pinion of 60 mm pitch radius), not linked."),
        new(CalcPackTemplates.MotorCircuit, "motor-circuit", "Servo axis motor circuit calc pack", "LINE-ELEC-001", "Client Co", "Line 12 axis 3", "Checked against SPEC-LINE-12 and DRG-LINE-120 rev B.",
            async host => Plain(await MotorCircuitInputsAsync(host)),
            "A ball-screw servo axis (50 kg, 0.5 m in 0.5 s) is sized, its panel demand and heat found, its cable and device chosen, and the voltage drop, earth-fault disconnection and enclosure temperature checked. **The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**"),
        new(CalcPackTemplates.WetHeating, "wet-heating", "Plant room heating calc pack", "HTG-001", "Client Co", "Plant room heating", "Checked against SPEC-HTG-01 §3.",
            host => Task.FromResult(Plain(ChainInputs.WetHeating(host))),
            "Two rooms' design heat losses are the two rows of the sealed system's room table (table-cell links); the sealed system's total heat, flow-to-return difference and mean water temperature give the primary pipework its flow and fluid; the pump's shaft torque and speed are the circulator motor's load. The loop is closed, so there is no static lift, and the 40 m of 10 mm microbore carries about 57 L/h for a 0.64 kW load: the pump duty is a deliberately tiny illustration. The sealed system's pipework water content (3.14 L) is entered, equal to the pipework's own V_pipe, because a link back from the pipework would be a cycle."),
        new(CalcPackTemplates.Cooling, "cooling", "Drive panel cooling calc pack", "MCC-COOL-001", "Client Co", "Motor control centre 3", "Checked against SPEC-MCC-03 and DRG-MCC-210/215 rev B.",
            host => Task.FromResult(Plain(ChainInputs.Cooling(host))),
            "The heat a drive panel dissipates is a linked load of its enclosure; the heat the enclosure cannot lose naturally is the required duty of a water-cooled air coil. The coil's hot side is the enclosure air, entering at the enclosure's temperature limit (E2 `T_max`, linked) with c_p 1006 J/(kg·K), the conventional dry-air value E2 itself uses; the air flow through the coil (0.4 kg/s) and U = 60 W/(m²·K) are the coil's own figures, and no intermediate water loop inside the enclosure is modelled. The coil's duty, cold-side temperature rise, mean cold-side temperature and cold-side specific heat (linked, so a glycol mixture changes the flow) give the cooling-water pipework and pump their flow and fluid. The enclosure step's own check, natural dissipation holding the limit, FAILS by design: that is the reason the coil is needed, and the duty it hands over is exactly the shortfall."),
        new(CalcPackTemplates.SteelSupport, "steel-support", "Equipment support steelwork calc pack", "STR-SUP-001", "Client Co", "Equipment support ES-4", "Checked against SPEC-STR-09 and DRG-STR-310 to 315 rev B.",
            host => Task.FromResult(Plain(ChainInputs.SteelSupport(host))),
            "The equipment's dead (12 kN) and maintenance (8 kN) loads, both downward, are combined (EN 1990 6.10: 28.2 kN); that ULS effect is a cell of the frame's load table; the frame's column forces check the column; the arm's end shear and moment load a six-bolt group whose governing-bolt shear and tension are the arm connection's design forces (S4); the frame's base reactions are the base plate's actions; and a second S4 run for the 25 mm base plate gives S6 its holding-down bolt data. The 5 kN horizontal action at the column top is an ULS design value (combined at factor 1). **The links depend on the frame's numbering: nodes 1 (column base, fixed), 2 (column top) and 3 (arm tip); member 1 the column (1 → 2) and member 2 the arm (2 → 3); the first load row the vertical load at node 3 (filled from the load combinations) and the second the horizontal action at node 2.** The bolt group's bolt resistances (94.0 kN shear, 141.0 kN tension) are typed values equal to what the arm connection step computes for M20 8.8; a test guards the match. The holding-down bolts' own demand fields are zero: S6 checks the anchor bolts against the base reactions. **The anchorage (concrete and anchor design) is done in Hilti PROFIS: the file attached to the base-plate step is a PLACEHOLDER, not a PROFIS report, to be replaced by the engineer's own report before the pack is used.**"),
        new(CalcPackTemplates.HydraulicPowerUnit, "hydraulic-power-unit", "Press hydraulic power unit calc pack", "HPU-001", "Client Co", "Press 5 hydraulic power unit", "Checked against SPEC-HPU-05 and DRG-HPU-510 rev B.",
            async host => Plain(await HydraulicPowerUnitInputsAsync(host)),
            "A 100/70 × 500 mm cylinder at 160 bar and 36 L/min, fed by a 28 cm³/rev pump at 1450 rpm (38.57 L/min delivered), presses with 80 kN and pulls with 40 kN. Its swept volume (3.927 L, the extend stroke) and its supply pressure (160 bar) size an emergency-stroke accumulator (26.7 L of gas, n = 1.4, 10 to 50 °C, working range 120 to 160 bar; 120 bar still finishes the 80 kN push, which needs 107 bar). Its circuit flow (36 L/min = 2.16 m³/h) runs through 12 m of 16 mm pressure line; the oil (870 kg/m³, 30 mPa·s) and the tube bore are entered equal to the cylinder step's `rho` and `d_prs` (the cylinder step offers no output for them, and a test guards the match), the flow is laminar (Re 1385) and the line costs 157.5 kPa (1.6 bar of the 160). The pump's shaft torque (76.97 N·m) and speed (1450 rpm) are the continuous load of its drive motor (77.75 N·m through a 0.99 coupling, 11.81 kW, against a candidate 18.5 kW motor of 120 N·m), and that continuous shaft power sizes the supply cable (400 V, η 0.92, cos φ 0.86: 21.5 A, a 25 A type C device, 6 mm²). The cable is sized for the pump's continuous demand, not the nameplate of the motor that will be fitted, and the pressure line carries the circuit flow because the relief valve is taken to be at the pump (the cylinder step takes the larger pump delivery for its own line velocities). **The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**"),
    ];

    /// <summary>The motor circuit's inputs with the synthetic E4 tables saved as (unlicensed, synthetic) records and pinned.</summary>
    internal static async Task<Dictionary<string, IReadOnlyList<CalcInputValue>>> MotorCircuitInputsAsync(LibraryTestHost host)
    {
        var (ampacity, ratings) = await SyntheticCableRecordsAsync(host);
        return ChainInputs.MotorCircuit(host, ampacity, ratings);
    }

    /// <summary>The hydraulic power unit's inputs with the synthetic E4 tables saved as (unlicensed, synthetic) records and pinned.</summary>
    internal static async Task<Dictionary<string, IReadOnlyList<CalcInputValue>>> HydraulicPowerUnitInputsAsync(LibraryTestHost host)
    {
        var (ampacity, ratings) = await SyntheticCableRecordsAsync(host);
        return ChainInputs.HydraulicPowerUnit(host, ampacity, ratings);
    }

    /// <summary>The cable step's two tables as SYNTHETIC records (test values, not from any regulation or manufacturer), and the provenance that pins each.</summary>
    private static async Task<(CalcProvenance Ampacity, CalcProvenance Ratings)> SyntheticCableRecordsAsync(LibraryTestHost host)
    {
        var (ampacity, r1) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "cable.ampacity", "SYNTHETIC cable capacities (test values, not a regulation table)", 0, "Synthetic test data, not from any standard", "n/a", "invented values for the worked example", "Tempest test data", "tempest.tests",
            false, CalcDataOrigin.UserEntered, [new CalcDataValue("ampacity", Rows: ConductorSizingCalculator.SyntheticAmpacity().Rows)], DateTimeOffset.UnixEpoch));
        var (ratings, r2) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "device.ratings", "SYNTHETIC device ratings (test values, not a manufacturer table)", 0, "Synthetic test data, not from any catalogue", "n/a", "invented values for the worked example", "Tempest test data", "tempest.tests",
            false, CalcDataOrigin.UserEntered, [new CalcDataValue("ratings", Rows: ConductorSizingCalculator.TypicalRatings().Rows)], DateTimeOffset.UnixEpoch));
        Assert.Null(r1);
        Assert.Null(r2);
        return (CalcInstanceService.RecordProvenance(ampacity!), CalcInstanceService.RecordProvenance(ratings!));
    }

    private static async Task<Guid?> ProjectAsync(LibraryTestHost host, string name)
    {
        var factory = new EngineeringObjectFactory<Project>(
            "Project", host.Context, (doc, rev) => new Project(doc, rev, host.Context, "EX-" + name.Split(' ')[0].ToUpperInvariant(), name, EngineeringObjectMetadata.Empty));
        return (await factory.CreateAsync("Worked example project.")).Id;
    }

    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All, .. MechanicalCalculators.All, .. BuildingServicesCalculators.All, .. StructuralCalculators.All, .. FluidPowerCalculators.All],
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

        if (template.Id == CalcPackTemplates.MotorCircuit.Id || template.Id == CalcPackTemplates.HydraulicPowerUnit.Id)
            sb.Append("\nThe cable-capacity and device-rating tables are the library's synthetic test tables (`ConductorSizingCalculator.SyntheticAmpacity()` and `TypicalRatings()`), saved as unlicensed records titled “SYNTHETIC … (test values, not a regulation table)” and pinned to the cable step. They are not published values.\n");

        if (template.Id == CalcPackTemplates.SteelSupport.Id)
            sb.Append("\nThe base-plate step cites `PROFIS-BP1-PLACEHOLDER.txt`, filed against its Evidence; that file only says it is a placeholder, and the base plate's anchorage condition reads “external report attached: PROFIS-BP1-PLACEHOLDER.txt” so the issued sheet names what was attached. Its PASS in the base-plate step therefore records that a file is attached, not that the anchorage has been designed.\n");

        sb.Append("\n## Files\n\n- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.\n");

        foreach (var step in template.Steps)
            sb.Append("- `").Append(step.Key).Append('-').Append(step.TemplateId.Replace('.', '-')).Append(".sheet.txt` — ").Append(step.Name).Append(", the issued sheet.\n");

        sb.Append("- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.\n");
        sb.Append("\nGenerated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.\n");
        return sb.ToString();
    }
}
