using System.Text.Json.Nodes;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing.Xero.Sync;
using static Tempest.Core.Tests.Invoicing.Xero.Sync.Stores.StoresFixtures;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

/// <summary>`v0.24.0` B2 (design §6.1, §11): the outbox — round-trip, schema versioning, per-document FIFO, supersede, dedupe, retry, the drain's transitions, concurrency.</summary>
public sealed class PersistenceXeroOutboxTests
{
    private static readonly XeroOutboxState[] AllStates = [];

    private readonly YieldingInMemoryPersistenceStore _persistence = new();
    private readonly SteppingClock _clock = new(Start);

    private PersistenceXeroOutbox Outbox(ICurrentPrincipalAccessor? principals = null) => new(_persistence, principals, _clock);

    [Fact]
    public async Task EnqueuedEntry_IsPendingWithAFixedKey_AndRoundTrips()
    {
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        Assert.Equal(XeroOutboxEntry.CurrentSchemaVersion, entry.SchemaVersion);
        Assert.Equal(XeroOutboxState.Pending, entry.State);
        Assert.Equal(0, entry.Attempts);
        Assert.Equal(Start, entry.EnqueuedAtUtc);
        Assert.Null(entry.NotBeforeUtc);
        Assert.Equal(PersistenceXeroOutbox.SystemPrincipal, entry.EnqueuedBy);
        Assert.Equal(XeroIdempotencyKey.Create(Quote(1), XeroOperation.PushQuote, "h1"), entry.IdempotencyKey);

        Assert.Equal(entry, (await Outbox().ListForDocumentAsync(Quote(1))).Single());
        Assert.Equal(entry, await Outbox().FindAsync(entry.Id));
        Assert.NotNull(_persistence.Raw("Xero.Outbox", entry.Id.ToString("D")));
    }

    [Fact]
    public async Task EnqueuedBy_IsTheSignedInPrincipal()
    {
        var principals = new CurrentPrincipalAccessor();
        principals.SetCurrent(new PlatformPrincipal(new PlatformIdentity("po-01", "Product Owner"), ApplicationPermissions.LocalSession));

        var entry = await Outbox(principals).EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        Assert.Equal("po-01", entry.EnqueuedBy);
    }

    [Fact]
    public async Task SameContent_IsNotQueuedTwice_WhilePendingOrAfterSuccess()
    {
        var first = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var again = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        Assert.Equal(first, again);

        var claimed = await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(claimed!.Id, XeroOutboxState.Succeeded);

        var afterSuccess = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        Assert.Equal(first.Id, afterSuccess.Id);
        Assert.Equal(XeroOutboxState.Succeeded, afterSuccess.State);
        Assert.Single(await Outbox().ListAsync(AllStates));
    }

    [Fact]
    public async Task SameContent_AsAFailedEntry_ReturnsIt_RetryIsTheWayBack()
    {
        var first = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(first.Id, XeroOutboxState.Failed, "No contact link.");

        var again = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(XeroOutboxState.Failed, again.State);
    }

    [Fact]
    public async Task NewContent_SupersedesAPendingEntry_OfTheSameOperation()
    {
        var old = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var newer = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2");

        var entries = await Outbox().ListForDocumentAsync(Quote(1));

        Assert.Equal([old.Id, newer.Id], entries.Select(e => e.Id));
        Assert.Equal([XeroOutboxState.Superseded, XeroOutboxState.Pending], entries.Select(e => e.State));
        Assert.NotEqual(old.IdempotencyKey, newer.IdempotencyKey);
        Assert.Equal(newer.Id, (await Outbox().ClaimNextDueAsync())!.Id);
    }

