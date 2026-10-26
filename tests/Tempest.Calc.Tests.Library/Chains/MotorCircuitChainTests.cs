using System.Text;
using Tempest.Calc.Electrical;
using Tempest.Core.Evidence;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>Addendum A, §A7 motor circuit: E1 → E6 → E4 → E3 / E5 → E2 instantiated from the built-in pack template.</summary>
public sealed class MotorCircuitChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All],
        packTemplates: CalcPackTemplates.All);

    [Fact]
    public async Task Motor_Demand_Reaches_The_Cable_Voltage_Drop_Fault_Check_And_Enclosure()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.MotorCircuit));
        var made = await ChainKit.InstantiateAsync(host, CalcPackTemplates.MotorCircuit, "Conveyor motor circuit", ChainInputs.MotorCircuit(host));
        var e1 = made.Instances["e1"];
        var e6 = made.Instances["e6"];
        var e4 = made.Instances["e4"];
        var e3 = made.Instances["e3"];
        var e5 = made.Instances["e5"];
        var e2 = made.Instances["e2"];

        Assert.Equal(Si(e1, "P_rms"), InputSi(e6, "P_add"), 9);
        Assert.True(Si(e1, "P_rms") < Si(e1, "P_pk") / 2, "the continuous demand is the cycle's RMS power, not the instantaneous peak");
        Assert.Equal(Si(e6, "I_3ph"), InputSi(e4, "I_b_in"), 9);
        Assert.Equal(Si(e6, "I_3ph"), Si(e4, "I_b"), 9);
        Assert.Equal(Si(e4, "I_b"), InputSi(e3, "I"), 9);
        Assert.Equal(Si(e4, "L"), InputSi(e3, "L"), 9);
        Assert.Equal(Si(e4, "CSA"), InputSi(e3, "A_c"), 9);
        Assert.Equal(Si(e4, "CSA"), InputSi(e5, "S_line"), 9);

        // chain.motor-circuit-06: the protective conductor follows the cable (it kept the 1.5 mm2 default whatever E4 chose).
        Assert.Equal(Si(e4, "CSA"), InputSi(e5, "S_cpc"), 9);
        Assert.Contains(e5.Inputs, i => i.Symbol == "S_cpc" && CalcLink.From(i.Provenance) is { OutputSymbol: "CSA" });
        Assert.Equal(Si(e4, "L"), InputSi(e5, "L"), 9);
        Assert.Equal(Si(e4, "I_n"), InputSi(e5, "I_n"), 9);
        Assert.Equal(Si(e4, "device_code"), InputSi(e5, "device_code"), 9);
        Assert.Equal(2, Si(e4, "device_code"), 9);
        Assert.Equal(Si(e6, "Phi"), InputSi(e2, "P_add"), 9);
        Assert.Equal(6, made.Pack.InstanceIds.Count);

        // The motor is outside the panel: E6's heat (24 V electronics and the fan, all of their input) is what the enclosure receives.
        Assert.Equal(0.12e3 + 0.02e3, Si(e6, "Phi"), 6);
        Assert.Equal(0.7841424404208093e3, Si(e1, "P_rms"), 6);

        // E3 is run as a three-phase circuit (it was DC), and the enclosure holds its limit.
        Assert.Equal("AC three-phase", e3.Inputs.Single(i => i.Symbol == "system").Text);
        Assert.All(e2.Checks, c => Assert.True(c.Passed, c.Name));
    }

    [Fact]
    public async Task A_Panel_Load_At_Half_Duty_Sizes_The_Cable_On_Its_Running_Current_Not_Half_Of_It()
    {
        // chain.motor-circuit-02 (with elec.load-budget-R1): a 7.5 kW fan at d 0.5 on the bus gave I_3ph for 3.75 kW, and E4 sized the cable for it. The cable carries the fan while it is on.
        var host = Host();
        var inputs = ChainInputs.MotorCircuit(host);
        var fan = CalcRow.Of(("name", new CalcCell(Text: "Supply fan")), ("supply", new CalcCell(Text: "AC three-phase")), ("P", new CalcCell(7.5, "kW")), ("cosphi", new CalcCell(0.85)), ("eta", new CalcCell(0.9)), ("heat", new CalcCell(0)), ("d", new CalcCell(0.5)), ("k", new CalcCell(1)), ("inrush", new CalcCell(1)), ("backed", new CalcCell(Flag: false)));
        inputs["e6"] = [.. inputs["e6"].Select(v => v.Symbol == "loads" ? CalcInputValue.Table("loads", [.. v.Rows!, fan]) with { Provenance = v.Provenance } : v)];
        var made = await ChainKit.InstantiateAsync(host, CalcPackTemplates.MotorCircuit, "Conveyor motor circuit", inputs);
        var (e6, e4) = (made.Instances["e6"], made.Instances["e4"]);

        var motor = InputSi(e6, "P_add") / InputSi(e6, "eta_add") / InputSi(e6, "cosphi_add");
        var expected = (motor + 7500 / 0.85) / (Math.Sqrt(3) * 400);
        Assert.Equal(expected, Si(e6, "I_3ph"), 6);
        Assert.Equal(Si(e6, "I_3ph"), Si(e4, "I_b"), 9);
        Assert.True(Si(e6, "I_3ph") > (motor + 0.5 * 7500 / 0.85) / (Math.Sqrt(3) * 400) + 5, "the fan counts at its running current, not at half of it");
    }

    [Fact]
    public async Task A_Fault_Check_On_A_Different_Device_Than_The_Cable_Step_Sized_Is_Blocked()
    {
        // Green G-B-01 / Black B-A-03: E4 selects a type C MCB; E5 used to keep its type B default and pass loop impedances up to twice the limit.
        var host = Host();
        var inputs = ChainInputs.MotorCircuit(host);
        inputs["e5"] = [.. inputs["e5"].Select(v => v.Symbol == "device" ? CalcInputValue.Choice("device", "MCB type B (I_a = 5 I_n)") with { Provenance = v.Provenance } : v)];
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.MotorCircuit, await host.ProjectAsync(), "Conveyor motor circuit", inputs);
        Assert.Null(refusal);
        var e5 = made!.Instances["e5"];
        var block = Assert.Single(e5.Blocks);
        Assert.StartsWith("device:", block.Message, StringComparison.Ordinal);
        Assert.Contains("MCB type C", block.Message, StringComparison.Ordinal);

        // A pack with a blocked member is not checked.
        await ChainKit.AuthoredByAnotherAsync(host, made);
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
    }

    [Fact]
    public async Task Changing_The_Cable_Steps_Device_Re_Blocks_The_Fault_Check_Until_It_Follows()
    {
        var host = Host();
        var made = await ChainKit.InstantiateAsync(host, CalcPackTemplates.MotorCircuit, "Conveyor motor circuit", ChainInputs.MotorCircuit(host));
        var e4 = await ChainKit.Fresh(host, made, "e4");
        var changed = await host.Service.UpdateInputsAsync(e4.Id, [.. e4.Inputs.Select(v => v.Symbol == "device" ? CalcInputValue.Choice("device", "MCB type D") with { Provenance = v.Provenance } : v)]);
        Assert.True(changed.Succeeded, changed.Reason);
        var e4New = await ChainKit.Fresh(host, made, "e4");
        Assert.Equal(3, Si(e4New, "device_code"), 9);
        var update = await host.Service.UpdateLinksAsync((await ChainKit.Fresh(host, made, "e5")).Id);
        Assert.True(update.Succeeded, update.Reason);
        var e5 = await ChainKit.Fresh(host, made, "e5");
        Assert.Equal(3, InputSi(e5, "device_code"), 9);
        Assert.Contains(e5.Blocks, b => b.RuleId == "device");
    }

    [Fact]
    public async Task A_Bigger_Motor_Makes_The_Chain_Stale_Blocks_The_Pack_And_Updating_Carries_The_Current_Through()
    {
        var host = Host();
        var made = await ChainKit.InstantiateAsync(host, CalcPackTemplates.MotorCircuit, "Conveyor motor circuit", ChainInputs.MotorCircuit(host));
        var e1 = made.Instances["e1"];

        await ChainKit.ReviseAndUpdateAsync(host, made, "e1", "m", 1.5, ["e6", "e4", "e3", "e5", "e2"]);

        var e1New = await Fresh(host, made, "e1");
        Assert.NotEqual(Si(e1, "P_rms"), Si(e1New, "P_rms"));
        var e6 = await Fresh(host, made, "e6");
        var e4 = await Fresh(host, made, "e4");
        var e3 = await Fresh(host, made, "e3");
        Assert.Equal(Si(e1New, "P_rms"), InputSi(e6, "P_add"), 9);
        Assert.Equal(Si(e6, "I_3ph"), Si(e4, "I_b"), 9);
        Assert.Equal(Si(e4, "I_b"), InputSi(e3, "I"), 9);
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
        var (ampacity, r1) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "cable.ampacity", "Panel wiring capacities", 0, "Owner's wiring regulations", "2018+A2:2022", "capacity table (owner's copy)", "S. Kreczman (own copy)", "s.kreczman",
            true, CalcDataOrigin.UserEntered, [new CalcDataValue("ampacity", Rows: ConductorSizingCalculator.SyntheticAmpacity().Rows)], DateTimeOffset.UnixEpoch));
        var (ratings, r2) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "device.ratings", "Preferred MCB ratings", 0, "Manufacturer catalogue", "2024", "MCB range", "—", "s.kreczman",
            false, CalcDataOrigin.UserEntered, [new CalcDataValue("ratings", Rows: ConductorSizingCalculator.TypicalRatings().Rows)], DateTimeOffset.UnixEpoch));
        Assert.Null(r1);
        Assert.Null(r2);

        var inputs = ChainInputs.MotorCircuit(host, CalcInstanceService.RecordProvenance(ampacity!), CalcInstanceService.RecordProvenance(ratings!));

        var made = await ChainKit.InstantiateAsync(host, CalcPackTemplates.MotorCircuit, "Conveyor motor circuit", inputs);

        // E4 and E5 demand an independent check: the chain is drawn up by another engineer. Full provenance, one check, one issue.
        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.MotorCircuit, "CONV-ELEC-001", "Client Co", "Checked against SPEC-LINE-12.");

        // The links are named on the issued sheet.
        Assert.Contains("link: Motor sizing rev A · P_rms", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Load budget rev A · I_3ph", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cable and device rev A · CSA", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cable and device rev A · device_code", sheet, StringComparison.Ordinal);

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
