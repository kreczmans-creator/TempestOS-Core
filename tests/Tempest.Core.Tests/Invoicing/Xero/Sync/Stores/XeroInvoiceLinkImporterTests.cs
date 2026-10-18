using Tempest.Core.Invoicing.Xero.Sync;
using static Tempest.Core.Tests.Invoicing.Xero.Sync.Stores.StoresFixtures;
using Legacy = Tempest.Core.Invoicing.Xero.Sync.XeroInvoiceLinkImporter.LegacyInvoiceLink;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

/// <summary>`v0.24.0` B2 (design §5): invoice requests sent to Xero before `v0.24.0` are imported as links, once, without changing anything.</summary>
public sealed class XeroInvoiceLinkImporterTests
{
    private static readonly Guid SentToXero = Guid.Parse("00000001-0000-4000-8000-000000000001");
    private static readonly Guid SentToXeroToo = Guid.Parse("00000002-0000-4000-8000-000000000002");
    private static readonly Guid SentToQuickBooks = Guid.Parse("00000003-0000-4000-8000-000000000003");
    private static readonly Guid DraftNeverSent = Guid.Parse("00000004-0000-4000-8000-000000000004");
    private static readonly Guid XeroButNoExternalId = Guid.Parse("00000005-0000-4000-8000-000000000005");

    private readonly YieldingInMemoryPersistenceStore _persistence = new();
    private readonly SteppingClock _clock = new(Start);
    private readonly List<Legacy> _requests =
    [
        new(SentToXero, "Xero", "inv-guid-1", "INV-0001", "DRAFT", Start.AddDays(-30)),
        new(SentToXeroToo, "Xero", "inv-guid-2", null, null, null),
        new(SentToQuickBooks, "QuickBooksOnline", "qbo-77", "1001", "Open", Start.AddDays(-3)),
        new(DraftNeverSent, null, null, null, null, null),
        new(XeroButNoExternalId, "Xero", "  ", null, null, null),
    ];

    private int _reads;

    private PersistenceXeroLinkStore Links() => new(_persistence);

    private XeroInvoiceLinkImporter Importer() => new(
        _ =>
        {
            _reads++;
            return Task.FromResult<IReadOnlyList<Legacy>>(_requests);
        },
        Links(),
        _clock);

    [Theory]
    [InlineData("xero")]
    [InlineData("XERO")]
    [InlineData("Xero ")]
    public async Task OnlyTheConnectorNamedExactlyXero_IsImported(string connector)
    {
        // Defect 7: the design says Connector == "Xero" (ordinal).
        var request = Guid.Parse("00000009-0000-4000-8000-000000000009");
        _requests.Clear();
        _requests.Add(new(request, connector, "inv-guid-9", null, null, null));

        var report = await Importer().ImportAsync(DemoTenant);

        Assert.Equal(0, report.Imported);
        Assert.Null(await Links().FindAsync(DemoTenant, XeroDocumentRef.For(XeroDocumentKind.Invoice, request)));
    }

    [Fact]
    public async Task EveryXeroSendWithAnExternalId_IsImported_AsAnImportedLink()
    {
        var report = await Importer().ImportAsync(DemoTenant);

        Assert.Equal(2, report.Imported);
        Assert.Equal(0, report.AlreadyLinked);
        Assert.Empty(report.ConflictingRequestIds);

        var links = await Links().ListAsync(DemoTenant, XeroDocumentKind.Invoice);
        Assert.Equal([SentToXero.ToString("D"), SentToXeroToo.ToString("D")], links.Select(l => l.Document.TempestKey));

        var first = links[0];
        Assert.Equal("inv-guid-1", first.XeroId);
        Assert.Equal("INV-0001", first.XeroNumber);
        Assert.Equal("DRAFT", first.LastKnownXeroStatus);
        Assert.Equal(Start.AddDays(-30), first.LinkedAtUtc);
        Assert.Equal("imported", first.LinkedBy);
        Assert.Null(first.LastPushedContentHash);
        Assert.Null(first.LastReadAtUtc);
        Assert.Equal(XeroLink.CurrentSchemaVersion, first.SchemaVersion);

        Assert.Equal(Start, links[1].LinkedAtUtc);
    }

    [Fact]
    public async Task ImportingTwice_WritesNothingTheSecondTime()
    {
        await Importer().ImportAsync(DemoTenant);
        var writesAfterFirst = _persistence.Writes;

        var second = await Importer().ImportAsync(DemoTenant);

        Assert.Equal(0, second.Imported);
        Assert.Equal(2, second.AlreadyLinked);
        Assert.Equal(writesAfterFirst, _persistence.Writes);
    }