    [Fact]
    public async Task NewContent_SupersedesFailedAndWaiting_ButNeverAnEntryThatMayHaveReachedXero()
    {
        var failed = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(failed.Id, XeroOutboxState.Failed, "Rejected.");
        var waiting = await Outbox().EnqueueAsync(XeroOperation.PushPurchaseOrder, Quote(2), "h1");
        await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(waiting.Id, XeroOutboxState.WaitingForAuthorisation, "401");
        var unknown = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(3), "h1");
        await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(unknown.Id, XeroOutboxState.Unknown, "Response lost.", Start.AddMinutes(1));
        var inFlight = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(4), "h1");
        await Outbox().ClaimNextDueAsync();

        await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2");
        await Outbox().EnqueueAsync(XeroOperation.PushPurchaseOrder, Quote(2), "h2");
        await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(3), "h2");
        await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(4), "h2");

        Assert.Equal(XeroOutboxState.Superseded, (await Outbox().FindAsync(failed.Id))!.State);
        Assert.Equal(XeroOutboxState.Superseded, (await Outbox().FindAsync(waiting.Id))!.State);
        Assert.Equal(XeroOutboxState.Unknown, (await Outbox().FindAsync(unknown.Id))!.State);
        Assert.Equal(XeroOutboxState.InFlight, (await Outbox().FindAsync(inFlight.Id))!.State);
    }

    [Fact]
    public async Task DifferentOperationsOrArguments_DoNotSupersedeEachOther()
    {
        var push = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var sent = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "h1", "SENT");
        var accepted = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "h1", "ACCEPTED");
        var otherDocument = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(2), "h9");

        Assert.All(await Outbox().ListAsync(AllStates), e => Assert.Equal(XeroOutboxState.Pending, e.State));
        Assert.Equal(4, new[] { push, sent, accepted, otherDocument }.Select(e => e.IdempotencyKey).Distinct().Count());
    }

    [Fact]
    public async Task ContentChangedAndChangedBack_IsQueuedAgain_WithAFreshKey()
    {
        var a = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "A");
        await SucceedNextAsync();
        await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "B");
        await SucceedNextAsync();

        var aAgain = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "A");

        Assert.NotEqual(a.Id, aAgain.Id);
        Assert.Equal(XeroOutboxState.Pending, aAgain.State);
        Assert.NotEqual(a.IdempotencyKey, aAgain.IdempotencyKey);
        Assert.Equal(XeroIdempotencyKey.Create(Quote(1), XeroOperation.PushQuote, "A", occurrence: 1), aAgain.IdempotencyKey);
    }

    [Fact]
    public async Task PerDocumentFifo_AStatusChangeNeverOvertakesItsCreate()
    {
        var create = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "h1", "SENT");
        var other = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(2), "h1");

        Assert.Equal(create.Id, (await Outbox().ClaimNextDueAsync())!.Id);

        // Quote 1's create is in flight: its status change waits; quote 2 does not.
        Assert.Equal(other.Id, (await Outbox().ClaimNextDueAsync())!.Id);
        Assert.Null(await Outbox().ClaimNextDueAsync());

        await Outbox().RecordOutcomeAsync(create.Id, XeroOutboxState.Succeeded);

        Assert.Equal(status.Id, (await Outbox().ClaimNextDueAsync())!.Id);
    }

    [Theory]
    [InlineData(XeroOutboxState.Failed)]
    [InlineData(XeroOutboxState.WaitingForAuthorisation)]
    [InlineData(XeroOutboxState.InFlight)]
    public async Task AnOpenEarlierEntry_HoldsItsDocumentsQueue(XeroOutboxState earlierState)
    {
        var create = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "h1", "SENT");
        await Outbox().ClaimNextDueAsync();
        if (earlierState != XeroOutboxState.InFlight)
            await Outbox().RecordOutcomeAsync(create.Id, earlierState, "reason");

        Assert.Null(await Outbox().ClaimNextDueAsync());
    }

    [Fact]
    public async Task PendingWithBackoff_IsNotDueUntilNotBefore_AndHoldsItsDocument()
    {
        var create = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "h1", "SENT");
        await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(create.Id, XeroOutboxState.Pending, "503", Start.AddSeconds(30));

        Assert.Null(await Outbox().ClaimNextDueAsync());

        _clock.Advance(TimeSpan.FromSeconds(30));
        var again = await Outbox().ClaimNextDueAsync();

        Assert.Equal(create.Id, again!.Id);
        Assert.Equal(2, again.Attempts);
        Assert.Equal(create.IdempotencyKey, again.IdempotencyKey);
        Assert.Equal(Start.AddSeconds(30), again.LastAttemptAtUtc);
    }

    [Fact]
    public async Task Claim_PersistsInFlightBeforeReturning_AndOldestDocumentGoesFirst()
    {
        var first = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(9), "h1");
        await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        var claimed = await Outbox().ClaimNextDueAsync();

        Assert.Equal(first.Id, claimed!.Id);
        Assert.Equal(XeroOutboxState.InFlight, claimed.State);
        Assert.Equal(1, claimed.Attempts);
        Assert.Equal(Start, claimed.LastAttemptAtUtc);
        Assert.Equal(claimed, await new PersistenceXeroOutbox(_persistence, null, _clock).FindAsync(first.Id));
    }

    [Fact]
    public async Task SameInstantEnqueues_KeepQueueOrder()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 12; i++)
            ids.Add((await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(i), $"h{i}")).Id);

        Assert.Equal(ids, (await Outbox().ListAsync(AllStates)).Select(e => e.Id));

        var claimedOrder = new List<Guid>();
        while (await Outbox().ClaimNextDueAsync() is { } claimed)
            claimedOrder.Add(claimed.Id);

        Assert.Equal(ids, claimedOrder);
    }

    [Fact]
    public async Task RecoverInFlight_MakesThemUnknown_AndUnknownIsClaimedAgainWithItsKey()
    {
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        await Outbox().ClaimNextDueAsync();

        // "Restart": a new instance over the same store.
        var restarted = new PersistenceXeroOutbox(_persistence, null, _clock);
        Assert.Equal(1, await restarted.RecoverInFlightAsync());

        var recovered = await restarted.FindAsync(entry.Id);
        Assert.Equal(XeroOutboxState.Unknown, recovered!.State);

        var reclaimed = await restarted.ClaimNextDueAsync();
        Assert.Equal(entry.IdempotencyKey, reclaimed!.IdempotencyKey);
        Assert.Equal(2, reclaimed.Attempts);
    }

    [Fact]
    public async Task ResumeAfterAuthorisation_ReturnsWaitingEntriesToPending()
    {
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(entry.Id, XeroOutboxState.WaitingForAuthorisation, "Missing scope.");

        Assert.Equal(1, await Outbox().ResumeAfterAuthorisationAsync());
        Assert.Equal(XeroOutboxState.Pending, (await Outbox().FindAsync(entry.Id))!.State);
        Assert.Equal(0, await Outbox().ResumeAfterAuthorisationAsync());
    }

    [Fact]
    public async Task Retry_MovesOnlyAFailedEntryBackToPending_KeepingItsKey()
    {
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        Assert.False(await Outbox().RetryAsync(entry.Id));
        Assert.False(await Outbox().RetryAsync(Guid.NewGuid()));

        await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(entry.Id, XeroOutboxState.Failed, "Number already used in Xero.");

        Assert.True(await Outbox().RetryAsync(entry.Id));

        var retried = await Outbox().FindAsync(entry.Id);
        Assert.Equal(XeroOutboxState.Pending, retried!.State);
        Assert.Equal(entry.IdempotencyKey, retried.IdempotencyKey);
        Assert.Equal(1, retried.Attempts);
        Assert.False(await Outbox().RetryAsync(entry.Id));
    }

    [Fact]
    public async Task RecordOutcome_OnlyForAClaimedEntry_AndOnlyAnOutcomeState()
    {
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Outbox().RecordOutcomeAsync(entry.Id, XeroOutboxState.Succeeded));
        await Assert.ThrowsAsync<ArgumentException>(() => Outbox().RecordOutcomeAsync(entry.Id, XeroOutboxState.Superseded));
        await Assert.ThrowsAsync<ArgumentException>(() => Outbox().RecordOutcomeAsync(entry.Id, XeroOutboxState.InFlight));
        Assert.Null(await Outbox().RecordOutcomeAsync(Guid.NewGuid(), XeroOutboxState.Succeeded));

        await Outbox().ClaimNextDueAsync();
        var failed = await Outbox().RecordOutcomeAsync(entry.Id, XeroOutboxState.Failed, "Why.");
        Assert.Equal("Why.", failed!.LastError);
    }

    [Fact]
    public async Task List_FiltersByState_OldestFirst()
    {
        var a = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        _clock.Advance(TimeSpan.FromMinutes(1));
        var b = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(2), "h1");
        await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(a.Id, XeroOutboxState.Failed, "x");

        Assert.Equal([b.Id], (await Outbox().ListAsync([XeroOutboxState.Pending])).Select(e => e.Id));
        Assert.Equal([a.Id], (await Outbox().ListAsync([XeroOutboxState.Failed, XeroOutboxState.Unknown])).Select(e => e.Id));
        Assert.Equal([a.Id, b.Id], (await Outbox().ListAsync(AllStates)).Select(e => e.Id));
    }

    [Fact]
    public async Task VersionOneEntry_WithUnknownProperties_IsRead_AndTheyAreKeptOnRewrite()
    {
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var key = entry.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json["AddedLaterInVersionOne"] = "kept";
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, json.ToJsonString());

        Assert.Equal(entry, await Outbox().FindAsync(entry.Id));

        await Outbox().ClaimNextDueAsync();

        var rewritten = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!;
        Assert.Equal("InFlight", (string)rewritten["State"]!);
        Assert.Equal("kept", (string)rewritten["AddedLaterInVersionOne"]!);
    }

    [Fact]
    public async Task NewerVersionEntry_IsListed_ButNeverClaimedRetriedSupersededOrRewritten_AndHoldsItsDocument()
    {
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var key = entry.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json["SchemaVersion"] = 2;
        json["State"] = "Failed";
        var newer = json.ToJsonString();
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, newer);

        Assert.Equal(2, (await Outbox().ListForDocumentAsync(Quote(1))).Single().SchemaVersion);
        Assert.False(await Outbox().RetryAsync(entry.Id));

        var next = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2");
        Assert.Null(await Outbox().ClaimNextDueAsync());
        Assert.Equal(0, await Outbox().ResumeAfterAuthorisationAsync());
        Assert.Equal(newer, _persistence.Raw(PersistenceXeroOutbox.Collection, key));
        Assert.Equal(XeroOutboxState.Pending, (await Outbox().FindAsync(next.Id))!.State);
    }

    [Fact]
    public async Task UnreadableEntry_IsLeftInPlace_AndOutOfListings()
    {
        var garbageKey = Guid.NewGuid().ToString("D");
        _persistence.Seed(PersistenceXeroOutbox.Collection, garbageKey, "{ not json");
        _persistence.Seed(PersistenceXeroOutbox.Collection, "not-a-guid", "{}");
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        Assert.Equal([entry.Id], (await Outbox().ListAsync(AllStates)).Select(e => e.Id));
        Assert.Equal("{ not json", _persistence.Raw(PersistenceXeroOutbox.Collection, garbageKey));
    }

    [Fact]
    public async Task SupersedingACreate_BehindAStatusChange_KeepsTheCreateAhead()
    {
        // Defect 1: create, status change, then a changed create. The
        // replacement must not move behind the status change.
        await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");
        var changed = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2");

        Assert.Equal(changed.Id, (await Outbox().ClaimNextDueAsync())!.Id);
        Assert.Null(await Outbox().ClaimNextDueAsync());
        await Outbox().RecordOutcomeAsync(changed.Id, XeroOutboxState.Succeeded);
        Assert.Equal(status.Id, (await Outbox().ClaimNextDueAsync())!.Id);

        Assert.Equal(
            [XeroOperation.PushQuote, XeroOperation.PushQuote, XeroOperation.SetQuoteStatus],
            (await Outbox().ListForDocumentAsync(Quote(1))).Select(e => e.Operation));
    }

    [Fact]
    public async Task SupersedingAFailedCreate_BehindAStatusChange_UnblocksTheDocumentInOrder()
    {
        var create = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(create.Id, XeroOutboxState.Failed, "No contact link.");
        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");

        var fixedCreate = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2");

        Assert.Equal(fixedCreate.Id, (await Outbox().ClaimNextDueAsync())!.Id);
        await Outbox().RecordOutcomeAsync(fixedCreate.Id, XeroOutboxState.Succeeded);
        Assert.Equal(status.Id, (await Outbox().ClaimNextDueAsync())!.Id);
    }

    [Fact]
    public async Task SupersedeInterruptedByACrash_SendsTheOldContentFirst_ThenTheReplacement()
    {
        // The replacement shares its predecessor's place; if a crash leaves
        // both pending at the same instant, the old content still goes first.
        var old = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var oldJson = _persistence.Raw(PersistenceXeroOutbox.Collection, old.Id.ToString("D"))!;
        var replacement = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2");
        _persistence.Seed(PersistenceXeroOutbox.Collection, old.Id.ToString("D"), oldJson);

        Assert.True(replacement.EnqueuedAtUtc > old.EnqueuedAtUtc);
        Assert.Equal(old.Id, (await Outbox().ClaimNextDueAsync())!.Id);
        await Outbox().RecordOutcomeAsync(old.Id, XeroOutboxState.Succeeded);
        Assert.Equal(replacement.Id, (await Outbox().ClaimNextDueAsync())!.Id);
    }

    [Fact]
    public async Task EntryWithNoSequence_DoesNotMakeTheNextOneOverflow_OrJumpTheQueue()
    {
        // Defect 2: a stored entry without `Sequence` must not read as
        // long.MaxValue, from which the next sequence wraps to long.MinValue.
        var create = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var key = create.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json.Remove("Sequence");
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, json.ToJsonString());

        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");

        var stored = (long)JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, status.Id.ToString("D"))!)!["Sequence"]!;
        Assert.True(stored > 0, $"Sequence {stored}");
        Assert.Equal(create.Id, (await Outbox().ClaimNextDueAsync())!.Id);
        Assert.Null(await Outbox().ClaimNextDueAsync());
    }

    [Fact]
    public async Task EntriesQueuedAtTheSameInstant_AreStampedInQueueOrder()
    {
        // Time order is queue order, so an entry whose Sequence is lost is
        // still placed unambiguously by its time.
        var create = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");
        var other = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(2), "h1");

        Assert.Equal(Start, create.EnqueuedAtUtc);
        Assert.True(create.EnqueuedAtUtc < status.EnqueuedAtUtc && status.EnqueuedAtUtc < other.EnqueuedAtUtc);
    }

    [Fact]
    public async Task LaterEntryWithNoSequence_DoesNotJumpAheadOfTheCreate()
    {
        // Re-verify defect 1: an unsequenced entry queued after the create
        // must not read as sequence 0 and be sent ahead of it.
        var create = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        _clock.Advance(TimeSpan.FromSeconds(1));
        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");
        var key = status.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json.Remove("Sequence");
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, json.ToJsonString());

        Assert.Equal([create.Id, status.Id], (await Outbox().ListForDocumentAsync(Quote(1))).Select(e => e.Id));
        Assert.Equal(create.Id, (await Outbox().ClaimNextDueAsync())!.Id);
        Assert.Null(await Outbox().ClaimNextDueAsync());
        await Outbox().RecordOutcomeAsync(create.Id, XeroOutboxState.Succeeded);
        Assert.Equal(status.Id, (await Outbox().ClaimNextDueAsync())!.Id);
    }

    [Fact]
    public async Task EntryWithNoSequence_QueuedBeforeASupersedingCreate_StillWaitsForThatCreate()
    {
        // Re-verify round 3: the replacement create inherits sequence 1 but a
        // later time; the status change, stripped of its sequence, must still
        // be filled in after it, never level with the superseded original.
        await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        _clock.Advance(TimeSpan.FromSeconds(1));
        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");
        _clock.Advance(TimeSpan.FromSeconds(1));
        var replacement = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2");
        var key = status.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json.Remove("Sequence");
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, json.ToJsonString());

        Assert.Equal(replacement.Id, (await Outbox().ClaimNextDueAsync())!.Id);
        Assert.Null(await Outbox().ClaimNextDueAsync());
        await Outbox().RecordOutcomeAsync(replacement.Id, XeroOutboxState.Succeeded);
        Assert.Equal(status.Id, (await Outbox().ClaimNextDueAsync())!.Id);
    }

    [Fact]
    public async Task EntryWithNoSequence_IsRewrittenWithoutOne_SoARetryCannotMoveItAhead()
    {
        // A rewrite (here Failed -> Retry) of an unsequenced entry must not
        // freeze it at sequence 0, ahead of a create queued before it.
        var other = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(2), "h0");
        _clock.Advance(TimeSpan.FromSeconds(1));
        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");
        var key = status.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json.Remove("Sequence");
        json["State"] = "Failed";
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, json.ToJsonString());

        Assert.True(await Outbox().RetryAsync(status.Id));
        Assert.False(JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject().ContainsKey("Sequence"));

        // The entry queued before the unsequenced status change stays ahead
        // of it after the rewrite: its place is still worked out from its
        // time, not frozen at 0.
        Assert.Equal([other.Id, status.Id], (await Outbox().ListAsync(AllStates)).Select(e => e.Id));
    }

    [Fact]
    public async Task NewerVersionEntry_OfAnUnknownDocumentKind_IsListed_ButHoldsNoRealDocument()
    {
        // Re-verify defect 2: a newer TempestOS's entry for a kind of record
        // this build does not know must still be listed (for the badge), and
        // an operation it does not know must not read as PushQuote.
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var key = entry.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json["SchemaVersion"] = 2;
        json["Operation"] = "PushCreditNote";
        json["Document"]!["Kind"] = "CreditNote";
        var newer = json.ToJsonString();
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, newer);

        var listed = Assert.Single(await Outbox().ListAsync(AllStates));
        Assert.Equal(entry.Id, listed.Id);
        Assert.Equal(2, listed.SchemaVersion);
        Assert.Equal(PersistenceXeroOutbox.UnknownOperation, listed.Operation);
        Assert.False(Enum.IsDefined(listed.Operation));
        Assert.Equal(PersistenceXeroOutbox.UnknownDocumentKind, listed.Document.Kind);
        Assert.False(Enum.IsDefined(listed.Document.Kind));
        Assert.Empty(await Outbox().ListForDocumentAsync(Quote(1)));

        // Holds no real document's queue, and is never claimed or rewritten.
        var create = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2");
        Assert.Equal(create.Id, (await Outbox().ClaimNextDueAsync())!.Id);
        Assert.Null(await Outbox().ClaimNextDueAsync());
        Assert.False(await Outbox().RetryAsync(entry.Id));
        Assert.Equal(newer, _persistence.Raw(PersistenceXeroOutbox.Collection, key));
    }

    [Fact]
    public async Task NewerVersionEntry_WithAnUnknownOperation_IsNotListedAsAPushQuote()
    {
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var key = entry.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json["SchemaVersion"] = 2;
        json["Operation"] = "PushCreditNote";
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, json.ToJsonString());

        var listed = Assert.Single(await Outbox().ListForDocumentAsync(Quote(1)));
        Assert.Equal(PersistenceXeroOutbox.UnknownOperation, listed.Operation);
    }

    [Fact]
    public async Task Enqueue_RefusesADocumentKindThisBuildDoesNotDefine()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Outbox().EnqueueAsync(XeroOperation.PushQuote, new XeroDocumentRef(PersistenceXeroOutbox.UnknownDocumentKind, "k"), "h1"));
        Assert.Empty(await Outbox().ListAsync(AllStates));
    }

    [Fact]
    public async Task EntryAtTheLargestSequence_DoesNotMakeTheNextOneWrap()
    {
        var create = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var key = create.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json["Sequence"] = long.MaxValue;
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, json.ToJsonString());
        _clock.Advance(TimeSpan.FromSeconds(1));

        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");

        Assert.Equal(long.MaxValue, (long)JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, status.Id.ToString("D"))!)!["Sequence"]!);
        Assert.Equal(create.Id, (await Outbox().ClaimNextDueAsync())!.Id);
        Assert.Null(await Outbox().ClaimNextDueAsync());
    }

    [Theory]
    [InlineData("Operation", "PushCreditNote")]
    [InlineData("State", "Parked")]
    public async Task NewerVersionEntry_ThisBuildCannotDeserialize_IsListedAsNewer_AndStillHoldsItsDocument(string property, string unknownValue)
    {
        // Defect 3: an unknown enum value in a newer entry must not make it
        // vanish (and stop holding its document's queue).
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var key = entry.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json["SchemaVersion"] = 2;
        json[property] = unknownValue;
        var newer = json.ToJsonString();
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, newer);

        var listed = Assert.Single(await Outbox().ListForDocumentAsync(Quote(1)));
        Assert.Equal(entry.Id, listed.Id);
        Assert.Equal(2, listed.SchemaVersion);
        Assert.Equal(2, (await Outbox().FindAsync(entry.Id))!.SchemaVersion);
        Assert.False(await Outbox().RetryAsync(entry.Id));

        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");
        Assert.Null(await Outbox().ClaimNextDueAsync());
        Assert.Equal(XeroOutboxState.Pending, (await Outbox().FindAsync(status.Id))!.State);
        Assert.Equal(newer, _persistence.Raw(PersistenceXeroOutbox.Collection, key));
    }

    [Fact]
    public async Task CorruptEntryOfThisVersion_IsListedAsFailed_AndHoldsItsDocument_Untouched()
    {
        // Defect 4: a corrupt entry must not let later work for its
        // document run ahead of it.
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var key = entry.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json["State"] = "Parked";
        json.Remove("IdempotencyKey");
        var corrupt = json.ToJsonString();
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, corrupt);

        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");
        var other = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(2), "h1");

        Assert.Equal(other.Id, (await Outbox().ClaimNextDueAsync())!.Id);
        Assert.Null(await Outbox().ClaimNextDueAsync());

        var listed = Assert.Single(await Outbox().ListAsync([XeroOutboxState.Failed]));
        Assert.Equal(entry.Id, listed.Id);
        Assert.Equal(Quote(1), listed.Document);
        Assert.Equal(PersistenceXeroOutbox.UnreadableError, listed.LastError);

        Assert.False(await Outbox().RetryAsync(entry.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Outbox().RecordOutcomeAsync(entry.Id, XeroOutboxState.Succeeded));
        await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2");
        Assert.Equal(1, await Outbox().RecoverInFlightAsync());
        Assert.Equal(0, await Outbox().ResumeAfterAuthorisationAsync());
        Assert.Equal(corrupt, _persistence.Raw(PersistenceXeroOutbox.Collection, key));
        Assert.Equal(XeroOutboxState.Pending, (await Outbox().FindAsync(status.Id))!.State);
    }

    [Fact]
    public async Task CorruptEntry_ThatSaysItSucceeded_DoesNotHoldItsDocument()
    {
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        await SucceedNextAsync();
        var key = entry.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json["Id"] = Guid.NewGuid().ToString("D");
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, json.ToJsonString());

        var status = await Outbox().EnqueueAsync(XeroOperation.SetQuoteStatus, Quote(1), "s1", "SENT");

        Assert.Equal(status.Id, (await Outbox().ClaimNextDueAsync())!.Id);
    }

    [Fact]
    public async Task ConcurrentIdenticalEnqueues_QueueOneEntry()
    {
        var outboxes = Enumerable.Range(0, 4).Select(_ => Outbox()).ToArray();

        var entries = await Task.WhenAll(Enumerable.Range(0, 24)
            .Select(i => Task.Run(() => outboxes[i % outboxes.Length].EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1"))));

        Assert.Single(entries.Select(e => e.Id).Distinct());
        Assert.Single(await Outbox().ListAsync(AllStates));
    }

    [Fact]
    public async Task ConcurrentChangedEnqueues_LeaveExactlyOnePending()
    {
        await Task.WhenAll(Enumerable.Range(0, 24)
            .Select(i => Task.Run(() => Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), $"h{i}"))));

        var entries = await Outbox().ListForDocumentAsync(Quote(1));

        Assert.Equal(24, entries.Count);
        Assert.Single(entries, e => e.State == XeroOutboxState.Pending);
        Assert.Equal(XeroOutboxState.Pending, entries[^1].State);
    }

    [Fact]
    public async Task ConcurrentClaims_NeverHandOutTheSameEntryTwice()
    {
        for (var i = 0; i < 10; i++)
            await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(i), "h1");

        var claims = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => Task.Run(() => Outbox().ClaimNextDueAsync())));

        var claimed = claims.Where(c => c is not null).Select(c => c!.Id).ToList();
        Assert.Equal(10, claimed.Count);
        Assert.Equal(10, claimed.Distinct().Count());
    }

    [Fact]
    public async Task BadArguments_AreRefused()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Outbox().EnqueueAsync(XeroOperation.PushQuote, null!, "h1"));
        await Assert.ThrowsAsync<ArgumentException>(() => Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), ""));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Outbox().EnqueueAsync((XeroOperation)999, Quote(1), "h1"));
    }

    private async Task SucceedNextAsync()
    {
        var claimed = await Outbox().ClaimNextDueAsync();
        await Outbox().RecordOutcomeAsync(claimed!.Id, XeroOutboxState.Succeeded);
    }
}
