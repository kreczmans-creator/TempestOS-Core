using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B §B7 chain 3, CAD-driven tolerance: W1 → M1 → M2. An NX expression file is imported into an M1 tolerance-stack
/// draft through a stored mapping profile; a linked M2 fit takes its size from the stack; re-importing a changed file updates the
/// draft through the ordinary input update, so the downstream fit goes stale through the existing staleness machinery (no new path),
/// and a checked or issued calculation is never changed in place.
/// </summary>
public sealed class CadDrivenToleranceChainTests
{
    private const string StackFile = """
        // Expression file created by NX 2306 for an invented stack (golden sample, not a real design)
        // Part: STACK-1
        // Revision: A
        [mm]plate_len=30
        [mm]spacer_len=10
        [mm]pin_len=5
        [mm]gap_min=24.5
        [mm]gap_max=25.5
        [mm]derived=plate_len-spacer_len
        """;

    private static readonly CalcProvenance Drawing = CalcProvenance.Drawing("DRG-STK-1", "A");

    private static LibraryTestHost Host(RecordingBus? bus = null) => new(calculators: MechanicalCalculators.All, events: bus);

    private static CadMappingProfile Profile(int version = 1) => new(
        "nx-stack", version, "NX stack lengths to M1", [
            new("plate_len", "contributors[0].nominal"),
            new("spacer_len", "contributors[1].nominal"),
            new("pin_len", "contributors[2].nominal"),
            new("gap_min", "minimum"),
            new("gap_max", "maximum"),
        ],
        CadSystem.Nx, ToleranceStackCalculator.Id);

    private static CadImportRequest Request(string file, CadMappingProfile? profile = null, string name = "stack-1.exp", CadParseSettings? settings = null) =>
        new(Source(file, name), CadSourceFormat.NxExpressions, profile ?? Profile(), settings);

    private static ImportedFile Source(string text, string name) => BoundedInputReader.FromBytes(System.Text.Encoding.UTF8.GetBytes(text), name);

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

