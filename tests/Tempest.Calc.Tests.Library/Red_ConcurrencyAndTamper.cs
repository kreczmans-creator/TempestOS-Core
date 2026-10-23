using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using Tempest.Core.Persistence;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// A store wrapper that can hold or fail the N-th transaction that goes
/// through it, so an interleaving or a crash between two writes of one
/// service call is reproduced deterministically. Everything else delegates.
/// </summary>
internal sealed class Red_GateStore(IQueryablePersistenceStore inner) : IQueryablePersistenceStore
{
    private int _count;

    /// <summary>1-based number of the transaction to hold until <see cref="Release"/> completes.</summary>
    public int BlockTransaction { get; set; } = -1;

    /// <summary>1-based number of the transaction to fail before it runs (a commit that never lands).</summary>
    public int FailTransaction { get; set; } = -1;

    public TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public long CurrentSequence => inner.CurrentSequence;

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) => inner.ListKeysAsync(collection, keyPrefix, cancellationToken);

    public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) => inner.ReadAllAsync(collection, cancellationToken);

    public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) => inner.ReadManyAsync(collection, keys, cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        var n = Interlocked.Increment(ref _count);

        if (n == FailTransaction)
            throw new PersistenceStoreUnavailableException("Injected: the transaction did not land.");

        if (n == BlockTransaction)
        {
            Blocked.TrySetResult();
            await Release.Task.ConfigureAwait(false);
        }

        await inner.ExecuteInTransactionAsync(work, cancellationToken).ConfigureAwait(false);
    }

    public Task<T> ExecuteInReadTransactionAsync<T>(Func<IPersistenceReadTransaction, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default) => inner.ExecuteInReadTransactionAsync(read, cancellationToken);

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) => inner.SearchAsync(query, limit, cancellationToken);

    public Task<bool> IsSearchIndexEmptyAsync(CancellationToken cancellationToken = default) => inner.IsSearchIndexEmptyAsync(cancellationToken);
}

/// <summary>
/// Red team (offensive review): concurrency on one service, crashes
/// between the two writes of one action, and tampering with issued
/// records through every Calc path. Every test asserts the secure
/// behaviour, so a failing test is an open hole.
/// </summary>
public sealed class Red_ConcurrencyAndTamper
{
    /// <summary>A second service over the same backing store and Evidence, whose instance writes pass through a gate.</summary>
    private static (CalcInstanceService Service, Red_GateStore Gate) Gated(LibraryTestHost host)
    {
        var gate = new Red_GateStore(host.Failing);
        return (new CalcInstanceService(host.Catalogue, new CalcInstanceStore(gate), host.Evidence, host.Context, renderer: null, applicationVersion: "red"), gate);
    }

