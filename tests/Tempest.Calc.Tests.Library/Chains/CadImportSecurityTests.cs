using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Security review of W1, M2 and L8-L10: a CAD-import provenance stands only while the calculation's import record backs it and the value
/// is the imported one; a renamed file is still compared with the import it replaces; Apply applies what was previewed.
/// </summary>
public sealed class CadImportSecurityTests
{
    private const string StackFile = "// Expression file created by NX 2306 (invented)\n[mm]plate_len=30\n[mm]spacer_len=10\n[mm]pin_len=5\n[mm]gap_min=24.5\n[mm]gap_max=25.5\n";

    private static readonly CalcProvenance Drawing = CalcProvenance.Drawing("DRG-STK-1", "A");

    private static LibraryTestHost Host() => new(calculators: MechanicalCalculators.All);

    private static readonly CadMappingProfile Profile = new(
        "nx-stack", 1, "NX stack lengths to M1", [
            new("plate_len", "contributors[0].nominal"),
            new("spacer_len", "contributors[1].nominal"),
            new("pin_len", "contributors[2].nominal"),
            new("gap_min", "minimum"),
            new("gap_max", "maximum"),
        ],
        CadSystem.Nx, ToleranceStackCalculator.Id);

    private static CadImportRequest Request(string text, string name = "stack-1.exp", DateTimeOffset? expected = null) =>
        new(BoundedInputReader.FromBytes(System.Text.Encoding.UTF8.GetBytes(text), name), CadSourceFormat.NxExpressions, Profile, null, expected);

    private static IReadOnlyList<CalcInputValue> StackInputs() =>
    [
        CalcInputValue.Table("contributors",
        [
            CalcRow.Of(("name", new CalcCell(Text: "Plate")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(29, "mm")), ("upper", new CalcCell(0.1, "mm")), ("lower", new CalcCell(-0.1, "mm"))),
            CalcRow.Of(("name", new CalcCell(Text: "Spacer")), ("direction", new CalcCell(Text: "subtracts")), ("nominal", new CalcCell(9, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm"))),
            CalcRow.Of(("name", new CalcCell(Text: "Pin")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(5, "mm")), ("upper", new CalcCell(0.02, "mm")), ("lower", new CalcCell(-0.02, "mm"))),
        ], Drawing),
        CalcInputValue.Quantity("minimum", 20, "mm", Drawing),
        CalcInputValue.Quantity("maximum", 30, "mm", Drawing),
        CalcInputValue.Of("k", 3, Drawing),
        CalcInputValue.Of("f_rss", 1.5, Drawing),
        CalcInputValue.Of("cpk_target", 1.33, Drawing),
        CalcInputValue.Of("n_mc", 2000, Drawing),
        CalcInputValue.Of("seed", 1, Drawing),
        CalcInputValue.Choice("notation", "BS 8888", Drawing),
    ];

    private static async Task<(LibraryTestHost Host, CalcInstance Imported)> ImportedAsync()
    {
        var host = Host();
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, await host.ProjectAsync(), "Pin stack", StackInputs());
        var outcome = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile));
        Assert.True(outcome.Action.Succeeded, outcome.Action.Reason);
        return (host, outcome.Action.Instance!);
    }

    private static CalcInputValue Input(CalcInstance i, string symbol) => i.Inputs.Single(x => x.Symbol == symbol);

    private static IReadOnlyList<CalcInputValue> Replace(CalcInstance i, string symbol, Func<CalcInputValue, CalcInputValue> change) =>
        [.. i.Inputs.Select(x => x.Symbol == symbol ? change(x) : x)];

    [Fact]
    public async Task M2_Saving_The_Imported_Draft_Unchanged_Keeps_Every_Import_Provenance_And_Says_Nothing()
    {
        var (host, imported) = await ImportedAsync();
        var saved = await host.Service.UpdateInputsAsync(imported.Id, imported.Inputs);
        Assert.True(saved.Succeeded, saved.Reason);
        Assert.Null(saved.Reason);
        Assert.Equal(CalcProvenanceKind.CadImport, Input(saved.Instance!, "minimum").Provenance!.Kind);
        Assert.Equal(CalcProvenanceKind.CadImport, Input(saved.Instance!, "contributors").Rows![0].Get("nominal")!.Provenance!.Kind);
    }

