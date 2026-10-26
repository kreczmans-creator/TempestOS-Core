using Tempest.Calc.Library;
using Tempest.Calc.Library.Dashboard;
// Addendum C, C-3 adds Tempest.Calc.Library.RecordRequirement (the record import's requirement); this file means the dashboard's.
using RecordRequirement = Tempest.Calc.Library.Dashboard.RecordRequirement;
using Tempest.Calc.Sheets;
using Tempest.Calc.Tests.Sheets;

namespace Tempest.Calc.Tests.Library.Dashboard;

/// <summary>Addendum C, C-4: one row per calculator and per chain, assembled read-only from the catalogue, the snapshot, the records store and the manual-test source.</summary>
public sealed class VerificationDashboardServiceTests
{
    private const string StressId = "test.direct-stress";
    private const string RecordedId = "test.recorded";

    /// <summary>A sample of the known-defects document: two tables and the "code unchanged" line.</summary>
    private const string SampleKnownDefects = """
        ### Critical (1)

        | ID | Finding (Track P) | Decision | Fix and test (C-1) |
        |---|---|---|---|
        | test.direct-stress-01 | Wrong stress. | **Fixed (abc12345)** | Fixed. |

        ### High (2)

        | ID | Finding (Track P) | Decision | Fix and test (C-1) |
        |---|---|---|---|
        | test.direct-stress-02 | Default allowable. | **Owner decision (DC-03)** | Needs the clause. |
        | test.direct-stress-R1 | Slender member. | **Fixed (def67890)** | Blocked. |

        ### Medium (86), bulk classification

        | ID | Decision | Evidence |
        |---|---|---|
        | test.direct-stress-03 | **Still open (partly fixed)** | Wording only. |
        | test.recorded-01 | **Fixed** | Done. |

        Still open, code unchanged (2): test.direct-stress-04, test.recorded-02.
        """;

    private static VerificationSnapshot Snapshot(string version = "1.0.0", int kats = 2, int passed = 2, IReadOnlyList<string>? failed = null) =>
        VerificationSnapshot.From(new Dictionary<string, SnapshotItem>
        {
            [StressId] = new(3, 0, 0, new SnapshotKat(version, kats, passed, failed ?? [], 3, []),
                new SnapshotRederivation("match-with-findings", "MATCH WITH FINDINGS (reviewed: one High)",
                [
                    .. KnownDefectsTable.Parse(SampleKnownDefects).Where(r => r.Id.StartsWith(StressId, StringComparison.Ordinal)).Select(r => new SnapshotFinding(r.Id, r.Severity, r.Status)),
                    new SnapshotFinding("test.direct-stress-05", "Low", null),
                    new SnapshotFinding("test.direct-stress-06", "Rejected", null),
                ])),
        });

    private static CalcCatalogue Catalogue() => new([new DirectStressCalculator(), new RecordedCalculator()]);

