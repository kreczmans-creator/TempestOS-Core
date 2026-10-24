using Tempest.Calc.Library;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>Phase 6: two issues raced on one checked instance; exactly one may succeed and the instance is issued once.</summary>
public sealed class ConcurrencyTests
{
    [Fact]
    public async Task Two_Simultaneous_Issues_Leave_One_Issued_Revision()
    {
        var host = new LibraryTestHost();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "RACE", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Task.Run(async () =>
        {
            try
            {
                return (await host.Service.IssueAsync(instance.Id, $"ISS-RACE-{i}", "Client Co")).Succeeded;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        })));

        Assert.Equal(1, results.Count(r => r));
        var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
        Assert.Equal(CalcInstanceStatus.Issued, stored.Status);
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(stored.EvidenceId!.Value))!;
        Assert.Equal(EvidenceStatus.Issued, evidence.Status);
        Assert.Single(await evidence.GetAttachmentsAsync());
    }
}
