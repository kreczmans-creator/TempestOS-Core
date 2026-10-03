using System.Globalization;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// `v0.24.0` F1 (review board M1): purchasing's "automatic from" moment and
/// <em>Send to Xero</em> opt-ins are kept per Xero organisation.
/// </summary>
public sealed class XeroPurchasingSyncStateOrganisationTests
{
    private const string Demo = "tenant-demo";
    private const string Live = "tenant-live";
    private static readonly XeroDocumentRef Order = XeroPurchaseOrderPlanner.Ref(Guid.Parse("00000001-0000-4000-8000-0000000000aa"));

    private readonly YieldingInMemoryPersistenceStore _store = new();
    private readonly InMemorySecretStore _secrets = new();
    private readonly SteppingClock _clock = new(StoresFixtures.Start);

    private XeroPurchasingSyncState State() => new(_store, _secrets, timeProvider: _clock);

    private Task ConnectAsync(string tenantId) => _secrets.SetAsync(XeroContactLinker.TenantIdSecretKey, tenantId);

    [Fact]
    public async Task TheFirstOrganisation_StartsWithTheWorkspace_ALaterOne_WhenFirstSeen()
    {
        // Before any organisation is connected, the workspace's own start is recorded.
        var workspaceStart = await State().AutomaticFromAsync();
        Assert.Equal(StoresFixtures.Start, workspaceStart);

        _clock.Advance(TimeSpan.FromHours(1));
        await ConnectAsync(Demo);
        Assert.Equal(workspaceStart, await State().AutomaticFromAsync());

        _clock.Advance(TimeSpan.FromDays(1));
        await ConnectAsync(Live);
        var liveFrom = _clock.GetUtcNow();
        Assert.Equal(liveFrom, await State().AutomaticFromAsync());

        _clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(liveFrom, await State().AutomaticFromAsync());
        Assert.Equal(workspaceStart, await State().AutomaticFromAsync(Demo));
    }

    [Fact]
    public async Task AStartRecordedBeforeF1_IsTakenOverByTheFirstOrganisationOnly()
    {
        var before = StoresFixtures.Start.AddDays(-3);
        await _store.WriteAsync(XeroPurchasingSyncState.StateCollection, XeroPurchasingSyncState.AutomaticFromKey, before.ToString("O", CultureInfo.InvariantCulture));

        await ConnectAsync(Live);
        Assert.Equal(before, await State().AutomaticFromAsync());
        Assert.Equal(Live, await _store.ReadAsync(XeroPurchasingSyncState.StateCollection, XeroPurchasingSyncState.AutomaticFromOwnerKey));

        await ConnectAsync(Demo);
        Assert.Equal(_clock.GetUtcNow(), await State().AutomaticFromAsync());
    }

    [Fact]
    public async Task ASendToXero_CountsOnlyForTheOrganisationItWasMadeFor_AndOneFromBeforeF1_ForTheFirstOnly()
    {
        await ConnectAsync(Demo);
        await State().AutomaticFromAsync(); // Demo is the first organisation

        await State().OptInAsync(Order, "PO-2026-001");
        Assert.True(await State().IsOptedInAsync(Order));
        Assert.NotNull(await _store.ReadAsync(XeroPurchasingSyncState.StateCollection, XeroPurchasingSyncState.OptInKey(Order, Demo)));

        await ConnectAsync(Live);
        await State().AutomaticFromAsync();
        Assert.False(await State().IsOptedInAsync(Order));

        // An opt-in written before F1 (no organisation in its key) belongs to the first organisation.
        await _store.WriteAsync(XeroPurchasingSyncState.StateCollection, XeroPurchasingSyncState.OptInKey(Order), "{}");
        Assert.False(await State().IsOptedInAsync(Order));
        await ConnectAsync(Demo);
        Assert.True(await State().IsOptedInAsync(Order));
    }
}
