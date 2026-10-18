using System.Text.Json.Nodes;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Persistence;
using static Tempest.Core.Tests.Invoicing.Xero.Sync.Stores.StoresFixtures;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

/// <summary>`v0.24.0` B2 (design §5, §11): the link store — round-trip, schema versioning, Xero-id immutability, tenant isolation, concurrency.</summary>
public sealed class PersistenceXeroLinkStoreTests
{
    private readonly YieldingInMemoryPersistenceStore _persistence = new();

    private PersistenceXeroLinkStore Store() => new(_persistence);

    [Fact]
    public async Task SavedLink_RoundTrips_FieldForField_UnderTheDocumentedKey()
    {
        var link = Link(DemoTenant, Quote(1));

        await Store().SaveAsync(link);

        Assert.Equal(link, await Store().FindAsync(DemoTenant, Quote(1)));
        Assert.NotNull(_persistence.Raw("Xero.Links", $"{DemoTenant}/Quote/{Quote(1).TempestKey}"));
    }

    [Fact]
    public async Task StoredJson_NamesEnumsAndCarriesTheSchemaVersion()
    {
        await Store().SaveAsync(Link(DemoTenant, Quote(1)));

        var json = JsonNode.Parse(_persistence.Raw(PersistenceXeroLinkStore.Collection, PersistenceXeroLinkStore.KeyFor(DemoTenant, Quote(1)))!)!;

        Assert.Equal(1, (int)json["SchemaVersion"]!);
        Assert.Equal("Quote", (string)json["Document"]!["Kind"]!);
    }

    [Fact]
    public async Task MissingLink_ReadsAsNull()
    {
        Assert.Null(await Store().FindAsync(DemoTenant, Quote(1)));
        Assert.Empty(await Store().ListAsync(DemoTenant));
    }

    [Fact]
    public async Task VersionOneRecord_WithUnknownProperties_IsRead_AndTheyAreKeptOnRewrite()
    {
        var key = PersistenceXeroLinkStore.KeyFor(DemoTenant, Quote(1));
        _persistence.Seed(PersistenceXeroLinkStore.Collection, key, $$"""
            {
              "SchemaVersion": 1,
              "TenantId": "{{DemoTenant}}",
              "Document": { "Kind": "Quote", "TempestKey": "{{Quote(1).TempestKey}}", "Future": true },
              "XeroId": "x-1",
              "XeroNumber": "QU-0042",
              "LinkedAtUtc": "2026-10-02T09:00:00+00:00",
              "LinkedBy": "created",
              "AddedLaterInVersionOne": { "nested": [1, 2, 3] }
            }
            """);

        var link = await Store().FindAsync(DemoTenant, Quote(1));

        Assert.NotNull(link);
        Assert.Equal("x-1", link.XeroId);
        Assert.Equal("QU-0042", link.XeroNumber);
        Assert.Null(link.LastPushedContentHash);
        Assert.Null(link.LastReadAtUtc);

        await Store().SaveAsync(link with { LastKnownXeroStatus = "SENT" });

        var rewritten = JsonNode.Parse(_persistence.Raw(PersistenceXeroLinkStore.Collection, key)!)!;
        Assert.Equal("SENT", (string)rewritten["LastKnownXeroStatus"]!);
        Assert.NotNull(rewritten["AddedLaterInVersionOne"]);
    }

    [Fact]
    public async Task NewerVersionRecord_IsReturned_FlaggedNewer_AndNeverOverwritten()
    {
        var key = PersistenceXeroLinkStore.KeyFor(DemoTenant, Quote(1));
        var original = $$"""
            { "SchemaVersion": 2, "TenantId": "{{DemoTenant}}",
              "Document": { "Kind": "Quote", "TempestKey": "{{Quote(1).TempestKey}}" },
              "XeroId": "x-1", "LinkedAtUtc": "2026-10-02T09:00:00+00:00", "LinkedBy": "created", "NewShape": 7 }
            """;
        _persistence.Seed(PersistenceXeroLinkStore.Collection, key, original);

        var link = await Store().FindAsync(DemoTenant, Quote(1));

        Assert.NotNull(link);
        Assert.Equal(2, link.SchemaVersion);
        Assert.True(PersistenceXeroLinkStore.IsFromNewerVersion(link));
        Assert.Single(await Store().ListAsync(DemoTenant));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().SaveAsync(Link(DemoTenant, Quote(1), xeroId: "x-1")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().SaveAsync(link));
        Assert.Equal(original, _persistence.Raw(PersistenceXeroLinkStore.Collection, key));
    }