    private static VerificationDashboardService Service(VerificationSnapshot? snapshot = null, IRequiredRecordsSource? records = null, IManualTestStatusSource? manual = null, IReadOnlyList<CalcPackTemplate>? packs = null, int maxRows = VerificationDashboardService.MaxRows) =>
        new(Catalogue(), packs ?? [], snapshot ?? Snapshot(), records, manual, new FixedTime(), maxRows);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 26, 9, 30, 0, TimeSpan.Zero);
    }

    [Fact]
    public async Task A_Template_With_Findings_Gets_Its_Version_Cases_Provenance_Flags_And_Defects()
    {
        var report = await Service().BuildAsync();
        var row = report.Rows.Single(r => r.Id == StressId);

        Assert.Equal(VerificationRowKind.Calculator, row.Kind);
        Assert.Equal("Structural", row.Discipline);
        Assert.Equal("1.0.0", row.Version);
        Assert.Equal("Direct stress check", row.Title);
        Assert.Equal(2, row.KatCount);
        Assert.Equal(KatRunState.Passed, row.KatState);
        Assert.Equal("2 of 2 pass", row.KatText);
        Assert.Equal(3, row.WorkedExamples);
        Assert.Equal(1, row.Unconfirmed);
        Assert.Equal(0, row.Secondary);
        Assert.Equal("Match, with findings", row.RederivationResult);
        Assert.Equal(new VerificationSeverityCounts(1, 2, 2), row.RederivationFindings);
        Assert.Equal("1 Critical, 2 High, 2 Medium", row.RederivationFindings.Text);
        Assert.Equal("MATCH WITH FINDINGS (reviewed: one High)", row.RederivationVerdict);

        // 'Rejected' is not a finding; Low and Info are listed but never open; Fixed rows are not open.
        Assert.DoesNotContain(row.Findings, f => f.Severity == "Rejected");
        Assert.Contains(row.Findings, f => f.Id == "test.direct-stress-05" && f.StatusLabel == "Not triaged");
        Assert.Equal(["test.direct-stress-02", "test.direct-stress-03", "test.direct-stress-04"], row.OpenDefects.Select(d => d.Id));
        Assert.Equal(3, row.OpenDefectCount);
        Assert.Equal("Open defects", row.Headline);
    }

    [Fact]
    public async Task A_Template_Without_Findings_Or_Results_Reads_As_Not_Re_Derived_And_Not_Run()
    {
        var report = await Service().BuildAsync();
        var row = report.Rows.Single(r => r.Id == RecordedId);

        Assert.Equal("Not re-derived", row.RederivationResult);
        Assert.Equal(VerificationSeverityCounts.None, row.RederivationFindings);
        Assert.Equal("none at Medium or above", row.RederivationFindings.Text);
        Assert.Empty(row.Findings);
        Assert.Equal(0, row.OpenDefectCount);
        Assert.Equal(0, row.WorkedExamples);
        Assert.Equal(KatRunState.NotRun, row.KatState);
        Assert.Equal("not run (no results recorded)", row.KatText);
        Assert.Equal("Not run", row.Headline);
        Assert.False(row.IsClean);
    }

    [Fact]
    public async Task Open_Defects_Are_The_Rows_Of_The_Known_Defects_Table_That_Are_Not_Fixed()
    {
        var table = KnownDefectsTable.Parse(SampleKnownDefects);
        Assert.Equal(7, table.Count);
        Assert.Equal(2, table.Count(r => r.Status == VerificationDefectStatus.Fixed && r.Id.StartsWith(StressId, StringComparison.Ordinal)));

        var report = await Service().BuildAsync();
        var stress = report.Rows.Single(r => r.Id == StressId);
        var expected = table.Where(r => r.Id.StartsWith(StressId, StringComparison.Ordinal) && r.Status != VerificationDefectStatus.Fixed).Select(r => r.Id).Order().ToList();

        Assert.Equal(expected, stress.OpenDefects.Select(d => d.Id).Order());
        Assert.Contains(stress.OpenDefects, d => d.Id == "test.direct-stress-02" && d.StatusLabel == "Owner decision");
        Assert.Contains(stress.OpenDefects, d => d.Id == "test.direct-stress-04" && d.StatusLabel == "Still open");
    }

    [Fact]
    public async Task A_Not_A_Defect_Row_Is_Not_Counted_As_Open()
    {
        var snapshot = VerificationSnapshot.From(new Dictionary<string, SnapshotItem>
        {
            [StressId] = new(0, 0, 0, null, new SnapshotRederivation("match", "MATCH", [new SnapshotFinding("test.direct-stress-01", "Medium", VerificationDefectStatus.NotADefect), new SnapshotFinding("test.direct-stress-02", "High", VerificationDefectStatus.StillOpen)])),
        });

        var row = (await Service(snapshot).BuildAsync()).Rows.Single(r => r.Id == StressId);
        Assert.Equal(["test.direct-stress-02"], row.OpenDefects.Select(d => d.Id));
    }

    [Fact]
    public async Task Results_For_Another_Version_Or_Case_Count_Are_Not_Used()
    {
        var older = (await Service(Snapshot(version: "0.9.0")).BuildAsync()).Rows.Single(r => r.Id == StressId);
        Assert.Equal(KatRunState.NotRun, older.KatState);
        Assert.Equal("not run (results are for version 0.9.0)", older.KatText);

        var fewer = (await Service(Snapshot(kats: 1, passed: 1)).BuildAsync()).Rows.Single(r => r.Id == StressId);
        Assert.Equal(KatRunState.NotRun, fewer.KatState);
        Assert.Contains("results hold 1 cases, the template has 2", fewer.KatText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Failed_Case_Makes_The_Row_Failing()
    {
        var row = (await Service(Snapshot(passed: 1, failed: ["ka-2"])).BuildAsync()).Rows.Single(r => r.Id == StressId);

        Assert.Equal(KatRunState.Failed, row.KatState);
        Assert.Equal(1, row.KatPassed);
        Assert.Equal("1 of 2 pass, 1 fail", row.KatText);
        Assert.True(row.IsFailing);
        Assert.Equal("Failing", row.Headline);
    }

    [Fact]
    public async Task Secondary_Cases_Are_Counted_From_The_Template()
    {
        var host = new LibraryTestHost(calculators: ShippedCalculators.All);
        var service = new VerificationDashboardService(host.Catalogue, [], VerificationSnapshot.From(new Dictionary<string, SnapshotItem>()));
        var report = await service.BuildAsync();
        var expected = host.Catalogue.Current().ToDictionary(e => e.Template.Id, e => (U: e.Template.TestCases.Count(c => c.Status == CalcReferenceStatus.Unconfirmed), S: e.Template.TestCases.Count(c => c.Source.Secondary)));

        Assert.All(report.Rows, r => Assert.Equal(expected[r.Id], (r.Unconfirmed, r.Secondary)));
        Assert.Contains(report.Rows, r => r.Secondary > 0);
        Assert.Contains(report.Rows, r => r.Unconfirmed > 0);
        Assert.True(report.Rows.Where(r => r.HasUnconfirmedOrSecondary).All(r => r.Matches(VerificationStatusFilter.UnconfirmedOrSecondary)));
    }

    [Fact]
    public async Task Rows_Run_Calculators_By_Discipline_And_Title_Then_Chains()
    {
        var packs = new[] { new CalcPackTemplate("pack.test-chain", "Test chain", "A chain.", [new CalcPackStep("a", StressId, "Stress"), new CalcPackStep("b", RecordedId, "Recorded")], []) };
        var report = await Service(packs: packs).BuildAsync();

        Assert.Equal([StressId, RecordedId, "pack.test-chain"], report.Rows.Select(r => r.Id));
        Assert.Equal(["Structural", "Electrical", "Chains"], report.Disciplines);
        Assert.Equal(3, report.TotalRows);
        Assert.False(report.Truncated);
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 9, 30, 0, TimeSpan.Zero), report.GeneratedAt);
    }

    private static CalcPackTemplate TestChain { get; } = new("pack.test-chain", "Test chain", "A chain.", [new CalcPackStep("a", StressId, "Stress"), new CalcPackStep("b", RecordedId, "Recorded"), new CalcPackStep("c", StressId, "Stress again")], []);

    private static readonly SnapshotPackRun PassingRun = new(3, 0, true, []);

    /// <summary>A snapshot in which the two test calculators have results (the stress check as <see cref="Snapshot"/> gives it) and the chain has the given run.</summary>
    private static VerificationSnapshot ChainSnapshot(SnapshotPackRun? run, bool recordedKat = true, bool stressFailed = false, SnapshotRederivation? chainRederivation = null)
    {
        var snapshot = Snapshot(passed: stressFailed ? 1 : 2, failed: stressFailed ? ["ka-2"] : null);
        var items = new Dictionary<string, SnapshotItem>(snapshot.Items)
        {
            ["pack.test-chain"] = new(1, 4, 3, null, chainRederivation ?? new SnapshotRederivation("values-transfer", "VALUES TRANSFER CORRECTLY", [new SnapshotFinding("chain.test-chain-01", "High", VerificationDefectStatus.StillOpen)]), run),
        };

        if (recordedKat)
            items[RecordedId] = new(1, 0, 0, new SnapshotKat("1.0.0", 2, 2, [], 1, []), new SnapshotRederivation("match", "MATCH", []));

        return VerificationSnapshot.From(items);
    }

    private static async Task<VerificationRow> ChainRowAsync(VerificationSnapshot snapshot, CalcPackTemplate? chain = null) =>
        (await Service(snapshot, packs: [chain ?? TestChain]).BuildAsync()).Rows.Single(r => r.Kind == VerificationRowKind.Chain);

    [Fact]
    public async Task A_Chain_Row_Has_No_Version_Lists_Its_Calculators_And_Sums_Their_Flags()
    {
        var row = await ChainRowAsync(ChainSnapshot(PassingRun));

        Assert.Equal("Chains", row.Discipline);
        Assert.Equal(string.Empty, row.Version);
        Assert.Equal([$"{StressId}@1.0.0", $"{RecordedId}@1.0.0"], row.Steps);
        Assert.Equal(1, row.Unconfirmed);
        Assert.Equal(3, row.KatCount);
        Assert.Equal(3, row.KatPassed);
        Assert.Equal(KatRunState.Passed, row.KatState);
        Assert.Equal("worked example and 2 of 2 calculators pass", row.KatText);
        Assert.Equal("Values transfer correctly", row.RederivationResult);
        Assert.Equal(["chain.test-chain-01"], row.OpenDefects.Select(d => d.Id));
    }

    [Fact]
    public async Task A_Chain_With_A_Worked_Example_But_No_Recorded_Run_Is_Not_Run_Not_Passed()
    {
        // Before the chain results were recorded, the example's README alone made a chain read "1 of 1 pass".
        var row = await ChainRowAsync(ChainSnapshot(run: null));

        Assert.Equal(1, row.WorkedExamples);
        Assert.Equal(KatRunState.NotRun, row.KatState);
        Assert.Equal("not run (worked example: no chain results recorded)", row.KatText);
        Assert.False(row.IsClean);
    }

    [Fact]
    public async Task A_Chain_Whose_Own_Run_Failed_Is_Failing_And_Says_What_Failed()
    {
        var snapshot = ChainSnapshot(new SnapshotPackRun(3, 0, false, ["step b is stale against its upstream step"]));
        var report = await Service(snapshot, packs: [TestChain]).BuildAsync();
        var row = report.Rows.Single(r => r.Kind == VerificationRowKind.Chain);

        Assert.Equal(KatRunState.Failed, row.KatState);
        Assert.True(row.IsFailing);
        Assert.Equal("Failing", row.Headline);
        Assert.Equal("fails (worked example: step b is stale against its upstream step)", row.KatText);
        Assert.Equal(["pack.test-chain"], report.Filter("Chains", VerificationStatusFilter.Failing).Select(r => r.Id));
    }

    [Fact]
    public async Task A_Chain_Is_Failing_When_Any_Of_Its_Calculators_Fails_Even_If_Its_Own_Run_Passed()
    {
        var row = await ChainRowAsync(ChainSnapshot(PassingRun, stressFailed: true));

        Assert.Equal(KatRunState.Failed, row.KatState);
        Assert.Equal(2, row.KatPassed);
        Assert.Contains(StressId, row.KatText, StringComparison.Ordinal);
        Assert.Equal("Failing", row.Headline);
    }

    [Fact]
    public async Task A_Chain_Is_Not_Run_When_Any_Of_Its_Calculators_Is_Not_Run_And_Failed_Beats_Not_Run()
    {
        var notRun = await ChainRowAsync(ChainSnapshot(PassingRun, recordedKat: false));
        Assert.Equal(KatRunState.NotRun, notRun.KatState);
        Assert.Contains($"{RecordedId}: no results recorded", notRun.KatText, StringComparison.Ordinal);

        var both = await ChainRowAsync(ChainSnapshot(PassingRun, recordedKat: false, stressFailed: true));
        Assert.Equal(KatRunState.Failed, both.KatState);
    }

    [Fact]
    public async Task A_Run_Of_A_Different_Chain_Is_Not_Used_And_A_Switched_Off_Calculator_Makes_The_Chain_Not_Run()
    {
        var changed = await ChainRowAsync(ChainSnapshot(new SnapshotPackRun(5, 0, true, [])));
        Assert.Equal(KatRunState.NotRun, changed.KatState);
        Assert.Contains("results are for 5 steps and 0 links, the chain has 3 and 0", changed.KatText, StringComparison.Ordinal);

        var off = new CalcPackTemplate("pack.test-chain", "Test chain", "A chain.", [new CalcPackStep("a", StressId, "Stress"), new CalcPackStep("z", "test.switched-off", "Gone")], []);
        var row = await ChainRowAsync(ChainSnapshot(new SnapshotPackRun(2, 0, true, [])), off);
        Assert.Equal(KatRunState.NotRun, row.KatState);
        Assert.Contains("test.switched-off: the calculator is switched off", row.KatText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Chain_Is_Not_Clean_While_Any_Of_Its_Calculators_Has_Open_Defects_And_The_Row_Says_How_Many()
    {
        var row = await ChainRowAsync(ChainSnapshot(PassingRun, chainRederivation: new SnapshotRederivation("match", "MATCH", [])));

        // Everything passes and the chain itself has no open defect, but the stress check it stands on has three.
        Assert.Equal(KatRunState.Passed, row.KatState);
        Assert.Equal(0, row.OpenDefectCount);
        Assert.Equal(3, row.StepOpenDefectCount);
        Assert.Equal(["test.direct-stress-02", "test.direct-stress-03", "test.direct-stress-04"], row.StepOpenDefects.Select(d => d.Id));
        Assert.Equal("0 (step defects: 3)", row.OpenDefectsText);
        Assert.False(row.IsClean);
        Assert.Equal("Open defects", row.Headline);
        Assert.True(row.Matches(VerificationStatusFilter.OpenDefects));
        Assert.False(row.Matches(VerificationStatusFilter.Clean));
    }

    [Fact]
    public async Task A_Chain_Of_Clean_Calculators_With_A_Passing_Run_And_A_Re_Derivation_Is_Clean()
    {
        var recordedOnly = new CalcPackTemplate("pack.test-chain", "Test chain", "A chain.", [new CalcPackStep("b", RecordedId, "Recorded")], []);
        var row = await ChainRowAsync(ChainSnapshot(new SnapshotPackRun(1, 0, true, []), chainRederivation: new SnapshotRederivation("match", "MATCH", [])), recordedOnly);

        Assert.Equal(0, row.StepOpenDefectCount);
        Assert.Equal("0", row.OpenDefectsText);
        Assert.True(row.IsClean);
    }

    [Fact]
    public async Task Missing_Records_Come_From_The_Records_Store_And_Never_Seed_Or_Change_It()
    {
        var host = new LibraryTestHost(calculators: [new RecordedCalculator()]);
        var source = new StoreRequiredRecordsSource(host.Store);
        var service = new VerificationDashboardService(host.Catalogue, [], VerificationSnapshot.From(new Dictionary<string, SnapshotItem>()), source);

        var before = (await service.BuildAsync()).Rows.Single(r => r.Id == RecordedId);
        Assert.Equal(["Test factors"], before.RecordKinds);
        Assert.Equal(["Test factors"], before.MissingRecordKinds);
        Assert.True(before.Matches(VerificationStatusFilter.MissingRecords));
        Assert.Empty(await host.Store.ListRecordsAsync());

        var (saved, refusal) = await host.Service.SaveRecordAsync(new CalcDataRecord(
            Guid.Empty, "test.factors", "Grouping factors", 0, "Test Wiring Rules", "2018+A2:2022", "Table 4C1", "IET (own copy)", "s.kreczman", true,
            CalcDataOrigin.UserEntered, [new CalcDataValue("k_1", 0.8)], DateTimeOffset.UnixEpoch));
        Assert.True(saved is not null, refusal);

        var after = (await service.BuildAsync()).Rows.Single(r => r.Id == RecordedId);
        Assert.Equal(["Test factors"], after.RecordKinds);
        Assert.Empty(after.MissingRecordKinds);
        Assert.Single(await host.Store.ListRecordsAsync());
    }

    [Fact]
    public async Task A_Chain_Is_Missing_The_Records_Any_Of_Its_Calculators_Lack()
    {
        var records = new FakeRecords { [RecordedId] = [new RecordRequirement("test.factors", "Test factors", false), new RecordRequirement("test.other", "Other table", true)] };
        var packs = new[] { new CalcPackTemplate("pack.test-chain", "Test chain", "A chain.", [new CalcPackStep("a", StressId, "Stress"), new CalcPackStep("b", RecordedId, "Recorded")], []) };
        var report = await Service(records: records, packs: packs).BuildAsync();

        Assert.Equal(["Test factors"], report.Rows.Single(r => r.Id == "pack.test-chain").MissingRecordKinds);
        Assert.Equal(["Test factors", "Other table"], report.Rows.Single(r => r.Id == "pack.test-chain").RecordKinds);
        Assert.Empty(report.Rows.Single(r => r.Id == StressId).RecordKinds);
    }

    [Fact]
    public async Task Manual_Test_Status_Is_Not_Tested_Until_A_Source_Says_Otherwise()
    {
        var plain = await Service().BuildAsync();
        Assert.All(plain.Rows, r => Assert.Equal("Not tested", r.ManualTest.Label));
        Assert.All(plain.Rows, r => Assert.True(r.Matches(VerificationStatusFilter.NotManuallyTested)));

        var wired = await Service(manual: new FakeManual()).BuildAsync();
        Assert.Equal("Passed", wired.Rows.Single(r => r.Id == StressId).ManualTest.Label);
        Assert.Equal("Not tested", wired.Rows.Single(r => r.Id == RecordedId).ManualTest.Label);
        Assert.False(wired.Rows.Single(r => r.Id == StressId).Matches(VerificationStatusFilter.NotManuallyTested));
    }

    [Fact]
    public async Task Rows_Are_Capped_And_The_Report_Says_So()
    {
        var report = await Service(maxRows: 1).BuildAsync();
        Assert.Single(report.Rows);
        Assert.Equal(2, report.TotalRows);
        Assert.True(report.Truncated);

        var over = new VerificationDashboardService(Catalogue(), [], Snapshot(), maxRows: 1_000_000);
        Assert.False((await over.BuildAsync()).Truncated);
        Assert.Equal(500, VerificationDashboardService.MaxRows);
    }

    [Fact]
    public async Task An_Unreadable_Snapshot_Still_Gives_The_Catalogue_Columns_And_Says_Why()
    {
        var report = await Service(VerificationSnapshot.Unavailable("The verification data is not part of this build.")).BuildAsync();

        Assert.Equal("The verification data is not part of this build.", report.DataProblem);
        Assert.Equal(2, report.Rows.Count);
        Assert.All(report.Rows, r => Assert.Equal(KatRunState.NotRun, r.KatState));
        Assert.Equal(2, report.Rows.Single(r => r.Id == StressId).KatCount);
    }

    [Fact]
    public async Task A_Switched_Off_Calculator_Is_Not_Listed()
    {
        var gated = new LibraryTestHost(calculators: ShippedCalculators.All);
        var service = new VerificationDashboardService(gated.Catalogue, [], VerificationSnapshot.From(new Dictionary<string, SnapshotItem>()));
        var report = await service.BuildAsync();

        Assert.DoesNotContain(report.Rows, r => r.Id is "mech.lifting-lug" or "struct.lifting");
        Assert.Equal(gated.Catalogue.Current().Count, report.Rows.Count(r => r.Kind == VerificationRowKind.Calculator));
    }

    [Fact]
    public async Task Filter_By_Discipline_And_By_Status()
    {
        var packs = new[] { new CalcPackTemplate("pack.test-chain", "Test chain", "A chain.", [new CalcPackStep("a", StressId, "Stress")], []) };
        var report = await Service(packs: packs, records: new FakeRecords { [RecordedId] = [new RecordRequirement("test.factors", "Test factors", false)] }).BuildAsync();

        Assert.Equal(3, report.Filter(null, VerificationStatusFilter.All).Count);
        Assert.Equal([StressId], report.Filter("Structural", VerificationStatusFilter.All).Select(r => r.Id));
        Assert.Equal([RecordedId], report.Filter("Electrical", VerificationStatusFilter.All).Select(r => r.Id));
        Assert.Equal(["pack.test-chain"], report.Filter("Chains", VerificationStatusFilter.All).Select(r => r.Id));
        Assert.Empty(report.Filter("Mechanical", VerificationStatusFilter.All));

        // The chain stands on the stress check, whose open defects it carries as step defects.
        Assert.Equal([StressId, "pack.test-chain"], report.Filter(null, VerificationStatusFilter.OpenDefects).Select(r => r.Id));
        Assert.Equal([StressId, "pack.test-chain"], report.Filter(null, VerificationStatusFilter.UnconfirmedOrSecondary).Select(r => r.Id));
        Assert.Equal([RecordedId], report.Filter(null, VerificationStatusFilter.MissingRecords).Select(r => r.Id));
        Assert.Equal([RecordedId, "pack.test-chain"], report.Filter(null, VerificationStatusFilter.NotRun).Select(r => r.Id));
        Assert.Equal([RecordedId, "pack.test-chain"], report.Filter(null, VerificationStatusFilter.NotRederived).Select(r => r.Id));
        Assert.Empty(report.Filter(null, VerificationStatusFilter.Failing));
        Assert.Empty(report.Filter(null, VerificationStatusFilter.Clean));

        // Both filters apply together.
        Assert.Empty(report.Filter("Electrical", VerificationStatusFilter.OpenDefects));
        Assert.Equal([StressId], report.Filter("Structural", VerificationStatusFilter.OpenDefects).Select(r => r.Id));
        Assert.Equal([RecordedId], report.Filter("Electrical", VerificationStatusFilter.NotRun).Select(r => r.Id));
    }

    [Fact]
    public async Task A_Row_With_Nothing_To_Flag_Is_Clean()
    {
        var snapshot = VerificationSnapshot.From(new Dictionary<string, SnapshotItem>
        {
            [RecordedId] = new(1, 0, 0, new SnapshotKat("1.0.0", 2, 2, [], 1, []), new SnapshotRederivation("match", "MATCH", [new SnapshotFinding("test.recorded-01", "Medium", VerificationDefectStatus.Fixed)])),
        });
        var row = (await Service(snapshot).BuildAsync()).Rows.Single(r => r.Id == RecordedId);

        Assert.True(row.IsClean);
        Assert.Equal("Clean", row.Headline);
        Assert.Equal("Match", row.RederivationResult);
        Assert.True(row.Matches(VerificationStatusFilter.Clean));
        Assert.False(row.Matches(VerificationStatusFilter.NotRederived));
    }

    [Fact]
    public async Task A_Row_Track_P_Has_Not_Re_Derived_Is_Not_Clean_Whatever_Else_Passes()
    {
        // Known-answer results current and passing, no defect, no flag, no record: but no independent re-derivation exists.
        var snapshot = VerificationSnapshot.From(new Dictionary<string, SnapshotItem>
        {
            [RecordedId] = new(1, 0, 0, new SnapshotKat("1.0.0", 2, 2, [], 1, []), null),
        });
        var report = await Service(snapshot).BuildAsync();
        var row = report.Rows.Single(r => r.Id == RecordedId);

        Assert.Equal(KatRunState.Passed, row.KatState);
        Assert.Equal(0, row.OpenDefectCount);
        Assert.False(row.IsRederived);
        Assert.False(row.IsClean);
        Assert.Equal("Not re-derived", row.Headline);
        Assert.False(row.Matches(VerificationStatusFilter.Clean));
        Assert.True(row.Matches(VerificationStatusFilter.NotRederived));
        Assert.Contains(RecordedId, report.Filter(null, VerificationStatusFilter.NotRederived).Select(r => r.Id));
        Assert.DoesNotContain(RecordedId, report.Filter(null, VerificationStatusFilter.Clean).Select(r => r.Id));
    }

    [Fact]
    public async Task When_The_Document_Data_Is_Unavailable_Open_Defects_Are_Unknown_Not_Zero()
    {
        var report = await Service(VerificationSnapshot.Unavailable("The verification data is not part of this build."), packs: [TestChain]).BuildAsync();

        Assert.All(report.Rows, r =>
        {
            Assert.False(r.DefectsKnown);
            Assert.Equal("unknown", r.OpenDefectsText);
            Assert.Equal("Defects unknown", r.Headline);
            Assert.False(r.IsClean);
        });

        var withData = await Service().BuildAsync();
        Assert.All(withData.Rows, r => Assert.True(r.DefectsKnown));
        Assert.Equal("3", withData.Rows.Single(r => r.Id == StressId).OpenDefectsText);
        Assert.Equal("0", withData.Rows.Single(r => r.Id == RecordedId).OpenDefectsText);
    }

    [Fact]
    public async Task Only_A_Record_Usable_For_Real_Work_Counts_As_Entered()
    {
        var host = new LibraryTestHost(calculators: [new RecordedCalculator()]);
        var service = new VerificationDashboardService(host.Catalogue, [], VerificationSnapshot.From(new Dictionary<string, SnapshotItem>()), new StoreRequiredRecordsSource(host.Store));

        async Task<VerificationRow> RowAsync() => (await service.BuildAsync()).Rows.Single(r => r.Id == RecordedId);

        async Task SaveAsync(string title, CalcDataOrigin origin)
        {
            var (saved, refusal) = await host.Service.SaveRecordAsync(new CalcDataRecord(
                Guid.Empty, "test.factors", title, 0, "Test source", "2018", "Table 1", "Owner", "s.kreczman", false, origin, [new CalcDataValue("k_1", 0.8)], DateTimeOffset.UnixEpoch));
            Assert.True(saved is not null, refusal);
        }

        // An indicative starting value and a synthetic test table are records, but not ones to work from.
        await SaveAsync("Generic starting values", CalcDataOrigin.Indicative);
        await SaveAsync("SYNTHETIC grouping factors (test values, not a regulation table)", CalcDataOrigin.UserEntered);
        Assert.Equal(2, (await host.Store.ListRecordsAsync()).Count);
        Assert.Equal(["Test factors"], (await RowAsync()).MissingRecordKinds);

        // A record the owner entered counts, whether or not it is licensed.
        await SaveAsync("Grouping factors", CalcDataOrigin.UserEntered);
        Assert.Empty((await RowAsync()).MissingRecordKinds);
    }

    [Fact]
    public async Task Cancelling_Stops_The_Build()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().BuildAsync(cts.Token));
    }

    private sealed class FakeRecords : Dictionary<string, IReadOnlyList<RecordRequirement>>, IRequiredRecordsSource
    {
        public Task<IReadOnlyDictionary<string, IReadOnlyList<RecordRequirement>>> RequirementsAsync(IReadOnlyList<CalcTemplate> templates, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<RecordRequirement>>>(this);
    }

    private sealed class FakeManual : IManualTestStatusSource
    {
        public Task<ManualTestStatus> StatusAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == StressId ? new ManualTestStatus(ManualTestOutcome.Passed, DateTimeOffset.UnixEpoch, "Checked against the book.") : ManualTestStatus.NotTested);
    }
}
