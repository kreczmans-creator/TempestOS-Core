using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library;

/// <summary>A calculator that draws a factor and a table from reference records (§A3).</summary>
internal sealed class RecordedCalculator : ICalcCalculator
{
    private static readonly IReadOnlyList<CalcColumnSpec> Columns = [new("S", "Size", CalcInputKind.Quantity, "mm^2"), new("I_t", "Rating", CalcInputKind.Quantity, "A")];

    public CalcTemplate Template { get; } = new(
        "test.recorded", "1.0.0", CalcDiscipline.Electrical, "Recorded factors",
        "Uses a tabulated factor and a tabulated rating table.",
        "Scaled rating: I = k_1 · max(I_t).",
        [
            new CalcInputSpec("k_1", "Tabulated factor", CalcInputKind.Number, Default: 1, Min: 0.1, Max: 2, HardMin: 0, HardMax: 10),
            new CalcInputSpec("ratings", "Tabulated ratings", CalcInputKind.Table, Columns: Columns, MaxRows: 50),
        ],
        [new CalcAssumption("Test only.", "—")],
        [new CalcClauseRef("Test", "—", "Test")],
        [],
        [
            new CalcTestCase("ka-1", "k = 1", new CalcReferenceSource("Hand", null, "1 × 20"), [CalcInputValue.Of("k_1", 1), Rated(20)], [new CalcExpectedValue("I", 20, "A", 1e-9)]),
            new CalcTestCase("ka-2", "k = 0.5", new CalcReferenceSource("Hand", null, "0.5 × 20"), [CalcInputValue.Of("k_1", 0.5), Rated(20)], [new CalcExpectedValue("I", 10, "A", 1e-9)]),
        ],
        RecordKinds: [new CalcRecordKind("test.factors", "Test factors", ["k_1", "ratings"], "From the user's own copy of the test table.")]);

    public static CalcInputValue Rated(double amps) =>
        CalcInputValue.Table("ratings", [CalcRow.Of(("S", new CalcCell(2.5, "mm^2")), ("I_t", new CalcCell(amps, "A")))]);

    public void Run(CalcRunContext c)
    {
        c.Input("k_1");
        var top = c.Rows("ratings").Select(r => r.Cells.TryGetValue("I_t", out var cell) ? cell.Number ?? 0 : 0).DefaultIfEmpty(0).Max();
        c.Record("I_t_max", "max(I_t)", top.ToString(System.Globalization.CultureInfo.InvariantCulture) + " A", Tempest.Calc.Engine.Values.ScalarValue.Of(top, "A"), "Largest tabulated rating");
        c.Assign("I", "k_1 * I_t_max", "A", "Scaled rating");
        c.Output("I", "Scaled rating");
    }
}

/// <summary>Addendum A, §A3 and §A5: reference records and the licensed-source export lock.</summary>
public sealed class RecordTests
{
    private static LibraryTestHost Host() => new(calculators: [new RecordedCalculator()]);

