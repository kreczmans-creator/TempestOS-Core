using System.Text;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Tests.Library.Materials;

namespace Tempest.Calc.Tests.Library;

/// <summary>A sink that takes a current, a volume and a modulus by link, and a table of currents: the places a licensed value could reach downstream.</summary>
internal sealed class LicenceSinkCalculator : ICalcCalculator
{
    public const string Id = "test.licence-sink";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Electrical, "Licence sink",
        "Takes values by link; a test sink for the licensed-value export lock.",
        "Output: I_out = I_in.",
        [
            new CalcInputSpec("I_in", "Current", CalcInputKind.Quantity, "A", Default: 1, Min: 0, Max: 1e9, HardMin: 0, HardMax: 1e12),
            new CalcInputSpec("V_in", "Volume", CalcInputKind.Quantity, "L", IsOptional: true, HardMin: 0, HardMax: 1e12),
            new CalcInputSpec("E_in", "Modulus", CalcInputKind.Quantity, "GPa", IsOptional: true, HardMin: 0, HardMax: 1e12),
            new CalcInputSpec("taps", "Taps", CalcInputKind.Table, IsOptional: true, MaxRows: 20, Columns:
            [
                new CalcColumnSpec("name", "Tap", CalcInputKind.Text, IsOptional: true),
                new CalcColumnSpec("I", "Current", CalcInputKind.Quantity, "A", HardMin: 0, HardMax: 1e12),
            ]),
        ],
        [new CalcAssumption("Test only.", "—")],
        [new CalcClauseRef("Test", "—", "Test")],
        [],
        [
            new CalcTestCase("ka-1", "2 A", new CalcReferenceSource("Hand", null, "2"), [CalcInputValue.Quantity("I_in", 2, "A")], [new CalcExpectedValue("I_out", 2, "A", 1e-9)]),
            new CalcTestCase("ka-2", "3 A", new CalcReferenceSource("Hand", null, "3"), [CalcInputValue.Quantity("I_in", 3, "A")], [new CalcExpectedValue("I_out", 3, "A", 1e-9)]),
        ],
        OutputPorts: [new CalcOutputPort("I_out", "A", "Current"), new CalcOutputPort("V_out", "L", "Volume"), new CalcOutputPort("E_out", "GPa", "Modulus")]);

    public void Run(CalcRunContext c)
    {
        c.Input("I_in");
        c.Assign("I_out", "I_in", "A", "Current");
        c.Output("I_out", "Current");

        if (c.Has("V_in"))
        {
            c.Input("V_in");
            c.Assign("V_out", "V_in", "L", "Volume");
            c.Output("V_out", "Volume");
        }

        if (c.Has("E_in"))
        {
            c.Input("E_in");
            c.Assign("E_out", "E_in", "GPa", "Modulus");
            c.Output("E_out", "Modulus");
        }

        if (c.Has("taps"))
            _ = c.Rows("taps").Count;
    }
}

/// <summary>A calculator that takes a material's modulus and gives it back unchanged: a licensed property passed through to its output.</summary>
internal sealed class MaterialPassThroughCalculator : ICalcCalculator
{
    public const string Id = "test.material-passthrough";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Mechanical, "Material pass-through",
        "Gives the modulus of a material as an output.",
        "Output: E_out = E.",
        [new CalcInputSpec("E", "Young's modulus", CalcInputKind.Quantity, "GPa", Default: 200, Min: 1, Max: 1000, HardMin: 0, HardMax: 10000, MaterialProperty: "E")],
        [new CalcAssumption("Test only.", "—")],
        [new CalcClauseRef("Test", "—", "Test")],
        [],
        [
            new CalcTestCase("ka-1", "210", new CalcReferenceSource("Hand", null, "210"), [CalcInputValue.Quantity("E", 210, "GPa")], [new CalcExpectedValue("E_out", 210, "GPa", 1e-9)]),
            new CalcTestCase("ka-2", "70", new CalcReferenceSource("Hand", null, "70"), [CalcInputValue.Quantity("E", 70, "GPa")], [new CalcExpectedValue("E_out", 70, "GPa", 1e-9)]),
        ],
        OutputPorts: [new CalcOutputPort("E_out", "GPa", "Young's modulus")],
        RecordKinds: [MaterialLibrary.RecordKind(["E"])]);

    public void Run(CalcRunContext c)
    {
        c.Input("E");
        c.Assign("E_out", "E", "GPa", "Young's modulus");
        c.Output("E_out", "Young's modulus");
    }
}

/// <summary>
/// B-0 review, M6: a licensed value is withheld on every export path wherever it travels by link — an input or a table cell bound to a
/// calculation that holds an input pinned to a licensed record, at any depth. Conservative: it is withheld whether or not that input reaches
/// the output linked to. The issued sheet prints the link and says so in place of the number.
/// </summary>
public sealed class LicensedLinkTests
{
    private static readonly CalcProvenance Spec = CalcProvenance.Specification("SPEC-LIC-01");