    [Fact]
    public async Task AnExistingLink_IsNeverReplaced_AndADifferentOneIsReported()
    {
        var document = XeroDocumentRef.For(XeroDocumentKind.Invoice, SentToXero);
        var created = Link(DemoTenant, document, xeroId: "a-different-invoice") with { LastPushedContentHash = "h" };
        await Links().SaveAsync(created);

        var report = await Importer().ImportAsync(DemoTenant);

        Assert.Equal(1, report.Imported);
        Assert.Equal([SentToXero], report.ConflictingRequestIds);
        Assert.Equal(created, await Links().FindAsync(DemoTenant, document));
    }

    [Fact]
    public async Task ALinkMatchingTheExternalId_CountsAsAlreadyLinked_RegardlessOfCase()
    {
        await Links().SaveAsync(Link(DemoTenant, XeroDocumentRef.For(XeroDocumentKind.Invoice, SentToXero), xeroId: "INV-GUID-1", linkedBy: "reconciled"));

        var report = await Importer().ImportAsync(DemoTenant);

        Assert.Equal(1, report.AlreadyLinked);
        Assert.Equal("reconciled", (await Links().FindAsync(DemoTenant, XeroDocumentRef.For(XeroDocumentKind.Invoice, SentToXero)))!.LinkedBy);
    }

    [Fact]
    public async Task AnUnreadableLink_IsReportedAndLeftAlone()
    {
        var key = PersistenceXeroLinkStore.KeyFor(DemoTenant, XeroDocumentRef.For(XeroDocumentKind.Invoice, SentToXero));
        _persistence.Seed(PersistenceXeroLinkStore.Collection, key, "garbage");

        var report = await Importer().ImportAsync(DemoTenant);

        Assert.Equal([SentToXero], report.ConflictingRequestIds);
        Assert.Equal("garbage", _persistence.Raw(PersistenceXeroLinkStore.Collection, key));
    }

    [Fact]
    public async Task ImportIsPerTenant()
    {
        await Importer().ImportAsync(DemoTenant);

        Assert.Empty(await Links().ListAsync(LiveTenant));

        var live = await Importer().ImportAsync(LiveTenant);
        Assert.Equal(2, live.Imported);
    }

    [Fact]
    public async Task EnsureImported_RunsOncePerTenantPerProcess()
    {
        var importer = Importer();

        var first = await importer.EnsureImportedAsync(DemoTenant);
        var second = await importer.EnsureImportedAsync(DemoTenant);
        var otherTenant = await importer.EnsureImportedAsync(LiveTenant);

        Assert.Equal(2, first!.Imported);
        Assert.Null(second);
        Assert.Equal(2, otherTenant!.Imported);
        Assert.Equal(2, _reads);
    }

    [Fact]
    public async Task EnsureImported_ConcurrentCallers_ImportOnce()
    {
        var importer = Importer();

        var reports = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => importer.EnsureImportedAsync(DemoTenant))));

        Assert.Single(reports, r => r is not null);
        Assert.Equal(1, _reads);
        Assert.Equal(2, (await Links().ListAsync(DemoTenant)).Count);
    }

    [Fact]
    public async Task EnsureImported_AFailedRunIsNotRemembered()
    {
        var fail = true;
        var importer = new XeroInvoiceLinkImporter(
            _ => fail ? throw new InvalidOperationException("Repository unavailable.") : Task.FromResult<IReadOnlyList<Legacy>>(_requests),
            Links(),
            _clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => importer.EnsureImportedAsync(DemoTenant));

        fail = false;
        Assert.Equal(2, (await importer.EnsureImportedAsync(DemoTenant))!.Imported);
    }

    [Fact]
    public async Task ConcurrentImports_FromSeparateImporters_LeaveOneLinkPerRequest()
    {
        var reports = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => Importer().ImportAsync(DemoTenant))));

        // Two importers racing may each write the same link (the store allows
        // rewriting a link with its own Xero id); the outcome is still one
        // link per request, and never a conflict.
        Assert.True(reports.Sum(r => r.Imported) >= 2);
        Assert.All(reports, r => Assert.Empty(r.ConflictingRequestIds));
        Assert.Equal(2, (await Links().ListAsync(DemoTenant)).Count);
    }
}
