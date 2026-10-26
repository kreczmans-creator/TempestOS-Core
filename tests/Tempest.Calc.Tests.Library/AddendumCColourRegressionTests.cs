using System.Reflection;
using Tempest.Calc.Library;
using Tempest.Calc.Library.Dashboard;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Calc.Tests.Library.ManualTesting;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;
using static Tempest.Calc.Tests.Library.RecordImport.RecordImportTestSupport;
using Guided = Tempest.Calc.Library.ManualTesting;

namespace Tempest.Calc.Tests.Library;

/// <summary>The colour-team findings on Addendum C (record import, dashboard, guided test, demo) fixed with a regression test (Black B-C-01, Orange O-C-01, Purple P-C-01).</summary>
public sealed class AddendumCColourRegressionTests
{
    private const string Ampacity = "CSA[mm2],AWG[-],I_t[A]";

    // ------------------------------------------------------------------ Black B-C-01

    private static string RunJson(Guid id, string version, string results) =>
        "{\"Schema\":1,\"Id\":\"" + id + "\",\"ScriptVersion\":\"" + version + "\",\"StartedUtc\":\"2026-10-26T09:00:00+00:00\",\"FinishedUtc\":\"2026-10-26T10:00:00+00:00\"," +
        "\"Build\":\"0.26.0\",\"BuildCommit\":\"1a2b3c4\",\"Tester\":\"Steven Kreczman\",\"TesterId\":\"s.kreczman\",\"Results\":" + results + "}";

    [Fact]
    public async Task Black_B_C_01_A_Damaged_Result_In_A_Run_Is_Left_Out_And_Stops_No_List_Sheet_Or_Dashboard()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var script = GuidedTestHelpers.SmallScript();
        var id = Guid.NewGuid();
        const string Good = "{\"StepId\":\"example:t.a/e0\",\"Outcome\":\"Fail\",\"Note\":null,\"ValuesShown\":null,\"RecordedUtc\":\"2026-10-26T09:30:00+00:00\"}";
        const string NoStep = "{\"StepId\":null,\"Outcome\":\"Pass\",\"Note\":\"x\",\"ValuesShown\":\"y\",\"RecordedUtc\":\"2026-10-26T09:31:00+00:00\"}";
        const string NoSuchOutcome = "{\"StepId\":\"example:t.b/e2\",\"Outcome\":7,\"Note\":\"x\",\"ValuesShown\":\"y\",\"RecordedUtc\":\"2026-10-26T09:32:00+00:00\"}";
        await store.ExecuteInTransactionAsync((tx, t) => tx.WriteAsync(ManualTestRunService.Collection, id.ToString("N"), RunJson(id, script.ScriptVersion, $"[null,{Good},{NoStep},{NoSuchOutcome}]"), t));
        var service = new ManualTestRunService(store, script, () => "0.26.0", () => "1a2b3c4", () => ("s.kreczman", "Steven Kreczman"));

        // Before the fix each of these threw a NullReferenceException on the null result, and the dashboard showed nothing at all.
        var run = (await service.FindAsync(id))!;
        var latest = await service.LatestByTemplateAsync();
        var plan = await service.PlanTrimAsync();
        var tally = ManualTestTally.Of(run, script);
        var sheet = service.ExportMarkdown(run);
        var host = Host();
        var report = await new VerificationDashboardService(host.Catalogue, [], manual: new GuidedManualTestStatusSource(service)).BuildAsync();

        var kept = Assert.Single(run.Results);
        Assert.Equal(("example:t.a/e0", Guided.ManualTestOutcome.Fail, string.Empty, string.Empty), (kept.StepId, kept.Outcome, kept.Note, kept.ValuesShown));
        Assert.Equal(Guided.ManualTestOutcome.Fail, latest["t.a"].Outcome);