    private static CalcDataRecord Draft(bool licensed, double k = 0.8, double amps = 27) => new(
        Guid.Empty, "test.factors", "Grouping factors", 0, "Test Wiring Rules", "2018+A2:2022", "Table 4C1", "IET (user's own copy)", "s.kreczman",
        licensed, CalcDataOrigin.UserEntered,
        [new CalcDataValue("k_1", k), new CalcDataValue("ratings", Rows: RecordedCalculator.Rated(amps).Rows)], DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task A_Record_Is_Revised_Never_Edited_And_Keeps_Its_Kind_And_Its_Source()
    {
        var host = Host();
        var (first, refusal) = await host.Service.SaveRecordAsync(Draft(licensed: true));
        Assert.Null(refusal);
        Assert.Equal(1, first!.Revision);

        var (second, _) = await host.Service.SaveRecordAsync(Draft(licensed: true, k: 0.7) with { Id = first.Id });
        Assert.Equal(2, second!.Revision);
        Assert.Equal(0.8, (await host.Store.FindRecordAsync(first.Id, 1))!.Values[0].Number);
        Assert.Equal(2, (await host.Store.FindRecordAsync(first.Id))!.Revision);
        await Assert.ThrowsAsync<CalcInstanceImmutableException>(() => host.Store.SaveRecordAsync(second));

        var (_, wrongKind) = await host.Service.SaveRecordAsync(Draft(true) with { Id = first.Id, Kind = "other.kind" });
        Assert.Contains("keeps its kind", wrongKind, StringComparison.Ordinal);
        var (_, noEdition) = await host.Service.SaveRecordAsync(Draft(true) with { Edition = " " });
        Assert.Contains("edition and amendment", noEdition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Applying_A_Record_Pins_It_And_A_Later_Revision_Makes_The_Calculation_Stale()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (record, _) = await host.Service.SaveRecordAsync(Draft(licensed: true));
        var calc = await host.Service.CreateAsync("test.recorded", project, "Feeder", [CalcInputValue.Of("k_1", 1, CalcProvenance.Specification("typed")), RecordedCalculator.Rated(20) with { Provenance = CalcProvenance.Specification("typed") }]);
        Assert.Contains(calc.Warnings, w => w.RuleId == "record.k_1");

        var applied = await host.Service.ApplyRecordAsync(calc.Id, record!.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var k = applied.Instance!.Inputs.Single(i => i.Symbol == "k_1");
        Assert.Equal(0.8, k.Number);
        Assert.Equal((record.Id, 1), CalcDataRecord.PinOf(k.Provenance));
        Assert.DoesNotContain(applied.Instance.Warnings, w => w.RuleId.StartsWith("record.", StringComparison.Ordinal));
        Assert.Equal(21.6, applied.Instance.Outputs.Single().Si!.Value, 9);

        var sheet = (await host.Service.RunAsync(calc.Id))!.SheetText;
        Assert.Contains("record: Grouping factors — Test Wiring Rules, 2018+A2:2022, Table 4C1 (licensed source) rev 1", sheet, StringComparison.Ordinal);

        await host.Service.SaveRecordAsync(Draft(licensed: true, k: 0.7) with { Id = record.Id });
        var stale = await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!);
        Assert.Contains(stale.Reasons, r => r.Contains("revision 2 has been entered since", StringComparison.Ordinal));
        Assert.False((await host.Service.CheckAsync(calc.Id, "J. Reviewer", "Client Co", "OK", Tempest.Core.Evidence.CheckOutcome.Accepted)).Succeeded);

        Assert.True((await host.Service.ApplyRecordAsync(calc.Id, record.Id)).Succeeded);
        Assert.False((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }

    [Fact]
    public async Task Values_From_A_Licensed_Record_Never_Leave_Through_An_Export()
    {
        var host = Host();
        var (licensed, _) = await host.Service.SaveRecordAsync(Draft(licensed: true));
        var (derived, _) = await host.Service.SaveRecordAsync(Draft(licensed: false) with { Title = "MCB multiples", Origin = CalcDataOrigin.Derived, LicenceHolder = "—" });

        var (csv, refused) = await host.Service.ExportRecordAsync(licensed!.Id);
        Assert.Null(csv);
        Assert.Contains("never exported", refused, StringComparison.Ordinal);
        var (open, _) = await host.Service.ExportRecordAsync(derived!.Id);
        Assert.Contains("k_1,0.8", open, StringComparison.Ordinal);
        Assert.Contains("(derived)", open, StringComparison.Ordinal);

        var inputs = new CalcInputSet([
            new CalcInputValue("k_1", Number: 0.8, Provenance: CalcInstanceService.RecordProvenance(licensed)),
            RecordedCalculator.Rated(27) with { Provenance = CalcInstanceService.RecordProvenance(licensed) },
        ]);
        var withheld = await host.Service.WithholdLicensedAsync(inputs);
        Assert.All(withheld.Values, v => Assert.True(v.Number is null && v.Rows is null, $"{v.Symbol} left with its value"));
        Assert.Contains("licensed value withheld", withheld.Find("k_1")!.Provenance!.Reference, StringComparison.Ordinal);

        var csvOut = CalcCsv.ExportInputs(new RecordedCalculator().Template, withheld);
        // Whole fields only: the record's pin is a random GUID and may itself contain "27".
        var fields = csvOut.Split('\n').SelectMany(line => line.Split(',')).Select(f => f.Trim().Trim('"')).ToList();
        Assert.DoesNotContain("0.8", fields);
        Assert.DoesNotContain(fields, f => f.StartsWith("27", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Licensed_Values_Cannot_Be_Laundered_Into_An_Exportable_Record()
    {
        // Opus review finding 5: a licensed record's revisions stay licensed, and a record saved from inputs that cite a
        // licensed record is licensed too.
        var host = Host();
        var (licensed, _) = await host.Service.SaveRecordAsync(Draft(licensed: true));
        var (unlocked, refusal) = await host.Service.SaveRecordAsync(licensed! with { LicensedSource = false });
        Assert.Null(unlocked);
        Assert.Contains("stay licensed", refusal, StringComparison.Ordinal);

        var inputs = new[] { new CalcInputValue("k_1", Number: 0.8, Provenance: CalcInstanceService.RecordProvenance(licensed)) };
        var (copy, _) = await host.Service.SaveRecordFromInputsAsync(Draft(licensed: false) with { Title = "My copy" }, inputs);
        Assert.True(copy!.LicensedSource);
        Assert.Null((await host.Service.ExportRecordAsync(copy.Id)).Csv);
    }

    [Fact]
    public async Task A_Record_Saved_From_Unlicensed_Inputs_Stays_Unlicensed()
    {
        var host = Host();
        var (copy, _) = await host.Service.SaveRecordFromInputsAsync(Draft(licensed: false), [new CalcInputValue("k_1", Number: 0.8, Provenance: CalcProvenance.Assumed("typed"))]);
        Assert.False(copy!.LicensedSource);
    }
}
