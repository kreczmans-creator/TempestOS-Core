using System.Globalization;
using System.Text;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B §B7 chain 2, scan-to-seal: W2 → M1 → F3. An inspection table of ten parts is imported into an M1 groove-diameter stack (the scanned groove root
/// becomes a normal contributor, mean shift and ± kσ band) and into F3's measured groove rows (bore and groove width as mean and σ); F3's groove root is linked to
/// the stack's mean. Re-importing a table in which the groove root moved updates the stack through the ordinary input update, so F3 goes stale through the
/// existing machinery, cannot be checked while stale, cannot be issued while stale, and a link update brings it back. The chain is a workflow proved here, not a pack template.
/// </summary>
public sealed class ScanToSealChainTests
{
    // Ten parts. Python3 (math.fsum): groove root mean 51.485, s 0.006236095644623894; bore mean 56.922, s 0.004371625682868286; width mean 4.835, s 0.010801234497346204.
    private static readonly double[] GrooveRoot = [51.480, 51.490, 51.485, 51.495, 51.480, 51.490, 51.485, 51.475, 51.490, 51.480];
    private static readonly double[] Bore = [56.915, 56.925, 56.920, 56.930, 56.918, 56.922, 56.921, 56.919, 56.926, 56.924];
    private static readonly double[] Width = [4.82, 4.84, 4.83, 4.85, 4.83, 4.84, 4.82, 4.85, 4.84, 4.83];

    private static readonly CalcProvenance Drawing = CalcProvenance.Drawing("DRG-SEAL-1", "A");

    private static LibraryTestHost Host(RecordingBus? bus = null) => new(calculators: [.. MechanicalCalculators.All, .. FluidPowerCalculators.All], events: bus);

    private static string Table(double groove = 0)
    {
        var sb = new StringBuilder("Element,Nominal,Actual,Deviation,Upper tol.,Lower tol.,Serial\r\n");

        for (var i = 0; i < GrooveRoot.Length; i++)
        {
            var serial = $"S{i + 1:000}";
            sb.Append(CultureInfo.InvariantCulture, $"Groove root,51.5,{GrooveRoot[i] + groove:0.000},{GrooveRoot[i] + groove - 51.5:0.000},0.05,-0.05,{serial}\r\n");
            sb.Append(CultureInfo.InvariantCulture, $"Bore,56.9,{Bore[i]:0.000},{Bore[i] - 56.9:0.000},0.046,0,{serial}\r\n");
            sb.Append(CultureInfo.InvariantCulture, $"Width,4.8,{Width[i]:0.00},{Width[i] - 4.8:0.00},0.1,0,{serial}\r\n");
        }

        return sb.ToString();
    }

    private static ScanFileInput File(string text, string name = "seal-scan.csv") => new(BoundedInputReader.FromBytes(new UTF8Encoding(false).GetBytes(text), name));

    private static ScanColumns Columns => new("Element", Nominal: "Nominal", Actual: "Actual", Deviation: "Deviation", UpperTolerance: "Upper tol.", LowerTolerance: "Lower tol.", Serial: "Serial");

    private static ScanProfile StackProfile(int version = 1) => new(
        "seal-stack", version, "Groove root into the M1 stack", Columns, [new ScanFeatureEntry("Groove root", "contributors[0]", Required: true)],
        ScanSoftware.ZeissInspect, TemplateId: ToleranceStackCalculator.Id);

    private static ScanProfile SealProfile(int version = 1) => new(
        "seal-measured", version, "Bore and width into F3's measured rows", Columns,
        [new ScanFeatureEntry("Bore", "measured{D_s}"), new ScanFeatureEntry("Width", "measured{b}")],
        ScanSoftware.ZeissInspect, TemplateId: ORingGlandCalculator.Id);

    private static ScanImportRequest Request(string text, ScanProfile profile) =>
        new([File(text)], profile, new ScanSettings { SoftwareVersion = "2024", Alignment = "RPS best fit A-B-C" });

    private static IReadOnlyList<CalcInputValue> StackInputs() =>
    [
        CalcInputValue.Table("contributors",
        [
            CalcRow.Of(("name", new CalcCell(Text: "Groove root (scanned)")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(51.5, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm"))),
            CalcRow.Of(("name", new CalcCell(Text: "Plating build")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(0, "mm")), ("upper", new CalcCell(0.002, "mm")), ("lower", new CalcCell(-0.002, "mm"))),
        ], Drawing),
        CalcInputValue.Quantity("minimum", 51.4, "mm", Drawing),
        CalcInputValue.Quantity("maximum", 51.6, "mm", Drawing),
        CalcInputValue.Of("k", 3, Drawing),
        CalcInputValue.Of("f_rss", 1.5, Drawing),
        CalcInputValue.Of("cpk_target", 1.33, Drawing),
        CalcInputValue.Of("n_mc", 2000, Drawing),
        CalcInputValue.Of("seed", 1, Drawing),
        CalcInputValue.Choice("notation", "BS 8888", Drawing),
    ];

    private static IReadOnlyList<CalcInputValue> GlandInputs() =>
        CalcTemplateHasher.DefaultInputs(new ORingGlandCalculator().Template).Values.Select(v => v with { Provenance = v.Provenance ?? Drawing }).ToList();

    private static async Task<(CalcInstance Stack, CalcInstance Gland)> BuildAsync(LibraryTestHost host)
    {
        var project = await host.ProjectAsync();
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, project, "Groove root stack", StackInputs());
        Assert.Empty(stack.Blocks);
        var gland = await host.Service.CreateAsync(ORingGlandCalculator.Id, project, "Piston seal gland", GlandInputs());
        Assert.Empty(gland.Blocks);
        var bound = await host.Service.BindAsync(gland.Id, "D_g", stack.Id, "x_mean");
        Assert.True(bound.Succeeded, bound.Reason);
        return (stack, bound.Instance!);
    }

    private static double Mean(IEnumerable<double> values) => values.Sum() / values.Count();

    [Fact]
    public async Task A_Scan_Table_Becomes_A_Normal_Contributor_In_The_Stack_And_Measured_Rows_In_The_Gland_And_Every_Value_Carries_Its_Scan_Provenance()
    {
        var host = Host();
        var (stack, gland) = await BuildAsync(host);
        Assert.Null((await host.Service.SaveScanProfileAsync(StackProfile())).Refusal);

        var preview = await host.Service.PreviewScanImportAsync(stack.Id, Request(Table(), StackProfile()));
        Assert.True(preview.CanApply, string.Join(" ", preview.Reasons));
        Assert.True(preview.Diff!.FirstImport);
        Assert.Equal(1, preview.Mapping!.Count(ScanRowStatus.Mapped));
        Assert.Equal(2, preview.Mapping.Count(ScanRowStatus.Unmapped));
        Assert.Equal(3, preview.Stats.Count);
        Assert.Equal(stack.UpdatedAt, (await host.Store.FindInstanceAsync(stack.Id))!.UpdatedAt); // a preview changes nothing

        var applied = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), StackProfile()));
        Assert.True(applied.Action.Succeeded, applied.Action.Reason);
        var imported = applied.Action.Instance!;
        var row = imported.Inputs.Single(i => i.Symbol == "contributors").Rows![0];

        // The contributor reads back the measured population: mid-limit = the mean 51.485 mm, σ = s = 0.006236 mm, normal.
        Assert.Equal("Groove root (scanned)", row.Get("name")!.Text);
        Assert.Equal(51.5, row.Get("nominal")!.Number!.Value, 12);
        Assert.Equal(51.485 - 51.5, row.Get("shift")!.Number!.Value, 9);
        Assert.Equal(3 * 0.006236095644623894, row.Get("upper")!.Number!.Value, 12);
        Assert.Equal("normal", row.Get("distribution")!.Text);
        Assert.Equal(0.051485, ChainKit.Si(imported, "x_mean"), 9);
        Assert.Equal(Math.Sqrt(0.006236095644623894 * 0.006236095644623894 + (0.002 / 3) * (0.002 / 3)) / 1000, ChainKit.Si(imported, "sigma"), 12);

        var info = ScanImportProvenance.TryParse(row.Get("upper")!.Provenance)!;
        Assert.Equal("ZEISS Inspect 2024", info.SoftwareAndVersion);
        Assert.Equal("S001..S010 (n=10)", info.Serials);
        Assert.Equal("RPS best fit A-B-C", info.Alignment);
        Assert.Equal(64, info.Sha256.Length);
        Assert.Equal(("seal-stack", 1), (info.ProfileId, info.ProfileVersion));
        Assert.Equal("Groove root", info.Feature);

        // The import is remembered: files, hashes, profile, who, every feature fitted (the unmapped ones too).
        var record = Assert.Single(imported.ScanImports!);
        Assert.Equal("seal-stack", record.ProfileId);
        Assert.Equal(info.Sha256, record.Files.Single().Sha256);
        Assert.Equal("test.engineer", record.ImportedBy);
        Assert.Equal(3, record.Features.Count);
        Assert.Equal("contributors[0]", record.Features.Single(f => f.Feature == "Groove root").Target);

        // The gland: groove root from the stack by link, bore and width from the same scan as measured rows.
        var linked = await host.Service.UpdateLinksAsync(gland.Id);
        Assert.True(linked.Succeeded, linked.Reason);
        Assert.Equal(51.485, linked.Instance!.Inputs.Single(i => i.Symbol == "D_g").Number!.Value, 9);

        var glandImport = await host.Service.ApplyScanImportAsync(gland.Id, Request(Table(), SealProfile()));
        Assert.True(glandImport.Action.Succeeded, glandImport.Action.Reason);
        var measured = glandImport.Action.Instance!.Inputs.Single(i => i.Symbol == "measured").Rows!;
        Assert.Equal(["D_s", "b"], measured.Select(r => r.Get("dim")!.Text!).ToArray());
        Assert.Equal(56.922, measured[0].Get("mean")!.Number!.Value, 9);
        Assert.Equal(0.004371625682868286, measured[0].Get("sigma")!.Number!.Value, 12);
        Assert.Equal(4.835, measured[1].Get("mean")!.Number!.Value, 9);
        Assert.Equal(0.010801234497346204, measured[1].Get("sigma")!.Number!.Value, 12);
        Assert.Equal("Normal", measured[0].Get("dist")!.Text);
        Assert.NotNull(ScanImportProvenance.TryParse(measured[1].Get("mean")!.Provenance));
        Assert.Empty(glandImport.Action.Instance.Blocks);
        Assert.All(new[] { "squeeze_min", "squeeze_max", "fill_max", "stretch" }, port => Assert.True(double.IsFinite(ChainKit.Si(glandImport.Action.Instance, port))));
        Assert.False((await host.Service.StalenessAsync(glandImport.Action.Instance)).IsStale);
    }

    [Fact]
    public async Task Re_Importing_A_Table_In_Which_The_Groove_Root_Moved_Makes_The_Gland_Stale_And_Blocks_Its_Check_Until_The_Link_Is_Updated()
    {
        var bus = new RecordingBus();
        var host = Host(bus);
        var (stack, gland) = await BuildAsync(host);
        Assert.True((await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), StackProfile()))).Action.Succeeded);
        var fresh = await host.Service.UpdateLinksAsync(gland.Id);
        Assert.True(fresh.Succeeded, fresh.Reason);
        Assert.True((await host.Service.ApplyScanImportAsync(gland.Id, Request(Table(), SealProfile()))).Action.Succeeded);
        Assert.False((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(gland.Id))!)).IsStale);
        bus.Published.Clear();

        // The same file again changes no value and leaves the gland fresh.
        var same = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), StackProfile()));
        Assert.True(same.Action.Succeeded, same.Action.Reason);
        Assert.False(same.Diff!.HasChanges);
        Assert.False((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(gland.Id))!)).IsStale);

        // A re-scan in which the groove root runs 0.010 mm larger: a different file, a different hash.
        var moved = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(groove: 0.010), StackProfile()));
        Assert.True(moved.Action.Succeeded, moved.Action.Reason);
        var diff = moved.Diff!;
        Assert.False(diff.FirstImport);
        var change = Assert.Single(diff.Changes);
        Assert.Equal(ScanChangeKind.Changed, change.Kind);
        Assert.Equal("Groove root", change.Feature);
        Assert.True(change.AffectsInput);
        Assert.Contains("mean 51.485 mm", change.Before, StringComparison.Ordinal);
        Assert.Contains("mean 51.495 mm", change.After, StringComparison.Ordinal);
        Assert.Contains("0 added, 0 removed, 1 changed", diff.Summary, StringComparison.Ordinal);
        Assert.Single(moved.Action.Instance!.ScanImports!); // one record per profile: replaced, not accumulated
        Assert.Equal(0.051495, ChainKit.Si(moved.Action.Instance, "x_mean"), 9);

        // The existing machinery did the rest.
        var glandNow = (await host.Store.FindInstanceAsync(gland.Id))!;
        Assert.True((await host.Service.StalenessAsync(glandNow)).IsStale);
        Assert.Contains(bus.Published.OfType<CalcStalenessChanged>(), e => e.AffectedIds.Contains(gland.Id) || e.CauseId == stack.Id);
        var check = await host.Service.CheckAsync(gland.Id, "J. Reviewer", "Client Co", "OK", Tempest.Core.Evidence.CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("stale", check.Reason, StringComparison.OrdinalIgnoreCase);

        var update = await host.Service.UpdateLinksAsync(gland.Id);
        Assert.True(update.Succeeded, update.Reason);
        Assert.Equal(51.495, update.Instance!.Inputs.Single(i => i.Symbol == "D_g").Number!.Value, 9);
        Assert.False((await host.Service.StalenessAsync(update.Instance)).IsStale);

        // Fresh again; the check now waits only on the upstream stack being checked and issued, which is the existing rule for any link.
        var checkAfter = await host.Service.CheckAsync(gland.Id, "J. Reviewer", "Client Co", "OK", Tempest.Core.Evidence.CheckOutcome.Accepted);
        Assert.DoesNotContain("stale", checkAfter.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Re_Importing_Into_The_Gland_Itself_Replaces_Its_Measured_Rows_And_Reports_The_Diff()
    {
        var host = Host();
        var (_, gland) = await BuildAsync(host);
        var first = await host.Service.ApplyScanImportAsync(gland.Id, Request(Table(), SealProfile()));
        Assert.True(first.Action.Succeeded, first.Action.Reason);

        var changed = Table().Replace("Bore,56.9,56.915", "Bore,56.9,56.935", StringComparison.Ordinal);
        var second = await host.Service.ApplyScanImportAsync(gland.Id, Request(changed, SealProfile()));

        Assert.True(second.Action.Succeeded, second.Action.Reason);
        var rows = second.Action.Instance!.Inputs.Single(i => i.Symbol == "measured").Rows!;
        Assert.Equal(2, rows.Count);
        Assert.Equal(Mean(Bore.Skip(1).Append(56.935)), rows[0].Get("mean")!.Number!.Value, 9);
        Assert.Equal(1, second.Diff!.Count(ScanChangeKind.Changed));
    }

    [Fact]
    public async Task A_Refused_Scan_Import_Changes_Nothing()
    {
        var host = Host();
        var (stack, _) = await BuildAsync(host);
        var before = (await host.Store.FindInstanceAsync(stack.Id))!;

        // The wrong columns; a feature the profile requires that is not in the table; a contributor row that does not exist.
        var wrongHeader = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table().Replace("Element", "Name", StringComparison.Ordinal), StackProfile()));
        Assert.False(wrongHeader.Action.Succeeded);
        Assert.Contains("does not match the profile 'seal-stack@1'", wrongHeader.Action.Reason, StringComparison.Ordinal);

        var missing = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table().Replace("Groove root", "Groove", StringComparison.Ordinal), StackProfile()));
        Assert.False(missing.Action.Succeeded);
        Assert.Contains("requires 'Groove root'", missing.Action.Reason, StringComparison.Ordinal);

        var noRow = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), StackProfile(2) with { Features = [new ScanFeatureEntry("Groove root", "contributors[4]")] }));
        Assert.False(noRow.Action.Succeeded);
        Assert.Contains("there is no row 4", noRow.Action.Reason, StringComparison.Ordinal);

        var wrongTemplate = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), SealProfile()));
        Assert.False(wrongTemplate.Action.Succeeded);
        Assert.Contains("is for the template fp.oring-gland", wrongTemplate.Action.Reason, StringComparison.Ordinal);

        var after = (await host.Store.FindInstanceAsync(stack.Id))!;
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Null(after.ScanImports);
    }

    [Fact]
    public async Task A_Checked_Or_Issued_Calculation_Is_Never_Changed_In_Place_And_The_Revision_Remembers_The_Scan_Import()
    {
        var host = Host();
        var (stack, _) = await BuildAsync(host);
        Assert.True((await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), StackProfile()))).Action.Succeeded);

        Assert.True((await host.Service.CheckAsync(stack.Id, "J. Reviewer", "Client Co", "OK", Tempest.Core.Evidence.CheckOutcome.Accepted)).Succeeded);
        var whileChecked = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(0.01), StackProfile()));
        Assert.False(whileChecked.Action.Succeeded);
        Assert.Contains("Checked", whileChecked.Action.Reason, StringComparison.Ordinal);

        Assert.True((await host.Service.IssueAsync(stack.Id, "ISS-1", "Client Co")).Succeeded);
        var whileIssued = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(0.01), StackProfile()));
        Assert.Contains("Revise it first", whileIssued.Action.Reason, StringComparison.Ordinal);

        var revised = await host.Service.ReviseAsync(stack.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Single(revised.Instance!.ScanImports!);
        var again = await host.Service.ApplyScanImportAsync(revised.Instance.Id, Request(Table(0.01), StackProfile()));
        Assert.True(again.Action.Succeeded, again.Action.Reason);
        Assert.Single(again.Diff!.Changes);
    }

    [Fact]
    public async Task Scan_Profiles_Are_Versioned_Immutable_And_The_Record_Survives_A_Store_Round_Trip()
    {
        var host = Host();
        Assert.Null((await host.Service.SaveScanProfileAsync(StackProfile(1))).Refusal);
        Assert.Contains("new version", (await host.Service.SaveScanProfileAsync(StackProfile(1))).Refusal, StringComparison.Ordinal);
        Assert.Null((await host.Service.SaveScanProfileAsync(StackProfile(2) with { Title = "Second" })).Refusal);
        Assert.Equal(2, (await host.Service.FindScanProfileAsync("seal-stack"))!.Version);
        Assert.Equal(1, (await host.Service.FindScanProfileAsync("seal-stack", 1))!.Version);
        Assert.Null(await host.Service.FindScanProfileAsync("seal-stack", 9));
        Assert.Equal("Second", Assert.Single(await host.Service.ListScanProfilesAsync()).Title);
        Assert.Contains("cannot be saved", (await host.Service.SaveScanProfileAsync(StackProfile() with { Id = "Bad Id" })).Refusal, StringComparison.Ordinal);

        var back = (await host.Service.FindScanProfileAsync("seal-stack", 1))!;
        Assert.Equal(StackProfile().Columns, back.Columns);
        Assert.Equal(ScanSoftware.ZeissInspect, back.Software);
        Assert.Equal("contributors[0]", back.Features[0].Target);

        var (stack, _) = await BuildAsync(host);
        await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), StackProfile()));
        var json = CalcInstanceStore.Serialise((await host.Store.FindInstanceAsync(stack.Id))!);
        var restored = CalcInstanceStore.Deserialise<CalcInstance>(json)!;
        var record = Assert.Single(restored.ScanImports!);
        Assert.Equal(3, record.Features.Count);
        Assert.Equal(CalcProvenanceKind.ScanImport, restored.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("upper")!.Provenance!.Kind);
    }

    [Fact]
    public async Task A_Table_The_Importer_Refuses_Is_Reported_In_The_Preview_And_Changes_Nothing()
    {
        var host = Host();
        var (stack, _) = await BuildAsync(host);
        var hostile = new ScanImportRequest([new ScanFileInput(BoundedInputReader.FromBytes([0xFF, 0xFE, 0x41, 0x00], "x.csv"))], StackProfile());

        var preview = await host.Service.PreviewScanImportAsync(stack.Id, hostile);
        Assert.False(preview.CanApply);
        Assert.Contains("UTF-8", preview.Refusal, StringComparison.Ordinal);
        Assert.Empty(preview.Stats);

        var outcome = await host.Service.ApplyScanImportAsync(stack.Id, hostile);
        Assert.False(outcome.Action.Succeeded);
        Assert.Null((await host.Store.FindInstanceAsync(stack.Id))!.ScanImports);
    }

    private static CalcInputValue Input(CalcInstance instance, string symbol) => instance.Inputs.Single(i => i.Symbol == symbol);

    private static CalcCell Cell(CalcInstance instance, string table, int row, string column) => Input(instance, table).Rows![row].Get(column)!;

    private static IReadOnlyList<CalcInputValue> EditCell(IReadOnlyList<CalcInputValue> inputs, int row, string column, Func<CalcCell, CalcCell> change) =>
        inputs.Select(i => i.Symbol != "contributors" ? i : i with
        {
            Rows = i.Rows!.Select((r, n) => n != row ? r : new CalcRow(new Dictionary<string, CalcCell>(r.Cells) { [column] = change(r.Cells[column]) })).ToList(),
        }).ToList();

    [Fact]
    public async Task Scan_Provenance_Is_A_Claim_That_Is_Checked_An_Edited_Or_Forged_Value_Becomes_A_Manual_Entry_With_The_Reason()
    {
        var host = Host();
        var (stack, _) = await BuildAsync(host);
        var imported = (await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), StackProfile()))).Action.Instance!;

        // Saving the imported inputs as they are keeps every claim, and an input the import did not touch can be edited.
        var resave = await host.Service.UpdateInputsAsync(stack.Id, imported.Inputs);
        Assert.True(resave.Succeeded, resave.Reason);
        Assert.True(string.IsNullOrEmpty(resave.Reason));
        Assert.Equal(CalcProvenanceKind.ScanImport, Cell(resave.Instance!, "contributors", 0, "upper").Provenance!.Kind);
        Assert.True((await host.Service.UpdateInputsAsync(stack.Id, imported.Inputs.Select(i => i.Symbol == "minimum" ? i with { Number = 51.3 } : i).ToList())).Succeeded);

        // Changing a fitted number but keeping its claim: saved, but that cell is a manual entry now and the person is told which.
        var edited = await host.Service.UpdateInputsAsync(stack.Id, EditCell(imported.Inputs, 0, "upper", c => c with { Number = 0.5 }));
        Assert.True(edited.Succeeded, edited.Reason);
        Assert.Contains("Scan-import provenance was removed from 'contributors[0].upper'", edited.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcProvenanceKind.Manual, Cell(edited.Instance!, "contributors", 0, "upper").Provenance!.Kind);
        Assert.Equal(0.5, Cell(edited.Instance!, "contributors", 0, "upper").Number);
        Assert.Equal(CalcProvenanceKind.ScanImport, Cell(edited.Instance!, "contributors", 0, "lower").Provenance!.Kind);

        // The kind chosen by hand with a reference copied from another value, and a made-up one: both lose the claim.
        var copied = Cell(imported, "contributors", 0, "upper").Provenance!;
        var forged = await host.Service.UpdateInputsAsync(stack.Id, imported.Inputs.Select(i => i.Symbol == "maximum" ? i with { Provenance = copied } : i).ToList());
        Assert.Contains("'maximum'", forged.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcProvenanceKind.Manual, Input(forged.Instance!, "maximum").Provenance!.Kind);
        var made = await host.Service.UpdateInputsAsync(stack.Id, imported.Inputs.Select(i => i.Symbol == "maximum" ? i with { Provenance = new CalcProvenance(CalcProvenanceKind.ScanImport, "scan:made up", "p@1") } : i).ToList());
        Assert.Equal(CalcProvenanceKind.Manual, Input(made.Instance!, "maximum").Provenance!.Kind);

        // Free text of the reference rewritten (a claim of 300 parts): the whole provenance must be the one written.
        var rewritten = await host.Service.UpdateInputsAsync(stack.Id, EditCell(imported.Inputs, 0, "upper", c => c with { Provenance = c.Provenance! with { Reference = c.Provenance.Reference!.Replace("S001..S010 (n=10)", "S001..S300 (n=300)", StringComparison.Ordinal) } }));
        Assert.Equal(CalcProvenanceKind.Manual, Cell(rewritten.Instance!, "contributors", 0, "upper").Provenance!.Kind);

        // A claim on the whole table is never honoured; a hand-typed row cannot borrow one.
        var wholeTable = await host.Service.UpdateInputsAsync(stack.Id, imported.Inputs.Select(i => i.Symbol == "contributors" ? i with { Provenance = copied, Rows = [.. i.Rows!, CalcRow.Of(("nominal", new CalcCell(1, "mm")), ("upper", new CalcCell(0.1, "mm", Provenance: copied)))] } : i).ToList());
        Assert.Equal(CalcProvenanceKind.Manual, Input(wholeTable.Instance!, "contributors").Provenance!.Kind);
        Assert.Equal(CalcProvenanceKind.Manual, Cell(wholeTable.Instance!, "contributors", 2, "upper").Provenance!.Kind);

        // A new calculation has no import record, so it cannot cite one.
        var created = await host.Service.CreateAsync(ToleranceStackCalculator.Id, null, "Copied", imported.Inputs);
        Assert.Equal(CalcProvenanceKind.Manual, Cell(created, "contributors", 0, "upper").Provenance!.Kind);

        // Overriding a fitted value is allowed with another source; a re-import is accepted against the new record.
        var overridden = await host.Service.UpdateInputsAsync(stack.Id, EditCell(imported.Inputs, 0, "upper", c => new CalcCell(0.5, "mm", Provenance: CalcProvenance.Assumed("measured on a different day"))));
        Assert.Equal(CalcProvenanceKind.Assumption, Cell(overridden.Instance!, "contributors", 0, "upper").Provenance!.Kind);
        Assert.True((await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(0.01), StackProfile()))).Action.Succeeded);
    }

    [Fact]
    public async Task The_Measured_Sigma_Goes_Into_M1_Itself_So_Changing_K_Later_Neither_Moves_It_Nor_Costs_The_Claim()
    {
        var host = Host();
        var (stack, _) = await BuildAsync(host);
        var imported = (await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), StackProfile()))).Action.Instance!;

        Assert.Equal(0.006236095644623894, Cell(imported, "contributors", 0, "sigma").Number!.Value, 12);
        Assert.Equal(3 * 0.006236095644623894, Cell(imported, "contributors", 0, "upper").Number!.Value, 12);

        // k from 3 to 6: the plating (band/k) halves its sigma, the scanned groove root keeps its measured sigma; both claims survive the save.
        var sixK = await host.Service.UpdateInputsAsync(stack.Id, imported.Inputs.Select(i => i.Symbol == "k" ? i with { Number = 6 } : i).ToList());
        Assert.True(sixK.Succeeded, sixK.Reason);
        Assert.True(string.IsNullOrEmpty(sixK.Reason));
        Assert.Equal(CalcProvenanceKind.ScanImport, Cell(sixK.Instance!, "contributors", 0, "sigma").Provenance!.Kind);
        Assert.Equal(Math.Sqrt(0.006236095644623894 * 0.006236095644623894 + (0.002 / 6) * (0.002 / 6)) / 1000, ChainKit.Si(sixK.Instance!, "sigma"), 12);
    }

    [Fact]
    public async Task A_Table_That_Came_From_A_Scan_Is_Issuable_Only_While_Every_Number_In_It_Has_A_Verified_Source()
    {
        var host = Host();
        var (_, gland) = await BuildAsync(host);
        var imported = (await host.Service.ApplyScanImportAsync(gland.Id, Request(Table(), SealProfile()))).Action.Instance!;
        var template = new ORingGlandCalculator().Template;

        // An import gives the whole table no provenance of its own; its cells stand for it.
        Assert.Null(Input(imported, "measured").Provenance);
        Assert.DoesNotContain(CalcInputValidator.ValidateProvenanceForIssue(template, new CalcInputSet(imported.Inputs)), n => n.Message.StartsWith("measured", StringComparison.Ordinal));

        // A value edited afterwards loses its scan source (the verifier), so the table is blocked like any input without a source.
        var edited = imported.Inputs.Select(i => i.Symbol != "measured" ? i : i with
        {
            Rows = i.Rows!.Select((r, n) => n != 0 ? r : new CalcRow(new Dictionary<string, CalcCell>(r.Cells) { ["mean"] = r.Cells["mean"] with { Number = 56.9 } })).ToList(),
        }).ToList();
        var saved = await host.Service.UpdateInputsAsync(gland.Id, edited);
        Assert.True(saved.Succeeded, saved.Reason);
        Assert.Contains(CalcInputValidator.ValidateProvenanceForIssue(template, new CalcInputSet(saved.Instance!.Inputs)), n => n.Message.StartsWith("measured", StringComparison.Ordinal));

        // A hand-typed row is blocked too.
        var typed = imported.Inputs.Select(i => i.Symbol != "measured" ? i : i with { Rows = [.. i.Rows!, CalcRow.Of(("dim", new CalcCell(Text: "D_g")), ("mean", new CalcCell(51.5, "mm")), ("sigma", new CalcCell(0.01, "mm")))] }).ToList();
        Assert.Contains(CalcInputValidator.ValidateProvenanceForIssue(template, new CalcInputSet(typed)), n => n.Message.StartsWith("measured", StringComparison.Ordinal));

        // A table with no scan cells at all and no source is, as before, blocked.
        var plain = imported.Inputs.Select(i => i.Symbol != "measured" ? i : i with { Rows = [CalcRow.Of(("dim", new CalcCell(Text: "D_s")), ("mean", new CalcCell(56.9, "mm")), ("sigma", new CalcCell(0.01, "mm")))] }).ToList();
        Assert.Contains(CalcInputValidator.ValidateProvenanceForIssue(template, new CalcInputSet(plain)), n => n.Message.StartsWith("measured", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Later_Import_Through_The_Same_Profile_Keeps_The_Provenance_Of_Values_It_Did_Not_Rewrite()
    {
        var host = Host();
        var (stack, _) = await BuildAsync(host);
        var two = new ScanProfile(
            "seal-two", 1, "Groove root and plating", Columns,
            [new ScanFeatureEntry("Groove root", "contributors[0]"), new ScanFeatureEntry("Plating", "contributors[1]")],
            ScanSoftware.ZeissInspect, TemplateId: ToleranceStackCalculator.Id);
        var plating = "Element,Nominal,Actual,Deviation,Upper tol.,Lower tol.,Serial\r\nPlating,0,0.001,0.001,0.002,-0.002,P1\r\nPlating,0,0.0015,0.0015,0.002,-0.002,P2\r\nPlating,0,0.0005,0.0005,0.002,-0.002,P3\r\n";

        var first = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), two));
        Assert.True(first.Action.Succeeded, first.Action.Reason);

        // The second file holds only the plating: the groove root is not rewritten, and keeps the first file's provenance.
        var second = await host.Service.ApplyScanImportAsync(stack.Id, Request(plating, two));
        Assert.True(second.Action.Succeeded, second.Action.Reason);
        var now = second.Action.Instance!;
        Assert.Equal(CalcProvenanceKind.ScanImport, Cell(now, "contributors", 0, "upper").Provenance!.Kind);
        Assert.Equal(CalcProvenanceKind.ScanImport, Cell(now, "contributors", 1, "upper").Provenance!.Kind);
        Assert.Equal(2, now.ScanImports!.Count);
        Assert.NotEqual(ScanImportProvenance.TryParse(Cell(now, "contributors", 0, "upper").Provenance)!.Sha256, ScanImportProvenance.TryParse(Cell(now, "contributors", 1, "upper").Provenance)!.Sha256);

        // A later save changes nothing about either claim.
        var resave = await host.Service.UpdateInputsAsync(stack.Id, now.Inputs);
        Assert.True(string.IsNullOrEmpty(resave.Reason));

        // Once an import rewrites the first file's values, the first record is dropped (nothing cites it).
        var third = await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(0.01), two));
        Assert.True(third.Action.Succeeded, third.Action.Reason);
        Assert.True(third.Action.Instance!.ScanImports!.Count <= 3);
    }

    [Fact]
    public async Task Issue_Is_Refused_While_A_Checked_Gland_Is_Stale_After_A_Moved_Table_Is_Re_Imported_Into_A_Revised_Stack()
    {
        var host = Host();
        var (stack, gland) = await BuildAsync(host);
        Assert.True((await host.Service.ApplyScanImportAsync(stack.Id, Request(Table(), StackProfile()))).Action.Succeeded);

        // The stack is checked and issued, so the gland can be checked against it.
        Assert.True((await host.Service.CheckAsync(stack.Id, "J. Reviewer", "Client Co", "OK", Tempest.Core.Evidence.CheckOutcome.Accepted)).Succeeded);
        var issuedStack = await host.Service.IssueAsync(stack.Id, "STK-1", "Client Co");
        Assert.True(issuedStack.Succeeded, issuedStack.Reason);
        var linked = await host.Service.UpdateLinksAsync(gland.Id);
        Assert.True(linked.Succeeded, linked.Reason);
        Assert.True((await host.Service.ApplyScanImportAsync(gland.Id, Request(Table(), SealProfile()))).Action.Succeeded);
        var checkedGland = await host.Service.CheckAsync(gland.Id, "J. Reviewer", "Client Co", "OK", Tempest.Core.Evidence.CheckOutcome.Accepted);
        Assert.True(checkedGland.Succeeded, checkedGland.Reason);
        Assert.False((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(gland.Id))!)).IsStale);

        // A re-scan in which the groove root runs 0.010 mm larger goes into a new revision of the stack; the checked gland still points at the issued one.
        var revised = await host.Service.ReviseAsync(stack.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        var moved = await host.Service.ApplyScanImportAsync(revised.Instance!.Id, Request(Table(groove: 0.010), StackProfile()));
        Assert.True(moved.Action.Succeeded, moved.Action.Reason);
        Assert.Equal(0.051495, ChainKit.Si(moved.Action.Instance!, "x_mean"), 9);

        var glandNow = (await host.Store.FindInstanceAsync(gland.Id))!;
        Assert.Equal(CalcInstanceStatus.Checked, glandNow.Status);
        Assert.True((await host.Service.StalenessAsync(glandNow)).IsStale);

        // The real Issue path refuses it at the staleness gate, and nothing is issued.
        var refused = await host.Service.IssueAsync(gland.Id, "GLD-1", "Client Co");
        Assert.False(refused.Succeeded);
        Assert.Contains("is stale", refused.Reason, StringComparison.Ordinal);
        Assert.Contains("Re-run it as a new revision first", refused.Reason, StringComparison.Ordinal);
        var after = (await host.Store.FindInstanceAsync(gland.Id))!;
        Assert.Equal(CalcInstanceStatus.Checked, after.Status);
        Assert.Null(after.IssueReference);
    }
}
