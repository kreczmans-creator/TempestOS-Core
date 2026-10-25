using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// The proof about issuing a CHECKED pack that <see cref="ChainKit.ReviseAndUpdateAsync(LibraryTestHost, CalcPackInstantiation, string, string, double, IReadOnlyList{string})"/> cannot make: it revises an unchecked pack, where Issue
/// is refused because the pack is not checked. A member of a checked pack is frozen (its inputs cannot be changed), so the only way a checked pack goes stale is a calculation outside it moving on; this is the drive train's pattern, applied
/// at the FIRST step of a chain, so that every step downstream of it is shown stale and Issue is refused at the staleness gate itself.
/// </summary>
internal static class ChainProofs
{
    private const string Reviewer = "j.reviewer";

    /// <summary>
    /// The pack's step <paramref name="firstKey"/> takes <paramref name="input"/> from a calculation outside the pack (<paramref name="externalOutput"/> of a <paramref name="externalTemplateId"/>); the pack is checked on it once it is issued,
    /// and it then moves on as a new revision. No pack member is touched, yet the first step and every step in <paramref name="downstream"/> is stale; the pack, still Checked, cannot be issued, and the refusal names the first step
    /// and the staleness (not the pack's status and not an edit after the check). Nothing is issued.
    /// </summary>
    public static async Task AnOutsideRevisionAfterTheCheckMakesTheWholeChainStaleAndRefusesIssueAsync(
        LibraryTestHost host, CalcPackInstantiation made, string firstKey, string input, string externalTemplateId, IReadOnlyList<CalcInputValue> externalInputs, string externalOutput, IReadOnlyList<string> downstream)
    {
        var first = made.Instances[firstKey];
        var external = await host.Service.CreateAsync(externalTemplateId, made.Pack.ProjectId, "From another job", externalInputs);
        var rebound = await host.Service.BindAsync(first.Id, input, external.Id, externalOutput);
        Assert.True(rebound.Succeeded, rebound.Reason);

        // Taking the value from outside may move the first step's outputs: bring the steps downstream of it up to date, in order, so the pack is checked on current members.
        foreach (var key in downstream)
            if ((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale)
                Assert.True((await host.Service.UpdateLinksAsync(made.Instances[key].Id)).Succeeded, key);

        await AuthoredByAnotherAsync(host, made);

        // While the outside calculation is current and issued, the pack checks on it.
        host.Principal = Reviewer;
        Assert.True((await host.Service.CheckAsync(external.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(external.Id, "EXT-1", "Client Co")).Succeeded);
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);

        foreach (var key in made.Instances.Keys)
            Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be current at the check");

        // The pack's own members are frozen once it is checked.
        var frozen = await host.Service.UpdateInputsAsync(first.Id, (await Fresh(host, made, firstKey)).Inputs);
        Assert.False(frozen.Succeeded);
        Assert.Contains("its inputs are frozen", frozen.Reason, StringComparison.Ordinal);

        // The outside calculation moves on after the check, as a new revision; no pack member is touched.
        var moved = await host.Service.ReviseAsync(external.Id);
        Assert.True(moved.Succeeded, moved.Reason);
        var stale = await Fresh(host, made, firstKey);
        Assert.True((await host.Service.StalenessAsync(stale)).IsStale);

        foreach (var key in downstream)
            Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be stale");

        // Issue is refused at the staleness gate: not for the pack's status, and not for a member edited after the check.
        var refused = await host.Service.IssuePackAsync(made.Pack.Id, "CHAIN-STALE-AFTER-CHECK", "Client Co");
        Assert.False(refused.Succeeded);
        Assert.Contains($"Pack member '{stale.Name}' is stale", refused.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("changed after the pack was checked", refused.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("check it before issuing", refused.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindPackAsync(made.Pack.Id))!.Status);

        foreach (var key in made.Instances.Keys)
            Assert.NotEqual(CalcInstanceStatus.Issued, (await Fresh(host, made, key)).Status);
    }
}
