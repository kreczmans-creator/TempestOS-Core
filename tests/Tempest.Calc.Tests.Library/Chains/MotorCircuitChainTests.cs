using System.Text;
using Tempest.Calc.Electrical;
using Tempest.Core.Evidence;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>Addendum A, §A7 motor circuit: E1 → E6 → E4 → E3 / E5 → E2 instantiated from the built-in pack template.</summary>
public sealed class MotorCircuitChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static double Si(CalcInstance instance, string symbol) => instance.Outputs.Single(o => o.Symbol == symbol).Si!.Value;

    private static double InputSi(CalcInstance instance, string symbol)
    {
        var input = instance.Inputs.Single(i => i.Symbol == symbol);
        return Tempest.Calc.Engine.Units.UnitCatalog.Parse(input.Unit ?? string.Empty).ToSi(input.Number!.Value);
    }

    // The ampacity and ratings records ship empty (§A5), so the chain is given the synthetic E4 test tables.
    private static Dictionary<string, IReadOnlyList<CalcInputValue>> Tables() => new(StringComparer.Ordinal)
    {
        ["e4"] = [ConductorSizingCalculator.TypicalRatings(), ConductorSizingCalculator.SyntheticAmpacity()],
    };

    [Fact]
    public async Task Motor_Demand_Reaches_The_Cable_Voltage_Drop_Fault_Check_And_Enclosure()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.MotorCircuit));
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.MotorCircuit, await host.ProjectAsync(), "Conveyor motor circuit", Tables());
        Assert.Null(refusal);
        var e1 = made!.Instances["e1"];
        var e6 = made.Instances["e6"];
        var e4 = made.Instances["e4"];
        var e3 = made.Instances["e3"];
        var e5 = made.Instances["e5"];
        var e2 = made.Instances["e2"];

        Assert.Equal(Si(e1, "P_pk"), InputSi(e6, "P_add"), 9);
        Assert.Equal(Si(e6, "I_3ph"), InputSi(e4, "I_b_in"), 9);
        Assert.Equal(Si(e6, "I_3ph"), Si(e4, "I_b"), 9);
        Assert.Equal(Si(e4, "I_b"), InputSi(e3, "I"), 9);
        Assert.Equal(Si(e4, "L"), InputSi(e3, "L"), 9);
        Assert.Equal(Si(e4, "CSA"), InputSi(e3, "A_c"), 9);
        Assert.Equal(Si(e4, "CSA"), InputSi(e5, "S_line"), 9);
        Assert.Equal(Si(e4, "L"), InputSi(e5, "L"), 9);
        Assert.Equal(Si(e4, "I_n"), InputSi(e5, "I_n"), 9);
        Assert.Equal(Si(e6, "Phi"), InputSi(e2, "P_add"), 9);
        Assert.Equal(6, made.Pack.InstanceIds.Count);
        Assert.All(made.Instances.Values, i => Assert.False(i.Blocks.Count > 0, $"{i.Name}: {string.Join("; ", i.Blocks.Select(b => b.Message))}"));
    }

    [Fact]
    public async Task A_Bigger_Motor_Makes_The_Chain_Stale_And_Updating_Carries_The_Current_Through()
    {
        var host = Host();
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.MotorCircuit, await host.ProjectAsync(), "Conveyor motor circuit", Tables());
        Assert.Null(refusal);
        var e1 = made!.Instances["e1"];

        var changed = await host.Service.UpdateInputsAsync(e1.Id, [.. e1.Inputs.Select(i => i.Number is { } n && i.Symbol == "m" ? i with { Number = n * 1.5 } : i)]);
        Assert.True(changed.Succeeded, changed.Reason);
        Assert.NotEqual(Si(e1, "P_pk"), Si(changed.Instance!, "P_pk"));

        foreach (var key in new[] { "e6", "e4", "e3", "e5", "e2" })
        {
            var current = (await host.Store.FindInstanceAsync(made.Instances[key].Id))!;

            if ((await host.Service.StalenessAsync(current)).IsStale)
                Assert.True((await host.Service.UpdateLinksAsync(current.Id)).Succeeded, key);
        }

        var e6 = (await host.Store.FindInstanceAsync(made.Instances["e6"].Id))!;
        var e4 = (await host.Store.FindInstanceAsync(made.Instances["e4"].Id))!;
        var e3 = (await host.Store.FindInstanceAsync(made.Instances["e3"].Id))!;
        Assert.Equal(Si(changed.Instance!, "P_pk"), InputSi(e6, "P_add"), 9);
        Assert.Equal(Si(e6, "I_3ph"), Si(e4, "I_b"), 9);
        Assert.Equal(Si(e4, "I_b"), InputSi(e3, "I"), 9);

        foreach (var key in new[] { "e6", "e4", "e3", "e5", "e2" })
            Assert.False((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(made.Instances[key].Id))!)).IsStale, $"{key} should be current");
    }

    [Fact]
    public async Task Without_The_Owners_Tables_The_Chain_Is_Refused_At_The_Cable_Step()
    {
        var host = Host();
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.MotorCircuit, await host.ProjectAsync(), "Conveyor motor circuit");
        Assert.Null(made);
        Assert.NotNull(refusal);
        Assert.StartsWith("Step 'e4' is blocked", refusal);
        Assert.Contains("ampacity:", refusal);
    }

    [Fact]
    public async Task The_Chain_Issues_As_One_Bundle_And_Its_Licensed_Ampacity_Table_Never_Leaves_In_An_Export()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (ampacity, r1) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "cable.ampacity", "Panel wiring capacities", 0, "Owner's wiring regulations", "2018+A2:2022", "capacity table (owner's copy)", "S. Kreczman (own copy)", "s.kreczman",
            true, CalcDataOrigin.UserEntered, [new CalcDataValue("ampacity", Rows: ConductorSizingCalculator.SyntheticAmpacity().Rows)], DateTimeOffset.UnixEpoch));
        var (ratings, r2) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "device.ratings", "Preferred MCB ratings", 0, "Manufacturer catalogue", "2024", "MCB range", "—", "s.kreczman",
            false, CalcDataOrigin.UserEntered, [new CalcDataValue("ratings", Rows: ConductorSizingCalculator.TypicalRatings().Rows)], DateTimeOffset.UnixEpoch));
        Assert.Null(r1);
        Assert.Null(r2);

        var spec = CalcProvenance.Specification("SPEC-CONV-01 §4");
        var inputs = new Dictionary<string, IReadOnlyList<CalcInputValue>>(StringComparer.Ordinal);

        foreach (var step in CalcPackTemplates.MotorCircuit.Steps)
        {
            var template = host.Catalogue.Get(step.TemplateId).Template;
            List<CalcInputValue> values = [.. CalcTemplateHasher.NewSheetInputs(template).Values.Select(v => v with { Provenance = spec })];

            if (step.Key == "e4")
            {
                values.Add(ConductorSizingCalculator.SyntheticAmpacity() with { Provenance = CalcInstanceService.RecordProvenance(ampacity!) });
                values.Add(ConductorSizingCalculator.TypicalRatings() with { Provenance = CalcInstanceService.RecordProvenance(ratings!) });
            }

            inputs[step.Key] = values;
        }

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.MotorCircuit, project, "Conveyor motor circuit", inputs);
        Assert.Null(refusal);

        // E4 and E5 demand an independent check: the chain was drawn up by another engineer.
        foreach (var instance in made!.Instances.Values)
        {
            var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
            await host.Store.SaveInstanceAsync(stored with { CreatedBy = "another.engineer" }, stored.UpdatedAt);
        }

        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "Checked against SPEC-CONV-01.", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        var issued = await host.Service.IssuePackAsync(made.Pack.Id, "CONV-ELEC-001", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);

        var pack = (await host.Store.FindPackAsync(made.Pack.Id))!;
        Assert.Equal(CalcInstanceStatus.Issued, pack.Status);
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(pack.EvidenceId!.Value))!;
        var attachment = Assert.Single(await evidence.GetAttachmentsAsync());
        var sheet = Encoding.UTF8.GetString((await evidence.ReadAttachmentContentAsync(attachment.Id)).Bytes);
        Assert.Contains("PACK INDEX", sheet, StringComparison.Ordinal);

        foreach (var step in CalcPackTemplates.MotorCircuit.Steps)
        {
            Assert.Contains(step.Name, sheet, StringComparison.Ordinal);
            Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(made.Instances[step.Key].Id))!.Status);
        }

        // The issued sheet may cite the licensed record it used (it is the user's own calculation) ...
        Assert.Contains("(licensed source)", sheet, StringComparison.Ordinal);

        // ... but the record never leaves, and an input export withholds its rows while keeping the pin.
        var (csv, refused) = await host.Service.ExportRecordAsync(ampacity!.Id);
        Assert.Null(csv);
        Assert.Contains("never exported", refused, StringComparison.Ordinal);

        var e4 = (await host.Store.FindInstanceAsync(made.Instances["e4"].Id))!;
        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet(e4.Inputs));
        Assert.Null(withheld.Find("ampacity")!.Rows);
        Assert.Contains("licensed value withheld", withheld.Find("ampacity")!.Provenance!.Reference, StringComparison.Ordinal);
        Assert.NotNull(withheld.Find("ratings")!.Rows);

        var fields = CalcCsv.ExportInputs(host.Catalogue.Get("elec.conductor-sizing").Template, withheld)
            .Split('\n').SelectMany(line => line.Split(',')).Select(f => f.Trim().Trim('"')).ToList();

        foreach (var capacity in new[] { "12.3", "17.7", "24.1", "31.9", "44.6" })
            Assert.DoesNotContain(fields, f => f.Contains(capacity, StringComparison.Ordinal));
    }
}
