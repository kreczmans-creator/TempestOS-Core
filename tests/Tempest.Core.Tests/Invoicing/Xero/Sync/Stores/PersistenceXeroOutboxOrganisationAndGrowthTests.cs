using System.Diagnostics;
using System.Text.Json.Nodes;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Invoicing.Connectors;
using static Tempest.Core.Tests.Invoicing.Xero.Sync.Stores.StoresFixtures;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

/// <summary>
/// `v0.24.0` F1 (review board M1, M9): the outbox knows which organisation an
/// entry succeeded in and de-duplicates only against open entries or entries
/// that succeeded in the organisation connected now; open writes queued for
/// another organisation are superseded on a switch; finished entries are
/// pruned after the retention, keeping what de-duplication and idempotency
/// keys need; and enqueue, claim and the badge's list never read every entry.
/// </summary>
public sealed class PersistenceXeroOutboxOrganisationAndGrowthTests
{
    private readonly CountingStore _persistence = new();
    private readonly SteppingClock _clock = new(Start);
    private readonly InMemorySecretStore _secrets = new();

    private PersistenceXeroOutbox Outbox() => new(_persistence, null, _clock, _secrets);

    private Task ConnectAsync(string tenantId) => _secrets.SetAsync(XeroContactLinker.TenantIdSecretKey, tenantId);

    private async Task<XeroOutboxEntry> SucceedNextAsync(string tenantId)
    {
        var claimed = await Outbox().ClaimNextDueForTenantAsync([], tenantId);
        Assert.NotNull(claimed);
        return (await Outbox().RecordOutcomeAsync(claimed.Id, XeroOutboxState.Succeeded))!;
    }

    // ------------------------------------------------------------------ M1

