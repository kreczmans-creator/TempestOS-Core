using Tempest.Calc.Library;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>
/// A guided test run is a document in the store like any other, and meets the same 4 MB cap as it is written (C-7 review, finding 3). The caps on one
/// result (a 2,000 character note, 4,000 characters of values) allow a run of a few hundred results to pass 4 MB once accented text is written as
/// six-byte escapes; the result that would take it over is refused in one sentence, and the run on file is as it was.
/// </summary>
public sealed class ManualTestRunDocumentCapTests
{
    private static (ManualTestRunService Service, InMemoryQueryablePersistenceStore Store, GuidedTestScript Script) Make()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var script = GuidedTestScript.Parse(GuidedTestHelpers.SmallScriptJson(exampleCount: 350));
        var service = new ManualTestRunService(store, script, () => "0.26.0 (1a2b3c4)", () => "1a2b3c4", () => ("s.tester", "Sam Tester"));
        return (service, store, script);
    }

    [Fact]
    public async Task A_Result_That_Would_Take_The_Run_Past_The_Document_Cap_Is_Refused_And_The_Run_On_File_Is_As_It_Was()
    {
        var (service, store, script) = Make();
        var run = await service.StartAsync();
        var note = new string('é', ManualTestRun.MaxNoteLength);
        var values = new string('é', ManualTestRun.MaxValuesShownLength);
        var recorded = 0;
        ManualTestRefusedException? refused = null;
        GuidedTestStep? refusedStep = null;

        foreach (var step in script.Steps)
        {
            try
            {
                await service.RecordAsync(run.Id, step.Id, ManualTestOutcome.Pass, note, values);
                recorded++;
            }
            catch (ManualTestRefusedException ex)
            {
                refused = ex;
                refusedStep = step;
                break;
            }
        }

        Assert.NotNull(refused);
        Assert.InRange(recorded, 50, 349);
        Assert.Equal($"The test run is larger than 4 MB, so the result for step '{refusedStep!.Title}' was not saved; finish this run and start a new one.", refused.Message);

        var stored = await service.FindAsync(run.Id);
        Assert.Equal(recorded, stored!.Results.Count);
        var onFile = (await store.ReadManyAsync(ManualTestRunService.Collection, [run.Id.ToString("N")])).Values.Single();
        Assert.InRange(onFile!.Length, 3_000_000, CalcInstanceStore.MaxDocumentBytes);
        Assert.Null(stored.FinishedUtc);
    }

    [Fact]
    public async Task A_Run_Under_The_Cap_Records_And_Finishes_As_Before()
    {
        var (service, _, script) = Make();
        var run = await service.StartAsync();

        foreach (var step in script.Steps.Take(40))
            await service.RecordAsync(run.Id, step.Id, ManualTestOutcome.Pass, "Matches the sheet.", "P = 3.93 kN");

        var finished = await service.FinishAsync(run.Id);

        Assert.NotNull(finished.FinishedUtc);
        Assert.Equal(40, finished.Results.Count);
    }
}
