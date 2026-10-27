using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// ADR-0177 review: an input (or a table column) whose default display unit changed at a template version declares it with
/// <see cref="CalcInputSpec.UnitChangedAt"/> and <see cref="CalcInputSpec.UnitWas"/>. A bare stored number (no unit of its own) on a calculation
/// made before that version is read in the old unit, never the new one: a bare 0.3 stored while <c>p</c> was in bar stays 0.3 bar (30 kPa) when the
/// default becomes kPa, instead of 0.3 kPa, a hundred times smaller and still within the hard range. A value with its own unit is read in it; a new
/// sheet reads a bare number in the new unit; a bare number on a record that carries no version is refused with a sentence naming the symbol and
/// both units. The one resolver is <see cref="CalcInputUnits"/>, used by the binder, the validator, the runner, the CSV and JSON readers, the sheet
/// builder, the draft upgrade and Revise.
/// </summary>
public sealed class UnitChangeTests
{
    private const string Id = "test.pressures";

    /// <summary>
    /// A pre-charge margin p and a table of stage pressures q, in bar on 1.0.0 and in kPa on 2.0.0, which says so. The outputs are the SI values,
    /// the table summed the way shipped calculators read cells: with the calculator's own fallback unit.
    /// </summary>
    private sealed class Pressures(string version, string unit, double @default, bool changed) : ICalcCalculator
    {
        public static Pressures V1 => new("1.0.0", "bar", 0.5, false);

        public static Pressures V2 => new("2.0.0", "kPa", 50, true);

        public CalcTemplate Template { get; } = new(Id, version, CalcDiscipline.Mechanical, "Pressures", "", "",
            [
                new CalcInputSpec("p", "Pre-charge margin", CalcInputKind.Quantity, unit, Default: @default, HardMin: 0, HardMax: changed ? 200 : 2,
                    UnitChangedAt: changed ? "2.0.0" : null, UnitWas: changed ? "bar" : null),
                new CalcInputSpec("rows", "Stages", CalcInputKind.Table, IsOptional: true,
                    Columns: [new CalcColumnSpec("q", "Stage pressure", CalcInputKind.Quantity, unit, UnitChangedAt: changed ? "2.0.0" : null, UnitWas: changed ? "bar" : null)]),
            ],
            [], [], [], []);

        public void Run(CalcRunContext c)
        {
            c.Input("p");
            c.Assign("p_out", "p", "Pa", "Margin");
            c.Output("p_out", "Margin");
            var sum = c.Rows("rows").Sum(r => r.Get("q") is { Number: { } n } cell ? UnitCatalog.Parse(cell.Unit ?? unit).ToSi(n) : 0);
            c.Output("q_out", "Stages", ScalarValue.Of(sum, UnitCatalog.Parse("Pa")));
        }
    }

    private const string Ambiguous = "p: the stored value has no unit and the record does not say which template version it was made on, so it cannot be read: p was in bar before version 2.0.0 and is in kPa since; enter it with its unit.";

    private static TemplateVersionHistory Recorded() => new(new[] { $"{Id}@1.0.0", $"{Id}@2.0.0" }.Select(r => new KeyValuePair<string, string>(r, new string('a', 64))));

    private static CalcInputValue Bare(double p = 0.3) => CalcInputValue.Of("p", p, CalcProvenance.Assumed("Test value."));

    private static CalcInputValue Stages(string? unit) => CalcInputValue.Table("rows", [new CalcRow(new Dictionary<string, CalcCell>(StringComparer.Ordinal) { ["q"] = new CalcCell(0.3, unit) })], CalcProvenance.Assumed("Test value."));

    /// <summary>The build after the unit changed: 2.0.0 installed over the same store, 1.0.0 recorded in the manifest.</summary>
    private static CalcInstanceService LaterBuild(LibraryTestHost host) =>
        new(new CalcCatalogue([Pressures.V2], history: Recorded()), host.Store, host.Evidence, host.Context, renderer: null, applicationVersion: "TempestOS later build") { PrincipalOverride = () => host.Principal };