    [Fact]
    public async Task AClaim_RecordsTheOrganisation_AndASucceededEntryKeepsIt()
    {
        await ConnectAsync(DemoTenant);
        await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        var succeeded = await SucceedNextAsync(DemoTenant);

        Assert.Equal(DemoTenant, succeeded.TenantId);
        Assert.Equal(DemoTenant, (await Outbox().FindAsync(succeeded.Id))!.TenantId);
        Assert.Equal(DemoTenant, JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, succeeded.Id.ToString("D"))!)!["TenantId"]!.GetValue<string>());

        // Read back from the store by a fresh process, too.
        PersistenceXeroOutbox.ForgetIndex(_persistence);
        Assert.Equal(DemoTenant, (await Outbox().FindAsync(succeeded.Id))!.TenantId);
    }

    [Fact]
    public async Task UnchangedContent_SentToAnotherOrganisation_IsQueuedAgainHere_WithANewKey()
    {
        await ConnectAsync(DemoTenant);
        var demo = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        await SucceedNextAsync(DemoTenant);

        // Still the Demo Company: already sent, not queued twice.
        Assert.Equal(demo.Id, (await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1")).Id);

        await ConnectAsync(LiveTenant);
        var live = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        Assert.NotEqual(demo.Id, live.Id);
        Assert.Equal(XeroOutboxState.Pending, live.State);
        Assert.NotEqual(demo.IdempotencyKey, live.IdempotencyKey);

        // Open: de-duplicated whatever the organisation.
        Assert.Equal(live.Id, (await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1")).Id);

        var sentHere = await SucceedNextAsync(LiveTenant);
        Assert.Equal(live.Id, sentHere.Id);
        Assert.Equal(live.Id, (await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1")).Id);

        // Back to the Demo Company: its own succeeded entry counts again.
        await ConnectAsync(DemoTenant);
        Assert.Equal(demo.Id, (await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1")).Id);
    }

    [Fact]
    public async Task WithNoOrganisationConnected_OnlyOpenEntriesDeduplicate()
    {
        await ConnectAsync(DemoTenant);
        var demo = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        await SucceedNextAsync(DemoTenant);

        await _secrets.RemoveAsync(XeroContactLinker.TenantIdSecretKey);
        var queued = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        Assert.NotEqual(demo.Id, queued.Id);
        Assert.Equal(queued.Id, (await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1")).Id);
    }

    [Fact]
    public async Task WithoutASecretStore_EverySucceededEntryCounts_AsBeforeF1()
    {
        var outbox = new PersistenceXeroOutbox(_persistence, null, _clock);
        var first = await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var claimed = await outbox.ClaimNextDueForTenantAsync([], DemoTenant);
        await outbox.RecordOutcomeAsync(claimed!.Id, XeroOutboxState.Succeeded);

        Assert.Equal(first.Id, (await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1")).Id);
    }

    [Fact]
    public async Task OnAnOrganisationSwitch_OpenWritesNotSentThere_AreSuperseded_WithTheReason()
    {
        await ConnectAsync(DemoTenant);
        var outbox = Outbox();
        var pending = await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var failed = await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(2), "h2");
        var unknown = await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(3), "h3");
        var waiting = await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(4), "h4");
        var done = await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(5), "h5");
        var sentToLive = await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(6), "h6");

        async Task Claim(XeroOutboxEntry entry, string tenantId, XeroOutboxState outcome)
        {
            // Claim exactly this one: every other head is held back for a moment.
            var claimed = await outbox.ClaimNextDueForTenantAsync([], tenantId);
            while (claimed!.Id != entry.Id)
            {
                await outbox.RecordOutcomeAsync(claimed.Id, XeroOutboxState.Pending, notBeforeUtc: Start.AddYears(1));
                claimed = await outbox.ClaimNextDueForTenantAsync([], tenantId);
            }

            await outbox.RecordOutcomeAsync(claimed.Id, outcome, outcome == XeroOutboxState.Failed ? "blocked: contact" : null);
        }

        await Claim(failed, DemoTenant, XeroOutboxState.Failed);
        _clock.Advance(TimeSpan.FromHours(2));
        await Claim(unknown, DemoTenant, XeroOutboxState.Unknown);
        _clock.Advance(TimeSpan.FromHours(2));
        await Claim(waiting, DemoTenant, XeroOutboxState.WaitingForAuthorisation);
        _clock.Advance(TimeSpan.FromHours(2));
        await Claim(done, DemoTenant, XeroOutboxState.Succeeded);
        _clock.Advance(TimeSpan.FromHours(2));
        await Claim(sentToLive, LiveTenant, XeroOutboxState.Unknown); // already being recovered in the new organisation

        var superseded = await outbox.SupersedeOpenForOtherOrganisationsAsync(LiveTenant, "queued for another organisation");

        Assert.Equal(new[] { pending.Id, failed.Id, unknown.Id, waiting.Id }.Order(), superseded.Order());
        foreach (var id in superseded)
        {
            var entry = (await outbox.FindAsync(id))!;
            Assert.Equal(XeroOutboxState.Superseded, entry.State);
            Assert.Equal("queued for another organisation", entry.LastError);
        }

        Assert.Equal(XeroOutboxState.Succeeded, (await outbox.FindAsync(done.Id))!.State);
        Assert.Equal(XeroOutboxState.Unknown, (await outbox.FindAsync(sentToLive.Id))!.State);
    }

    [Fact]
    public async Task OnAnOrganisationSwitch_ANewerTempestOSEntry_IsLeftAlone()
    {
        await ConnectAsync(DemoTenant);
        var entry = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");
        var key = entry.Id.ToString("D");
        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroOutbox.Collection, key)!)!.AsObject();
        json["SchemaVersion"] = XeroOutboxEntry.CurrentSchemaVersion + 1;
        _persistence.Seed(PersistenceXeroOutbox.Collection, key, json.ToJsonString());
        PersistenceXeroOutbox.ForgetIndex(_persistence);

        Assert.Empty(await Outbox().SupersedeOpenForOtherOrganisationsAsync(LiveTenant, "switched"));
        Assert.Equal(json.ToJsonString(), _persistence.Raw(PersistenceXeroOutbox.Collection, key));
    }

    // ------------------------------------------------------------------ M9: pruning

    [Fact]
    public async Task Prune_RemovesOldFinishedEntries_KeepsOpenOnes_TheNewestFinished_AndTheNewestSucceededPerOrganisation()
    {
        var outbox = Outbox();
        await ConnectAsync(DemoTenant);

        // Content h1 → h2 → h1 → h2 in the Demo Company, then h2 in the live organisation.
        var ids = new List<Guid>();
        foreach (var hash in new[] { "h1", "h2", "h1" })
        {
            ids.Add((await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(1), hash)).Id);
            await SucceedNextAsync(DemoTenant);
            _clock.Advance(TimeSpan.FromDays(1));
        }

        var demoNewest = (await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2")).Id;
        await SucceedNextAsync(DemoTenant);
        await ConnectAsync(LiveTenant);
        _clock.Advance(TimeSpan.FromDays(1));
        var liveNewest = (await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2")).Id;
        await SucceedNextAsync(LiveTenant);

        // A replaced write and an open one for another document.
        var replaced = (await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(2), "a")).Id;
        await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(2), "b");
        var stillPending = (await outbox.ListForDocumentAsync(Quote(2))).Single(e => e.State == XeroOutboxState.Pending).Id;

        _clock.Advance(TimeSpan.FromDays(40));
        var removed = await outbox.PruneAsync(TimeSpan.FromDays(30));

        Assert.Equal(3, removed); // the three oldest Demo entries; the replaced write is its group's newest finished entry
        var left = (await outbox.ListAsync([])).Select(e => e.Id).ToHashSet();
        Assert.DoesNotContain(ids[0], left);
        Assert.DoesNotContain(ids[1], left);
        Assert.DoesNotContain(ids[2], left);
        Assert.Contains(demoNewest, left);
        Assert.Contains(liveNewest, left);
        Assert.Contains(replaced, left);
        Assert.Contains(stillPending, left);
        Assert.Null(_persistence.Raw(PersistenceXeroOutbox.Collection, ids[0].ToString("D")));

        // De-duplication still holds in both organisations.
        Assert.Equal(liveNewest, (await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2")).Id);
        await ConnectAsync(DemoTenant);
        Assert.Equal(demoNewest, (await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h2")).Id);
    }

    [Fact]
    public async Task Prune_CarriesTheRemovedEntriesCounts_SoAnIdempotencyKeyIsNeverReused()
    {
        var outbox = Outbox();
        await ConnectAsync(DemoTenant);
        var keys = new List<string>();
        foreach (var hash in new[] { "h1", "h2", "h1", "h2" })
        {
            keys.Add((await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(1), hash)).IdempotencyKey);
            await SucceedNextAsync(DemoTenant);
            _clock.Advance(TimeSpan.FromDays(1));
        }

        _clock.Advance(TimeSpan.FromDays(60));
        Assert.Equal(3, await outbox.PruneAsync(TimeSpan.FromDays(30)));
        // Pruning again finds nothing more to remove, and keeps the counts.
        Assert.Equal(0, await outbox.PruneAsync(TimeSpan.FromDays(30)));

        // A restart reads the counts back from the store.
        PersistenceXeroOutbox.ForgetIndex(_persistence);
        var again = await Outbox().EnqueueAsync(XeroOperation.PushQuote, Quote(1), "h1");

        Assert.DoesNotContain(again.IdempotencyKey, keys);
        Assert.Equal(XeroIdempotencyKey.Create(Quote(1), XeroOperation.PushQuote, "h1", null, 2), again.IdempotencyKey);
    }

    [Fact]
    public async Task Prune_KeepsEverythingYoungerThanTheRetention()
    {
        var outbox = Outbox();
        await ConnectAsync(DemoTenant);
        foreach (var hash in new[] { "h1", "h2", "h3" })
        {
            await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(1), hash);
            await SucceedNextAsync(DemoTenant);
        }

        _clock.Advance(TimeSpan.FromDays(29));
        Assert.Equal(0, await outbox.PruneAsync(TimeSpan.FromDays(30)));
        Assert.Equal(3, (await outbox.ListAsync([])).Count);
    }

    // ------------------------------------------------------------------ M9: index

    [Fact]
    public async Task WithThousandsOfEntries_EnqueueClaimAndTheBadge_NeverReadEveryEntry()
    {
        const int Documents = 2_500;
        await ConnectAsync(DemoTenant);
        var outbox = Outbox();

        // 2,500 documents, each sent once (Succeeded) and changed once (Pending): 5,000 entries.
        for (var n = 1; n <= Documents; n++)
            await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(n), "v1");

        for (var n = 1; n <= Documents; n++)
            await SucceedNextAsync(DemoTenant);

        for (var n = 1; n <= Documents; n++)
            await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(n), "v2");

        Assert.Equal(2 * Documents, (await outbox.ListAsync([])).Count);

        // From here on: no store read, no key listing — whatever the outbox holds.
        _persistence.ResetCounts();
        var watch = Stopwatch.StartNew();

        for (var n = 1; n <= 500; n++)
        {
            var badge = await outbox.ListForDocumentAsync(Quote(n));
            Assert.Equal(2, badge.Count);
        }

        for (var n = 1; n <= 500; n++)
            await outbox.EnqueueAsync(XeroOperation.PushQuote, Quote(Documents + n), "new");

        for (var n = 1; n <= 500; n++)
            await SucceedNextAsync(DemoTenant);

        Assert.Equal(Documents - 500 + 500, (await outbox.ListAsync([XeroOutboxState.Pending])).Count);
        watch.Stop();

        Assert.Equal(0, _persistence.Reads);
        Assert.Equal(0, _persistence.KeyListings);
        Assert.Equal(500 + (2 * 500), _persistence.Writes); // one write per enqueue, two per claim-and-outcome

        // Generous on any machine: before F1 each call read every entry (about 60 ms each at 2,000 entries).
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"1,500 outbox calls over {2 * Documents} entries took {watch.Elapsed}.");

        // A new process reads the store once, then never again.
        PersistenceXeroOutbox.ForgetIndex(_persistence);
        _persistence.ResetCounts();
        await Outbox().ListForDocumentAsync(Quote(1));
        await Outbox().ListForDocumentAsync(Quote(2));
        Assert.Equal(1, _persistence.KeyListings);
        Assert.Equal((2 * Documents) + 500, _persistence.Reads);
    }

    /// <summary>An in-memory store that counts reads, key listings and writes.</summary>
    private sealed class CountingStore : IPersistenceStore
    {
        private readonly YieldingInMemoryPersistenceStore _inner = new();

        public int Reads { get; private set; }

        public int KeyListings { get; private set; }

        public int Writes { get; private set; }

        public void ResetCounts() => (Reads, KeyListings, Writes) = (0, 0, 0);

        public string? Raw(string collection, string key) => _inner.Raw(collection, key);

        public void Seed(string collection, string key, string value) => _inner.Seed(collection, key, value);

        public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default)
        {
            Reads++;
            return _inner.ReadAsync(collection, key, cancellationToken);
        }

        public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
        {
            Writes++;
            return _inner.WriteAsync(collection, key, value, cancellationToken);
        }

        public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(collection, key, cancellationToken);

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default)
        {
            KeyListings++;
            return _inner.ListKeysAsync(collection, cancellationToken);
        }
    }
}
