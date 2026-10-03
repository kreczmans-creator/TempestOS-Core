using Tempest.Core.Invoicing.Xero.Sync;
using static Tempest.Core.Tests.Invoicing.Xero.Sync.Stores.StoresFixtures;
using Check = Tempest.Core.Invoicing.Xero.Sync.XeroInvoiceLinkImporter.InvoiceCheck;
using Legacy = Tempest.Core.Invoicing.Xero.Sync.XeroInvoiceLinkImporter.LegacyInvoiceLink;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

/// <summary>
/// `v0.24.0` F1 (review board M1): the pre-v0.24 invoice-link import runs
/// only for requests sent before v0.24.0, imports only invoices the
/// organisation holds, and remembers per organisation that it ran.
/// </summary>
public sealed class XeroInvoiceLinkImporterOrganisationTests
{
    private static readonly Guid InDemo = Guid.Parse("00000001-0000-4000-8000-000000000001");
    private static readonly Guid InLive = Guid.Parse("00000002-0000-4000-8000-000000000002");
    private static readonly Guid ThroughSync = Guid.Parse("00000003-0000-4000-8000-000000000003");

    private readonly YieldingInMemoryPersistenceStore _persistence = new();
    private readonly SteppingClock _clock = new(Start);
    private readonly List<Legacy> _requests =
    [
        new(InDemo, "Xero", "demo-inv-1", "INV-0001", "DRAFT", Start.AddDays(-30)),
        new(InLive, "Xero", "live-inv-1", "INV-0002", "DRAFT", Start.AddDays(-20)),
    ];

    /// <summary>Which organisation holds which invoice id; <see langword="null"/> answers "Xero could not be reached".</summary>
    private readonly Dictionary<string, HashSet<string>?> _held = new(StringComparer.Ordinal)
    {
        [DemoTenant] = ["demo-inv-1", "demo-inv-2"],
        [LiveTenant] = ["live-inv-1"],
    };

    private int _asked;

    private PersistenceXeroLinkStore Links() => new(_persistence);

    private XeroInvoiceLinkImporter Importer(int budget = XeroInvoiceLinkImporter.DefaultBudget) => new(
        _ => Task.FromResult<IReadOnlyList<Legacy>>([.. _requests]),
        Links(),
        _clock,
        _persistence,
        (tenant, id, _) =>
        {
            _asked++;
            return Task.FromResult(_held[tenant] is not { } ids ? new Check(null, null, null) : ids.Contains(id) ? new Check(true, "AUTHORISED", "INV-X") : new Check(false, null, null));
        },
        budget);

    private async Task<IReadOnlyList<string>> LinkedAsync(string tenant) =>
        [.. (await Links().ListAsync(tenant, XeroDocumentKind.Invoice)).Select(l => l.XeroId)];

    [Fact]
    public async Task OnlyInvoicesTheOrganisationHolds_AreImported_WithXerosOwnStatus()
    {
        var demo = await Importer().EnsureImportedAsync(DemoTenant);
        var live = await Importer().EnsureImportedAsync(LiveTenant);

        Assert.Equal(["demo-inv-1"], await LinkedAsync(DemoTenant));
        Assert.Equal(["live-inv-1"], await LinkedAsync(LiveTenant));
        Assert.Equal((1, 1, true), (demo!.Imported, demo.NotInOrganisation, demo.Complete));
        Assert.Equal((1, 1, true), (live!.Imported, live.NotInOrganisation, live.Complete));

        var link = (await Links().FindAsync(LiveTenant, XeroDocumentRef.For(XeroDocumentKind.Invoice, InLive)))!;
        Assert.Equal("AUTHORISED", link.LastKnownXeroStatus);
        Assert.Equal("INV-X", link.XeroNumber);
        Assert.Equal(XeroInvoiceLinkImporter.ImportedLinkedBy, link.LinkedBy);
    }

    [Fact]
    public async Task ARequestSentThroughV024_IsNeverImported_IntoAnyOrganisation()
    {
        await Importer().EnsureImportedAsync(DemoTenant); // the first run: v0.24.0's sync is running from now
        _clock.Advance(TimeSpan.FromHours(1));
        _requests.Add(new(ThroughSync, "Xero", "demo-inv-2", "INV-0003", "DRAFT", _clock.GetUtcNow()));

        var live = await Importer().EnsureImportedAsync(LiveTenant);
        var demoAgain = await Importer().ImportAsync(DemoTenant);

        Assert.Equal(1, live!.SentThroughSync);
        Assert.Equal(1, demoAgain.SentThroughSync);
        Assert.DoesNotContain("demo-inv-2", await LinkedAsync(LiveTenant));
        Assert.DoesNotContain("demo-inv-2", await LinkedAsync(DemoTenant));
    }

    [Fact]
    public async Task AFinishedRun_IsRememberedPerOrganisation_AcrossRestarts()
    {
        Assert.NotNull(await Importer().EnsureImportedAsync(DemoTenant));
        Assert.NotNull(await _persistence.ReadAsync(XeroInvoiceLinkImporter.StateCollection, XeroInvoiceLinkImporter.RanKey(DemoTenant)));
        var asked = _asked;

        // A new process: nothing is asked or imported again for the Demo Company.
        Assert.Null(await Importer().EnsureImportedAsync(DemoTenant));
        Assert.Equal(asked, _asked);

        // Another organisation runs its own import.
        Assert.NotNull(await Importer().EnsureImportedAsync(LiveTenant));
    }

    [Fact]
    public async Task WhenXeroCannotAnswer_NothingIsImportedOnAGuess_AndTheNextRunCarriesOn()
    {
        _held[LiveTenant] = null;
        var offline = await Importer().EnsureImportedAsync(LiveTenant);

        Assert.False(offline!.Complete);
        Assert.Empty(await LinkedAsync(LiveTenant));
        Assert.Null(await _persistence.ReadAsync(XeroInvoiceLinkImporter.StateCollection, XeroInvoiceLinkImporter.RanKey(LiveTenant)));

        _held[LiveTenant] = ["live-inv-1"];
        var online = await Importer().EnsureImportedAsync(LiveTenant);

        Assert.True(online!.Complete);
        Assert.Equal(["live-inv-1"], await LinkedAsync(LiveTenant));
    }

    [Fact]
    public async Task ARunAsksAtMostItsBudget_AndAnAbsentInvoiceIsNotAskedAboutTwice()
    {
        var first = await Importer(budget: 1).EnsureImportedAsync(LiveTenant);
        Assert.False(first!.Complete);
        Assert.Equal(1, _asked); // demo-inv-1: not held here, recorded

        var second = await Importer(budget: 1).EnsureImportedAsync(LiveTenant);
        Assert.True(second!.Complete);
        Assert.Equal(2, _asked); // live-inv-1 only
        Assert.Equal(1, second.NotInOrganisation);
        Assert.Equal(["live-inv-1"], await LinkedAsync(LiveTenant));
    }
}