    private static async Task<CalcInstance> IssuedOnV1Async(LibraryTestHost host, params CalcInputValue[] inputs)
    {
        var created = await host.Service.CreateAsync(Id, await host.ProjectAsync(), "Margin", inputs);
        Assert.Empty(created.Blocks);
        Assert.True((await host.Service.CheckAsync(created.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var issued = await host.Service.IssueAsync(created.Id, "ISS-1", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);
        return (await host.Store.FindInstanceAsync(created.Id))!;
    }

    private static double Si(CalcInstance instance, string symbol) => instance.Outputs.Single(o => o.Symbol == symbol).Si!.Value;

    /// <summary>The 2.0.0 calculator with its template replaced, for a declaration the catalogue must refuse or hash differently.</summary>
    private sealed class Replaced(CalcTemplate template) : ICalcCalculator
    {
        public CalcTemplate Template { get; } = template;

        public void Run(CalcRunContext context) => Pressures.V2.Run(context);
    }

    [Fact]
    public void A_Unit_Change_Is_Declared_At_A_Version_The_Template_Has_Reached_With_The_Unit_It_Was_In_And_Is_Part_Of_The_Hash()
    {
        var template = Pressures.V2.Template;
        var p = template.Inputs.Single(i => i.Symbol == "p");
        var rows = template.Inputs.Single(i => i.Symbol == "rows");
        Assert.Null(Record.Exception(() => new CalcCatalogue([Pressures.V2])));

        CalcTemplate With(CalcInputSpec spec) => template with { Inputs = [spec, rows] };
        Assert.Contains("'p' says its unit changed at version '2.1.0', which is not a version of the template up to 2.0.0", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(With(p with { UnitChangedAt = "2.1.0" }))])).Message, StringComparison.Ordinal);
        Assert.Contains("not a version", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(With(p with { UnitChangedAt = "v2" }))])).Message, StringComparison.Ordinal);
        Assert.Contains("'p' says its unit changed at 2.0.0 but '' is not a unit it could have been in", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(With(p with { UnitWas = "" }))])).Message, StringComparison.Ordinal);
        Assert.Contains("'furlongs' is not a unit", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(With(p with { UnitWas = "furlongs" }))])).Message, StringComparison.Ordinal);
        Assert.Contains("'p' says its unit changed at 2.0.0 from 'mm' to 'kPa', which is not a change of unit within one dimension", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(With(p with { UnitWas = "mm" }))])).Message, StringComparison.Ordinal);
        Assert.Contains("from 'kPa' to 'kPa', which is not a change", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(With(p with { UnitWas = "kPa" }))])).Message, StringComparison.Ordinal);

        // The same for a table column, named table.column.
        var column = rows.Columns![0];
        Assert.Contains("'rows.q' says its unit changed at version '3.0.0'", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(template with { Inputs = [p, rows with { Columns = [column with { UnitChangedAt = "3.0.0" }] }] })])).Message, StringComparison.Ordinal);

        // A declared change is part of the version's content hash, for the input and for the column; a template that declares none hashes as before.
        var declared = new CalcCatalogue([Pressures.V2]).Get(Id).ContentHash;
        var undeclaredInput = new CalcCatalogue([new Replaced(With(p with { UnitChangedAt = null, UnitWas = null }))]).Get(Id).ContentHash;
        var undeclaredColumn = new CalcCatalogue([new Replaced(template with { Inputs = [p, rows with { Columns = [column with { UnitChangedAt = null, UnitWas = null }] }] })]).Get(Id).ContentHash;
        Assert.NotEqual(declared, undeclaredInput);
        Assert.NotEqual(declared, undeclaredColumn);
        Assert.NotEqual(undeclaredInput, undeclaredColumn);
        Assert.Equal(declared, new CalcCatalogue([Pressures.V2]).Get(Id).ContentHash);
    }

    [Fact]
    public async Task A_Bare_Number_Stored_Before_The_Unit_Changed_Is_Read_In_The_Old_Unit_Under_The_New_Version_And_Carried_With_It()
    {
        var host = new LibraryTestHost(calculators: [Pressures.V1]);
        var issued = await IssuedOnV1Async(host, Bare(), Stages(null));
        Assert.Equal(30_000, Si(issued, "p_out"), 6);
        Assert.Equal(30_000, Si(issued, "q_out"), 6);

        // Under 2.0.0 the bare 0.3 is still 0.3 bar: the re-run reproduces the record (0.3 kPa would be 300 Pa, within the hard range, and would not).
        var later = LaterBuild(host);
        var rerun = later.Rerun(issued);
        Assert.Equal(CalcRerunOutcome.Reproduced, rerun.Outcome);
        Assert.Equal($"{Id}@2.0.0", rerun.RanOn);

        // The same through the one resolver the binder, the validator and the sheet use.
        var p = Pressures.V2.Template.Inputs[0];
        Assert.Equal(30_000, ((ScalarValue)CalcInputBinder.Bind(p, Bare(), "1.0.0")).Si, 6);
        Assert.Equal(300, ((ScalarValue)CalcInputBinder.Bind(p, Bare(), "2.0.0")).Si, 6);
        Assert.Empty(CalcInputValidator.Validate(Pressures.V2.Template, new CalcInputSet([Bare(), Stages(null)], "1.0.0")));
        var header = new CalcSheetHeader("P", "C-1", "Margin", "A", CalcSheetStatus.Draft, "Alex", null, null, null, null);
        var sheet = CalcSheetBuilder.Build(Pressures.V2.Template, issued.InputSet(), CalcRunner.Run(Pressures.V2, issued.InputSet()), header, new CalcSheetFooter($"{Id}@2.0.0", "hash", "engine", 0, 0, null));
        Assert.Equal("bar", sheet.Inputs.Single(r => r.Symbol == "p").Unit);
        Assert.Contains("0.3 bar", sheet.Inputs.Single(r => r.Symbol == "rows[1]").Value, StringComparison.Ordinal);

        // Revise carries the number with the unit it was recorded in, so the revision means what the issued record meant.
        var revised = await later.ReviseAsync(issued.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        var draft = revised.Instance!;
        Assert.Equal($"{Id}@2.0.0", draft.TemplateReference);
        Assert.Equal(("bar", 0.3), (draft.Inputs.Single(i => i.Symbol == "p").Unit, draft.Inputs.Single(i => i.Symbol == "p").Number));
        Assert.Equal("bar", draft.Inputs.Single(i => i.Symbol == "rows").Rows![0].Get("q")!.Unit);
        Assert.Equal(30_000, Si(draft, "p_out"), 6);
        Assert.Equal(30_000, Si(draft, "q_out"), 6);
    }

    [Fact]
    public async Task An_Old_Draft_Carries_Its_Bare_Number_With_The_Old_Unit_Onto_The_New_Sheet()
    {
        var host = new LibraryTestHost(calculators: [Pressures.V1]);
        var draft = await host.Service.CreateAsync(Id, await host.ProjectAsync(), "Draft margin", [Bare(), Stages(null)]);

        var upgrade = LaterBuild(host).PlanDraftUpgrade(draft);
        Assert.NotNull(upgrade);
        Assert.Empty(upgrade.Dropped);
        Assert.Equal("bar", upgrade.Inputs.Find("p")!.Unit);
        Assert.Equal(0.3, upgrade.Inputs.Find("p")!.Number);
        Assert.Equal("bar", upgrade.Inputs.Find("rows")!.Rows![0].Get("q")!.Unit);
        var run = CalcRunner.Run(Pressures.V2, upgrade.Inputs);
        Assert.False(run.IsBlocked);
        Assert.Equal(30_000, run.OutputSi("p_out"), 6);
        Assert.Equal(30_000, run.OutputSi("q_out"), 6);
    }

    [Fact]
    public async Task A_Value_With_Its_Own_Unit_Is_Read_In_It_Whatever_The_Version()
    {
        var host = new LibraryTestHost(calculators: [Pressures.V1]);
        var issued = await IssuedOnV1Async(host, CalcInputValue.Quantity("p", 0.3, "kPa", CalcProvenance.Assumed("Test value.")), Stages("kPa"));
        Assert.Equal(300, Si(issued, "p_out"), 6);

        var later = LaterBuild(host);
        Assert.Equal(CalcRerunOutcome.Reproduced, later.Rerun(issued).Outcome);
        var revised = await later.ReviseAsync(issued.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Equal("kPa", revised.Instance!.Inputs.Single(i => i.Symbol == "p").Unit);
        Assert.Equal(300, Si(revised.Instance, "p_out"), 6);
        Assert.Equal(300, Si(revised.Instance, "q_out"), 6);
    }

    [Fact]
    public void A_New_Sheet_Reads_A_Bare_Number_In_The_New_Default_Unit()
    {
        var template = Pressures.V2.Template;
        Assert.Equal("kPa", CalcTemplateHasher.NewSheetInputs(template).Find("p")!.Unit);
        Assert.Equal(50, CalcTemplateHasher.NewSheetInputs(template).Find("p")!.Number);

        // Values entered against the installed template carry no version (MadeOn null) and are read in its units.
        var run = CalcRunner.Run(Pressures.V2, new CalcInputSet([Bare(), Stages(null)]));
        Assert.False(run.IsBlocked);
        Assert.Equal(300, run.OutputSi("p_out"), 6);
        Assert.Equal(300, run.OutputSi("q_out"), 6);
        Assert.Equal(300, ((ScalarValue)CalcInputBinder.Bind(template.Inputs[0], Bare())).Si, 6);
    }

    [Fact]
    public void A_Bare_Number_On_A_Record_With_No_Version_Is_Refused_Naming_The_Symbol_And_Both_Units()
    {
        var template = Pressures.V2.Template;
        var noVersion = new CalcInputSet([Bare()], CalcInputUnits.NoVersion);

        var run = CalcRunner.Run(Pressures.V2, noVersion);
        Assert.True(run.IsBlocked);
        Assert.Equal(Ambiguous, run.Blocks[0].Message);
        Assert.Contains(CalcInputValidator.Validate(template, noVersion), n => n.Severity == CalcNoticeSeverity.Block && n.Message == Ambiguous);
        Assert.Equal(Ambiguous, Assert.Throws<CalcEvaluationException>(() => CalcInputBinder.Bind(template.Inputs[0], Bare(), CalcInputUnits.NoVersion)).Message);
        Assert.False(CalcInputUnits.TryPin(template.Inputs[0], Bare(), CalcInputUnits.NoVersion, out _, out var refusal));
        Assert.Equal(Ambiguous, refusal);

        // A malformed version is as good as none.
        Assert.False(CalcInputUnits.TryResolve(template.Inputs[0], Bare(), "v1", out _, out _));

        // A table cell of a column whose unit changed, named by its place.
        var cells = CalcRunner.Run(Pressures.V2, new CalcInputSet([Bare(), Stages(null)]) { MadeOn = CalcInputUnits.NoVersion });
        Assert.True(cells.IsBlocked);
        var cellRun = CalcRunner.Run(Pressures.V2, new CalcInputSet([CalcInputValue.Quantity("p", 0.3, "bar"), Stages(null)], CalcInputUnits.NoVersion));
        Assert.True(cellRun.IsBlocked);
        Assert.StartsWith("rows[1].q: the stored value has no unit", cellRun.Blocks[0].Message, StringComparison.Ordinal);
        Assert.Contains("rows[1].q was in bar before version 2.0.0 and is in kPa since", cellRun.Blocks[0].Message, StringComparison.Ordinal);

        // A CSV or an inputs file carries no version: a bare number is refused on import, one with its unit is read as given, and one of an input whose unit never changed takes the default as before.
        var csv = CalcCsv.ImportInputs(template, "symbol,value,unit\np,0.3,\n");
        Assert.Contains(csv.Notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message == "Row 2: " + Ambiguous);
        Assert.Null(csv.Inputs.Find("p"));
        var csvCell = CalcCsv.ImportInputs(template, "symbol,value,unit\nrows[1].q,0.3,\n");
        Assert.Contains(csvCell.Notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.StartsWith("Row 2: rows[1].q: the stored value has no unit", StringComparison.Ordinal));
        var explicitCsv = CalcCsv.ImportInputs(template, "symbol,value,unit\np,0.3,bar\n");
        Assert.DoesNotContain(explicitCsv.Notices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.Equal("bar", explicitCsv.Inputs.Find("p")!.Unit);
        var unchanged = CalcCsv.ImportInputs(Pressures.V1.Template, "symbol,value,unit\np,0.3,\n");
        Assert.Equal("bar", unchanged.Inputs.Find("p")!.Unit);

        var json = CalcInputJson.Read("[{\"Symbol\":\"p\",\"Number\":0.3}]", template);
        Assert.Contains(json.Notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message == Ambiguous);
        Assert.Null(json.Inputs.Find("p"));
        var explicitJson = CalcInputJson.Read("[{\"Symbol\":\"p\",\"Number\":0.3,\"Unit\":\"bar\"}]", template);
        Assert.Empty(explicitJson.Notices);
        Assert.Equal("bar", explicitJson.Inputs.Find("p")!.Unit);
    }
}
