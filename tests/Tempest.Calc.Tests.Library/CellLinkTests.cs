using System.Text.Json;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>A sink with a table: loads with a kN column, a N column and a plain-number column, and their total.</summary>
internal sealed class LoadTableCalculator : ICalcCalculator
{
    public const string Id = "test.load-table";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Mechanical, "Load table",
        "Sums a table of loads; a test sink for table-cell links.",
        "Total: F_tot = Σ F + Σ F_n.",
        [
            new CalcInputSpec("loads", "Loads", CalcInputKind.Table, MaxRows: 300, Columns:
            [
                new CalcColumnSpec("name", "Load", CalcInputKind.Text, IsOptional: true),
                new CalcColumnSpec("F", "Force", CalcInputKind.Quantity, "kN", HardMin: 0, HardMax: 1e9, IsOptional: true),
                new CalcColumnSpec("F_n", "Force in newtons", CalcInputKind.Quantity, "N", HardMin: 0, HardMax: 1e12, IsOptional: true),
                new CalcColumnSpec("ratio", "Ratio", CalcInputKind.Number, HardMin: 0, HardMax: 1e9, IsOptional: true),
            ]),
        ],
        [new CalcAssumption("Test only.", "—")],
        [new CalcClauseRef("Test", "—", "Test")],
        [],
        [
            new CalcTestCase("ka-1", "2 kN + 500 N", new CalcReferenceSource("Hand", null, "2000 + 500"), [Loads(("A", 2, 500))], [new CalcExpectedValue("F_tot", 2.5, "kN", 1e-9)]),
            new CalcTestCase("ka-2", "1 kN", new CalcReferenceSource("Hand", null, "1000"), [Loads(("A", 1, 0))], [new CalcExpectedValue("F_tot", 1, "kN", 1e-9)]),
        ],
        OutputPorts: [new CalcOutputPort("F_tot", "kN", "Total force")]);

    public static CalcInputValue Loads(params (string Name, double F, double Fn)[] rows) =>
        CalcInputValue.Table("loads", rows.Select(r => CalcRow.Of(("name", new CalcCell(Text: r.Name)), ("F", new CalcCell(r.F, "kN")), ("F_n", new CalcCell(r.Fn, "N")))).ToList());

    public void Run(CalcRunContext c)
    {
        var total = 0.0;

        foreach (var row in c.Rows("loads"))
        {
            total += row.Get("F") is { Number: { } f } ? UnitCatalog.Parse("kN").ToSi(f) : 0;
            total += row.Get("F_n") is { Number: { } n } ? n : 0;
        }

        c.Record("F_tot", "Σ F", $"{c.Rows("loads").Count} row(s)", ScalarValue.Of(total / 1000, "kN"), "Total force");
        c.Output("F_tot", "Total force");
    }
}

/// <summary>ADR-0168 amendment 2: a number or quantity cell of a table input can be bound to an output of another calculation.</summary>
public sealed class CellLinkTests
{
    private static readonly CalcProvenance Spec = CalcProvenance.Specification("SPEC-1");

    private static LibraryTestHost Host() => new(calculators: [new ForceSourceCalculator(), new LoadTableCalculator()]);

