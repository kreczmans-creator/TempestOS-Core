using System.Collections.Concurrent;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Secrets;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U3: <see cref="XeroSyncBadgeControl"/> over X6's real engine,
/// link store and outbox (in memory, pinned clock, no network) — every badge
/// state rendered from the local state X6 itself writes, Retry going through
/// <see cref="IXeroOutbox.RetryAsync"/>, and Send again / Send to Xero offered
/// only where they apply.
/// </summary>
public sealed class BadgeControlTests
{
    private static readonly XeroDocumentRef Quote = XeroDocumentRef.For(XeroDocumentKind.Quote, Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly XeroDocumentRef Invoice = XeroDocumentRef.For(XeroDocumentKind.Invoice, Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly XeroDocumentRef Order = XeroDocumentRef.For(XeroDocumentKind.PurchaseOrder, Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static readonly XeroDocumentRef Bill = XeroDocumentRef.For(XeroDocumentKind.ExpenseBill, Guid.Parse("44444444-4444-4444-4444-444444444444"));

    private static async Task<XeroSyncBadgeControl> ShowAsync(IXeroBadgeSource source, XeroDocumentRef document, string reference, bool offerSendToXero = false)
    {
        var badge = new XeroSyncBadgeControl(source, document, reference, offerSendToXero);
        await badge.LoadAsync();
        return badge;
    }

    [AvaloniaFact]
    public async Task NothingQueued_ReadsNotSent_WithNoActions()
    {
        var kit = await BadgeTestKit.CreateAsync();
        var badge = await ShowAsync(kit.Source, Invoice, "INV-1");

        Assert.Equal("Xero: Not sent", badge.Text);
        Assert.False(badge.OffersRetry);
        Assert.False(badge.OffersSendAgain);
        Assert.False(badge.OffersSendToXero);
        Assert.Equal("Xero status for INV-1", AutomationProperties.GetName(badge));
    }

    [AvaloniaFact]
    public async Task Queued_WhilePendingInFlightOrUnknown()
    {
        var kit = await BadgeTestKit.CreateAsync();

        await kit.EntryAsync(Quote, XeroOperation.PushQuote);
        Assert.Equal("Xero: Queued", (await ShowAsync(kit.Source, Quote, "Q-1")).Text);

        var lost = await BadgeTestKit.CreateAsync();
        await lost.EntryAsync(Invoice, XeroOperation.PushInvoiceDraft, XeroOutboxState.Unknown);
        var unknown = await ShowAsync(lost.Source, Invoice, "INV-1");
        Assert.Equal("Xero: Queued", unknown.Text);
        Assert.Equal("Checking whether Xero received it.", unknown.Presentation!.Detail);
    }

    [AvaloniaFact]
    public async Task Failed_ShowsTheReason_AndRetry_GoesBackToTheQueueThroughTheOutbox()
    {
        var kit = await BadgeTestKit.CreateAsync();
        var failed = await kit.EntryAsync(Order, XeroOperation.PushPurchaseOrder, XeroOutboxState.Failed, "Xero refused: Account code 999 is not valid.");
        var badge = await ShowAsync(kit.Source, Order, "PO-2026-001");
        string? reported = null;
        badge.ActionCompleted += (message, outcome) => reported = $"{outcome.Succeeded}:{message}";

        Assert.Equal("Xero: Failed", badge.Text);
        Assert.Equal("Xero refused: Account code 999 is not valid.", badge.Presentation!.Detail);
        Assert.True(badge.OffersRetry);
        Assert.Equal(failed.Id, badge.Status!.Status.RetryableEntryId);

        await ClickAsync(badge, "Retry Xero for PO-2026-001");
        await WaitUntilAsync(() => reported is not null);

        var entry = Assert.Single(await kit.Outbox.ListForDocumentAsync(Order));
        Assert.Equal(XeroOutboxState.Pending, entry.State);
        Assert.Equal(failed.IdempotencyKey, entry.IdempotencyKey);
        Assert.Equal("True:Queued for Xero again.", reported);
        Assert.Equal("Xero: Queued", badge.Text);
        Assert.False(badge.OffersRetry);
    }

    [AvaloniaFact]
    public async Task WaitingForAuthorisation_WhenAWriteWaits_OrNoOrganisationIsConnected()
    {
        var kit = await BadgeTestKit.CreateAsync();
        await kit.EntryAsync(Invoice, XeroOperation.PushInvoiceDraft, XeroOutboxState.WaitingForAuthorisation, "Xero needs re-authorising to allow: accounting.invoices");
        var waiting = await ShowAsync(kit.Source, Invoice, "INV-1");
        Assert.Equal("Xero: Waiting for authorisation", waiting.Text);
        Assert.Equal("Xero needs re-authorising to allow: accounting.invoices", waiting.Presentation!.Detail);

        var offline = await BadgeTestKit.CreateAsync(connected: false);
        await offline.EntryAsync(Quote, XeroOperation.PushQuote);
        Assert.Equal("Xero: Waiting for authorisation", (await ShowAsync(offline.Source, Quote, "Q-1")).Text);
    }

    [AvaloniaFact]
    public async Task InvoiceReadBack_DraftAwaitingPaymentPaidVoidedDeleted()
    {
        var expected = new (string? Status, string Text)[]
        {
            ("DRAFT", "Xero: Draft in Xero — review and send from Xero"),
            ("SUBMITTED", "Xero: Draft in Xero — review and send from Xero"),
            ("AUTHORISED", "Xero: Awaiting payment"),
            ("PAID", "Xero: Paid"),
            ("VOIDED", "Xero: Voided"),
            ("DELETED", "Xero: Deleted in Xero"),
        };

        foreach (var (status, text) in expected)
        {
            var kit = await BadgeTestKit.CreateAsync();
            await kit.LinkAsync(Invoice, "INV-1", status);
            var badge = await ShowAsync(kit.Source, Invoice, "INV-1");
            Assert.Equal(text, badge.Text);
            Assert.Equal("INV-1", badge.Status!.Status.XeroNumber);
        }
    }

    [AvaloniaFact]
    public async Task QuoteReadBack_DraftSentAcceptedDeclinedDeleted()
    {
        var expected = new (string? Status, string Text)[]
        {
            ("DRAFT", "Xero: In Xero (draft)"),
            ("SENT", "Xero: Sent in Xero"),
            ("ACCEPTED", "Xero: Accepted in Xero"),
            ("DECLINED", "Xero: Declined in Xero"),
            ("INVOICED", "Xero: Invoiced in Xero"),
            ("DELETED", "Xero: Deleted in Xero"),
        };

        foreach (var (status, text) in expected)
        {
            var kit = await BadgeTestKit.CreateAsync();
            await kit.LinkAsync(Quote, "Q-1", status);
            Assert.Equal(text, (await ShowAsync(kit.Source, Quote, "Q-1")).Text);
        }
    }

    [AvaloniaFact]
    public async Task PurchaseOrderAndBillReadBack()
    {
        var kit = await BadgeTestKit.CreateAsync();
        await kit.LinkAsync(Order, "PO-1", "DRAFT");
        await kit.LinkAsync(Bill, "EXP-1", "AUTHORISED");

        Assert.Equal("Xero: In Xero (draft)", (await ShowAsync(kit.Source, Order, "PO-1")).Text);
        var bill = await ShowAsync(kit.Source, Bill, "Taxi");
        Assert.Equal("Xero: Awaiting payment", bill.Text);
        Assert.Equal("Approved in Xero.", bill.Presentation!.Detail);
    }

    [AvaloniaFact]
    public async Task CannotTell_IsShown_ForAPurchaseOrderWhoseCreateWasLost()
    {
        var kit = await BadgeTestKit.CreateAsync();
        var reason = XeroPurchasingOwnership.CannotTell("Purchase order", "order", "PO-1", "key expired", sourceGone: false).Reason;
        await kit.EntryAsync(Order, XeroOperation.PushPurchaseOrder, XeroOutboxState.Failed, reason);

        var badge = await ShowAsync(kit.Source, Order, "PO-1");
        Assert.Equal("Xero: Can't tell", badge.Text);
        Assert.Contains("check Xero for PO-1", badge.Presentation!.Detail, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task SendToXero_OfferedOnlyWhileNotSent_ForAnIssuedRecordOfAKindThatHasIt()
    {
        var fake = new FakeXeroBadgeSource();

        Assert.True((await ShowAsync(fake, Quote, "Q-1", offerSendToXero: true)).OffersSendToXero);
        Assert.False((await ShowAsync(fake, Quote, "Q-1", offerSendToXero: false)).OffersSendToXero); // a draft quote
        Assert.False((await ShowAsync(fake, Invoice, "INV-1", offerSendToXero: true)).OffersSendToXero); // X4 exposes none

        fake.Set(Quote, new XeroSyncStatus(XeroSyncBadge.Queued));
        Assert.False((await ShowAsync(fake, Quote, "Q-1", offerSendToXero: true)).OffersSendToXero);
    }

    [AvaloniaFact]
    public async Task SendToXero_And_SendAgain_GoToTheSource_AndReloadTheBadge()
    {
        var fake = new FakeXeroBadgeSource();
        var quote = await ShowAsync(fake, Quote, "Q-1", offerSendToXero: true);
        var reported = new List<string>();
        quote.ActionCompleted += (message, _) => reported.Add(message);

        fake.Set(Quote, new XeroSyncStatus(XeroSyncBadge.Queued)); // what the source will answer once queued
        await ClickAsync(quote, "Send Q-1 to Xero");
        await WaitUntilAsync(() => reported.Count == 1);
        Assert.Equal([Quote], fake.SentToXero);
        Assert.Equal("Xero: Queued", quote.Text);

        fake.Set(Bill, new XeroSyncStatus(XeroSyncBadge.NotSent, "The record TempestOS made in Xero was deleted there."), canSendAgain: true);
        var bill = await ShowAsync(fake, Bill, "Taxi", offerSendToXero: true);
        Assert.Equal("Xero: Deleted in Xero", bill.Text);
        Assert.True(bill.OffersSendAgain);
        Assert.False(bill.OffersSendToXero); // Send again, never Send to Xero, releases a deleted record

        var done = false;
        bill.ActionCompleted += (_, _) => done = true;
        await ClickAsync(bill, "Send Taxi to Xero again");
        await WaitUntilAsync(() => done);
        Assert.Equal([Bill], fake.SentAgain);
    }

    [AvaloniaFact]
    public async Task ReloadsItself_WhenTheSourceChanges_WhileOnScreen()
    {
        var fake = new FakeXeroBadgeSource();
        var badge = new XeroSyncBadgeControl(fake, Invoice, "INV-1");
        var window = new Window { Content = badge };
        window.Show();
        try
        {
            await badge.LoadAsync();
            Assert.Equal("Xero: Not sent", badge.Text);

            fake.Set(Invoice, new XeroSyncStatus(XeroSyncBadge.InXeroDraft, null, "INV-1", "DRAFT"));
            fake.RaiseChanged();
            await WaitUntilAsync(() => badge.Text == "Xero: Draft in Xero — review and send from Xero");
        }
        finally
        {
            window.Close();
        }

        // Off screen it no longer listens.
        var reads = fake.Read.Count;
        fake.RaiseChanged();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(reads, fake.Read.Count);
    }

    [AvaloniaFact]
    public async Task EngineSource_SendAgain_IsRefusedWithTheReason_WhenNothingWasDeleted()
    {
        var kit = await BadgeTestKit.CreateAsync();

        var result = await kit.Source.SendAgainAsync(Quote);

        Assert.False(result.Succeeded);
        Assert.Equal("Send again is offered for purchase orders and expenses only.", result.Message);
        Assert.False(kit.Source.CanSendToXero(XeroDocumentKind.Invoice));
    }

    private static async Task ClickAsync(Control root, string automationName)
    {
        var button = root.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == automationName);
        Assert.True(button.IsVisible, $"'{automationName}' is not offered.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
    }

    internal static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DesktopTestHelpers.Deadline(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(condition(), "The condition was never met.");
    }
}


/// <summary>
/// `v0.24.0` U3 test kit: X6's real <see cref="XeroSyncService"/> over the
/// real B2 link store and outbox, in memory, on a pinned clock — so a badge
/// test drives the same local state the shell reads, never a network.
/// </summary>
internal sealed class BadgeTestKit
{
    public const string TenantId = "tenant-badge";

    private BadgeTestKit(InMemoryPersistenceStore store, InMemorySecretStore secrets, PinnedClock clock)
    {
        Store = store;
        Secrets = secrets;
        Clock = clock;
        Links = new PersistenceXeroLinkStore(store);
        Outbox = new PersistenceXeroOutbox(store);
        Engine = new XeroSyncService(new XeroSyncParts(Links, Outbox, secrets), Outbox, store, timeProvider: clock);
        Source = new XeroSyncServiceBadgeSource(Engine);
    }

    public InMemoryPersistenceStore Store { get; }

    public InMemorySecretStore Secrets { get; }

    public PinnedClock Clock { get; }

    public PersistenceXeroLinkStore Links { get; }

    public PersistenceXeroOutbox Outbox { get; }

    public XeroSyncService Engine { get; }

    public XeroSyncServiceBadgeSource Source { get; }

    /// <summary>A kit with a Xero organisation connected (or not).</summary>
    public static async Task<BadgeTestKit> CreateAsync(bool connected = true)
    {
        var secrets = new InMemorySecretStore();
        if (connected)
            await secrets.SetAsync(XeroContactLinker.TenantIdSecretKey, TenantId);
        return new BadgeTestKit(new InMemoryPersistenceStore(), secrets, new PinnedClock(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero)));
    }

    /// <summary>Queues one entry and, when <paramref name="outcome"/> is given, claims it and records that outcome — as the drain would.</summary>
    public async Task<XeroOutboxEntry> EntryAsync(XeroDocumentRef document, XeroOperation operation, XeroOutboxState? outcome = null, string? lastError = null)
    {
        var entry = await Outbox.EnqueueAsync(operation, document, $"hash-{Guid.NewGuid():N}");
        if (outcome is not { } state)
            return entry;

        var claimed = await Outbox.ClaimNextDueAsync();
        Assert.NotNull(claimed);
        Assert.Equal(entry.Id, claimed!.Id);
        return (await Outbox.RecordOutcomeAsync(entry.Id, state, lastError))!;
    }

    /// <summary>Links <paramref name="document"/> as Xero last read it.</summary>
    public Task LinkAsync(XeroDocumentRef document, string number, string? xeroStatus) =>
        Links.SaveAsync(new XeroLink(
            XeroLink.CurrentSchemaVersion, TenantId, document, $"xero-{Guid.NewGuid():N}", number, "content", xeroStatus,
            null, null, Clock.GetUtcNow(), Clock.GetUtcNow(), "created"));

    internal sealed class InMemorySecretStore : ISecretStore
    {
        private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }

    internal sealed class PinnedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

/// <summary>A hand-written <see cref="IXeroBadgeSource"/> recording every action, for the views' and actions' own tests.</summary>
internal sealed class FakeXeroBadgeSource : IXeroBadgeSource
{
    private readonly Dictionary<XeroDocumentRef, XeroDocumentSyncStatus> _statuses = [];

    public event Action? Changed;

    public List<Guid> Retried { get; } = [];

    public List<XeroDocumentRef> SentAgain { get; } = [];

    public List<XeroDocumentRef> SentToXero { get; } = [];

    public List<XeroDocumentRef> Read { get; } = [];

    public HashSet<XeroDocumentKind> SendToXeroKinds { get; } = [XeroDocumentKind.Quote, XeroDocumentKind.PurchaseOrder, XeroDocumentKind.ExpenseBill];

    public void Set(XeroDocumentRef document, XeroSyncStatus status, bool canSendAgain = false) =>
        _statuses[document] = new XeroDocumentSyncStatus(status, XeroSyncService.LabelFor(status.Badge), status.RetryableEntryId is not null, canSendAgain);

    public void RaiseChanged() => Changed?.Invoke();

    public Task<XeroDocumentSyncStatus> GetStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        lock (Read)
            Read.Add(document);
        return Task.FromResult(_statuses.TryGetValue(document, out var status)
            ? status
            : new XeroDocumentSyncStatus(new XeroSyncStatus(XeroSyncBadge.NotSent), XeroSyncService.LabelFor(XeroSyncBadge.NotSent), false, false));
    }

    public Task<XeroBadgeActionResult> RetryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        Retried.Add(entryId);
        return Task.FromResult(new XeroBadgeActionResult(true, "Queued for Xero again."));
    }

    public Task<XeroBadgeActionResult> SendAgainAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        SentAgain.Add(document);
        return Task.FromResult(new XeroBadgeActionResult(true, "Queued to be sent to Xero again as a new draft."));
    }

    public Task<XeroBadgeActionResult> SendToXeroAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        SentToXero.Add(document);
        return Task.FromResult(new XeroBadgeActionResult(true, "Queued for Xero."));
    }

    public bool CanSendToXero(XeroDocumentKind kind) => SendToXeroKinds.Contains(kind);
}