        // An outcome no build writes is not counted, and above all not as a Pass.
        Assert.False(latest.ContainsKey("t.b"));
        Assert.Equal((0, 1), (tally.Pass, tally.Fail));
        Assert.Empty(plan.Delete);
        Assert.Contains("| Fail |", sheet, StringComparison.Ordinal);
        Assert.NotEmpty(report.Rows);
    }

    // ------------------------------------------------------------------ Purple P-C-01

    [Fact]
    public async Task Purple_P_C_01_A_Record_File_At_The_Row_And_Column_Caps_Is_Read_And_One_Over_Either_Is_Refused_Whole()
    {
        var host = Host();
        var import = host.Service.RecordImport;
        var rows = Enumerable.Range(1, RecordImportService.MaxFileRows - 1).Select(i => $"Grout {i},0.2").ToArray();

        var atCap = await import.PreviewAsync(Request("51", Csv("grout[text],C_fd[-]", rows)));
        var overRows = await import.PreviewAsync(Request("51", Csv("grout[text],C_fd[-]", [.. rows, "Grout extra,0.2"])));
        var overColumns = await import.PreviewAsync(Request("51", Csv(string.Join(',', ["grout[text]", "C_fd[-]", .. Enumerable.Range(1, 63).Select(i => $"x{i}[-]")]), "Grout,0.2" + new string(',', 63))));

        Assert.True(atCap.CanCommit, Describe(atCap));
        Assert.Equal(RecordImportService.MaxFileRows - 1, atCap.Planned.Count);
        Assert.False(overRows.CanCommit);
        Assert.Empty(overRows.Planned);
        // Y-C-04: the person counts data rows, not the header.
        Assert.Equal("The file has more than 1,000 data rows; split it into files of at most 1,000 rows, each with the header.", overRows.Refusal);
        Assert.False(overColumns.CanCommit);
        Assert.NotNull(overColumns.Refusal);
        Assert.Empty(await host.Store.ListRecordsAsync("joint.friction"));
    }

    // ------------------------------------------------------------------ Orange O-C-01

    [Fact]
    public async Task Orange_O_C_01_The_Records_Screen_List_Reads_The_Records_Once_Not_Once_Per_Kind_Or_Per_Record()
    {
        var host = Host();
        var import = host.Service.RecordImport;
        Assert.True((await import.CommitAsync(Request("04", Csv(Ampacity, "1.5,,15"), Provenance("Licensed")))).Committed);
        Assert.True((await import.CommitAsync(Request("04", Csv(Ampacity, "1.5,,15"), Provenance("Open", licensed: false)))).Committed);

        // A record licensed in revision 1 and not in revision 2 is licensed: no revision releases it (SEC-B-02). The list must keep that rule.
        var once = (await import.CommitAsync(Request("04", Csv(Ampacity, "2.5,,21"), Provenance("Was licensed")))).Records.Single();
        await host.Store.SaveRecordAsync(once with { Revision = 2, LicensedSource = false, Values = [.. once.Values.Select(v => v with { Source = null })] });
        Assert.False((await host.Store.FindRecordAsync(once.Id))!.IsLicensed());

        var proxy = DispatchProxy.Create<IQueryablePersistenceStore, CountingProxy>();
        var counting = (CountingProxy)(object)proxy;
        counting.Target = host.Backing;
        var service = new CalcInstanceService(host.Catalogue, new CalcInstanceStore(proxy), host.Evidence, host.Context);
        await service.Materials.EnsureSeededAsync();
        counting.Counts.Clear();

        var types = await service.RecordImport.ListTypesAsync();

        var ampacity = types.Single(t => t.Kind == "cable.ampacity");
        Assert.Equal((3, 2), (ampacity.Count, ampacity.Licensed));
        Assert.True(types.Single(t => t.Kind == "material.properties").Count > 0);

        // One read of the record collection for the whole list (before the fix: one per kind, 68 here, and two more per unlicensed record).
        Assert.True(counting.Counts.GetValueOrDefault("ReadAllAsync") <= 1, $"{counting.Counts.GetValueOrDefault("ReadAllAsync")} reads of a whole collection");
        Assert.Equal(0, counting.Counts.GetValueOrDefault("ListKeysAsync"));

        // O-C-02: the worksheet's missing-records panel asks for one calculator's kinds each time a form opens; it reads the records once too.
        counting.Counts.Clear();
        var spring = await service.RecordImport.RequirementsAsync("mech.spring");
        var conductor = await service.RecordImport.RequirementsAsync("elec.conductor-sizing");

        Assert.True(spring.Count > 3, $"{spring.Count} kinds");
        Assert.True(conductor.Single(r => r.Kind == "cable.ampacity").Present);
        Assert.Equal(3, conductor.Single(r => r.Kind == "cable.ampacity").Count);
        Assert.True(counting.Counts.GetValueOrDefault("ReadAllAsync") <= 2, $"{counting.Counts.GetValueOrDefault("ReadAllAsync")} reads of a whole collection for two calculators");
    }

    // ------------------------------------------------------------------ Black B-C-02, B-C-03, B-C-04

    [Fact]
    public async Task Black_B_C_03_A_Removed_Demo_Project_Is_Not_Listed_For_A_Person_But_Still_Holds_Its_Number()
    {
        var host = DemoHost.New();
        var demo = DemoHost.Service(host);
        var real = await host.ProjectAsync();
        var directory = new Tempest.Workspace.Projects.ProjectDirectory(host.Context);
        Assert.True((await demo.InstallAsync()).Succeeded);
        Assert.Contains(await directory.ListAsync(), p => p.Identifier == CalcDemoProject.Identifier);

        Assert.True((await demo.RemoveAsync()).Succeeded);

        // Before the fix the soft-deleted demo stayed in the Projects browser and the Reports picker, and a re-install listed it twice.
        var listed = await directory.ListAsync();
        Assert.DoesNotContain(listed, p => p.Identifier == CalcDemoProject.Identifier);
        Assert.Contains(listed, p => p.Id == real);
        Assert.Contains(await directory.ListIncludingDeletedAsync(), p => p.Identifier == CalcDemoProject.Identifier);
        await Assert.ThrowsAsync<Tempest.Workspace.Projects.DuplicateProjectIdentifierException>(() => directory.CreateAsync(CalcDemoProject.Identifier, "Someone else's project"));

        Assert.True((await demo.InstallAsync()).Succeeded);
        Assert.Single(await directory.ListAsync(), p => p.Identifier == CalcDemoProject.Identifier);
        Assert.Equal(2, (await directory.ListIncludingDeletedAsync()).Count(p => p.Identifier == CalcDemoProject.Identifier));
    }

    [Fact]
    public async Task Black_B_C_02_Remove_Keeps_A_Demo_Scan_Profile_A_Calculation_In_Another_Project_Uses_And_Says_So()
    {
        var host = DemoHost.New();
        var demo = DemoHost.Service(host);
        Assert.True((await demo.InstallAsync()).Succeeded);
        var real = await host.ProjectAsync();
        var stack = await host.Service.CreateAsync("mech.tolerance-stack", real, "Real groove stack", CalcDemoProject.SealStackInputs());
        var applied = await host.Service.ApplyScanImportAsync(stack.Id, new ScanImportRequest([CalcDemoProject.ScanFile()], CalcDemoProject.StackProfile, CalcDemoProject.ScanSettings));
        Assert.True(applied.Action.Succeeded, applied.Action.Reason);

        var removed = await demo.RemoveAsync();

        Assert.True(removed.Succeeded, removed.Message);
        Assert.Contains($"The scan profile '{CalcDemoProject.StackProfileId}' version 1 was kept, because a calculation in another project uses it.", removed.Message, StringComparison.Ordinal);
        Assert.NotNull(await host.Service.FindScanProfileAsync(CalcDemoProject.StackProfileId, 1));
        Assert.Null(await host.Service.FindScanProfileAsync(CalcDemoProject.GlandProfileId, 1));
        Assert.Single((await host.Store.FindInstanceAsync(stack.Id))!.ScanImports!);

        // The kept profile is the install's own, so the demo installs and removes again; the profile stays while the real calculation cites it.
        Assert.True((await demo.InstallAsync()).Succeeded);
        Assert.Contains("was kept", (await demo.RemoveAsync()).Message, StringComparison.Ordinal);
        Assert.NotNull(await host.Service.FindScanProfileAsync(CalcDemoProject.StackProfileId, 1));
    }

    /// <summary>
    /// The two removal rules together (security SEC-C-03 and black-team B-C-02, merged in the v0.26.0 integration): a demo profile version is deleted only
    /// when it is stored word for word as the install writes it <b>and</b> no calculation of another project cites it. Only a shipped version spared by a
    /// citation is reported as kept; a person's own version under the demonstration's id is never the demonstration's, whether or not it is cited.
    /// </summary>
    [Fact]
    public async Task Integration_SEC_C_03_And_B_C_02_Remove_Deletes_Only_A_Shipped_Demo_Profile_No_Other_Project_Cites()
    {
        // The stack profile: shipped and cited elsewhere, so kept and said. The gland profile: the person's own, saved before the install, not cited,
        // so kept and not claimed as the demonstration's.
        var host = DemoHost.New();
        var demo = DemoHost.Service(host);
        var myGland = CalcDemoProject.GlandProfile with { Title = "My own gland profile" };
        Assert.Null((await host.Service.SaveScanProfileAsync(myGland)).Refusal);
        Assert.True((await demo.InstallAsync()).Succeeded);
        var real = await host.ProjectAsync();
        var stack = await host.Service.CreateAsync("mech.tolerance-stack", real, "Real groove stack", CalcDemoProject.SealStackInputs());
        Assert.True((await host.Service.ApplyScanImportAsync(stack.Id, new ScanImportRequest([CalcDemoProject.ScanFile()], CalcDemoProject.StackProfile, CalcDemoProject.ScanSettings))).Action.Succeeded);

        var removed = await demo.RemoveAsync();

        Assert.True(removed.Succeeded, removed.Message);
        Assert.Contains($"The scan profile '{CalcDemoProject.StackProfileId}' version 1 was kept, because a calculation in another project uses it.", removed.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(CalcDemoProject.GlandProfileId, removed.Message, StringComparison.Ordinal);
        Assert.Equal(CalcDemoProject.StackProfile.Title, (await host.Service.FindScanProfileAsync(CalcDemoProject.StackProfileId, 1))?.Title);
        Assert.Equal("My own gland profile", (await host.Service.FindScanProfileAsync(CalcDemoProject.GlandProfileId, 1))?.Title);

        // The other way round: the person's own stack profile is cited by their calculation, and the shipped gland profile is cited by nothing. The own
        // version stays and is not reported as kept (no deletion was ever due); the shipped, uncited version is deleted.
        var second = DemoHost.New();
        var secondDemo = DemoHost.Service(second);
        var myStack = CalcDemoProject.StackProfile with { Title = "My own seal-stack profile" };
        Assert.Null((await second.Service.SaveScanProfileAsync(myStack)).Refusal);
        Assert.True((await secondDemo.InstallAsync()).Succeeded);
        var mine = await second.ProjectAsync();
        var myCalc = await second.Service.CreateAsync("mech.tolerance-stack", mine, "My groove stack", CalcDemoProject.SealStackInputs());
        Assert.True((await second.Service.ApplyScanImportAsync(myCalc.Id, new ScanImportRequest([CalcDemoProject.ScanFile()], myStack, CalcDemoProject.ScanSettings))).Action.Succeeded);

        var deletion = await second.Store.DeleteDemonstrationAsync((await secondDemo.StatusAsync()).ProjectId!.Value, CalcDemoProject.ScanProfiles);

        Assert.Equal(1, deletion.ScanProfiles);
        Assert.Empty(deletion.KeptScanProfiles);
        Assert.Equal("My own seal-stack profile", (await second.Service.FindScanProfileAsync(CalcDemoProject.StackProfileId, 1))?.Title);
        Assert.Null(await second.Service.FindScanProfileAsync(CalcDemoProject.GlandProfileId, 1));
    }

    private sealed class Now(Action<string> act) : IProgress<string>
    {
        public void Report(string value) => act(value);
    }

    [Fact]
    public async Task Black_B_C_04_A_Cancelled_Install_Whose_Clean_Up_Fails_Says_Part_Of_It_Is_Left()
    {
        var host = DemoHost.New();
        var demo = DemoHost.Service(host);
        using var cancel = new CancellationTokenSource();

        // Cancel once every pack is made and the check begins, and make every commit fail from then on, so the clean-up cannot remove what was made.
        // The in-memory store does not observe the token, so the cancellation is raised where a store that does would raise it: inside the install.
        var outcome = await demo.InstallAsync(new Now(line =>
        {
            if (line.StartsWith("Checking every calculation", StringComparison.Ordinal))
            {
                host.Failing.FailEveryCommit = true;
                cancel.Cancel();
                cancel.Token.ThrowIfCancellationRequested();
            }
        }), cancel.Token);

        Assert.False(outcome.Succeeded);
        Assert.True(outcome.PartlyDone);
        Assert.Equal("Installing the demo project was cancelled; part of it is left; use Remove demo project to clear it.", outcome.Message);

        // The sentence is true, and the removal it names clears it.
        host.Failing.FailEveryCommit = false;
        Assert.True((await demo.StatusAsync()).Installed);
        Assert.True((await demo.RemoveAsync()).Succeeded);
        Assert.Empty(host.Backing.CommittedKeys(CalcInstanceStore.InstanceCollection));
    }

    [Fact]
    public async Task Black_B_C_04_A_Cancelled_Install_Whose_Clean_Up_Works_Is_Still_A_Cancellation_And_Leaves_Nothing()
    {
        var host = DemoHost.New();
        var demo = DemoHost.Service(host);
        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => demo.InstallAsync(new Now(line =>
        {
            if (line.StartsWith("Checking every calculation", StringComparison.Ordinal))
            {
                cancel.Cancel();
                cancel.Token.ThrowIfCancellationRequested();
            }
        }), cancel.Token));

        Assert.False((await demo.StatusAsync()).Installed);
        Assert.Empty(host.Backing.CommittedKeys(CalcInstanceStore.InstanceCollection));
    }

    // ------------------------------------------------------------------ Green G-C-01

    [Fact]
    public void Green_G_C_01_The_Dashboard_And_The_Guided_Test_Types_Can_Be_Named_In_One_File()
    {
        // This file imports both namespaces; before the rename these names were ambiguous (CS0104) and the file did not compile.
        Assert.NotEqual(typeof(IManualTestStatusSource), typeof(IDashboardManualTestSource));
        Assert.NotEqual(typeof(ManualTestStatus), typeof(DashboardManualTestStatus));
        Assert.NotEqual(typeof(ManualTestOutcome), typeof(DashboardManualTestOutcome));
        Assert.NotEqual(typeof(RecordRequirement), typeof(DashboardRecordRequirement));
        Assert.Equal(DashboardManualTestOutcome.Failed, GuidedManualTestStatusSource.Map(new ManualTestStatus("t.a", ManualTestOutcome.Fail, DateTimeOffset.UnixEpoch, "c", "t", Guid.Empty, 1, 1)).Outcome);
    }
}
