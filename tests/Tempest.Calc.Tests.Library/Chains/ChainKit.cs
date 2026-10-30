using System.Text;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>What every chain test proves, written once: reading values, instantiating, revising upstream, and checking and issuing as one bundle.</summary>
internal static class ChainKit
{
    public static double Si(CalcInstance instance, string symbol) => instance.Outputs.Single(o => o.Symbol == symbol).Si!.Value;

    public static double InputSi(CalcInstance instance, string symbol)
    {
        var input = instance.Inputs.Single(i => i.Symbol == symbol);
        return Tempest.Calc.Engine.Units.UnitCatalog.Parse(input.Unit ?? string.Empty).ToSi(input.Number!.Value);
    }

    public static double CellSi(CalcInstance instance, string table, int row, string column)
    {
        var cell = instance.Inputs.Single(i => i.Symbol == table).Rows![row].Get(column)!;
        return Tempest.Calc.Engine.Units.UnitCatalog.Parse(cell.Unit ?? string.Empty).ToSi(cell.Number!.Value);
    }

    public static async Task<CalcInstance> Fresh(LibraryTestHost host, CalcPackInstantiation made, string key) => (await host.Store.FindInstanceAsync(made.Instances[key].Id))!;

    /// <summary>Instantiates <paramref name="template"/> from <paramref name="inputs"/> and asserts nothing was refused or blocked.</summary>
    public static async Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host, CalcPackTemplate template, string title, IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>> inputs, Guid? project = null)
    {
        Assert.Null(host.Service.ValidatePackTemplate(template));
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, project ?? await host.ProjectAsync(), title, inputs);
        Assert.True(refusal is null, refusal);
        Assert.Equal(template.Steps.Count, made!.Pack.InstanceIds.Count);
        Assert.All(made.Instances.Values, i => Assert.False(i.Blocks.Count > 0, $"{i.Name}: {string.Join("; ", i.Blocks.Select(b => b.Message))}"));

        foreach (var instance in made.Instances.Values)
            Assert.False((await host.Service.StalenessAsync(instance)).IsStale, instance.Name);

        return made;
    }

    /// <summary>Records another engineer as the author of every step, so a template that demands an independent check can be checked.</summary>
    public static async Task AuthoredByAnotherAsync(LibraryTestHost host, CalcPackInstantiation made)
    {
        foreach (var instance in made.Instances.Values)
        {
            var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
            await host.Store.SaveInstanceAsync(stored with { CreatedBy = "another.engineer", EditedBy = ["another.engineer"] }, stored.UpdatedAt);
        }
    }

    /// <summary>A revision that multiplies the plain number or quantity input <paramref name="symbol"/>.</summary>
    public static Func<IReadOnlyList<CalcInputValue>, IReadOnlyList<CalcInputValue>> Scale(string symbol, double factor) =>
        inputs => [.. inputs.Select(i => i.Number is { } n && i.Symbol == symbol ? i with { Number = n * factor } : i)];

    /// <summary>A revision that multiplies one number cell of a table input.</summary>
    public static Func<IReadOnlyList<CalcInputValue>, IReadOnlyList<CalcInputValue>> ScaleCell(string table, int row, string column, double factor) =>
        inputs => [.. inputs.Select(i => i.Symbol != table ? i : i with
        {
            Rows = [.. i.Rows!.Select((r, index) => index != row ? r : new CalcRow(r.Cells.ToDictionary(c => c.Key, c => c.Key == column ? c.Value with { Number = c.Value.Number * factor } : c.Value, StringComparer.Ordinal)))],
        })];

    /// <summary>
    /// Revises an input of <paramref name="changedKey"/> and proves what ADR-0168 promises: every step downstream is stale, the pack
    /// cannot be checked and cannot be issued, updating the steps in <paramref name="downstreamInOrder"/> clears the staleness, and
    /// the pack then checks.
    /// </summary>
    public static Task ReviseAndUpdateAsync(LibraryTestHost host, CalcPackInstantiation made, string changedKey, string changedSymbol, double factor, IReadOnlyList<string> downstreamInOrder) =>
        ReviseAndUpdateAsync(host, made, changedKey, Scale(changedSymbol, factor), downstreamInOrder);

    public static async Task ReviseAndUpdateAsync(LibraryTestHost host, CalcPackInstantiation made, string changedKey, Func<IReadOnlyList<CalcInputValue>, IReadOnlyList<CalcInputValue>> revision, IReadOnlyList<string> downstreamInOrder)
    {
        await AuthoredByAnotherAsync(host, made);
        var designer = host.Principal;
        const string Reviewer = "j.reviewer";
        var before = await Fresh(host, made, changedKey);
        var changed = await host.Service.UpdateInputsAsync(before.Id, revision(before.Inputs));
        Assert.True(changed.Succeeded, changed.Reason);
        Assert.NotEqual(before.OutputsHash, changed.Instance!.OutputsHash);

        foreach (var key in downstreamInOrder)
            Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be stale");

        // A stale pack is not checked (the reason names the stale member), and an unchecked pack is not issued (that is the reason given: this
        // refusal is about the pack's status; the issue-time staleness gate is proved separately, where a member goes stale after the check).
        host.Principal = Reviewer;
        var blocked = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(blocked.Succeeded);
        Assert.Contains("is stale", blocked.Reason, StringComparison.Ordinal);
        Assert.Contains("Update it before checking the pack", blocked.Reason, StringComparison.Ordinal);
        var notIssued = await host.Service.IssuePackAsync(made.Pack.Id, "CHAIN-STALE", "Client Co");
        Assert.False(notIssued.Succeeded);
        Assert.Contains("check it before issuing", notIssued.Reason, StringComparison.Ordinal);
        host.Principal = designer;

        // Updating in chain order clears the staleness one step at a time; the pack stays refused until the last is current.
        for (var i = 0; i < downstreamInOrder.Count; i++)
        {
            var current = await Fresh(host, made, downstreamInOrder[i]);

            if ((await host.Service.StalenessAsync(current)).IsStale)
                Assert.True((await host.Service.UpdateLinksAsync(current.Id)).Succeeded, downstreamInOrder[i]);

            if (i < downstreamInOrder.Count - 1 && (await host.Service.StalenessAsync(await Fresh(host, made, downstreamInOrder[^1]))).IsStale)
            {
                host.Principal = Reviewer;
                Assert.False((await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
                host.Principal = designer;
            }
        }

        foreach (var key in downstreamInOrder)
            Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be current");

        host.Principal = Reviewer;
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK once re-pinned.", CheckOutcome.Accepted);
        host.Principal = designer;
        Assert.True(check.Succeeded, check.Reason);
    }

    /// <summary>Asserts full provenance, checks and issues the pack as one bundle, and returns the issued sheet text.</summary>
    public static async Task<string> CheckAndIssueAsync(LibraryTestHost host, CalcPackInstantiation made, CalcPackTemplate template, string reference, string client, string note)
    {
        // Full provenance on every input of every step (the links carry their own).
        foreach (var (key, instance) in made.Instances)
        {
            var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
            Assert.All(stored.Inputs, i => Assert.True(i.Provenance?.IsSufficientToIssue == true, $"{key}.{i.Symbol} lacks provenance"));
        }

        await AuthoredByAnotherAsync(host, made);
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", client, note, CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        var issued = await host.Service.IssuePackAsync(made.Pack.Id, reference, client);
        Assert.True(issued.Succeeded, issued.Reason);

        var pack = (await host.Store.FindPackAsync(made.Pack.Id))!;
        Assert.Equal(CalcInstanceStatus.Issued, pack.Status);
        Assert.Equal(reference, pack.IssueReference);
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(pack.EvidenceId!.Value))!;
        Assert.Equal(EvidenceStatus.Issued, evidence.Status);
        var attachment = Assert.Single(await evidence.GetAttachmentsAsync());
        var sheet = Encoding.UTF8.GetString((await evidence.ReadAttachmentContentAsync(attachment.Id)).Bytes);
        Assert.Contains("PACK INDEX", sheet, StringComparison.Ordinal);

        foreach (var step in template.Steps)
        {
            Assert.Contains(step.Name, sheet, StringComparison.Ordinal);
            var member = (await host.Store.FindInstanceAsync(made.Instances[step.Key].Id))!;
            Assert.Equal(CalcInstanceStatus.Issued, member.Status);
            Assert.Equal(reference, member.IssueReference);
        }

        // Issued means immutable: the first step cannot be changed afterwards.
        Assert.False((await host.Service.UpdateInputsAsync(made.Instances[template.Steps[0].Key].Id, [])).Succeeded);
        return sheet;
    }

    /// <summary>
    /// Proves the staleness gate of Issue (not the unchecked-pack gate). A pack member of a checked pack is frozen, so the first step cannot be revised in place; as in the drive-train
    /// test, an outside copy of the first step (<paramref name="firstKey"/>) feeds the first downstream step in its place (<paramref name="rebind"/>, done while the pack is a draft), is checked and issued, the pack is
    /// checked on it, and then the outside copy moves on to a new revision. No pack member is touched, yet every step downstream of the rebound one is stale: Issue is refused naming the first stale member,
    /// not for the pack's status and not for an edit after the check, and the pack stays Checked.
    /// </summary>
    public static async Task IssueIsRefusedAtTheStalenessGateWhenTheFirstStepMovesAfterTheCheckAsync(
        LibraryTestHost host, CalcPackInstantiation made, CalcPackTemplate template, string firstKey, string firstDownstreamKey,
        Func<Guid, Guid, Task<CalcActionResult>> rebind, IReadOnlyList<string> downstreamInOrder)
    {
        var first = template.Steps.Single(s => s.Key == firstKey);
        var outside = await host.Service.CreateAsync(first.TemplateId, made.Pack.ProjectId, "Outside copy of " + first.Name, (await Fresh(host, made, firstKey)).Inputs);
        Assert.Empty(outside.Blocks);
        var rebound = await rebind(made.Instances[firstDownstreamKey].Id, outside.Id);
        Assert.True(rebound.Succeeded, rebound.Reason);
        await AuthoredByAnotherAsync(host, made);
        host.Principal = "j.reviewer";
        Assert.True((await host.Service.CheckAsync(outside.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(outside.Id, "EXT-1", "Client Co")).Succeeded);
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindPackAsync(made.Pack.Id))!.Status);

        // The first step moves on after the check, as a new revision; no pack member is touched.
        var moved = await host.Service.ReviseAsync(outside.Id);
        Assert.True(moved.Succeeded, moved.Reason);

        foreach (var key in downstreamInOrder)
            Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be stale");

        var refused = await host.Service.IssuePackAsync(made.Pack.Id, "STALE-ISSUE", "Client Co");
        Assert.False(refused.Succeeded);
        Assert.Contains($"Pack member '{made.Instances[downstreamInOrder[0]].Name}' is stale", refused.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("changed after the pack was checked", refused.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("check it before issuing", refused.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindPackAsync(made.Pack.Id))!.Status);
    }
}
