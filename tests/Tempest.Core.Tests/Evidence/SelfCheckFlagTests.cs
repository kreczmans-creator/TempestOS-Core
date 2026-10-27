using Tempest.Core.Evidence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Evidence;

/// <summary>
/// DA-12 review, D3: an owner that records a preparer's own check (the calculation worksheet's self-check) says so to the Evidence layer,
/// and <see cref="CheckRecord.SelfCheck"/> is set on the check record whether or not the global independent-check rule is on — so the
/// Evidence narrative says "Checked by its own author" for it, as it does when the rule finds the same thing itself.
/// </summary>
public sealed class SelfCheckFlagTests
{
    private static readonly EvidenceOwner Owner = new(Tempest.Core.Evidence.Evidence.CalcWorksheetOwner);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_Owners_Self_Check_Is_Marked_On_The_Check_Record_Whatever_The_Global_Rule_Says(bool selfCheck)
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path, independentCheck: false);

        try
        {
            EvidenceTestHost.SignIn(host);
            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);
            var managed = await service.CreateManagedAsync(projectId, "Circuit calc", EvidenceClassification.Calculation, Tempest.Core.Evidence.Evidence.CalcWorksheetOwner);

            var check = await service.RecordCheckInOneTransactionAsync(managed.Id, "Sam Preparer", "Tempest Engineering", "Checked on the worksheet.", CheckOutcome.Accepted, alsoWrite: null, owner: Owner, selfCheck: selfCheck);
            Assert.True(check.Succeeded, check.Reason);

            Assert.Equal(selfCheck, check.Evidence!.Check!.SelfCheck);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