    [Fact]
    public async Task M2_Changing_The_Unit_Without_Changing_The_Quantity_Keeps_The_Provenance()
    {
        var (host, imported) = await ImportedAsync();
        var inMetres = Replace(imported, "minimum", x => x with { Number = 0.0245, Unit = "m" });
        var saved = await host.Service.UpdateInputsAsync(imported.Id, inMetres);
        Assert.True(saved.Succeeded, saved.Reason);
        Assert.Null(saved.Reason);
        Assert.Equal(CalcProvenanceKind.CadImport, Input(saved.Instance!, "minimum").Provenance!.Kind);
    }

    [Fact]
    public async Task M2_Editing_An_Imported_Value_Removes_Its_Claim_To_Come_From_The_File_And_Says_So()
    {
        var (host, imported) = await ImportedAsync();
        var edited = Replace(imported, "minimum", x => x with { Number = 30 });
        var saved = await host.Service.UpdateInputsAsync(imported.Id, edited);

        Assert.True(saved.Succeeded, saved.Reason);
        Assert.Contains("CAD-import provenance was removed from 'minimum'", saved.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcProvenanceKind.Manual, Input(saved.Instance!, "minimum").Provenance!.Kind);
        Assert.Equal(CalcProvenanceKind.CadImport, Input(saved.Instance!, "maximum").Provenance!.Kind);
        Assert.Equal(CalcProvenanceKind.Manual, Input((await host.Store.FindInstanceAsync(imported.Id))!, "minimum").Provenance!.Kind);
    }

    [Fact]
    public async Task M2_Editing_An_Imported_Table_Cell_Removes_The_Claim_From_That_Cell_Only()
    {
        var (host, imported) = await ImportedAsync();
        var edited = Replace(imported, "contributors", t => t with
        {
            Rows = [.. t.Rows!.Select((r, i) => i != 1 ? r : new CalcRow(r.Cells.ToDictionary(c => c.Key, c => c.Key == "nominal" ? c.Value with { Number = 11 } : c.Value, StringComparer.Ordinal)))],
        });
        var saved = await host.Service.UpdateInputsAsync(imported.Id, edited);

        Assert.True(saved.Succeeded, saved.Reason);
        Assert.Contains("'contributors[1].nominal'", saved.Reason, StringComparison.Ordinal);
        var rows = Input(saved.Instance!, "contributors").Rows!;
        Assert.Equal(CalcProvenanceKind.Manual, rows[1].Get("nominal")!.Provenance!.Kind);
        Assert.Equal(CalcProvenanceKind.CadImport, rows[0].Get("nominal")!.Provenance!.Kind);
        Assert.Equal(CalcProvenanceKind.CadImport, rows[2].Get("nominal")!.Provenance!.Kind);
    }

    [Fact]
    public async Task M2_A_Provenance_Copied_To_Another_Input_Or_Naming_Another_File_Is_Not_Honoured()
    {
        var (host, imported) = await ImportedAsync();
        var minimumProvenance = Input(imported, "minimum").Provenance!;

        // The same claim pasted onto a different input that happens to hold the same number.
        var pasted = Replace(imported, "maximum", x => x with { Number = 24.5, Provenance = minimumProvenance });
        var saved = await host.Service.UpdateInputsAsync(imported.Id, pasted);
        Assert.Equal(CalcProvenanceKind.Manual, Input(saved.Instance!, "maximum").Provenance!.Kind);
        Assert.Equal(CalcProvenanceKind.CadImport, Input(saved.Instance!, "minimum").Provenance!.Kind);

        // A claim naming a file hash no import record holds.
        var info = CadImportProvenance.TryParse(minimumProvenance)!;
        var forged = new CalcProvenance(CalcProvenanceKind.CadImport, minimumProvenance.Reference!.Replace(info.FileSha256, new string('a', 64), StringComparison.Ordinal), minimumProvenance.Revision);
        var other = await host.Service.UpdateInputsAsync(imported.Id, Replace(saved.Instance!, "minimum", x => x with { Provenance = forged }));
        Assert.Equal(CalcProvenanceKind.Manual, Input(other.Instance!, "minimum").Provenance!.Kind);
    }

