using Tempest.Calc.Library;
using Tempest.Calc.Library.Dashboard;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Guided = Tempest.Calc.Library.ManualTesting;

namespace Tempest.Calc.Tests.Library.Dashboard;

/// <summary>
/// Addendum C integration: the dashboard (C-4) reads its manual-test column from the guided test's run records (C-5) and its records column from
/// C-3's required kinds per calculator, keeping C-4's rule for what counts as entered.
/// </summary>
public sealed class DashboardIntegrationSourcesTests
{
    private const string SpringId = "mech.spring";
    private const string RecordedId = "test.recorded";

    private static VerificationDashboardService Service(LibraryTestHost host, IDashboardManualTestSource? manual = null) =>
        new(host.Catalogue, [], VerificationSnapshot.From(new Dictionary<string, SnapshotItem>()), new RecordRequirementsRecordsSource(host.Store), manual);

    private static Guided.ManualTestStatus Guide(string id, Guided.ManualTestOutcome outcome, int recorded, int inScript, string build = "0.26.0 (abc1234)", string commit = "abc1234") =>
        new(id, outcome, new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero), commit, "S. Kreczman", Guid.NewGuid(), recorded, inScript, build);

    // ------------------------------------------------------------------
    // Manual test status from the guided test (C-5)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(Guided.ManualTestOutcome.Pass, 2, 2, DashboardManualTestOutcome.Passed, "Passed")]
    [InlineData(Guided.ManualTestOutcome.Pass, 1, 2, DashboardManualTestOutcome.Partial, "Partly passed")]
    [InlineData(Guided.ManualTestOutcome.Blocked, 2, 2, DashboardManualTestOutcome.Partial, "Partly passed")]
    [InlineData(Guided.ManualTestOutcome.Fail, 2, 2, DashboardManualTestOutcome.Failed, "Failed")]
    [InlineData(Guided.ManualTestOutcome.Fail, 1, 3, DashboardManualTestOutcome.Failed, "Failed")]
    [InlineData(Guided.ManualTestOutcome.Pass, 0, 0, DashboardManualTestOutcome.Partial, "Partly passed")]
    public void A_Guided_Result_Maps_To_The_Dashboard_Status(Guided.ManualTestOutcome guided, int recorded, int inScript, DashboardManualTestOutcome expected, string label)
    {
        var status = GuidedManualTestStatusSource.Map(Guide(SpringId, guided, recorded, inScript));

        Assert.Equal(expected, status.Outcome);
        Assert.Equal(label, status.Label);
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero), status.At);
        Assert.Contains($"{recorded} of {inScript} worked examples recorded", status.Note, StringComparison.Ordinal);
        Assert.Contains("abc1234", status.Note, StringComparison.Ordinal);
        Assert.Equal(guided == Guided.ManualTestOutcome.Blocked, status.Note!.Contains("could not be tried", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_Dashboard_Shows_The_Guided_Status_Reads_The_Runs_Once_And_Says_Not_Tested_For_The_Rest()
    {
        var host = new LibraryTestHost(calculators: [new SpringCalculator(), new RecordedCalculator()]);
        var runs = new FakeRuns { [SpringId] = Guide(SpringId, Guided.ManualTestOutcome.Pass, 3, 3) };
        var chain = new CalcPackTemplate("pack.test-chain", "Test chain", "A chain.", [new CalcPackStep("a", SpringId, "Spring")], []);
        var service = new VerificationDashboardService(
            host.Catalogue, [chain], VerificationSnapshot.From(new Dictionary<string, SnapshotItem>()), new RecordRequirementsRecordsSource(host.Store), new GuidedManualTestStatusSource(runs));

        var report = await service.BuildAsync();

        Assert.Equal("Passed", report.Rows.Single(r => r.Id == SpringId).ManualTest.Label);
        Assert.False(report.Rows.Single(r => r.Id == SpringId).Matches(VerificationStatusFilter.NotManuallyTested));
        Assert.Equal("Not tested", report.Rows.Single(r => r.Id == RecordedId).ManualTest.Label);
        Assert.Equal("Not tested", report.Rows.Single(r => r.Id == "pack.test-chain").ManualTest.Label);
        Assert.Equal(1, runs.Reads);
    }

    [Fact]
    public async Task One_Row_Asked_Alone_Reads_The_Same_Status()
    {
        var runs = new FakeRuns { [SpringId] = Guide(SpringId, Guided.ManualTestOutcome.Fail, 1, 3) };
        var source = new GuidedManualTestStatusSource(runs);

        Assert.Equal(DashboardManualTestOutcome.Failed, (await source.StatusAsync(SpringId)).Outcome);
        Assert.Same(DashboardManualTestStatus.NotTested, await source.StatusAsync(RecordedId));
        Assert.Throws<ArgumentNullException>(() => new GuidedManualTestStatusSource(null!));
    }

    // ------------------------------------------------------------------
    // SEC-C-06: the manual-test column names the build
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(Guided.ManualTestOutcome.Pass, 2, 2, "Passed — 0.26.0 (abc1234)")]
    [InlineData(Guided.ManualTestOutcome.Pass, 1, 2, "Partly passed — 0.26.0 (abc1234)")]
    [InlineData(Guided.ManualTestOutcome.Fail, 1, 2, "Failed — 0.26.0 (abc1234)")]
    public void A_Result_Reads_With_The_Build_It_Was_Recorded_On(Guided.ManualTestOutcome guided, int recorded, int inScript, string expected)
    {
        var status = GuidedManualTestStatusSource.Map(Guide(SpringId, guided, recorded, inScript), new RunningBuild("0.26.0 (abc1234)", "abc1234"));

        Assert.Equal(expected, status.Display);
        Assert.False(status.RecordedOnOtherBuild);
        Assert.Equal("0.26.0 (abc1234)", status.Build);
        Assert.Equal("abc1234", status.BuildCommit);
        Assert.Contains("on build 0.26.0 (abc1234).", status.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Result_Recorded_On_Another_Commit_Is_Flagged_With_Both_Builds()
    {
        var status = GuidedManualTestStatusSource.Map(Guide(SpringId, Guided.ManualTestOutcome.Pass, 2, 2), new RunningBuild("0.26.0 (def5678)", "def5678"));

        Assert.True(status.RecordedOnOtherBuild);
        Assert.Equal(DashboardManualTestOutcome.Passed, status.Outcome);
        Assert.Equal("Passed", status.Label);
        Assert.Equal("Passed — 0.26.0 (abc1234) — recorded on a different build", status.Display);
        Assert.Contains("not the build running now (0.26.0 (def5678))", status.Note, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.26.0 (abc1234)", "ABC1234", false)]
    [InlineData("0.26.1 (abc1234)", "abc1234", false)]
    [InlineData("0.26.0 (abc1234)", "abc1235", true)]
    [InlineData("0.26.0", "", true)]
    [InlineData("", "", false)]
    public void The_Running_Build_Is_Compared_By_Commit_When_Both_Name_One(string runningBuild, string runningCommit, bool differs) =>
        Assert.Equal(differs, new RunningBuild(runningBuild, runningCommit).Differs(Guide(SpringId, Guided.ManualTestOutcome.Pass, 1, 1)));

    [Fact]
    public void Without_A_Commit_On_Either_Side_The_Build_Text_Decides_And_A_Build_That_Names_Nothing_Flags_Nothing()
    {
        var recorded = Guide(SpringId, Guided.ManualTestOutcome.Pass, 1, 1, build: "0.26.0", commit: "unknown");

        Assert.False(new RunningBuild("0.26.0", string.Empty).Differs(recorded));
        Assert.True(new RunningBuild("0.26.1", string.Empty).Differs(recorded));
        Assert.False(new RunningBuild(string.Empty, string.Empty).Differs(recorded));
        Assert.Equal("Passed — 0.26.0", GuidedManualTestStatusSource.Map(recorded).Display);
    }

    [Fact]
    public void A_Result_That_Names_No_Build_Says_So_And_A_Row_Nobody_Tested_Names_None()
    {
        var none = GuidedManualTestStatusSource.Map(Guide(SpringId, Guided.ManualTestOutcome.Pass, 1, 1, build: string.Empty, commit: string.Empty));

        Assert.Equal("Passed — unknown build", none.Display);
        Assert.Equal("Not tested", DashboardManualTestStatus.NotTested.Display);
        Assert.Equal(string.Empty, DashboardManualTestStatus.NotTested.BuildText);
        Assert.Equal("Passed — abc1234", new DashboardManualTestStatus(DashboardManualTestOutcome.Passed, BuildCommit: "abc1234").Display);
    }

    [Fact]
    public async Task The_Dashboard_And_Its_Markdown_Show_The_Build_And_The_Flag_Read_From_The_Running_Build()
    {
        var host = new LibraryTestHost(calculators: [new SpringCalculator(), new RecordedCalculator()]);
        var runs = new FakeRuns
        {
            [SpringId] = Guide(SpringId, Guided.ManualTestOutcome.Pass, 3, 3),
            [RecordedId] = Guide(RecordedId, Guided.ManualTestOutcome.Pass, 1, 1, build: "0.26.0 (0000000)", commit: "0000000"),
        };
        var running = new RunningBuild("0.26.0 (abc1234)", "abc1234");
        var service = Service(host, new GuidedManualTestStatusSource(runs, () => running));

        var report = await service.BuildAsync();

        Assert.Equal("Passed — 0.26.0 (abc1234)", report.Rows.Single(r => r.Id == SpringId).ManualTest.Display);
        Assert.Equal("Passed — 0.26.0 (0000000) — recorded on a different build", report.Rows.Single(r => r.Id == RecordedId).ManualTest.Display);
        Assert.Equal(1, runs.Reads);

        var text = VerificationMarkdown.Write(report, report.Rows, string.Empty, "0.26.0");
        Assert.Contains("| Passed — 0.26.0 \\(abc1234\\) |", text, StringComparison.Ordinal);
        Assert.Contains("| Passed — 0.26.0 \\(0000000\\) — recorded on a different build |", text, StringComparison.Ordinal);

        // The same results asked for one row at a time carry the same flag, and the build running is read each time it is asked.
        var source = new GuidedManualTestStatusSource(runs, () => running);
        Assert.True((await source.StatusAsync(RecordedId)).RecordedOnOtherBuild);
        running = new RunningBuild("0.26.0 (0000000)", "0000000");
        Assert.False((await source.StatusAsync(RecordedId)).RecordedOnOtherBuild);
        Assert.True((await source.StatusAsync(SpringId)).RecordedOnOtherBuild);
    }

    // ------------------------------------------------------------------
    // Records not entered from C-3's required kinds
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_Calculator_C3_States_Lists_Only_Its_Required_Kinds_And_Any_Other_Lists_Every_Kind()
    {
        var host = new LibraryTestHost(calculators: [new SpringCalculator(), new RecordedCalculator()]);
        var report = await Service(host).BuildAsync();

        Assert.True(RecordRequirementsRecordsSource.IsStated(SpringId));
        Assert.False(RecordRequirementsRecordsSource.IsStated(RecordedId));

        // mech.spring declares five kinds (one a material); C-3 marks the wire strength and the endurance strength Required.
        var spring = report.Rows.Single(r => r.Id == SpringId);
        Assert.Equal(["Wire tensile strength R_m(d)", "Endurance strength"], spring.RecordKinds);
        Assert.Equal(["Wire tensile strength R_m(d)", "Endurance strength"], spring.MissingRecordKinds);

        // A calculator C-3 does not name keeps C-4's reading: every kind it declares.
        Assert.Equal(["Test factors"], report.Rows.Single(r => r.Id == RecordedId).MissingRecordKinds);

        // The dashboard reads; it neither seeds the material library nor writes anything else.
        Assert.Empty(await host.Store.ListRecordsAsync());
    }

    [Fact]
    public async Task A_Required_Kind_Is_Entered_Only_By_A_Usable_Record_That_Holds_A_Value_The_Calculator_Takes()
    {
        var host = new LibraryTestHost(calculators: [new SpringCalculator()]);
        var service = Service(host);

        async Task<IReadOnlyList<string>> MissingAsync() => (await service.BuildAsync()).Rows.Single(r => r.Id == SpringId).MissingRecordKinds;

        async Task SaveAsync(string title, CalcDataOrigin origin, string symbol)
        {
            var (saved, refusal) = await host.Service.SaveRecordAsync(new CalcDataRecord(
                Guid.Empty, "spring.wire-strength", title, 0, "Wire standard", "2011", "Table 4", "Owner", "s.kreczman", false, origin,
                [new CalcDataValue(symbol, 1700)], DateTimeOffset.UnixEpoch));
            Assert.True(saved is not null, refusal);
        }

        // C-4's rule: an indicative value and a synthetic test table are records, but not ones to work from.
        await SaveAsync("Generic wire strength", CalcDataOrigin.Indicative, "Rm");
        await SaveAsync("SYNTHETIC wire strengths (test values, not a standard's table)", CalcDataOrigin.UserEntered, "Rm");

        // C-3's rule: a record of the kind that holds none of the calculator's inputs does not fill it.
        await SaveAsync("Wire strengths, other column", CalcDataOrigin.UserEntered, "Rm_other");
        Assert.Equal(["Wire tensile strength R_m(d)", "Endurance strength"], await MissingAsync());

        await SaveAsync("Wire strengths", CalcDataOrigin.UserEntered, "Rm");
        Assert.Equal(["Endurance strength"], await MissingAsync());
        Assert.Equal(4, (await host.Store.ListRecordsAsync()).Count);
    }

    [Fact]
    public async Task The_Required_Kinds_Agree_With_C3s_Missing_Required_List()
    {
        var host = new LibraryTestHost(calculators: [new SpringCalculator()]);
        var dashboard = (await Service(host).BuildAsync()).Rows.Single(r => r.Id == SpringId).MissingRecordKinds;
        var c3 = await host.Service.RecordImport.MissingRequiredAsync(SpringId);

        Assert.Equal(c3.Where(r => !MaterialLibrary.IsMaterial(r.Kind)).Select(r => r.Title), dashboard);
    }

    private sealed class FakeRuns : Dictionary<string, Guided.ManualTestStatus>, Guided.IManualTestStatusSource
    {
        public int Reads { get; private set; }

        public Task<IReadOnlyDictionary<string, Guided.ManualTestStatus>> LatestByTemplateAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult<IReadOnlyDictionary<string, Guided.ManualTestStatus>>(new Dictionary<string, Guided.ManualTestStatus>(this, StringComparer.Ordinal));
        }
    }
}