    [Fact]
    public async Task NewerVersionRecord_ThisBuildCannotParse_StillReadsAsLinked_SoNothingIsCreated()
    {
        var key = PersistenceXeroLinkStore.KeyFor(DemoTenant, Quote(1));
        _persistence.Seed(PersistenceXeroLinkStore.Collection, key, """{ "SchemaVersion": 3, "Document": "a new shape" }""");

        var link = await Store().FindAsync(DemoTenant, Quote(1));

        Assert.NotNull(link);
        Assert.Equal(3, link.SchemaVersion);
        Assert.Equal(string.Empty, link.XeroId);
        Assert.Equal(Quote(1), link.Document);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "SchemaVersion": 1 }""")]
    [InlineData("""{ "TenantId": "t" }""")]
    [InlineData("""{ "SchemaVersion": 0, "XeroId": "x" }""")]
    public async Task UnreadableRecordOfAKnownVersion_FailsFind_IsLeftOutOfList_AndIsNotOverwritten(string json)
    {
        var key = PersistenceXeroLinkStore.KeyFor(DemoTenant, Quote(1));
        _persistence.Seed(PersistenceXeroLinkStore.Collection, key, json);
        await Store().SaveAsync(Link(DemoTenant, Quote(2)));

        await Assert.ThrowsAsync<PersistenceException>(() => Store().FindAsync(DemoTenant, Quote(1)));
        Assert.Equal([Quote(2)], (await Store().ListAsync(DemoTenant)).Select(l => l.Document));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().SaveAsync(Link(DemoTenant, Quote(1))));
        Assert.Equal(json, _persistence.Raw(PersistenceXeroLinkStore.Collection, key));
    }

    [Fact]
    public async Task RecordWhoseContentNamesAnotherDocument_IsTreatedAsUnreadable()
    {
        await Store().SaveAsync(Link(DemoTenant, Quote(1)));
        var json = _persistence.Raw(PersistenceXeroLinkStore.Collection, PersistenceXeroLinkStore.KeyFor(DemoTenant, Quote(1)))!;
        _persistence.Seed(PersistenceXeroLinkStore.Collection, PersistenceXeroLinkStore.KeyFor(DemoTenant, Quote(2)), json);

        await Assert.ThrowsAsync<PersistenceException>(() => Store().FindAsync(DemoTenant, Quote(2)));
    }

    [Fact]
    public async Task ChangingALinksXeroId_IsRefused_UnlinkThenLinkIsAllowed()
    {
        await Store().SaveAsync(Link(DemoTenant, Quote(1), xeroId: "x-1"));

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => Store().SaveAsync(Link(DemoTenant, Quote(1), xeroId: "x-2")));
        Assert.Contains("x-1", refused.Message, StringComparison.Ordinal);
        Assert.Equal("x-1", (await Store().FindAsync(DemoTenant, Quote(1)))!.XeroId);

        await Store().UnlinkAsync(DemoTenant, Quote(1));
        Assert.Null(await Store().FindAsync(DemoTenant, Quote(1)));

        await Store().SaveAsync(Link(DemoTenant, Quote(1), xeroId: "x-2"));
        Assert.Equal("x-2", (await Store().FindAsync(DemoTenant, Quote(1)))!.XeroId);
    }

    [Fact]
    public async Task UpdatingEverythingButTheXeroId_IsAllowed()
    {
        await Store().SaveAsync(Link(DemoTenant, Quote(1)));
        var updated = Link(DemoTenant, Quote(1)) with { XeroNumber = "QU-0002", LastKnownXeroStatus = "SENT", LastReadAtUtc = Start.AddHours(1) };

        await Store().SaveAsync(updated);

        Assert.Equal(updated, await Store().FindAsync(DemoTenant, Quote(1)));
    }

    [Fact]
    public async Task Tenants_AreIsolated_InFindListAndUnlink()
    {
        await Store().SaveAsync(Link(DemoTenant, Quote(1), xeroId: "demo-id"));
        await Store().SaveAsync(Link(LiveTenant, Quote(1), xeroId: "live-id"));
        await Store().SaveAsync(Link(DemoTenant, Invoice(1), xeroId: "demo-inv"));

        Assert.Equal("demo-id", (await Store().FindAsync(DemoTenant, Quote(1)))!.XeroId);
        Assert.Equal("live-id", (await Store().FindAsync(LiveTenant, Quote(1)))!.XeroId);
        Assert.Null(await Store().FindAsync(LiveTenant, Invoice(1)));
        Assert.Equal(2, (await Store().ListAsync(DemoTenant)).Count);
        Assert.Equal(["live-id"], (await Store().ListAsync(LiveTenant)).Select(l => l.XeroId));
        Assert.Empty(await Store().ListAsync("another-tenant"));

        await Store().UnlinkAsync(LiveTenant, Quote(1));

        Assert.NotNull(await Store().FindAsync(DemoTenant, Quote(1)));
    }

    [Fact]
    public async Task TenantPrefix_DoesNotMatchALongerTenantId()
    {
        await Store().SaveAsync(Link("tenant-1", Quote(1)));
        await Store().SaveAsync(Link("tenant-10", Quote(2)));

        Assert.Equal([Quote(1)], (await Store().ListAsync("tenant-1")).Select(l => l.Document));
    }

    [Fact]
    public async Task List_FiltersByKind_AndOrdersStably()
    {
        await Store().SaveAsync(Link(DemoTenant, Quote(2)));
        await Store().SaveAsync(Link(DemoTenant, Invoice(1)));
        await Store().SaveAsync(Link(DemoTenant, Quote(1)));
        await Store().SaveAsync(Link(DemoTenant, new XeroDocumentRef(XeroDocumentKind.Contact, "ACME/LTD")));

        Assert.Equal([Quote(1), Quote(2)], (await Store().ListAsync(DemoTenant, XeroDocumentKind.Quote)).Select(l => l.Document));
        Assert.Equal("ACME/LTD", (await Store().ListAsync(DemoTenant, XeroDocumentKind.Contact)).Single().Document.TempestKey);
        Assert.Equal(4, (await Store().ListAsync(DemoTenant)).Count);
    }

    [Fact]
    public async Task ConcurrentSaves_WithDifferentXeroIds_ExactlyOneWins()
    {
        var stores = Enumerable.Range(0, 4).Select(_ => Store()).ToArray();

        var attempts = Enumerable.Range(0, 24)
            .Select(i => Task.Run(async () =>
            {
                try
                {
                    await stores[i % stores.Length].SaveAsync(Link(DemoTenant, Quote(1), xeroId: $"x-{i}"));
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(r => r));
        var winner = (await Store().FindAsync(DemoTenant, Quote(1)))!.XeroId;
        Assert.Equal($"x-{Array.IndexOf(results, true)}", winner);
    }

    [Fact]
    public async Task ConcurrentSaves_OfTheSameXeroId_AllSucceed()
    {
        var saves = Enumerable.Range(0, 24).Select(i => Task.Run(() => Store().SaveAsync(Link(DemoTenant, Quote(1)) with { XeroNumber = $"QU-{i}" })));

        await Task.WhenAll(saves);

        Assert.NotNull(await Store().FindAsync(DemoTenant, Quote(1)));
    }

    [Fact]
    public async Task BadArguments_AreRefused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Store().FindAsync("a/b", Quote(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => Store().FindAsync(" ", Quote(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => Store().SaveAsync(Link(DemoTenant, Quote(1), xeroId: "")));
        await Assert.ThrowsAsync<ArgumentException>(() => Store().SaveAsync(Link(DemoTenant, new XeroDocumentRef(XeroDocumentKind.Quote, ""))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store().SaveAsync(Link(DemoTenant, Quote(1)) with { SchemaVersion = 2 }));
    }
}
