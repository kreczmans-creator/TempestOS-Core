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
        Assert.Contains("more than", overRows.Refusal, StringComparison.Ordinal);
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
}