    [Fact]
    public async Task M2_A_Draft_That_Was_Never_Imported_Into_Cannot_Carry_The_Claim_Whether_Created_Or_Updated()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = new CadSourceInfo(CadSystem.Nx, CadSourceFormat.NxExpressions, "x.exp", new string('b', 64), "NX 2306", "P", "A");
        var forged = CadImportProvenance.Create(source, Profile, "gap_min", DateTimeOffset.UtcNow);

        var created = await host.Service.CreateAsync(ToleranceStackCalculator.Id, project, "Forged at creation", [.. StackInputs().Select(i => i.Symbol == "minimum" ? i with { Provenance = forged } : i)]);
        Assert.Equal(CalcProvenanceKind.Manual, Input(created, "minimum").Provenance!.Kind);

        var plain = await host.Service.CreateAsync(ToleranceStackCalculator.Id, project, "Forged on update", StackInputs());
        var updated = await host.Service.UpdateInputsAsync(plain.Id, [.. plain.Inputs.Select(i => i.Symbol == "maximum" ? i with { Provenance = forged } : i)]);
        Assert.True(updated.Succeeded, updated.Reason);
        Assert.Contains("'maximum'", updated.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcProvenanceKind.Manual, Input(updated.Instance!, "maximum").Provenance!.Kind);
    }

    [Fact]
    public async Task L8_A_Renamed_File_Without_A_Part_Header_Is_Still_Compared_With_The_Import_It_Replaces()
    {
        var host = Host();
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, await host.ProjectAsync(), "Pin stack", StackInputs());
        Assert.True((await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile, "stack-v1.exp"))).Action.Succeeded);

        var second = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile.Replace("plate_len=30", "plate_len=31", StringComparison.Ordinal), "stack-v2-final.exp"));
        Assert.True(second.Action.Succeeded, second.Action.Reason);
        Assert.False(second.Diff!.FirstImport);
        Assert.Single(second.Diff.Changes);
        var record = Assert.Single(second.Action.Instance!.CadImports!);
        Assert.Equal("stack-v2-final.exp", record.FileName);
    }

    [Fact]
    public async Task L10_Apply_Is_Refused_When_The_Draft_Changed_Since_The_Preview()
    {
        var host = Host();
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, await host.ProjectAsync(), "Pin stack", StackInputs());
        var preview = await host.Service.PreviewCadImportAsync(stack.Id, Request(StackFile));
        Assert.True(preview.CanApply);
        Assert.Equal(stack.UpdatedAt, preview.InstanceUpdatedAt);

        // Somebody edits the draft between the preview and the apply.
        Assert.True((await host.Service.UpdateInputsAsync(stack.Id, [.. stack.Inputs.Select(i => i.Symbol == "k" ? i with { Number = 4 } : i)])).Succeeded);

        var stale = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile, expected: preview.InstanceUpdatedAt));
        Assert.False(stale.Action.Succeeded);
        Assert.Contains("changed after this import was previewed", stale.Action.Reason, StringComparison.Ordinal);
        Assert.Null((await host.Store.FindInstanceAsync(stack.Id))!.CadImports);

        var fresh = await host.Service.PreviewCadImportAsync(stack.Id, Request(StackFile));
        var applied = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile, expected: fresh.InstanceUpdatedAt));
        Assert.True(applied.Action.Succeeded, applied.Action.Reason);
    }
}