    private static LibraryTestHost Host() => new(calculators: [new SealedSystemCalculator(), new LicenceSinkCalculator(), new MaterialPassThroughCalculator(), new RecordedCalculator()]);

    private static CalcDataRecord Fluid(bool licensed) => new(
        Guid.Empty, "fluid.expansion", licensed ? "Glycol 30 % (supplier)" : "Glycol 30 % (derived)", 0, "Supplier data sheet", "rev 1", "Table 2", "Supplier Ltd", "ignored", licensed,
        licensed ? CalcDataOrigin.UserEntered : CalcDataOrigin.Derived, [new CalcDataValue("e_fluid", 0.035)], default);

    /// <summary>B6 on the vessel case, its expansion coefficient taken from a fluid record.</summary>
    private static async Task<CalcInstance> SealedSystemAsync(LibraryTestHost host, Guid? project, CalcDataRecord fluid, string name = "B6 loop")
    {
        var template = new SealedSystemCalculator().Template;
        var inputs = template.TestCases!.Single(c => c.Id == "ka-vessel").Inputs.Select(i => i with { Provenance = Spec }).ToList();
        var made = await host.Service.CreateAsync(SealedSystemCalculator.Id, project, name, inputs);
        var applied = await host.Service.ApplyRecordAsync(made.Id, fluid.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        Assert.Contains(applied.Instance!.Inputs, i => i.Symbol == "e_fluid");
        Assert.Contains(applied.Instance.Outputs, o => o.Symbol == "V_e");
        return applied.Instance;
    }

    private static async Task<CalcDataRecord> SavedAsync(LibraryTestHost host, CalcDataRecord draft)
    {
        var (record, refusal) = await host.Service.SaveRecordAsync(draft);
        Assert.Null(refusal);
        return record!;
    }

    private static async Task<CalcInstance> SinkAsync(LibraryTestHost host, Guid? project, string name = "Sink") =>
        await host.Service.CreateAsync(LicenceSinkCalculator.Id, project, name, [CalcInputValue.Quantity("I_in", 2, "A", Spec)]);

    private static async Task<CalcInstance> BoundAsync(LibraryTestHost host, Guid downstream, string input, Guid upstream, string output)
    {
        var bound = await host.Service.BindAsync(downstream, input, upstream, output);
        Assert.True(bound.Succeeded, bound.Reason);
        return bound.Instance!;
    }

    private static CalcSheetInputRow Row(CalcSheet sheet, string symbol) => sheet.Inputs.Single(r => r.Symbol == symbol);

    [Fact]
    public async Task A_Value_Linked_From_A_Calculation_That_Holds_A_Licensed_Fluid_Record_Is_Withheld_From_The_Inputs_Export()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var licensed = await SavedAsync(host, Fluid(licensed: true));
        var b6 = await SealedSystemAsync(host, project, licensed);
        var vessel = b6.Outputs.Single(o => o.Symbol == "V_e").Si!.Value * 1000;   // litres
        Assert.True(vessel > 1);

        var sink = await BoundAsync(host, (await SinkAsync(host, project)).Id, "V_in", b6.Id, "V_e");
        Assert.Equal(vessel, sink.Inputs.Single(i => i.Symbol == "V_in").Number!.Value, 9);   // the calculation itself still uses the value

        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet(sink.Inputs));
        var v = withheld.Find("V_in")!;
        Assert.Null(v.Number);
        Assert.Contains("licensed value withheld", v.Provenance!.Reference, StringComparison.Ordinal);
        Assert.Contains($"calc:{b6.Id:N}#V_e", v.Provenance.Reference, StringComparison.Ordinal);
        Assert.Equal(2, withheld.Find("I_in")!.Number);   // an unlinked value is untouched

        var csv = CalcCsv.ExportInputs(new LicenceSinkCalculator().Template, withheld);
        Assert.DoesNotContain(vessel.ToString("R", System.Globalization.CultureInfo.InvariantCulture), csv, StringComparison.Ordinal);
        var (_, notices) = CalcCsv.ImportInputs(new LicenceSinkCalculator().Template, csv);
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("V_in", StringComparison.Ordinal) && n.Message.Contains("withheld", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_Same_Link_To_An_Unlicensed_Fluid_Record_Exports_As_Before()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var open = await SavedAsync(host, Fluid(licensed: false));
        var b6 = await SealedSystemAsync(host, project, open);
        var sink = await BoundAsync(host, (await SinkAsync(host, project)).Id, "V_in", b6.Id, "V_e");

        var kept = await host.Service.WithholdLicensedAsync(new CalcInputSet(sink.Inputs));
        Assert.Equal(sink.Inputs.Single(i => i.Symbol == "V_in").Number, kept.Find("V_in")!.Number);
        Assert.False(CalcCsv.IsWithheld(kept.Find("V_in")!.Provenance));

        var sheet = await host.Service.SheetAsync(sink);
        Assert.DoesNotContain("withheld", Row(sheet, "V_in").Value, StringComparison.Ordinal);
        Assert.NotEmpty(Row(sheet, "V_in").Unit);
    }