    private static async Task<CalcInstance> IssuedAsync(LibraryTestHost host, string name = "RED")
    {
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), name, LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var issued = await host.Service.IssueAsync(instance.Id, "ISS-RED", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);
        return issued.Instance!;
    }

    private static async Task<EvidenceStatus> EvidenceStatusAsync(LibraryTestHost host, CalcInstance instance) =>
        ((Evidence)(await host.Context.Repository.FindAsync(instance.EvidenceId!.Value))!).Status;

    // ------------------------------------------------------------------
    // Tampering
    // ------------------------------------------------------------------

    /// <summary>R-14: the immutability check lets an issued record be rewritten (name, inputs, outputs, checks) as long as it is marked Superseded and the counts match.</summary>
    [Fact]
    public async Task Issued_Content_Cannot_Be_Rewritten_Under_A_Superseded_Mark()
    {
        var host = new LibraryTestHost();
        var issued = await IssuedAsync(host);

        // The straightforward routes are closed.
        await Assert.ThrowsAsync<CalcInstanceImmutableException>(() => host.Store.SaveInstanceAsync(issued with { Name = "TAMPERED" }));
        await Assert.ThrowsAsync<CalcInstanceImmutableException>(() => host.Store.DeleteDraftInstanceAsync(issued.Id));
        Assert.False((await host.Service.UpdateInputsAsync(issued.Id, LibraryTestHost.ProvenancedInputs(f: 1))).Succeeded);
        Assert.False((await host.Service.CheckAsync(issued.Id, "x", "y", "z", CheckOutcome.Accepted)).Succeeded);

        // The superseded mark is the loophole: same counts, different content.
        var tampered = issued with
        {
            Status = CalcInstanceStatus.Superseded,
            Name = "TAMPERED",
            Revision = "Z",
            IssueReference = "ISS-FAKE",
            Inputs = issued.Inputs.Select(i => i.Number is { } ? i with { Number = 999_999 } : i).ToList(),
            Outputs = issued.Outputs.Select(o => o with { Text = "999999 MPa", Si = 999_999e6 }).ToList(),
            Checks = [],
            Blocks = [],
        };

        var ex = await Record.ExceptionAsync(() => host.Store.SaveInstanceAsync(tampered));
        var stored = (await host.Store.FindInstanceAsync(issued.Id))!;
        Assert.True(ex is CalcInstanceImmutableException, "an issued instance was rewritten under a Superseded mark");
        Assert.Equal(issued.Name, stored.Name);
        Assert.Equal(issued.Outputs, stored.Outputs);
    }

    // ------------------------------------------------------------------
    // Concurrency
    // ------------------------------------------------------------------

    /// <summary>R-15: Update and Check both read a draft; Check records on the Evidence and writes Checked; the slower Update then writes Draft with new inputs. The Evidence says Checked, the instance says Draft, and no further check is ever accepted (Checked → Checked is not a permitted Evidence transition).</summary>
    [Fact]
    public async Task Update_Racing_A_Check_Cannot_Revert_A_Checked_Instance()
    {
        var host = new LibraryTestHost();
        var (service2, gate) = Gated(host);
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "RACE-UC", LibraryTestHost.ProvenancedInputs());

        gate.BlockTransaction = 1;
        var update = service2.UpdateInputsAsync(instance.Id, LibraryTestHost.ProvenancedInputs(f: 24));
        await gate.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var check = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);

        gate.Release.SetResult();
        var updated = await update;

        var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
        Assert.True(stored.Status == CalcInstanceStatus.Checked && !updated.Succeeded, $"the instance is {stored.Status} (update succeeded: {updated.Succeeded}) while its Evidence is {await EvidenceStatusAsync(host, stored)}");
    }

    /// <summary>R-15 (crash form): a crash between the Evidence check commit and the instance write leaves the same split state, and the retry is refused for ever.</summary>
    [Fact]
    public async Task Crash_After_The_Check_Is_Recorded_Leaves_A_Recoverable_Instance()
    {
        var host = new LibraryTestHost();
        var (service2, gate) = Gated(host);
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "CRASH-CHECK", LibraryTestHost.ProvenancedInputs());

        // Check now commits the Evidence check record and the instance in one Core transaction (Blue fix for R-15);
        // the Calc store gate no longer sees a write of its own, so the injected failure cannot split the two writes.
        gate.FailTransaction = 1;
        var crashed = await Record.ExceptionAsync(() => service2.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted));

        var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
        var evidenceStatus = await EvidenceStatusAsync(host, stored);

        if (crashed is null)
        {
            Assert.Equal(CalcInstanceStatus.Checked, stored.Status);
            Assert.Equal(EvidenceStatus.Checked, evidenceStatus);
            return;
        }

        Assert.Equal(CalcInstanceStatus.Draft, stored.Status);
        Assert.Equal(EvidenceStatus.Draft, evidenceStatus);
        var retry = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(retry.Succeeded, $"after a crash mid-check the retry is refused: {retry.Reason}");
    }

    /// <summary>R-16: Revise writes the new draft first and supersedes the old one second, outside one transaction; two concurrent revisions (or a crash in between) leave two revision-B drafts of one issued calculation.</summary>
    [Fact]
    public async Task Two_Simultaneous_Revisions_Produce_One_Revision_B()
    {
        var host = new LibraryTestHost();
        var (service2, gate) = Gated(host);
        var issued = await IssuedAsync(host);

        gate.BlockTransaction = 1; // the single transaction that writes the new draft and supersedes the old revision together
        var first = service2.ReviseAsync(issued.Id);
        await gate.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The Evidence title's uniqueness refuses the second revision, but by throwing (DuplicateBusinessIdentifierException), not by a refusal.
        var secondError = await Record.ExceptionAsync(() => host.Service.ReviseAsync(issued.Id));

        gate.Release.SetResult();
        _ = await Record.ExceptionAsync(() => first);
        Assert.True(secondError is null, $"the concurrent revision escaped as {secondError?.GetType().Name} instead of a refusal");

        var revisions = (await host.Store.ListInstancesAsync(issued.ProjectId)).Where(i => i.PredecessorId == issued.Id).ToList();
        Assert.True(revisions.Count == 1, $"{revisions.Count} revisions of one issued calculation exist: {string.Join(", ", revisions.Select(r => r.Revision + "/" + r.Status))}");
    }

    /// <summary>R-16 (crash form): a crash after the new draft is written but before the old revision is superseded leaves an issued A with a dangling B.</summary>
    [Fact]
    public async Task Crash_Mid_Revise_Leaves_No_Orphan_Draft()
    {
        var host = new LibraryTestHost();
        var (service2, gate) = Gated(host);
        var issued = await IssuedAsync(host);

        gate.FailTransaction = 1; // the single transaction that writes the new draft and supersedes the old revision together
        await Assert.ThrowsAnyAsync<Exception>(() => service2.ReviseAsync(issued.Id));

        var stored = (await host.Store.FindInstanceAsync(issued.Id))!;
        var drafts = (await host.Store.ListInstancesAsync(issued.ProjectId)).Where(i => i.PredecessorId == issued.Id).ToList();

        // A retry must recover: either it succeeds, or the state was consistent already.
        CalcActionResult? retried = null;
        var retryError = await Record.ExceptionAsync(async () => retried = await host.Service.ReviseAsync(issued.Id));
        Assert.True(stored.Status == CalcInstanceStatus.Superseded || drafts.Count == 0, $"the original is {stored.Status} and {drafts.Count} orphan draft(s) exist; a retry {(retryError is null ? "succeeded" : "threw " + retryError.GetType().Name)}");
        Assert.True(retryError is null, $"after a crash mid-revise the issued calculation can never be revised again: {retryError?.GetType().Name}: {retryError?.Message}");
        Assert.True(retried!.Succeeded, $"after a crash mid-revise the retry is refused: {retried.Reason}");
        Assert.Single((await host.Store.ListInstancesAsync(issued.ProjectId)).Where(i => i.PredecessorId == issued.Id));
    }

    /// <summary>R-19: four concurrent checks on one draft all succeed; the Evidence records a check four times over (no optimistic concurrency in RecordCheckAsync either).</summary>
    [Fact]
    public async Task Two_Simultaneous_Checks_Leave_One_Check()
    {
        var host = new LibraryTestHost();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "RACE-CC", LibraryTestHost.ProvenancedInputs());
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Task.Run(async () =>
        {
            try
            {
                return (await host.Service.CheckAsync(instance.Id, $"Checker {i}", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        })));
        var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
        Assert.Equal(CalcInstanceStatus.Checked, stored.Status);
        Assert.True(results.Count(r => r) == 1, $"{results.Count(r => r)} of 4 concurrent checks were accepted on one draft");
        Assert.Equal(EvidenceStatus.Checked, await EvidenceStatusAsync(host, stored));
    }

    /// <summary>Tried, no issue: a crash mid-issue followed by a retry issues exactly once.</summary>
    [Fact]
    public async Task Crash_Mid_Issue_Then_Retry_Issues_Once()
    {
        var host = new LibraryTestHost();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "CRASH-ISSUE", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        host.Failing.FailNextCommit = true;
        await Assert.ThrowsAnyAsync<Exception>(() => host.Service.IssueAsync(instance.Id, "ISS-1", "Client Co"));
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindInstanceAsync(instance.Id))!.Status);

        var retry = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client Co");
        Assert.True(retry.Succeeded, retry.Reason);
        var evidence = (Evidence)(await host.Context.Repository.FindAsync(instance.EvidenceId!.Value))!;
        Assert.Single(await evidence.GetAttachmentsAsync());
        Assert.Equal(1, (await host.Store.ListInstancesAsync(instance.ProjectId)).Count(i => i.Status == CalcInstanceStatus.Issued));
    }

    // ------------------------------------------------------------------
    // Packs
    // ------------------------------------------------------------------

    /// <summary>R-17: a pack's check leaves its members Draft, so a member's inputs can be changed after the pack was checked and the pack then issues the unchecked inputs under the checked Evidence.</summary>
    [Fact]
    public async Task Pack_Member_Changed_After_The_Pack_Check_Cannot_Be_Issued()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var member = await host.Service.CreateAsync("test.direct-stress", project, "PACK-M", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "Pack", [member.Id]);
        var check = await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);

        var update = await host.Service.UpdateInputsAsync(member.Id, LibraryTestHost.ProvenancedInputs(f: 2400));
        var issue = update.Succeeded ? await host.Service.IssuePackAsync(pack.Id, "ISS-P", "Client Co") : null;
        var stored = (await host.Store.FindInstanceAsync(member.Id))!;

        Assert.True(!update.Succeeded || issue is { Succeeded: false },
            $"a pack member was changed after the pack check (update succeeded: {update.Succeeded}) and issued with the new inputs (issued: {issue?.Succeeded}, sigma = {stored.Outputs.FirstOrDefault(o => o.Symbol == "sigma")?.Text})");
    }

    /// <summary>R-18: a pack listing the same instance twice is accepted, and its issue then throws from inside the transaction instead of refusing.</summary>
    [Fact]
    public async Task Pack_With_Duplicate_Members_Is_Refused_Or_Issues_Cleanly()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var member = await host.Service.CreateAsync("test.direct-stress", project, "PACK-D", LibraryTestHost.ProvenancedInputs());

        CalcPack pack;

        try
        {
            pack = await host.Service.CreatePackAsync(project, "Dup pack", [member.Id, member.Id]);
        }
        catch (ArgumentException)
        {
            return;
        }

        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var ex = await Record.ExceptionAsync(() => host.Service.IssuePackAsync(pack.Id, "ISS-D", "Client Co"));
        Assert.True(ex is null, $"issuing a pack with a duplicate member threw {ex?.GetType().Name}: {ex?.Message}");
    }

    // ------------------------------------------------------------------
    // Oversized input text through the service
    // ------------------------------------------------------------------

    /// <summary>R-05 (service form): a 2 MB provenance reference is accepted by UpdateInputs, blows the sheet, and makes Issue throw on the 4 MB document cap from inside the issue transaction.</summary>
    [Fact]
    public async Task Oversized_Provenance_Is_Refused_And_Issue_Never_Throws()
    {
        var host = new LibraryTestHost();
        var instance = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "BIG", LibraryTestHost.ProvenancedInputs());
        var reference = new string('R', 2_000_000);
        var inputs = LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "F" ? i with { Provenance = CalcProvenance.Drawing(reference, "B") } : i).ToList();

        var update = await host.Service.UpdateInputsAsync(instance.Id, inputs);

        if (!update.Succeeded)
            return;

        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var ex = await Record.ExceptionAsync(() => host.Service.IssueAsync(instance.Id, "ISS-BIG", "Client Co"));
        var stored = (await host.Store.FindInstanceAsync(instance.Id))!;
        Assert.True(ex is null, $"Issue threw {ex?.GetType().Name}: {ex?.Message}");
        Assert.True(stored.Issue is null || stored.Issue.SheetText.Length < 400_000, $"the issued sheet text is {stored.Issue!.SheetText.Length} characters");
        Assert.True(false, "a 2 MB provenance reference was accepted by UpdateInputs");
    }

    /// <summary>Tried (Low): a crash on the instance write of Create leaves an Evidence and a register entry that govern nothing.</summary>
    [Fact(Skip = "R-22 accepted (Low, D-32): a crash between the Evidence record and the instance write leaves a Draft Evidence record with no instance; it cannot be issued, is visible in the register, and a retry of Create makes a new record. Not fixed in v0.26.0.")]
    public async Task Crash_Mid_Create_Leaves_No_Orphan_Evidence()
    {
        var host = new LibraryTestHost();
        var (service2, gate) = Gated(host);
        var project = await host.ProjectAsync();
        var before = (await host.Context.Repository.ListAllAsync()).Count;

        gate.FailTransaction = 1;
        await Assert.ThrowsAnyAsync<Exception>(() => service2.CreateAsync("test.direct-stress", project, "ORPHAN", LibraryTestHost.ProvenancedInputs()));

        Assert.Empty(await host.Store.ListInstancesAsync(project));
        var after = (await host.Context.Repository.ListAllAsync()).Count;
        Assert.True(after == before, $"{after - before} orphan Evidence record(s) remain after a failed Create");
    }
}