    private static async Task<(CalcInstance Stack, CalcInstance Fit)> StackAndFitAsync(LibraryTestHost host, Guid? project)
    {
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, project, "Pin stack", StackInputs());
        Assert.Empty(stack.Blocks);
        var fit = await host.Service.CreateAsync("mech.iso286-fit", project, "Seat fit", [CalcInputValue.Quantity("D", 25, "mm", Drawing)]);
        var bound = await host.Service.BindAsync(fit.Id, "D", stack.Id, "x_nom");
        Assert.True(bound.Succeeded, bound.Reason);
        return (stack, bound.Instance!);
    }

    /// <summary>What a person does after an import that moved the stack: update the fit's link so it is fresh again.</summary>
    private static async Task<CalcInstance> FreshAsync(LibraryTestHost host, CalcInstance fit)
    {
        var update = await host.Service.UpdateLinksAsync(fit.Id);
        Assert.True(update.Succeeded, update.Reason);
        Assert.False((await host.Service.StalenessAsync(update.Instance!)).IsStale);
        return update.Instance!;
    }

    [Fact]
    public async Task An_Nx_Expression_File_Drives_The_Stack_Through_A_Stored_Profile_With_Provenance_And_The_Fit_Follows()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (profile, refusal) = await host.Service.SaveImportProfileAsync(Profile());
        Assert.Null(refusal);
        var (stack, fit) = await StackAndFitAsync(host, project);

        var preview = await host.Service.PreviewCadImportAsync(stack.Id, Request(StackFile, profile));
        Assert.True(preview.CanApply, string.Join(" ", preview.Reasons));
        Assert.True(preview.Diff!.FirstImport);
        Assert.Equal(5, preview.Mapping!.Count(CadRowStatus.Mapped));
        Assert.Equal(1, preview.Mapping.Count(CadRowStatus.Expression));
        Assert.Empty(preview.BlocksAfter);
        Assert.Equal(stack.UpdatedAt, (await host.Store.FindInstanceAsync(stack.Id))!.UpdatedAt); // a preview changes nothing

        var applied = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile, profile));
        Assert.True(applied.Action.Succeeded, applied.Action.Reason);
        var imported = applied.Action.Instance!;

        // 30 − 10 + 5 = 25 mm; the stack's limits come from the file.
        Assert.Equal(0.025, ChainKit.Si(imported, "x_nom"), 9);
        Assert.Equal(24.5, imported.Inputs.Single(i => i.Symbol == "minimum").Number);
        Assert.Equal(25.5, imported.Inputs.Single(i => i.Symbol == "maximum").Number);
        Assert.Equal(30, imported.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("nominal")!.Number);
        Assert.Equal("Plate", imported.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("name")!.Text);

        var info = CadImportProvenance.TryParse(imported.Inputs.Single(i => i.Symbol == "minimum").Provenance)!;
        Assert.Equal("NX 2306", info.SystemAndVersion);
        Assert.Equal("STACK-1", info.Part);
        Assert.Equal("A", info.PartRevision);
        Assert.Equal(64, info.FileSha256.Length);
        Assert.Equal(("nx-stack", 1), (info.ProfileId, info.ProfileVersion));
        Assert.NotNull(CadImportProvenance.TryParse(imported.Inputs.Single(i => i.Symbol == "contributors").Rows![2].Get("nominal")!.Provenance));

        // The import is remembered: file, hash, every parameter (the expression too, as text), who and when.
        var record = Assert.Single(imported.CadImports!);
        Assert.Equal("stack-1.exp", record.FileName);
        Assert.Equal(info.FileSha256, record.FileSha256);
        Assert.Equal(6, record.Parameters.Count);
        Assert.Equal("plate_len-spacer_len", record.Parameters.Single(p => p.Name == "derived").Text);
        Assert.Equal("test.engineer", record.ImportedBy);
        Assert.Null(record.Parameters.Single(p => p.Name == "derived").Number);

        // The fit still holds the stack's nominal (25 mm) it was linked at; the stack's other outputs moved with the new limits, so updating the link brings it up to date.
        var fitNow = (await host.Store.FindInstanceAsync(fit.Id))!;
        Assert.Equal(25, fitNow.Inputs.Single(i => i.Symbol == "D").Number!.Value, 9);
        Assert.Equal(25, (await FreshAsync(host, fitNow)).Inputs.Single(i => i.Symbol == "D").Number!.Value, 9);
    }

    [Fact]
    public async Task Re_Importing_A_Changed_File_Reports_The_Diff_Updates_The_Draft_And_Makes_The_Linked_Fit_Stale()
    {
        var bus = new RecordingBus();
        var host = Host(bus);
        var project = await host.ProjectAsync();
        var (stack, fit) = await StackAndFitAsync(host, project);
        var first = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile));
        Assert.True(first.Action.Succeeded, first.Action.Reason);
        await FreshAsync(host, (await host.Store.FindInstanceAsync(fit.Id))!);
        bus.Published.Clear();

        // The designer changes the plate to 31 mm, drops gap_max and adds a pin length expression: a different file.
        var changed = StackFile
            .Replace("plate_len=30", "plate_len=31", StringComparison.Ordinal)
            .Replace("[mm]gap_max=25.5\n", string.Empty, StringComparison.Ordinal)
            .Replace("// Revision: A", "// Revision: B", StringComparison.Ordinal);
        changed = changed.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n[mm]shim=0.5\n";

        var second = await host.Service.ApplyCadImportAsync(stack.Id, Request(changed));
        Assert.True(second.Action.Succeeded, second.Action.Reason);
        var diff = second.Diff!;

        Assert.False(diff.FirstImport);
        Assert.Equal(1, diff.Count(CadChangeKind.Added));
        Assert.Equal(1, diff.Count(CadChangeKind.Removed));
        Assert.Equal(1, diff.Count(CadChangeKind.Changed));
        Assert.Equal("30 mm", diff.Changes.Single(c => c.Kind == CadChangeKind.Changed).Before);
        Assert.Equal("31 mm", diff.Changes.Single(c => c.Kind == CadChangeKind.Changed).After);
        Assert.Equal("shim", diff.Changes.Single(c => c.Kind == CadChangeKind.Added).Name);
        Assert.Equal("gap_max", diff.Changes.Single(c => c.Kind == CadChangeKind.Removed).Name);
        Assert.Contains("1 added, 1 removed, 1 changed", diff.Summary, StringComparison.Ordinal);

        // The draft carries the new nominal; the removed parameter leaves its input at the last imported value (the diff says so).
        var updated = second.Action.Instance!;
        Assert.Equal(31, updated.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("nominal")!.Number);
        Assert.Equal(25.5, updated.Inputs.Single(i => i.Symbol == "maximum").Number);
        Assert.Equal(0.026, ChainKit.Si(updated, "x_nom"), 9);
        Assert.Equal("B", CadImportProvenance.TryParse(updated.Inputs.Single(i => i.Symbol == "minimum").Provenance)!.PartRevision);
        Assert.Single(updated.CadImports!); // one record per source: replaced, not accumulated
        Assert.Equal("B", updated.CadImports![0].PartRevision);

        // The existing machinery did the rest: the fit is stale, the event was published, the fit cannot be checked, updating the link clears it.
        var fitStale = (await host.Store.FindInstanceAsync(fit.Id))!;
        var staleness = await host.Service.StalenessAsync(fitStale);
        Assert.True(staleness.IsStale);
        Assert.Contains(bus.Published.OfType<CalcStalenessChanged>(), e => e.AffectedIds.Contains(fit.Id) || e.CauseId == stack.Id);

        var check = await host.Service.CheckAsync(fit.Id, "J. Reviewer", "Client Co", "OK", Tempest.Core.Evidence.CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("stale", check.Reason, StringComparison.OrdinalIgnoreCase);

        var update = await host.Service.UpdateLinksAsync(fit.Id);
        Assert.True(update.Succeeded, update.Reason);
        Assert.Equal(26, update.Instance!.Inputs.Single(i => i.Symbol == "D").Number!.Value, 9);
        Assert.False((await host.Service.StalenessAsync(update.Instance)).IsStale);
    }

    [Fact]
    public async Task Importing_The_Same_File_Again_Changes_No_Value_And_Leaves_The_Fit_Fresh()
    {
        var host = Host();
        var (stack, fit) = await StackAndFitAsync(host, await host.ProjectAsync());
        await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile));
        await FreshAsync(host, (await host.Store.FindInstanceAsync(fit.Id))!);
        var again = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile));

        Assert.True(again.Action.Succeeded, again.Action.Reason);
        Assert.False(again.Diff!.HasChanges);
        Assert.Contains("No change", again.Diff.Summary, StringComparison.Ordinal);
        Assert.False((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(fit.Id))!)).IsStale);
    }

    [Fact]
    public async Task A_Refused_Import_Changes_Nothing_Not_Even_The_Parts_That_Were_Fine()
    {
        var host = Host();
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, await host.ProjectAsync(), "Pin stack", StackInputs());
        var before = (await host.Store.FindInstanceAsync(stack.Id))!;

        // gap_min in newtons, and an expression mapped as a number.
        var bad = StackFile.Replace("[mm]gap_min=24.5", "[N]gap_min=24.5", StringComparison.Ordinal);
        var outcome = await host.Service.ApplyCadImportAsync(stack.Id, Request(bad));
        Assert.False(outcome.Action.Succeeded);
        Assert.Contains("not the same kind of quantity", outcome.Action.Reason, StringComparison.Ordinal);

        var withExpression = Profile() with { Version = 2, Entries = [.. Profile().Entries, new("derived", "contributors[2].upper")] };
        var second = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile, withExpression));
        Assert.False(second.Action.Succeeded);
        Assert.Contains("never evaluated", second.Action.Reason, StringComparison.Ordinal);

        var after = (await host.Store.FindInstanceAsync(stack.Id))!;
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Null(after.CadImports);
        Assert.Equal(20, after.Inputs.Single(i => i.Symbol == "minimum").Number);
    }

    [Fact]
    public async Task A_File_The_Importer_Refuses_Is_Reported_In_The_Preview_And_Changes_Nothing()
    {
        var host = Host();
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, await host.ProjectAsync(), "Pin stack", StackInputs());
        var hostile = new CadImportRequest(BoundedInputReader.FromBytes([0xFF, 0xFE, 0x41, 0x00], "x.exp"), CadSourceFormat.NxExpressions, Profile());

        var preview = await host.Service.PreviewCadImportAsync(stack.Id, hostile);
        Assert.False(preview.CanApply);
        Assert.Contains("UTF-8", preview.Refusal, StringComparison.Ordinal);

        var outcome = await host.Service.ApplyCadImportAsync(stack.Id, hostile);
        Assert.False(outcome.Action.Succeeded);
        Assert.Null((await host.Store.FindInstanceAsync(stack.Id))!.CadImports);
    }

    [Fact]
    public async Task The_Preview_Says_What_The_Calculation_Would_Block_After_The_Import()
    {
        var host = Host();
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, await host.ProjectAsync(), "Pin stack", StackInputs());

        // A nominal of 5e9 mm is outside M1's hard limits for a contributor: the preview shows the block; the draft would show it too.
        var preview = await host.Service.PreviewCadImportAsync(stack.Id, Request(StackFile.Replace("plate_len=30", "plate_len=5000000000", StringComparison.Ordinal)));
        Assert.True(preview.CanApply);
        Assert.NotEmpty(preview.BlocksAfter);
    }

    [Fact]
    public async Task A_Checked_Or_Issued_Calculation_Is_Never_Changed_In_Place_Revise_First_And_The_Revision_Remembers_The_Import()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, project, "Pin stack", StackInputs());
        var imported = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile));
        Assert.True(imported.Action.Succeeded, imported.Action.Reason);

        var check = await host.Service.CheckAsync(stack.Id, "J. Reviewer", "Client Co", "OK", Tempest.Core.Evidence.CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        var whileChecked = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile.Replace("plate_len=30", "plate_len=31", StringComparison.Ordinal)));
        Assert.False(whileChecked.Action.Succeeded);
        Assert.Contains("Checked", whileChecked.Action.Reason, StringComparison.Ordinal);

        var issue = await host.Service.IssueAsync(stack.Id, "ISS-1", "Client Co");
        Assert.True(issue.Succeeded, issue.Reason);
        var whileIssued = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile.Replace("plate_len=30", "plate_len=31", StringComparison.Ordinal)));
        Assert.False(whileIssued.Action.Succeeded);
        Assert.Contains("Revise it first", whileIssued.Action.Reason, StringComparison.Ordinal);
        Assert.Equal(30, (await host.Store.FindInstanceAsync(stack.Id))!.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("nominal")!.Number);

        var revised = await host.Service.ReviseAsync(stack.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Single(revised.Instance!.CadImports!);

        var again = await host.Service.ApplyCadImportAsync(revised.Instance.Id, Request(StackFile.Replace("plate_len=30", "plate_len=31", StringComparison.Ordinal)));
        Assert.True(again.Action.Succeeded, again.Action.Reason);
        Assert.False(again.Diff!.FirstImport);
        Assert.Single(again.Diff.Changes);
    }

    [Fact]
    public async Task A_Profile_For_Another_Template_Or_System_Is_Refused_For_This_Draft()
    {
        var host = Host();
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, await host.ProjectAsync(), "Pin stack", StackInputs());

        var otherTemplate = Profile() with { TemplateId = "mech.iso286-fit" };
        Assert.False((await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile, otherTemplate))).Action.Succeeded);

        var otherSystem = Profile() with { System = CadSystem.Creo };
        var outcome = await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile, otherSystem));
        Assert.False(outcome.Action.Succeeded);
        Assert.Contains("for Creo files", outcome.Action.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Profiles_Are_Versioned_Immutable_And_Listed_By_Their_Latest_Version()
    {
        var host = Host();
        Assert.Null((await host.Service.SaveImportProfileAsync(Profile(1))).Refusal);
        var again = await host.Service.SaveImportProfileAsync(Profile(1));
        Assert.Contains("new version", again.Refusal, StringComparison.Ordinal);
        Assert.Null((await host.Service.SaveImportProfileAsync(Profile(2) with { Title = "Second" })).Refusal);

        Assert.Equal(2, (await host.Service.FindImportProfileAsync("nx-stack"))!.Version);
        Assert.Equal("NX stack lengths to M1", (await host.Service.FindImportProfileAsync("nx-stack", 1))!.Title);
        Assert.Null(await host.Service.FindImportProfileAsync("nx-stack", 9));
        Assert.Null(await host.Service.FindImportProfileAsync("nope"));
        var listed = Assert.Single(await host.Service.ListImportProfilesAsync());
        Assert.Equal("Second", listed.Title);

        var invalid = await host.Service.SaveImportProfileAsync(new CadMappingProfile("Bad Id", 1, "t", []));
        Assert.Null(invalid.Profile);
        Assert.Contains("cannot be saved", invalid.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Import_Record_Survives_A_Store_Round_Trip_As_Stored_Json()
    {
        var host = Host();
        var stack = await host.Service.CreateAsync(ToleranceStackCalculator.Id, await host.ProjectAsync(), "Pin stack", StackInputs());
        await host.Service.ApplyCadImportAsync(stack.Id, Request(StackFile));

        var json = CalcInstanceStore.Serialise((await host.Store.FindInstanceAsync(stack.Id))!);
        var back = CalcInstanceStore.Deserialise<CalcInstance>(json)!;
        var record = Assert.Single(back.CadImports!);
        Assert.Equal(CadSystem.Nx, record.System);
        Assert.Equal(CadSourceFormat.NxExpressions, record.Format);
        Assert.Equal(CadParameterKind.Expression, record.Parameters.Single(p => p.Name == "derived").Kind);
        Assert.Equal("contributors[0].nominal", record.Parameters.Single(p => p.Name == "plate_len").Target);
        Assert.Equal(CalcProvenanceKind.CadImport, back.Inputs.Single(i => i.Symbol == "minimum").Provenance!.Kind);
    }
}