    [Fact]
    public async Task The_Issued_Sheet_Prints_The_Link_And_Says_The_Value_Is_Withheld_In_Place_Of_The_Number()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var licensed = await SavedAsync(host, Fluid(licensed: true));
        var b6 = await SealedSystemAsync(host, project, licensed);
        var sink = await BoundAsync(host, (await SinkAsync(host, project)).Id, "V_in", b6.Id, "V_e");

        var sheet = await host.Service.SheetAsync(sink);
        var row = Row(sheet, "V_in");
        Assert.Equal("value derived from a licensed record — withheld", row.Value);
        Assert.Equal(string.Empty, row.Unit);
        Assert.Equal("link: B6 loop rev A · V_e", row.Source);
        Assert.Equal("2", Row(sheet, "I_in").Value);

        // The working may still show the value the calculation used (§A5: the user's own calculation).
        var text = (await host.Service.RunAsync(sink.Id))!.SheetText;
        Assert.Contains("value derived from a licensed record — withheld", text, StringComparison.Ordinal);
        Assert.Contains("link: B6 loop rev A · V_e", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Lock_Follows_The_Links_Through_Any_Depth()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var licensed = await SavedAsync(host, Fluid(licensed: true));
        var b6 = await SealedSystemAsync(host, project, licensed);
        var middle = await BoundAsync(host, (await SinkAsync(host, project, "Middle")).Id, "V_in", b6.Id, "V_e");
        var last = await BoundAsync(host, (await SinkAsync(host, project, "Last")).Id, "V_in", middle.Id, "V_out");

        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet(last.Inputs));
        Assert.Null(withheld.Find("V_in")!.Number);
        Assert.Equal("value derived from a licensed record — withheld", Row(await host.Service.SheetAsync(last), "V_in").Value);
    }

    [Fact]
    public async Task A_Linked_Table_Cell_Is_Withheld_On_Export_And_On_The_Sheet()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var sink = await host.Service.CreateAsync(LicenceSinkCalculator.Id, project, "Taps", [
            CalcInputValue.Quantity("I_in", 2, "A", Spec),
            CalcInputValue.Table("taps", [CalcRow.Of(("name", new CalcCell(Text: "T1")), ("I", new CalcCell(4, "A"))), CalcRow.Of(("name", new CalcCell(Text: "T2")), ("I", new CalcCell(5, "A")))]) with { Provenance = Spec },
        ]);
        var upstreamCurrent = await host.Service.CreateAsync(LicenceSinkCalculator.Id, project, "Current source", [CalcInputValue.Quantity("I_in", 7, "A", Spec)]);

        // Row 2 takes a value from a calculation whose table is pinned to a licensed record; row 1 from one that holds none.
        var currentSource = await SinkOfLicensedTableAsync(host, project);
        var bound = await host.Service.BindCellAsync(sink.Id, "taps", 1, "I", currentSource.Id, "I_out");
        Assert.True(bound.Succeeded, bound.Reason);
        var open = await host.Service.BindCellAsync(bound.Instance!.Id, "taps", 0, "I", upstreamCurrent.Id, "I_out");
        Assert.True(open.Succeeded, open.Reason);
        sink = open.Instance!;

        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet(sink.Inputs));
        var rows = withheld.Find("taps")!.Rows!;
        Assert.Equal(7, rows[0].Get("I")!.Number);                       // linked to an unlicensed upstream: kept
        Assert.Null(rows[1].Get("I")!.Number);                           // linked to a licensed-derived one: withheld
        Assert.True(CalcCsv.IsWithheld(rows[1].Get("I")!.Provenance));
        Assert.Equal("T2", rows[1].Get("name")!.Text);

        var csv = CalcCsv.ExportInputs(new LicenceSinkCalculator().Template, withheld);
        Assert.Contains("taps[2].I,,", csv, StringComparison.Ordinal);
        Assert.Contains("licensed value withheld", csv, StringComparison.Ordinal);
        var (_, notices) = CalcCsv.ImportInputs(new LicenceSinkCalculator().Template, csv);
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("taps[2].I", StringComparison.Ordinal));

        var sheet = await host.Service.SheetAsync(sink);
        var line1 = Row(sheet, "taps[1]");
        var line2 = Row(sheet, "taps[2]");
        Assert.Contains("I = 7 A", line1.Value, StringComparison.Ordinal);
        Assert.Contains("value derived from a licensed record — withheld", line2.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("I = 5", line2.Value, StringComparison.Ordinal);
        Assert.Contains("I: link: Current source rev A · I_out", line1.Source, StringComparison.Ordinal);
        Assert.Contains("I: link: Licensed taps rev A · I_out", line2.Source, StringComparison.Ordinal);
    }

    /// <summary>A sink whose own table is pinned to a licensed record, so what it gives is derived from one.</summary>
    private static async Task<CalcInstance> SinkOfLicensedTableAsync(LibraryTestHost host, Guid? project)
    {
        var licensedTable = await SavedAsync(host, new CalcDataRecord(Guid.Empty, "test.factors", "Tap ratings", 0, "Wiring rules", "2018", "Table 9", "IET", "x", true, CalcDataOrigin.UserEntered,
            [new CalcDataValue("taps", Rows: [CalcRow.Of(("name", new CalcCell(Text: "T1")), ("I", new CalcCell(9, "A")))])], default));
        var sink = await host.Service.CreateAsync(LicenceSinkCalculator.Id, project, "Licensed taps", [
            CalcInputValue.Quantity("I_in", 3, "A", Spec),
            CalcInputValue.Table("taps", [CalcRow.Of(("name", new CalcCell(Text: "T1")), ("I", new CalcCell(9, "A")))]) with { Provenance = CalcInstanceService.RecordProvenance(licensedTable) },
        ]);
        return sink;
    }

    [Fact]
    public async Task A_Record_Made_From_Linked_Licensed_Inputs_Is_Itself_Licensed()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var licensed = await SavedAsync(host, Fluid(licensed: true));
        var b6 = await SealedSystemAsync(host, project, licensed);
        var sink = await BoundAsync(host, (await SinkAsync(host, project)).Id, "V_in", b6.Id, "V_e");

        var (copy, refusal) = await host.Service.SaveRecordFromInputsAsync(
            new CalcDataRecord(Guid.Empty, "test.copy", "Copy", 0, "Me", "1", "t", "—", "x", false, CalcDataOrigin.UserEntered, [new CalcDataValue("V", sink.Inputs.Single(i => i.Symbol == "V_in").Number, "L")], default),
            sink.Inputs);
        Assert.Null(refusal);
        Assert.True(copy!.LicensedSource);
        Assert.Null((await host.Service.ExportRecordAsync(copy.Id)).Csv);
    }

    // ------------------------------------------------------------------ a material property passed through

    private static CalcDataRecord Steel(bool licensed) => new(
        Guid.Empty, MaterialLibrary.Kind, licensed ? "S355 (licensed data sheet)" : "S355 (own certificate)", 0, "Data sheet 12", "rev A", "p. 3, Table 1", "Steelworks Ltd", "x", licensed, CalcDataOrigin.UserEntered,
        [new CalcDataValue("E", 210, "GPa", Source: new CalcValueSource("Data sheet 12", "rev A", "p. 3, Table 1", licensed ? CalcLicenceClass.LicensedSource : CalcLicenceClass.Public, string.Empty, null, MaterialLibrary.Sha256Hex(Encoding.UTF8.GetBytes("abc"))))],
        default);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Material_Property_Passed_Through_A_Calculation_Is_Withheld_Downstream_Only_When_The_Material_Is_Licensed(bool licensed)
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (steel, refusal) = await host.Service.Materials.SaveAsync(Steel(licensed));
        Assert.Null(refusal);
        var upstream = await host.Service.CreateAsync(MaterialPassThroughCalculator.Id, project, "Modulus", [CalcInputValue.Quantity("E", 1, "GPa", Spec)]);
        var applied = await host.Service.Materials.ApplyAsync(upstream.Id, steel!.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        Assert.Equal(210, applied.Instance!.Outputs.Single(o => o.Symbol == "E_out").Si!.Value / 1e9, 6);

        var sink = await BoundAsync(host, (await SinkAsync(host, project)).Id, "E_in", upstream.Id, "E_out");
        Assert.Equal(210, sink.Inputs.Single(i => i.Symbol == "E_in").Number!.Value, 6);

        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet(sink.Inputs));
        var sheet = await host.Service.SheetAsync(sink);

        if (licensed)
        {
            Assert.Null(withheld.Find("E_in")!.Number);
            Assert.Equal("value derived from a licensed record — withheld", Row(sheet, "E_in").Value);
            Assert.DoesNotContain("210", Row(sheet, "E_in").Value, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(210, withheld.Find("E_in")!.Number!.Value, 6);
            Assert.Equal("210", Row(sheet, "E_in").Value);
        }
    }
}
