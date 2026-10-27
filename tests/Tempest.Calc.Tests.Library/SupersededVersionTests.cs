using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// ADR-0177 (DC-14): a template version that has been superseded stays resolvable by <c>id@version</c>. An issued calculation on an older version
/// re-opens as stored and re-runs only where the installed formulas reproduce it exactly (otherwise it is read-only with a sentence saying why); an
/// older draft is offered as a new sheet on the current version with its inputs; the current version is the default for new sheets; and nothing a
/// re-run does touches a stored record.
/// </summary>
public sealed class SupersededVersionTests
{
    private const string Id = "test.vsn";

    /// <summary>The same worksheet in any version: y = factor * x. Two versions with different factors stand for formulas that changed.</summary>
    private sealed class Scaler(string version, double factor, string id = Id) : ICalcCalculator
    {
        public CalcTemplate Template { get; } = new(id, version, CalcDiscipline.Mechanical, "Scaler", "", "", [new CalcInputSpec("x", "x", CalcInputKind.Number, Default: 1)], [], [], [], []);

        public void Run(CalcRunContext context)
        {
            context.Input("x");
            context.Assign("y", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"x * {factor}"));
            context.Output("y", "scaled");
        }
    }

    /// <summary>
    /// The same worksheet on a length x (mm) giving y = factor · x, with what a later version may add (review of DC-14): a check the stored
    /// calculation fails, a block on the stored input, a refusal of the stored input (an exception the runner does not turn into a block), the
    /// output shown in another unit with the same SI value, or a value that rounds to the same printed text.
    /// </summary>
    private sealed class Lengths(string version, double factor = 2, string unit = "mm", bool failingCheck = false, bool passingCheck = false, bool blocksLarge = false, bool refusesLarge = false) : ICalcCalculator
    {
        public CalcTemplate Template { get; } = new(Id, version, CalcDiscipline.Mechanical, "Lengths", "", "", [new CalcInputSpec("x", "x", CalcInputKind.Quantity, "mm", Default: 1)], [], [], [], []);

        public void Run(CalcRunContext c)
        {
            c.Input("x");

            if (refusesLarge && c.InputSi("x") > 0.003)
                throw new NotSupportedException("x above 3 mm is not supported by this version.");

            c.Assign("y", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"x * {factor}"), unit, "scaled");
            c.Output("y", "scaled");

            if (blocksLarge && c.InputSi("x") > 0.003)
                c.Block("large", "x: above 3 mm is outside this version.");

            if (failingCheck || passingCheck)
            {
                c.Assign("u", failingCheck ? "x / 2 mm" : "x / 20 mm", null, "Utilisation");
                c.CheckUtilisation("Limit", "u");
            }
        }
    }

    private static IReadOnlyList<CalcInputValue> Lengths4() => [CalcInputValue.Quantity("x", 4, "mm", CalcProvenance.Assumed("Test value."))];

    private static TemplateVersionHistory Recorded(params string[] references) => new(references.Select(r => new KeyValuePair<string, string>(r, new string('a', 64))));

    private static IReadOnlyList<CalcInputValue> Inputs(double x = 4) => [CalcInputValue.Of("x", x, CalcProvenance.Assumed("Test value."))];

    /// <summary>The stored record is what it was: the same document, byte for byte, as the store writes it (a record's lists compare by reference, so the JSON is compared).</summary>
    private static void AssertUnchanged(CalcInstance? expected, CalcInstance? actual) =>
        Assert.Equal(CalcInstanceStore.Serialise(expected), CalcInstanceStore.Serialise(actual));

    /// <summary>A second service over the same store, evidence and context as <paramref name="host"/>, with another catalogue: the build after a template was revised.</summary>
    private static CalcInstanceService LaterBuild(LibraryTestHost host, CalcCatalogue catalogue) =>
        new(catalogue, host.Store, host.Evidence, host.Context, renderer: null, applicationVersion: "TempestOS later build") { PrincipalOverride = () => host.Principal };

    private static async Task<CalcInstance> IssuedOnV1Async(LibraryTestHost host, Guid? project, string name = "Issued scaler")
    {
        var instance = await host.Service.CreateAsync(Id, project, name, Inputs());
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var issued = await host.Service.IssueAsync(instance.Id, "ISS-V1", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);
        return (await host.Store.FindInstanceAsync(instance.Id))!;
    }

    [Fact]
    public void Every_Version_In_The_Manifest_Resolves_By_Id_And_Version_And_The_Current_One_Is_Installed()
    {
        var catalogue = new CalcCatalogue(ShippedCalculators.All, new CalcFeatureFlags(CalcFeatures.All));
        var history = TemplateVersionHistory.Shipped;
        Assert.NotEmpty(history.References);
        var installed = 0;
        var superseded = 0;

        foreach (var reference in history.References)
        {
            var resolution = catalogue.Resolve(reference);
            Assert.NotEqual(CalcVersionState.Gone, resolution.State);
            Assert.NotEqual(CalcVersionState.Unrecorded, resolution.State);
            Assert.Equal(history.HashOf(reference), resolution.RecordedHash);
            Assert.NotNull(resolution.Current);

            if (resolution.State == CalcVersionState.Installed)
            {
                installed++;
                Assert.Equal(reference, resolution.Entry!.Reference);
                Assert.Equal(history.HashOf(reference), resolution.Entry.ContentHash);
            }
            else
            {
                superseded++;
                Assert.Equal(CalcVersionState.Superseded, resolution.State);
                Assert.Null(resolution.Entry);
                Assert.Null(catalogue.Find(reference));
                Assert.True(CalcCatalogue.CompareVersions(reference[(reference.LastIndexOf('@') + 1)..], resolution.Current!.Template.Version) < 0, $"{reference} is recorded but newer than the installed {resolution.Current.Reference}.");
            }
        }

        Assert.True(installed > 0);
        Assert.True(superseded > 0, "the manifest records superseded versions (the point of the record)");
        Assert.All(catalogue.Current(), entry => Assert.Equal(CalcVersionState.Installed, catalogue.Resolve(entry.Template.Id).State));
    }

    [Fact]
    public void The_Embedded_Manifest_Is_The_Committed_Manifest()
    {
        var path = TemplateVersionManifestTests.ManifestPath();
        Assert.True(File.Exists(path), path);
        var onDisk = TemplateVersionHistory.Parse(File.ReadAllText(path));
        Assert.Equal(onDisk.Count, TemplateVersionHistory.Shipped.Count);
        Assert.All(onDisk.References, r => Assert.Equal(onDisk.HashOf(r), TemplateVersionHistory.Shipped.HashOf(r)));
    }

    [Theory]
    [InlineData("{\"a.b@1.0.0\": \"short\"}")]
    [InlineData("{\"nover\": \"0000000000000000000000000000000000000000000000000000000000000000\"}")]
    [InlineData("{\"a.b@1.0.0\": \"ZZ00000000000000000000000000000000000000000000000000000000000000\"}")]
    public void A_Manifest_Line_That_Is_Not_An_Id_Version_And_A_Hash_Is_Refused(string json) => Assert.Throws<InvalidDataException>(() => TemplateVersionHistory.Parse(json));

    [Fact]
    public void Resolve_Tells_Installed_Superseded_Unrecorded_Newer_Withheld_And_Gone_Apart()
    {
        var catalogue = new CalcCatalogue([new Scaler("1.0.0", 2), new Scaler("1.2.0", 2)], history: Recorded($"{Id}@1.0.0", $"{Id}@1.1.0", $"{Id}@1.2.0"));

        Assert.Equal(CalcVersionState.Installed, catalogue.Resolve($"{Id}@1.2.0").State);
        Assert.True(catalogue.Resolve($"{Id}@1.2.0").IsCurrent);
        Assert.Equal(CalcVersionState.Installed, catalogue.Resolve($"{Id}@1.0.0").State);
        Assert.False(catalogue.Resolve($"{Id}@1.0.0").IsCurrent);
        Assert.Equal(CalcVersionState.Installed, catalogue.Resolve(Id).State);
        Assert.Equal(CalcVersionState.Superseded, catalogue.Resolve($"{Id}@1.1.0").State);
        Assert.Equal(CalcVersionState.Unrecorded, catalogue.Resolve($"{Id}@0.9.0").State);
        Assert.Equal(CalcVersionState.Newer, catalogue.Resolve($"{Id}@2.0.0").State);
        Assert.Equal(CalcVersionState.Gone, catalogue.Resolve("test.nothing@1.0.0").State);
        Assert.Equal(CalcVersionState.Gone, catalogue.Resolve(" ").State);
        Assert.Equal($"{Id}@1.2.0", catalogue.Resolve($"{Id}@1.1.0").Current!.Reference);

        // A reference that is not installed as itself never finds another version's formulas.
        Assert.Null(catalogue.Find($"{Id}@1.1.0"));
        Assert.Throws<CalcVersionNotInstalledException>(() => catalogue.Get($"{Id}@1.1.0"));
        Assert.Throws<KeyNotFoundException>(() => catalogue.Get("test.nothing@1.0.0"));
    }

    [Fact]
    public void A_Template_Switched_Off_Resolves_As_Withheld()
    {
        var catalogue = new CalcCatalogue(ShippedCalculators.All);
        var lifting = ShippedCalculators.All.First(c => c.Template.FeatureFlag is { Length: > 0 });
        Assert.Equal(CalcVersionState.Withheld, catalogue.Resolve(lifting.Template.Reference).State);
        Assert.Equal(CalcVersionState.Withheld, catalogue.Resolve(lifting.Template.Id).State);
    }

    [Fact]
    public void The_Current_Version_Is_The_Default_For_A_New_Sheet_Whatever_Older_Versions_Are_Installed()
    {
        var catalogue = new CalcCatalogue([new Scaler("1.2.0", 3), new Scaler("1.0.0", 2), new Scaler("1.1.0", 2)], history: Recorded());
        Assert.Equal($"{Id}@1.2.0", catalogue.Find(Id)!.Reference);
        Assert.Equal($"{Id}@1.2.0", Assert.Single(catalogue.Current()).Reference);
        Assert.Equal(3, catalogue.All().Count);
    }

    [Fact]
    public async Task A_New_Sheet_Is_Made_On_The_Current_Version()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2), new Scaler("1.1.0", 3)]);
        var fresh = await host.Service.CreateAsync(Id, await host.ProjectAsync(), "Fresh", Inputs());
        Assert.Equal($"{Id}@1.1.0", fresh.TemplateReference);
        Assert.Contains(fresh.Outputs, o => o.Symbol == "y" && o.Si == 12);

        // A frozen copy is still there by its own reference.
        var frozen = await host.Service.CreateAsync($"{Id}@1.0.0", await host.ProjectAsync(), "Frozen", Inputs());
        Assert.Equal($"{Id}@1.0.0", frozen.TemplateReference);
        Assert.Contains(frozen.Outputs, o => o.Symbol == "y" && o.Si == 8);
    }

    [Fact]
    public async Task An_Issued_Calculation_Re_Runs_On_Its_Own_Version_When_That_Versions_Formulas_Are_Kept_Beside_The_Current_Ones()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var issued = await IssuedOnV1Async(host, await host.ProjectAsync());

        // The next build keeps 1.0.0 (a frozen copy) and has a 1.1.0 whose formula differs.
        var later = LaterBuild(host, new CalcCatalogue([new Scaler("1.0.0", 2), new Scaler("1.1.0", 3)], history: Recorded($"{Id}@1.0.0", $"{Id}@1.1.0")));
        Assert.Null(later.OpenOlderVersion(issued));
        var rerun = later.Rerun(issued);
        Assert.Equal(CalcRerunOutcome.Reproduced, rerun.Outcome);
        Assert.Equal($"{Id}@1.0.0", rerun.RanOn);
        Assert.Contains("its own version", rerun.Message, StringComparison.Ordinal);
        var run = await later.RunAsync(issued);
        Assert.Contains("8", run.Result.Outputs.Single(o => o.Symbol == "y").ValueText, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(issued.Id))!.Status);
    }

    [Fact]
    public async Task An_Issued_Calculation_On_A_Superseded_Version_Re_Runs_Where_The_Installed_Formulas_Reproduce_It_Exactly()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var issued = await IssuedOnV1Async(host, await host.ProjectAsync());
        var before = await host.Store.FindInstanceAsync(issued.Id);

        // 1.1.0 only rewords: the same formula, so the issued numbers are reproduced.
        var later = LaterBuild(host, new CalcCatalogue([new Scaler("1.1.0", 2)], history: Recorded($"{Id}@1.0.0", $"{Id}@1.1.0")));
        Assert.Equal(CalcVersionState.Superseded, later.ResolveVersion(issued).State);

        var rerun = later.Rerun(issued);
        Assert.Equal(CalcRerunOutcome.Reproduced, rerun.Outcome);
        Assert.Equal($"{Id}@1.1.0", rerun.RanOn);
        Assert.Empty(rerun.Differing);
        Assert.Contains("all 1 stored output", rerun.Message, StringComparison.Ordinal);
        Assert.Contains($"{Id}@1.0.0", rerun.Message, StringComparison.Ordinal);
        Assert.Contains("'Issued scaler' rev A", rerun.Message, StringComparison.Ordinal);

        // It opens as stored: the sheet as issued, read-only, with the re-run's verdict and the way forward.
        var opening = later.OpenOlderVersion(issued)!;
        Assert.Equal(CalcOlderVersionKind.StoredRecord, opening.Kind);
        Assert.Equal(issued.Issue!.SheetText, opening.RecordText);
        Assert.Contains(rerun.Message, opening.Message, StringComparison.Ordinal);
        Assert.Contains($"revised to {Id}@1.1.0", opening.Message, StringComparison.Ordinal);
        Assert.Contains("Revise it", opening.Message, StringComparison.Ordinal);

        // Nothing was written.
        AssertUnchanged(before, await host.Store.FindInstanceAsync(issued.Id));
    }

    [Fact]
    public async Task An_Issued_Calculation_The_Installed_Formulas_Do_Not_Reproduce_Opens_Read_Only_With_A_Sentence_Saying_Why()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var issued = await IssuedOnV1Async(host, await host.ProjectAsync());
        var before = await host.Store.FindInstanceAsync(issued.Id);
        var later = LaterBuild(host, new CalcCatalogue([new Scaler("1.1.0", 3)], history: Recorded($"{Id}@1.0.0", $"{Id}@1.1.0")));

        var rerun = later.Rerun(issued);
        Assert.Equal(CalcRerunOutcome.Differs, rerun.Outcome);
        Assert.Equal(["y"], rerun.Differing);
        Assert.Contains("cannot be re-run exactly", rerun.Message, StringComparison.Ordinal);
        Assert.Contains($"{Id}@1.1.0", rerun.Message, StringComparison.Ordinal);
        Assert.Contains("y is ", rerun.Message, StringComparison.Ordinal);
        Assert.Contains("where it was ", rerun.Message, StringComparison.Ordinal);
        Assert.Contains("stays as stored, read-only", rerun.Message, StringComparison.Ordinal);

        var opening = later.OpenOlderVersion(issued)!;
        Assert.Equal(CalcOlderVersionKind.StoredRecord, opening.Kind);
        Assert.Equal(issued.Issue!.SheetText, opening.RecordText);
        Assert.Contains("read-only", opening.Message, StringComparison.Ordinal);

        // The issued record is untouched, and cannot be edited or issued again.
        AssertUnchanged(before, await host.Store.FindInstanceAsync(issued.Id));
        var edit = await later.UpdateInputsAsync(issued.Id, Inputs(9));
        Assert.False(edit.Succeeded);
        AssertUnchanged(before, await host.Store.FindInstanceAsync(issued.Id));
    }

    [Fact]
    public async Task An_Issued_Calculation_On_A_Superseded_Version_Is_Revised_Onto_The_Current_One_With_Its_Inputs_And_Stays_As_Issued()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var issued = await IssuedOnV1Async(host, await host.ProjectAsync());
        var later = LaterBuild(host, new CalcCatalogue([new Scaler("1.1.0", 3)], history: Recorded($"{Id}@1.0.0", $"{Id}@1.1.0")));

        var revised = await later.ReviseAsync(issued.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Equal($"{Id}@1.1.0", revised.Instance!.TemplateReference);
        Assert.Equal("B", revised.Instance.Revision);
        Assert.Equal(CalcInstanceStatus.Draft, revised.Instance.Status);
        Assert.Equal(4, new CalcInputSet(revised.Instance.Inputs).Find("x")!.Number);
        Assert.Contains(revised.Instance.Outputs, o => o.Symbol == "y" && o.Si == 12);

        // The issued record changed only in being marked Superseded; its sheet, inputs and outputs are what they were.
        var old = (await host.Store.FindInstanceAsync(issued.Id))!;
        Assert.Equal(CalcInstanceStatus.Superseded, old.Status);
        AssertUnchanged(issued with { Status = CalcInstanceStatus.Superseded, UpdatedAt = old.UpdatedAt }, old);
        Assert.Equal(issued.Issue, old.Issue);
    }

    [Fact]
    public async Task A_Checked_Calculation_On_A_Superseded_Version_Opens_Read_Only_Cannot_Be_Issued_There_And_Is_Revised_Forward()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var created = await host.Service.CreateAsync(Id, await host.ProjectAsync(), "Checked scaler", Inputs());
        Assert.True((await host.Service.CheckAsync(created.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var checkedOne = (await host.Store.FindInstanceAsync(created.Id))!;
        var later = LaterBuild(host, new CalcCatalogue([new Scaler("1.1.0", 2)], history: Recorded($"{Id}@1.0.0", $"{Id}@1.1.0")));

        var opening = later.OpenOlderVersion(checkedOne)!;
        Assert.Equal(CalcOlderVersionKind.StoredRecord, opening.Kind);
        Assert.Contains("Checked scaler", opening.RecordText, StringComparison.Ordinal);
        Assert.Contains("y — scaled = ", opening.RecordText, StringComparison.Ordinal);
        Assert.Equal(CalcRerunOutcome.Reproduced, opening.Rerun!.Outcome);
        Assert.Contains("then check and issue it again", opening.Message, StringComparison.Ordinal);

        var issue = await later.IssueAsync(created.Id, "ISS-X", "Client Co");
        Assert.False(issue.Succeeded);
        Assert.Contains("has been revised to " + Id + "@1.1.0", issue.Reason, StringComparison.Ordinal);
        Assert.Contains("revise it", issue.Reason, StringComparison.Ordinal);

        // The checked calculation is stale because its template moved on, so it can be revised, which carries it to the current version.
        Assert.Contains("newer version", (await later.StalenessAsync(checkedOne)).Reasons[0], StringComparison.Ordinal);
        var revised = await later.ReviseAsync(created.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Equal($"{Id}@1.1.0", revised.Instance!.TemplateReference);
    }

    [Fact]
    public async Task An_Older_Draft_Is_Offered_As_A_New_Sheet_On_The_Current_Version_With_Its_Inputs_And_Is_Left_Unchanged()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var draft = await host.Service.CreateAsync(Id, await host.ProjectAsync(), "Draft scaler", [.. Inputs(7), CalcInputValue.Of("retired", 5)]);
        var before = await host.Store.FindInstanceAsync(draft.Id);
        var later = LaterBuild(host, new CalcCatalogue([new Scaler("1.1.0", 3)], history: Recorded($"{Id}@1.0.0", $"{Id}@1.1.0")));

        var opening = later.OpenOlderVersion(draft)!;
        Assert.Equal(CalcOlderVersionKind.DraftUpgrade, opening.Kind);
        Assert.Equal($"{Id}@1.1.0", opening.Upgrade!.Current.Reference);
        Assert.Equal(7, opening.Upgrade.Inputs.Find("x")!.Number);
        Assert.Equal(["retired"], opening.Upgrade.Dropped);
        Assert.Contains("left unchanged", opening.Message, StringComparison.Ordinal);
        Assert.Contains("retired", opening.Message, StringComparison.Ordinal);

        // The seed runs on the current version, and the old draft is exactly as it was.
        Assert.Contains(later.Preview(opening.Upgrade.Current.Reference, opening.Upgrade.Inputs).Outputs, o => o.Symbol == "y");
        AssertUnchanged(before, await host.Store.FindInstanceAsync(draft.Id));

        // Where it is, it cannot be edited, and the sentence says what to do.
        var edit = await later.UpdateInputsAsync(draft.Id, Inputs(9));
        Assert.False(edit.Succeeded);
        Assert.Contains($"has been revised to {Id}@1.1.0", edit.Reason, StringComparison.Ordinal);
        Assert.Contains("carry its inputs into a new sheet", edit.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Calculation_Made_By_A_Newer_Build_Is_Shown_As_Stored_And_Is_Never_Offered_An_Upgrade_To_An_Older_Version()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("2.0.0", 2)]);
        var draft = await host.Service.CreateAsync(Id, await host.ProjectAsync(), "From the future", Inputs());
        var older = LaterBuild(host, new CalcCatalogue([new Scaler("1.0.0", 2)], history: Recorded($"{Id}@1.0.0")));

        Assert.Equal(CalcVersionState.Newer, older.ResolveVersion(draft).State);
        Assert.Null(older.PlanDraftUpgrade(draft));
        var opening = older.OpenOlderVersion(draft)!;
        Assert.Equal(CalcOlderVersionKind.StoredRecord, opening.Kind);
        Assert.Contains("newer than the " + Id + "@1.0.0 installed here", opening.Message, StringComparison.Ordinal);
        Assert.Equal(CalcRerunOutcome.Unavailable, older.Rerun(draft with { Status = CalcInstanceStatus.Checked }).Outcome);
        Assert.Contains("newer than", (await older.UpdateInputsAsync(draft.Id, Inputs())).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Calculation_Whose_Template_Is_Gone_Or_Unrecorded_Is_Shown_As_Stored_With_The_Old_Sentence()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var issued = await IssuedOnV1Async(host, await host.ProjectAsync());
        var later = LaterBuild(host, new CalcCatalogue([new Scaler("1.1.0", 2)], history: Recorded()));

        // Unrecorded: made on 1.0.0, which this build's record does not list; the current version is installed, so the way forward is the same.
        Assert.Equal(CalcVersionState.Unrecorded, later.ResolveVersion(issued).State);
        Assert.Equal(CalcRerunOutcome.Reproduced, later.Rerun(issued).Outcome);
        Assert.Equal(CalcOlderVersionKind.StoredRecord, later.OpenOlderVersion(issued)!.Kind);
        Assert.Contains($"has been revised to {Id}@1.1.0", later.OlderVersionRefusal(issued), StringComparison.Ordinal);

        // Gone: the template is not installed at all.
        var gone = issued with { TemplateReference = "test.gone@1.0.0" };
        Assert.Equal(CalcVersionState.Gone, later.ResolveVersion(gone).State);
        var rerun = later.Rerun(gone);
        Assert.Equal(CalcRerunOutcome.Unavailable, rerun.Outcome);
        Assert.Contains("no longer installed", rerun.Message, StringComparison.Ordinal);
        Assert.Equal(issued.Issue!.SheetText, later.OpenOlderVersion(gone)!.RecordText);
        Assert.Equal("Template 'test.gone@1.0.0' is no longer registered.", later.OlderVersionRefusal(gone));
    }

    [Fact]
    public async Task A_Calculation_Whose_Template_Is_Gone_Or_Newer_Than_This_Build_Is_Not_Revised_And_The_Refusal_Says_So()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var issued = await IssuedOnV1Async(host, await host.ProjectAsync());

        // Gone: no version of the template is installed, so there is nothing to revise onto; the refusal is the old sentence, not a thrown lookup.
        var without = LaterBuild(host, new CalcCatalogue([new Scaler("1.1.0", 2, id: "test.other")], history: Recorded()));
        var gone = await without.ReviseAsync(issued.Id);
        Assert.False(gone.Succeeded);
        Assert.Equal($"Template '{Id}@1.0.0' is no longer registered.", gone.Reason);
        Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(issued.Id))!.Status);

        // Newer: this build's version is older than the calculation's; it is never revised down onto it.
        var older = LaterBuild(host, new CalcCatalogue([new Scaler("0.9.0", 2)], history: Recorded($"{Id}@0.9.0")));
        var newer = await older.ReviseAsync(issued.Id);
        Assert.False(newer.Succeeded);
        Assert.Contains($"which is newer than the {Id}@0.9.0 installed here", newer.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(issued.Id))!.Status);
    }

    [Fact]
    public async Task A_Calculation_On_An_Installed_Version_Or_A_Switched_Off_Template_Or_A_Legacy_Archive_Has_Nothing_To_Explain()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var draft = await host.Service.CreateAsync(Id, await host.ProjectAsync(), "Current", Inputs());
        Assert.Null(host.Service.OpenOlderVersion(draft));
        Assert.Null(host.Service.OpenOlderVersion(draft with { TemplateReference = "legacy/calc.something@1" }));
        Assert.Equal(CalcRerunOutcome.Unavailable, host.Service.Rerun(draft with { TemplateReference = "legacy/calc.something@1" }).Outcome);

        var lifting = ShippedCalculators.All.First(c => c.Template.FeatureFlag is { Length: > 0 });
        var withheld = new LibraryTestHost(calculators: [lifting]);
        Assert.Null(withheld.Service.OpenOlderVersion(draft with { TemplateReference = lifting.Template.Id + "@0.0.1" }));
    }

    /// <summary>An issued calculation of <see cref="Lengths"/> 1.0.0 on x = 4 mm (y = 8 mm), and a later build whose 1.1.0 is <paramref name="later"/>.</summary>
    private static async Task<(CalcInstance Issued, CalcInstanceService Later)> IssuedLengthsAsync(Lengths later, double factor = 2)
    {
        var host = new LibraryTestHost(calculators: [new Lengths("1.0.0", factor)]);
        var created = await host.Service.CreateAsync(Id, await host.ProjectAsync(), "Lengths", Lengths4());
        Assert.True((await host.Service.CheckAsync(created.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var issued = await host.Service.IssueAsync(created.Id, "ISS-L1", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);
        var stored = (await host.Store.FindInstanceAsync(created.Id))!;
        Assert.Equal(0.004 * factor, stored.Outputs.Single(o => o.Symbol == "y").Si!.Value, 12);
        return (stored, LaterBuild(host, new CalcCatalogue([later], history: Recorded($"{Id}@1.0.0", $"{Id}@1.1.0"))));
    }

    [Fact]
    public async Task A_Rerun_That_The_Version_Blocks_Does_Not_Reproduce_And_Says_So()
    {
        var (issued, later) = await IssuedLengthsAsync(new Lengths("1.1.0", blocksLarge: true));

        var rerun = later.Rerun(issued);

        Assert.Equal(CalcRerunOutcome.Differs, rerun.Outcome);
        Assert.Equal($"{Id}@1.1.0", rerun.RanOn);
        Assert.Empty(rerun.Differing);
        Assert.Contains($"cannot be re-run on {Id}@1.1.0, which has replaced {Id}@1.0.0: that version blocks it (x: above 3 mm is outside this version)", rerun.Message, StringComparison.Ordinal);
        Assert.Contains("stays as stored, read-only", rerun.Message, StringComparison.Ordinal);
        Assert.Contains($"Revise it to carry its inputs to {Id}@1.1.0", rerun.Message, StringComparison.Ordinal);
        Assert.Equal(1, rerun.OutputsCompared);
        Assert.Equal(CalcOlderVersionKind.StoredRecord, later.OpenOlderVersion(issued)!.Kind);
    }

    [Fact]
    public async Task A_Rerun_Whose_Inputs_The_Version_Refuses_Does_Not_Reproduce_And_Names_The_Refusal()
    {
        var (issued, later) = await IssuedLengthsAsync(new Lengths("1.1.0", refusesLarge: true));

        var rerun = later.Rerun(issued);

        Assert.Equal(CalcRerunOutcome.Differs, rerun.Outcome);
        Assert.Equal($"{Id}@1.1.0", rerun.RanOn);
        Assert.Empty(rerun.Differing);
        Assert.Contains($"cannot be re-run on {Id}@1.1.0: that version refuses its inputs (x above 3 mm is not supported by this version)", rerun.Message, StringComparison.Ordinal);
        Assert.Contains("stays as it was issued", rerun.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Rerun_That_Adds_A_Check_The_Calculation_Fails_Does_Not_Reproduce_But_A_Check_It_Passes_Does()
    {
        var (issued, later) = await IssuedLengthsAsync(new Lengths("1.1.0", failingCheck: true));

        var rerun = later.Rerun(issued);

        Assert.Equal(CalcRerunOutcome.Differs, rerun.Outcome);
        Assert.Equal(["Limit"], rerun.Differing);
        Assert.Contains("it adds the check 'Limit', which this calculation does not meet", rerun.Message, StringComparison.Ordinal);
        Assert.Equal(0, rerun.ChecksCompared);

        // A check the version adds that the calculation passes is not a difference: the stored outputs are what they were.
        var (issuedAgain, passing) = await IssuedLengthsAsync(new Lengths("1.1.0", passingCheck: true));
        var ok = passing.Rerun(issuedAgain);
        Assert.Equal(CalcRerunOutcome.Reproduced, ok.Outcome);
        Assert.Empty(ok.Differing);
    }

    [Fact]
    public async Task A_Changed_Display_Unit_With_The_Same_SI_Value_Reproduces_And_A_Changed_Value_With_The_Same_Text_Does_Not()
    {
        // 1.1.0 shows y in m: "0.008 m" where the record says "8 mm", the same length to the last digit.
        var (issued, inMetres) = await IssuedLengthsAsync(new Lengths("1.1.0", unit: "m"));
        Assert.Equal("8 mm", issued.Outputs.Single(o => o.Symbol == "y").Text);
        var unitOnly = inMetres.Rerun(issued);
        Assert.Equal(CalcRerunOutcome.Reproduced, unitOnly.Outcome);
        Assert.Empty(unitOnly.Differing);
        Assert.Contains("all 1 stored output", unitOnly.Message, StringComparison.Ordinal);

        // 1.1.0 with a factor that moves y by a part in twenty million: still printed "8.400 mm", not the same number.
        var (issuedAgain, nudged) = await IssuedLengthsAsync(new Lengths("1.1.0", factor: 2.1000001), factor: 2.1);
        Assert.Equal("8.400 mm", issuedAgain.Outputs.Single(o => o.Symbol == "y").Text);
        var preview = nudged.Preview(Id, new CalcInputSet(Lengths4()));
        Assert.Equal("8.400 mm", preview.Outputs.Single(o => o.Symbol == "y").ValueText);
        var valueOnly = nudged.Rerun(issuedAgain);
        Assert.Equal(CalcRerunOutcome.Differs, valueOnly.Outcome);
        Assert.Equal(["y"], valueOnly.Differing);
        Assert.Contains("y is 0.00840000", valueOnly.Message, StringComparison.Ordinal);
        Assert.Contains(" where it was 0.0084", valueOnly.Message, StringComparison.Ordinal);
        Assert.Contains(" in SI (both shown as 8.400 mm)", valueOnly.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Record_Whose_Stored_Fingerprint_Is_Not_The_One_Recorded_For_Its_Version_Is_Said_To_Be_From_A_Build_Before_The_Content_Was_Final()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var issued = await IssuedOnV1Async(host, await host.ProjectAsync());
        var recordedHash = host.Catalogue.Find(Id)!.ContentHash;
        var later = LaterBuild(host, new CalcCatalogue([new Scaler("1.1.0", 2)], history: new TemplateVersionHistory([new($"{Id}@1.0.0", recordedHash), new($"{Id}@1.1.0", new string('b', 64))])));

        // The record carries the hash the manifest records for 1.0.0: nothing to add.
        var final = later.OpenOlderVersion(issued)!;
        Assert.DoesNotContain("before its content was final", final.Message, StringComparison.Ordinal);

        // A record made on a build of 1.0.0 whose content was not yet what the manifest records: the sentence names both fingerprints.
        var earlyHash = new string('c', 64);
        var early = later.OpenOlderVersion(issued with { TemplateHash = earlyHash })!;
        Assert.Contains($"Its stored template fingerprint ({CalcTemplateHasher.Short(earlyHash)}) is not the one recorded for {Id}@1.0.0 ({CalcTemplateHasher.Short(recordedHash)}): it was made on a build of that version before its content was final.", early.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Rerun_With_Nothing_Stored_Is_Unavailable_And_A_Stored_Output_The_Version_No_Longer_Gives_Differs()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var issued = await IssuedOnV1Async(host, await host.ProjectAsync());
        var nothing = issued with { Outputs = [], Checks = [] };
        Assert.Equal(CalcRerunOutcome.Unavailable, host.Service.Rerun(nothing).Outcome);
        Assert.Contains("no stored outputs or checks", host.Service.Rerun(nothing).Message, StringComparison.Ordinal);

        // A stored output the version no longer gives is a difference.
        var extraStored = issued with { Outputs = [.. issued.Outputs, new CalcOutputSnapshot("gone_out", "retired output", "5 m", 5, "m")] };
        var rerun = host.Service.Rerun(extraStored);
        Assert.Equal(CalcRerunOutcome.Differs, rerun.Outcome);
        Assert.Equal(["gone_out"], rerun.Differing);
        Assert.Contains("gone_out is no longer given", rerun.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Differing_List_Is_Capped()
    {
        var host = new LibraryTestHost(calculators: [new Scaler("1.0.0", 2)]);
        var issued = await IssuedOnV1Async(host, await host.ProjectAsync());
        var many = issued with { Outputs = [.. issued.Outputs, .. Enumerable.Range(0, 60).Select(i => new CalcOutputSnapshot($"o{i}", "gone", "1", 1, null))] };
        var rerun = host.Service.Rerun(many);
        Assert.Equal(CalcRerunOutcome.Differs, rerun.Outcome);
        Assert.Equal(CalcInstanceService.MaxDifferingListed, rerun.Differing.Count);
        Assert.Contains("and 57 more", rerun.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Long_Stored_Summary_Is_Capped()
    {
        var outputs = Enumerable.Range(0, CalcInstanceService.MaxSummaryRows + 30).Select(i => new CalcOutputSnapshot($"o{i}", "d", "1", 1, null)).ToList();
        var instance = new CalcInstance(Guid.NewGuid(), "test.gone@1.0.0", "h", null, "Many", "A", [], outputs, [], [], [], CalcInstanceStatus.Checked, null, null, "me", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var text = CalcInstanceService.StoredRecordText(instance);
        Assert.Contains($"the first {CalcInstanceService.MaxSummaryRows} are listed", text, StringComparison.Ordinal);
        Assert.DoesNotContain($"o{CalcInstanceService.MaxSummaryRows + 1} ", text, StringComparison.Ordinal);
    }
}