    private static async Task<CalcInstance> Source(LibraryTestHost host, Guid? project, string name, double p = 5) =>
        await host.Service.CreateAsync("test.force-source", project, name, [CalcInputValue.Quantity("P", p, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);

    private static async Task<CalcInstance> Sink(LibraryTestHost host, Guid? project, string name = "Loads") =>
        await host.Service.CreateAsync(LoadTableCalculator.Id, project, name, [LoadTableCalculator.Loads(("A", 1, 0), ("B", 2, 0)) with { Provenance = Spec }]);

    private static async Task<CalcInstance> BoundCell(LibraryTestHost host, CalcInstance sink, int row, string column, CalcInstance upstream, string output)
    {
        var bound = await host.Service.BindCellAsync(sink.Id, "loads", row, column, upstream.Id, output);
        Assert.True(bound.Succeeded, bound.Reason);
        return bound.Instance!;
    }

    private static CalcCell Cell(CalcInstance instance, int row, string column) => instance.Inputs.Single(i => i.Symbol == "loads").Rows![row].Get(column)!;

    private static double Total(CalcInstance instance) => instance.Outputs.Single(o => o.Symbol == "F_tot").Si!.Value;

    [Fact]
    public async Task Binding_A_Cell_Sets_The_Value_In_The_Columns_Unit_And_Pins_The_Revision()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await Sink(host, project);

        sink = await BoundCell(host, sink, 1, "F", source, "F_out");

        var cell = Cell(sink, 1, "F");
        Assert.Equal(10, cell.Number!.Value, 9);
        Assert.Equal("kN", cell.Unit);
        Assert.Equal(new CalcLink(source.Id, "F_out", "A"), CalcLink.From(cell.Provenance));
        Assert.Equal(11000, Total(sink), 6);

        var link = Assert.Single(sink.CellLinks);
        Assert.Equal(("loads", 1, "F", new CalcLink(source.Id, "F_out", "A")), link);
        Assert.Empty(sink.Links);
        Assert.Contains(source.Id, sink.UpstreamIds);
        Assert.True(sink.HasLinks);
        Assert.False((await host.Service.StalenessAsync(sink)).IsStale);

        // The other cells, and the other rows, are untouched.
        Assert.Null(Cell(sink, 0, "F").Provenance);
        Assert.Equal("B", Cell(sink, 1, "name").Text);

        var sheet = (await host.Service.RunAsync(sink.Id))!.SheetText;
        Assert.Contains("F: link: Source rev A · F_out", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Cell_Takes_The_Output_Converted_To_Its_Columns_Default_Unit()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await BoundCell(host, await Sink(host, project), 0, "F_n", source, "F_out");

        Assert.Equal(10_000, Cell(sink, 0, "F_n").Number!.Value, 6);
        Assert.Equal("N", Cell(sink, 0, "F_n").Unit);

        // A plain number column takes a dimensionless output and no unit.
        sink = await BoundCell(host, sink, 0, "ratio", source, "k");
        Assert.Equal(0.5, Cell(sink, 0, "ratio").Number!.Value, 9);
        Assert.Null(Cell(sink, 0, "ratio").Unit);
        Assert.False((await host.Service.StalenessAsync(sink)).IsStale);
    }

    [Fact]
    public async Task A_Dimension_Mismatch_Is_Refused()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await Sink(host, project);

        var length = await host.Service.BindCellAsync(sink.Id, "loads", 0, "F", source.Id, "L_out");
        Assert.False(length.Succeeded);
        Assert.Contains("'loads[1].F' needs kN", length.Reason, StringComparison.Ordinal);
        Assert.Contains("dimensions differ", length.Reason, StringComparison.Ordinal);

        Assert.False((await host.Service.BindCellAsync(sink.Id, "loads", 0, "ratio", source.Id, "F_out")).Succeeded);
        Assert.False((await host.Service.BindCellAsync(sink.Id, "loads", 0, "F", source.Id, "k")).Succeeded);
        Assert.Contains("no numeric output 'nothing'", (await host.Service.BindCellAsync(sink.Id, "loads", 0, "F", source.Id, "nothing")).Reason, StringComparison.Ordinal);
        Assert.Empty((await host.Store.FindInstanceAsync(sink.Id))!.CellLinks);
    }

    [Fact]
    public async Task A_Missing_Row_A_Text_Column_Or_A_Missing_Table_Is_Refused_And_No_Row_Is_Ever_Created()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await Sink(host, project);

        var row = await host.Service.BindCellAsync(sink.Id, "loads", 2, "F", source.Id, "F_out");
        Assert.False(row.Succeeded);
        Assert.Contains("has 2 row(s); there is no row 3 to link", row.Reason, StringComparison.Ordinal);
        Assert.Contains("never creates rows", row.Reason, StringComparison.Ordinal);
        Assert.False((await host.Service.BindCellAsync(sink.Id, "loads", -1, "F", source.Id, "F_out")).Succeeded);

        var text = await host.Service.BindCellAsync(sink.Id, "loads", 0, "name", source.Id, "F_out");
        Assert.False(text.Succeeded);
        Assert.Contains("no number or quantity column 'name'", text.Reason, StringComparison.Ordinal);
        Assert.Contains("no number or quantity column 'nope'", (await host.Service.BindCellAsync(sink.Id, "loads", 0, "nope", source.Id, "F_out")).Reason, StringComparison.Ordinal);
        Assert.Contains("is not a table input", (await host.Service.BindCellAsync(sink.Id, "nope", 0, "F", source.Id, "F_out")).Reason, StringComparison.Ordinal);
        Assert.Contains("is not a table input", (await host.Service.BindCellAsync(source.Id, "P", 0, "F", sink.Id, "F_tot")).Reason, StringComparison.Ordinal);
        Assert.Contains("own output", (await host.Service.BindCellAsync(sink.Id, "loads", 0, "F", sink.Id, "F_tot")).Reason, StringComparison.Ordinal);

        var stored = (await host.Store.FindInstanceAsync(sink.Id))!;
        Assert.Equal(2, stored.Inputs.Single(i => i.Symbol == "loads").Rows!.Count);
        Assert.Empty(stored.CellLinks);
    }

