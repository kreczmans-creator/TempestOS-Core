using Tempest.Calc.Library;
using Tempest.Calc.Library.ManualTesting;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>A full guided run, over the shipped script, never alters a calculation, an Evidence record, an issued sheet or anything else in the store but its own collection.</summary>
public sealed class GuidedRunNoSideEffectsTests
{
    [Fact]
    public async Task A_Full_Guided_Run_Leaves_Every_Other_Table_Exactly_As_It_Was()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();

        // A draft and an issued calculation, each with its Evidence, as a tester's project would hold.
        var draft = await host.Service.CreateAsync("test.direct-stress", project, "CALC-001 Draft", LibraryTestHost.ProvenancedInputs());
        var issued = await host.Service.CreateAsync("test.direct-stress", project, "CALC-002 Issued", LibraryTestHost.ProvenancedInputs(20));
        Assert.True((await host.Service.CheckAsync(issued.Id, "test.checker", "Tempest Engineering", "Checked.", Tempest.Core.Evidence.CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(issued.Id, "TEST-001", "Test")).Succeeded);

        var before = host.Backing.CommittedSnapshot();
        Assert.Contains(before.Keys, k => k.Collection == CalcInstanceStore.InstanceCollection);
        Assert.DoesNotContain(before.Keys, k => k.Collection == ManualTestRunService.Collection);

        var script = GuidedTestScript.Current;
        var service = new ManualTestRunService(host.Backing, script, () => "0.26.0 (1a2b3c4)", () => "1a2b3c4", () => ("s.kreczman", "Steven Kreczman"));

        var run = await service.StartAsync();

        foreach (var (step, index) in script.Steps.Select((s, i) => (s, i)))
        {
            var outcome = (index % 4) switch { 0 => ManualTestOutcome.Pass, 1 => ManualTestOutcome.Fail, 2 => ManualTestOutcome.Blocked, _ => ManualTestOutcome.Skipped };
            await service.RecordAsync(run.Id, step.Id, outcome, $"note {index}", $"values {index}");
        }

        run = await service.FinishAsync(run.Id);
        Assert.Equal(script.Steps.Count, run.Results.Count);
        Assert.NotEmpty(service.ExportMarkdown(run));
        Assert.NotEmpty(await service.LatestByTemplateAsync());
        Assert.Single(await service.ListAsync());

        var after = host.Backing.CommittedSnapshot();
        var others = after.Where(kv => kv.Key.Collection != ManualTestRunService.Collection).ToDictionary();

        Assert.True(before.Count == others.Count && before.All(kv => others.TryGetValue(kv.Key, out var v) && v == kv.Value),
            "A guided run changed something other than its own test-run record.");
        Assert.Equal([ManualTestRunService.Collection], after.Keys.Select(k => k.Collection).Except(before.Keys.Select(k => k.Collection)).Distinct());
        Assert.Single(host.Backing.CommittedKeys(ManualTestRunService.Collection));

        // The calculations themselves read back as they were, status and all.
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindInstanceAsync(draft.Id))!.Status);
        Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(issued.Id))!.Status);
    }
}