    [Fact]
    public async Task Only_A_Draft_Takes_A_Cell_Link()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await Sink(host, project);
        Assert.True((await host.Service.CheckAsync(sink.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        var refused = await host.Service.BindCellAsync(sink.Id, "loads", 0, "F", source.Id, "F_out");
        Assert.False(refused.Succeeded);
        Assert.Contains("only a draft takes new links", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Changed_Upstream_Makes_The_Cell_Stale_Directly_And_Through_A_Further_Link_Downstream()
    {
        var bus = new RecordingBus();
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator(), new LoadTableCalculator()], events: bus);
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await BoundCell(host, await Sink(host, project), 1, "F", source, "F_out");

        // A further calculation takes the sink's total through an ordinary top-level link.
        var downstream = await Source(host, project, "Downstream", 1);
        var bound = await host.Service.BindAsync(downstream.Id, "P", sink.Id, "F_tot");
        Assert.True(bound.Succeeded, bound.Reason);
        downstream = bound.Instance!;
        Assert.False((await host.Service.StalenessAsync(downstream)).IsStale);

        var changed = await host.Service.UpdateInputsAsync(source.Id, [CalcInputValue.Quantity("P", 7, "kN", CalcProvenance.Drawing("DRG-9", "B"))]);
        Assert.True(changed.Succeeded, changed.Reason);

        var published = Assert.IsType<CalcStalenessChanged>(bus.Published.Last());
        Assert.Equal(source.Id, published.CauseId);
        Assert.Equal([sink.Id, downstream.Id], published.AffectedIds);

        var direct = await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(sink.Id))!);
        Assert.True(direct.IsStale);
        Assert.Contains(direct.Reasons, r => r.Contains("now gives a different 'F_out' than the value 'loads[2].F' took", StringComparison.Ordinal));

        var through = await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(downstream.Id))!);
        Assert.True(through.IsStale);
        Assert.Contains(through.Reasons, r => r.Contains("is itself stale", StringComparison.Ordinal));

        var check = await host.Service.CheckAsync(sink.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("stale", check.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Updating_Links_Re_Pins_The_Cell_And_Recalculates_In_The_Columns_Unit()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await BoundCell(host, await Sink(host, project), 0, "F_n", source, "F_out");
        sink = await BoundCell(host, sink, 1, "F", source, "F_out");

        Assert.True((await host.Service.UpdateInputsAsync(source.Id, [CalcInputValue.Quantity("P", 7, "kN", CalcProvenance.Drawing("DRG-9", "B"))])).Succeeded);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(sink.Id))!)).IsStale);

        var updated = await host.Service.UpdateLinksAsync(sink.Id);
        Assert.True(updated.Succeeded, updated.Reason);
        Assert.Equal(14_000, Cell(updated.Instance!, 0, "F_n").Number!.Value, 6);
        Assert.Equal(14, Cell(updated.Instance!, 1, "F").Number!.Value, 9);
        Assert.Equal(2, updated.Instance!.CellLinks.Count);
        Assert.False((await host.Service.StalenessAsync(updated.Instance)).IsStale);
        Assert.Equal(1 + 14 + 14 + 0, Total(updated.Instance) / 1000, 6);
    }

    [Fact]
    public async Task Updating_A_Checked_Calculation_With_Cell_Links_Makes_A_New_Revision()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await BoundCell(host, await Sink(host, project), 1, "F", source, "F_out");

        // While the source is a draft the sink is not checked on it; the source is checked and issued first.
        Assert.False((await host.Service.CheckAsync(sink.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.CheckAsync(source.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(source.Id, "ISS-S", "Client Co")).Succeeded);
        Assert.True((await host.Service.CheckAsync(sink.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        var revisedSource = (await host.Service.ReviseAsync(source.Id)).Instance!;
        Assert.True((await host.Service.UpdateInputsAsync(revisedSource.Id, [CalcInputValue.Quantity("P", 3, "kN", CalcProvenance.Drawing("DRG-9", "B"))])).Succeeded);

        var issue = await host.Service.IssueAsync(sink.Id, "ISS-1", "Client Co");
        Assert.False(issue.Succeeded);
        Assert.Contains("stale", issue.Reason, StringComparison.Ordinal);

        var updated = await host.Service.UpdateLinksAsync(sink.Id);
        Assert.True(updated.Succeeded, updated.Reason);
        Assert.Equal("B", updated.Instance!.Revision);
        Assert.Equal(6, Cell(updated.Instance, 1, "F").Number!.Value, 9);
        Assert.Equal(CalcInstanceStatus.Superseded, (await host.Store.FindInstanceAsync(sink.Id))!.Status);
    }

    [Fact]
    public async Task A_Cycle_Through_A_Cell_Link_Is_Refused()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await BoundCell(host, await Sink(host, project), 0, "F", source, "F_out");

        // The source taking the sink's total would close source → sink → source.
        var cycle = await host.Service.BindAsync(source.Id, "P", sink.Id, "F_tot");
        Assert.False(cycle.Succeeded);
        Assert.Contains("cycle", cycle.Reason, StringComparison.Ordinal);

        // The same from an import that states the link in the source's own inputs.
        var forged = await host.Service.UpdateInputsAsync(source.Id, [CalcInputValue.Quantity("P", 1, "kN", CalcProvenance.Link(sink.Id, "F_tot", "A"))]);
        Assert.False(forged.Succeeded);
        Assert.Contains("cycle", forged.Reason, StringComparison.Ordinal);

        // And the other way: a second sink's cell linked into the first, whose cell is linked back.
        var other = await Sink(host, project, "Other");
        var viaOther = await host.Service.BindAsync(source.Id, "P", other.Id, "F_tot");
        Assert.True(viaOther.Succeeded, viaOther.Reason);
        var loop = await host.Service.BindCellAsync(other.Id, "loads", 0, "F", source.Id, "F_out");
        Assert.False(loop.Succeeded);
        Assert.Contains("cycle", loop.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Superseded_Or_Other_Project_Upstream_Is_Refused()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        Assert.True((await host.Service.CheckAsync(source.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(source.Id, "ISS-S", "Client Co")).Succeeded);
        Assert.True((await host.Service.ReviseAsync(source.Id)).Succeeded);
        var sink = await Sink(host, project);

        var superseded = await host.Service.BindCellAsync(sink.Id, "loads", 0, "F", source.Id, "F_out");
        Assert.False(superseded.Succeeded);
        Assert.Contains("is superseded", superseded.Reason, StringComparison.Ordinal);

        var elsewhere = await Source(host, await host.ProjectAsync(), "Elsewhere");
        var cross = await host.Service.BindCellAsync(sink.Id, "loads", 0, "F", elsewhere.Id, "F_out");
        Assert.False(cross.Succeeded);
        Assert.Contains("another project", cross.Reason, StringComparison.Ordinal);

        Assert.Contains("No calculation", (await host.Service.BindCellAsync(sink.Id, "loads", 0, "F", Guid.NewGuid(), "F_out")).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Forged_Cell_Link_On_Create_Or_Update_Is_Refused()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await Sink(host, project);

        static CalcInputValue Table(CalcCell cell, string column = "F") =>
            CalcInputValue.Table("loads", [column == "name" ? CalcRow.Of((column, cell)) : CalcRow.Of(("name", new CalcCell(Text: "A")), (column, cell))], Spec);

        var missingOutput = Table(new CalcCell(10, "kN", Provenance: CalcProvenance.Link(source.Id, "nothing", "A")));
        var create = await Assert.ThrowsAsync<CalcLinkRefusedException>(() => host.Service.CreateAsync(LoadTableCalculator.Id, project, "Forged", [missingOutput]));
        Assert.Contains("'loads[1].F' is linked to", create.Message, StringComparison.Ordinal);
        Assert.Contains("cannot take", create.Message, StringComparison.Ordinal);

        Assert.Contains("does not exist", (await host.Service.UpdateInputsAsync(sink.Id, [Table(new CalcCell(10, "kN", Provenance: CalcProvenance.Link(Guid.NewGuid(), "F_out", "A")))])).Reason, StringComparison.Ordinal);
        Assert.Contains("cannot take", (await host.Service.UpdateInputsAsync(sink.Id, [Table(new CalcCell(10, "kN", Provenance: CalcProvenance.Link(source.Id, "L_out", "A")))])).Reason, StringComparison.Ordinal);
        Assert.Contains("only a number or quantity column", (await host.Service.UpdateInputsAsync(sink.Id, [Table(new CalcCell(Text: "x", Provenance: CalcProvenance.Link(source.Id, "F_out", "A")), "name")])).Reason, StringComparison.Ordinal);

        // A well-formed one is accepted, and a forged value that does not match its upstream is stale and cannot be checked.
        var forgedValue = await host.Service.UpdateInputsAsync(sink.Id, [Table(new CalcCell(99, "kN", Provenance: CalcProvenance.Link(source.Id, "F_out", "A")))]);
        Assert.True(forgedValue.Succeeded, forgedValue.Reason);
        Assert.True((await host.Service.StalenessAsync(forgedValue.Instance!)).IsStale);
        Assert.False((await host.Service.CheckAsync(sink.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        // An over-long cell provenance is refused like an over-long input provenance.
        var tooLong = Table(new CalcCell(1, "kN", Provenance: CalcProvenance.Specification(new string('x', CalcProvenance.MaxReferenceLength + 1))));
        Assert.Contains("longer than", (await host.Service.UpdateInputsAsync(sink.Id, [tooLong])).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cell_Provenance_Survives_The_Json_Round_Trips()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await BoundCell(host, await Sink(host, project), 1, "F", source, "F_out");

        var read = CalcInputJson.Read(CalcInputJson.Write(new CalcInputSet(sink.Inputs)));
        Assert.Empty(read.Notices);
        Assert.Equal(new CalcLink(source.Id, "F_out", "A"), CalcLink.From(read.Inputs.Find("loads")!.Rows![1].Get("F")!.Provenance));
        Assert.Null(read.Inputs.Find("loads")!.Rows![0].Get("F")!.Provenance);

        var stored = CalcInstanceStore.Deserialise<CalcInstance>(CalcInstanceStore.Serialise(sink))!;
        Assert.Equal(sink.CellLinks, stored.CellLinks);
        Assert.Equal(sink.UpstreamIds, stored.UpstreamIds);

        var fromStore = (await host.Store.FindInstanceAsync(sink.Id))!;
        Assert.Equal(sink.CellLinks, fromStore.CellLinks);
        Assert.False((await host.Service.StalenessAsync(fromStore)).IsStale);
    }

    [Fact]
    public async Task An_Instance_Without_Cell_Links_Serialises_Exactly_As_Before()
    {
        // A cell without provenance writes nothing for it: the JSON is byte-for-byte what v0.26.0 wrote.
        var table = CalcInputValue.Table("loads", [CalcRow.Of(("name", new CalcCell(Text: "A")), ("F", new CalcCell(2, "kN")), ("flag", new CalcCell(Flag: true)))]);
        var json = CalcInputJson.Write(new CalcInputSet([table]));
        Assert.DoesNotContain("Provenance", json, StringComparison.Ordinal);
        var compact = JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);
        Assert.Equal("[{\"Symbol\":\"loads\",\"Rows\":[{\"Cells\":{\"name\":{\"Text\":\"A\"},\"F\":{\"Number\":2,\"Unit\":\"kN\"},\"flag\":{\"Flag\":true}}}],\"ValueText\":\"1 row(s)\"}]", compact);

        var host = Host();
        var sink = await Sink(host, await host.ProjectAsync());
        Assert.DoesNotContain("\"Provenance\":{\"Kind\":\"Link\"", CalcInstanceStore.Serialise(sink), StringComparison.Ordinal);
        Assert.Empty(sink.CellLinks);
        Assert.False(sink.HasLinks);

        // The output hash covers outputs only, so it is unchanged by the new field.
        Assert.Equal(CalcInstanceService.HashOutputs(sink.Outputs), sink.OutputsHash);

        // Updating links on an instance with none is still refused.
        Assert.Contains("no linked inputs", (await host.Service.UpdateLinksAsync(sink.Id)).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Chain_Saved_From_Cell_Links_Keeps_The_Row_And_Column()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await BoundCell(host, await Sink(host, project), 1, "F_n", source, "F_out");

        var (template, refusal) = await host.Service.PackTemplateFromChainAsync("Cell chain", [sink.Id, source.Id]);
        Assert.Null(refusal);
        var binding = Assert.Single(template!.Bindings);
        Assert.Equal(new CalcPackBinding("s1", "F_out", "s2", "loads", 1, "F_n"), binding);
        Assert.Equal(["test.force-source", LoadTableCalculator.Id], template.Steps.Select(s => s.TemplateId));
    }

    [Fact]
    public async Task A_Pack_Template_With_A_Cell_Binding_Is_Validated_And_Instantiated()
    {
        var steps = new[] { new CalcPackStep("src", "test.force-source", "Source"), new CalcPackStep("sink", LoadTableCalculator.Id, "Loads") };
        var good = new CalcPackTemplate("test.cells", "Cells", "Cells.", steps, [new CalcPackBinding("src", "F_out", "sink", "loads", Row: 1, Column: "F")]);
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator(), new LoadTableCalculator()], packTemplates: [good]);
        Assert.Null(host.Service.ValidatePackTemplate(good));

        Assert.Contains("needs both a row and a column", host.Service.ValidatePackTemplate(good with { Bindings = [new("src", "F_out", "sink", "loads", Row: 1)] }), StringComparison.Ordinal);
        Assert.Contains("has no table input", host.Service.ValidatePackTemplate(good with { Bindings = [new("src", "F_out", "sink", "nope", 0, "F")] }), StringComparison.Ordinal);
        Assert.Contains("no number or quantity column 'name'", host.Service.ValidatePackTemplate(good with { Bindings = [new("src", "F_out", "sink", "loads", 0, "name")] }), StringComparison.Ordinal);
        Assert.Contains("no number or quantity column 'nope'", host.Service.ValidatePackTemplate(good with { Bindings = [new("src", "F_out", "sink", "loads", 0, "nope")] }), StringComparison.Ordinal);
        Assert.Contains("no number or quantity input", host.Service.ValidatePackTemplate(good with { Bindings = [new("src", "F_out", "sink", "loads")] }), StringComparison.Ordinal);

        var project = await host.ProjectAsync();
        var (made, refused) = await host.Service.InstantiatePackTemplateAsync(good, project, "Cells pack",
            new Dictionary<string, IReadOnlyList<CalcInputValue>> { ["sink"] = [LoadTableCalculator.Loads(("A", 1, 0), ("B", 2, 0))] });
        Assert.Null(refused);
        Assert.Equal(10, Cell(made!.Instances["sink"], 1, "F").Number!.Value, 9);
        Assert.Single(made.Instances["sink"].CellLinks);

        // The step is not given the rows: instantiating never creates them.
        var (none, noRows) = await host.Service.InstantiatePackTemplateAsync(good, await host.ProjectAsync(), "Cells pack two");
        Assert.Null(none);
        Assert.Contains("Step 'sink'", noRows, StringComparison.Ordinal);
        Assert.Contains("never creates rows", noRows, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Over_Long_Cell_Provenance_Is_Refused_On_Create_Update_And_In_Pack_Steps()
    {
        var host = new LibraryTestHost(calculators: [new ForceSourceCalculator(), new LoadTableCalculator()],
            packTemplates: [new CalcPackTemplate("test.long", "Long", "Long.", [new CalcPackStep("sink", LoadTableCalculator.Id, "Loads")], [])]);
        var project = await host.ProjectAsync();
        var tooLong = CalcProvenance.Specification(new string('x', CalcProvenance.MaxReferenceLength + 1));
        var table = CalcInputValue.Table("loads", [CalcRow.Of(("name", new CalcCell(Text: "A")), ("F", new CalcCell(1, "kN", Provenance: tooLong)))], Spec);

        var create = await Assert.ThrowsAsync<CalcLinkRefusedException>(() => host.Service.CreateAsync(LoadTableCalculator.Id, project, "Long cell", [table]));
        Assert.Contains("longer than", create.Message, StringComparison.Ordinal);

        var topLevel = await Assert.ThrowsAsync<CalcLinkRefusedException>(() => host.Service.CreateAsync("test.force-source", project, "Long input", [CalcInputValue.Quantity("P", 1, "kN", tooLong)]));
        Assert.Contains("longer than", topLevel.Message, StringComparison.Ordinal);

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(host.Service.PackTemplates.Single(), await host.ProjectAsync(), "Long pack",
            new Dictionary<string, IReadOnlyList<CalcInputValue>> { ["sink"] = [table] });
        Assert.Null(made);
        Assert.StartsWith("Step 'sink':", refusal);
        Assert.Contains("longer than", refusal, StringComparison.Ordinal);

        var sink = await Sink(host, project, "Fine");
        Assert.Contains("longer than", (await host.Service.UpdateInputsAsync(sink.Id, [table])).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Cell_Cannot_Be_Linked_Into_A_Table_Pinned_To_A_Reference_Record()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var pin = CalcProvenance.FromReference($"record:{Guid.NewGuid():N}@1");
        var pinned = await host.Service.CreateAsync(LoadTableCalculator.Id, project, "Pinned", [LoadTableCalculator.Loads(("A", 1, 0)) with { Provenance = pin }]);

        var bind = await host.Service.BindCellAsync(pinned.Id, "loads", 0, "F", source.Id, "F_out");
        Assert.False(bind.Succeeded);
        Assert.Contains("pinned to a reference record", bind.Reason, StringComparison.Ordinal);
        Assert.Contains("'loads[1].F'", bind.Reason, StringComparison.Ordinal);

        // The same by an import that states the cell link inside the pinned table.
        var forged = LoadTableCalculator.Loads(("A", 1, 0)) with { Provenance = pin };
        var cell = new CalcCell(10, "kN", Provenance: CalcProvenance.Link(source.Id, "F_out", "A"));
        forged = forged with { Rows = [CalcRow.Of(("name", new CalcCell(Text: "A")), ("F", cell))] };
        var update = await host.Service.UpdateInputsAsync(pinned.Id, [forged]);
        Assert.False(update.Succeeded);
        Assert.Contains("pinned to a reference record", update.Reason, StringComparison.Ordinal);
        Assert.Empty((await host.Store.FindInstanceAsync(pinned.Id))!.CellLinks);
    }

    [Fact]
    public async Task A_Bind_Built_From_An_Earlier_Read_Is_Refused_If_The_Calculation_Changed_In_Between()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var sink = await Sink(host, project);
        var read = (await host.Store.FindInstanceAsync(sink.Id))!;

        // Someone edits the second row after the read ...
        var edited = await host.Service.UpdateInputsAsync(sink.Id, [LoadTableCalculator.Loads(("A", 1, 0), ("B", 99, 0)) with { Provenance = Spec }]);
        Assert.True(edited.Succeeded, edited.Reason);

        // ... so inputs built from the old read are refused rather than written over the edit.
        var stale = await host.Service.UpdateInputsAsync(sink.Id, read.Inputs, read.UpdatedAt);
        Assert.False(stale.Succeeded);
        Assert.Contains("was changed after these inputs were read", stale.Reason, StringComparison.Ordinal);
        Assert.Equal(99, Cell((await host.Store.FindInstanceAsync(sink.Id))!, 1, "F").Number!.Value, 9);

        // With the current stamp the same call goes through, and the binds themselves still work.
        var current = (await host.Store.FindInstanceAsync(sink.Id))!;
        Assert.True((await host.Service.UpdateInputsAsync(sink.Id, current.Inputs, current.UpdatedAt)).Succeeded);
        Assert.True((await host.Service.BindCellAsync(sink.Id, "loads", 1, "F", source.Id, "F_out")).Succeeded);
        Assert.True((await host.Service.BindAsync((await Source(host, project, "Other")).Id, "P", source.Id, "F_out")).Succeeded);
    }

    [Fact]
    public async Task Many_Cell_Links_To_One_Upstream_Are_Judged_Together_And_Stay_Fast()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Source");
        var rows = Enumerable.Range(0, 200).Select(r => (Name: $"L{r}", F: 1.0, Fn: 0.0)).ToArray();
        var sink = await host.Service.CreateAsync(LoadTableCalculator.Id, project, "Big", [LoadTableCalculator.Loads(rows) with { Provenance = Spec }]);

        var inputs = sink.Inputs.ToList();

        for (var r = 0; r < 200; r++)
        {
            var table = inputs.Single(i => i.Symbol == "loads");
            var cells = new Dictionary<string, CalcCell>(table.Rows![r].Cells) { ["F"] = new CalcCell(10, "kN", Provenance: CalcProvenance.Link(source.Id, "F_out", "A")) };
            inputs[inputs.IndexOf(table)] = table with { Rows = [.. table.Rows.Select((row, i) => i == r ? new CalcRow(cells) : row)] };
        }

        var linked = await host.Service.UpdateInputsAsync(sink.Id, inputs);
        Assert.True(linked.Succeeded, linked.Reason);
        Assert.Equal(200, linked.Instance!.CellLinks.Count);
        Assert.False((await host.Service.StalenessAsync(linked.Instance)).IsStale);

        Assert.True((await host.Service.UpdateInputsAsync(source.Id, [CalcInputValue.Quantity("P", 7, "kN", CalcProvenance.Drawing("DRG-9", "B"))])).Succeeded);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var stale = await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(sink.Id))!);
        Assert.True(stale.IsStale);
        Assert.Equal(200, stale.Reasons.Count(r => r.Contains("now gives a different 'F_out'", StringComparison.Ordinal)));
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(5), $"staleness took {started.Elapsed}");

        var updated = await host.Service.UpdateLinksAsync(sink.Id);
        Assert.True(updated.Succeeded, updated.Reason);
        Assert.False((await host.Service.StalenessAsync(updated.Instance!)).IsStale);
        Assert.Equal(200 * 14_000.0, Total(updated.Instance!), 3);
    }
}
