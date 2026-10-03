using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` review-board fixes on the Xero badge (<see cref="XeroSyncBadgeControl"/>),
/// headless: <b>Check Xero now</b> (M3), <b>Unlink from Xero</b> with
/// confirmation (M5), a <em>Can't tell</em> badge without Retry but with the
/// bookkeeper guidance and <b>I found it in Xero — link by Xero number</b>,
/// confirmed (m15), <em>Waiting: link …</em> instead of a red Failed (n5), and
/// the "checked with Xero" time in local time (n6). Then the same over X6's
/// real engine and link actions.
/// </summary>
public sealed class BadgeReviewFixesTests
{
    private static readonly XeroDocumentRef Quote = XeroDocumentRef.For(XeroDocumentKind.Quote, Guid.Parse("51111111-1111-1111-1111-111111111111"));
    private static readonly XeroDocumentRef Order = XeroDocumentRef.For(XeroDocumentKind.PurchaseOrder, Guid.Parse("53333333-3333-3333-3333-333333333333"));
    private static readonly XeroDocumentRef Bill = XeroDocumentRef.For(XeroDocumentKind.ExpenseBill, Guid.Parse("54444444-4444-4444-4444-444444444444"));

    private static async Task<XeroSyncBadgeControl> ShowAsync(IXeroBadgeSource source, XeroDocumentRef document, string reference)
    {
        var badge = new XeroSyncBadgeControl(source, document, reference) { TimeZone = TimeZoneInfo.Utc };
        await badge.LoadAsync();
        return badge;
    }

    [AvaloniaFact]
    public async Task CannotTell_HidesRetry_ShowsTheGuidance_AndLinksByXeroNumberOnlyOnceConfirmed()
    {
        var source = new ReviewFixBadgeSource();
        var reason = XeroPurchasingOwnership.CannotTell("Purchase order", "order", "PO-9", "key expired", sourceGone: false).Reason;
        source.Set(Order, new XeroSyncStatus(XeroSyncBadge.Failed, reason, RetryableEntryId: Guid.NewGuid()), cannotTell: true);
        source.Answers.Enqueue("PO-9");
        source.Confirmations.Enqueue(false); // first time: the person says no

        var badge = await ShowAsync(source, Order, "PO-9");
        Assert.Equal("Xero: Can't tell", badge.Text);
        Assert.False(badge.OffersRetry); // Retry would do nothing
        Assert.True(badge.OffersLinkByNumber);
        Assert.Equal(XeroDocumentLinkActions.BookkeeperGuidance, badge.Guidance);
        Assert.Contains("whoever keeps the books", badge.Guidance, StringComparison.Ordinal);

        string? reported = null;
        badge.ActionCompleted += (message, _) => reported = message;
        await ClickAsync(badge, "Link PO-9 by Xero number");
        await BadgeControlTests.WaitUntilAsync(() => reported is not null);
        Assert.Equal("Nothing was linked.", reported);
        Assert.Equal(["PO-9"], source.LookedUp);
        Assert.Empty(source.Linked);
        Assert.Contains("PO-9 for Northern Steel Ltd, GBP 120.00, DRAFT in Xero", source.ConfirmMessages[^1], StringComparison.Ordinal);

        reported = null;
        source.Answers.Enqueue("PO-9");
        source.Confirmations.Enqueue(true);
        await ClickAsync(badge, "Link PO-9 by Xero number");
        await BadgeControlTests.WaitUntilAsync(() => reported is not null);
        Assert.Equal("Linked to purchase order PO-9 in Xero.", reported);
        Assert.Equal("xero-po-9", Assert.Single(source.Linked).XeroId);
    }

    [AvaloniaFact]
    public async Task CannotTell_WithoutAWayToAsk_OffersNoLink_ButStillTheGuidance()
    {
        var source = new ReviewFixBadgeSource { HasPrompts = false };
        source.Set(Order, new XeroSyncStatus(XeroSyncBadge.Failed, "cannot tell", RetryableEntryId: Guid.NewGuid()), cannotTell: true);

        var badge = await ShowAsync(source, Order, "PO-9");
        Assert.False(badge.OffersRetry);
        Assert.False(badge.OffersLinkByNumber);
        Assert.NotNull(badge.Guidance);
    }

    [AvaloniaFact]
    public async Task UnlinkFromXero_IsOfferedForACopyDeletedInXero_AndRunsOnlyOnceConfirmed()
    {
        var source = new ReviewFixBadgeSource();
        source.Set(Quote, new XeroSyncStatus(XeroSyncBadge.Voided, null, "QU-0042", "DELETED"));
        source.Unlinkable.Add(Quote);

        var badge = await ShowAsync(source, Quote, "Q-42");
        Assert.Equal("Xero: Deleted in Xero", badge.Text);
        Assert.True(badge.OffersUnlink);
        Assert.Equal(XeroDocumentLinkActions.UnlinkActionName, (string?)FindButton(badge, "Unlink Q-42 from Xero").Content);

        string? reported = null;
        badge.ActionCompleted += (message, _) => reported = message;
        source.Confirmations.Enqueue(false);
        await ClickAsync(badge, "Unlink Q-42 from Xero");
        await BadgeControlTests.WaitUntilAsync(() => reported is not null);
        Assert.Equal("Nothing was unlinked.", reported);
        Assert.Empty(source.Unlinked);
        Assert.Contains("audit", source.ConfirmMessages[^1], StringComparison.Ordinal);

        reported = null;
        source.Confirmations.Enqueue(true);
        await ClickAsync(badge, "Unlink Q-42 from Xero");
        await BadgeControlTests.WaitUntilAsync(() => reported is not null);
        Assert.Equal([Quote], source.Unlinked);
    }

    [AvaloniaFact]
    public async Task Unlink_IsNotOffered_ForALiveCopy_OrWithNothingToAskWith()
    {
        var live = new ReviewFixBadgeSource();
        live.Set(Quote, new XeroSyncStatus(XeroSyncBadge.InXero, null, "QU-1", "SENT"));
        Assert.False((await ShowAsync(live, Quote, "Q-1")).OffersUnlink);

        var noPrompts = new ReviewFixBadgeSource { HasPrompts = false };
        noPrompts.Set(Quote, new XeroSyncStatus(XeroSyncBadge.Voided, null, "QU-1", "DELETED"));
        noPrompts.Unlinkable.Add(Quote);
        Assert.False((await ShowAsync(noPrompts, Quote, "Q-1")).OffersUnlink);
    }

    [AvaloniaFact]
    public async Task CheckXeroNow_IsOfferedOnceAnythingWasSent_AndAsksTheSourceToReadBackNow()
    {
        var source = new ReviewFixBadgeSource();
        Assert.False((await ShowAsync(source, Quote, "Q-1")).OffersCheckNow); // Not sent: nothing to check

        source.Set(Quote, new XeroSyncStatus(XeroSyncBadge.InXero, null, "QU-1", "SENT", new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero)));
        var badge = await ShowAsync(source, Quote, "Q-1");
        Assert.True(badge.OffersCheckNow);
        Assert.Equal(XeroSyncBadgeControl.CheckNowText, (string?)FindButton(badge, "Check Xero now for Q-1").Content);

        string? reported = null;
        badge.ActionCompleted += (message, _) => reported = message;
        await ClickAsync(badge, "Check Xero now for Q-1");
        await BadgeControlTests.WaitUntilAsync(() => reported is not null);
        Assert.Equal([Quote], source.Checked);
        Assert.Equal("Checked Xero.", reported);
    }

    [AvaloniaFact]
    public async Task TheCheckedWithXeroTime_IsShownInTheBadgesTimeZone()
    {
        var source = new ReviewFixBadgeSource();
        source.Set(Quote, new XeroSyncStatus(XeroSyncBadge.InXero, null, "QU-1", "SENT", new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero)));

        var utc = await ShowAsync(source, Quote, "Q-1");
        Assert.Equal("Checked with Xero 02 Oct 2026 09:30", utc.CheckedAtText);

        var london = new XeroSyncBadgeControl(source, Quote, "Q-1") { TimeZone = TimeZoneInfo.CreateCustomTimeZone("BST-test", TimeSpan.FromHours(1), "BST", "BST") };
        await london.LoadAsync();
        Assert.Equal("Checked with Xero 02 Oct 2026 10:30", london.CheckedAtText);

        Assert.Null((await ShowAsync(new ReviewFixBadgeSource(), Quote, "Q-1")).CheckedAtText);
    }

    [AvaloniaFact]
    public async Task AWriteWaitingForAContactLink_ReadsWaitingLink_NotFailed()
    {
        var source = new ReviewFixBadgeSource();
        source.Set(Bill, new XeroSyncStatus(
            XeroSyncBadge.Failed, "'NEWCO' is not linked to a Xero contact yet; link or create it under Customers & suppliers.", RetryableEntryId: Guid.NewGuid()), blocked: true);

        var badge = await ShowAsync(source, Bill, "Train fare");
        Assert.Equal("Xero: Waiting: link NEWCO", badge.Text);
        Assert.Equal(XeroBadgeTone.Pending, badge.Presentation!.Tone);
        Assert.Contains("Customers & suppliers", badge.Presentation.Detail, StringComparison.Ordinal);

        // Blocked on something else: still waiting, not a failure.
        source.Set(Bill, new XeroSyncStatus(XeroSyncBadge.Failed, "No Xero account code is mapped for Travel expenses.", RetryableEntryId: Guid.NewGuid()), blocked: true);
        var other = await ShowAsync(source, Bill, "Train fare");
        Assert.Equal("Xero: Waiting", other.Text);
        Assert.Equal(XeroBadgeTone.Pending, other.Presentation!.Tone);

        // A refusal is still Failed.
        source.Set(Bill, new XeroSyncStatus(XeroSyncBadge.Failed, "Xero refused: Account code 999 is not valid.", RetryableEntryId: Guid.NewGuid()));
        Assert.Equal("Xero: Failed", (await ShowAsync(source, Bill, "Train fare")).Text);
    }

    [Fact]
    public void ContactAwaitingLink_ReadsTheX2LinkersOwnWords()
    {
        Assert.Equal("Acme Ltd", XeroBadgeText.ContactAwaitingLink("'Acme Ltd' is not linked to a Xero contact yet; link or create it under Customers & suppliers."));
        Assert.Null(XeroBadgeText.ContactAwaitingLink("Something else."));
        Assert.Null(XeroBadgeText.ContactAwaitingLink(null));
    }

    [AvaloniaFact]
    public async Task OverTheRealEngine_AnUnlinkedQuote_OffersSendAgain_WithItsNote()
    {
        var kit = await BadgeTestKit.CreateAsync();
        await kit.LinkAsync(Quote, "QU-0042", "DELETED");
        var actions = new XeroDocumentLinkActions(new XeroSyncParts(kit.Links, kit.Outbox, kit.Secrets), kit.Engine, kit.Store, api: null, timeProvider: kit.Clock);
        var source = new XeroSyncServiceBadgeSource(kit.Engine, links: actions)
        {
            Prompts = new XeroBadgePrompts((_, _, _) => Task.FromResult(true), (_, _) => Task.FromResult<string?>(null)),
        };

        var badge = await ShowAsync(source, Quote, "Q-42");
        Assert.True(badge.OffersUnlink);
        Assert.True(badge.OffersCheckNow);

        string? reported = null;
        badge.ActionCompleted += (message, _) => reported = message;
        await ClickAsync(badge, "Unlink Q-42 from Xero");
        await BadgeControlTests.WaitUntilAsync(() => reported is not null);
        Assert.Contains("Unlinked quote QU-0042 from Xero", reported, StringComparison.Ordinal);

        await badge.LoadAsync();
        Assert.Equal("Xero: Deleted in Xero", badge.Text);
        Assert.Equal(XeroSyncServiceBadgeSource.UnlinkedNote, badge.Presentation!.Detail);
        Assert.True(badge.OffersSendAgain);
        Assert.False(badge.OffersUnlink);
    }

    [AvaloniaFact]
    public async Task OverTheRealEngine_APushThatFailedAgainstTheDeletedCopy_AfterUnlink_OffersSendAgain_NotRetry()
    {
        // Verifier F3 defect 3: the badge stayed Failed with "…Unlink, then
        // Send again", offering only Retry — which sent with no Send again chosen.
        var kit = await BadgeTestKit.CreateAsync();
        await kit.LinkAsync(Quote, "QU-0042", "DELETED");
        await kit.EntryAsync(
            Quote, XeroOperation.PushQuote, XeroOutboxState.Failed,
            "Quote QU-0042 was deleted in Xero. To send the quotation again, choose Unlink from Xero on its Xero badge, then Send again.");
        var actions = new XeroDocumentLinkActions(new XeroSyncParts(kit.Links, kit.Outbox, kit.Secrets), kit.Engine, kit.Store, api: null, timeProvider: kit.Clock);
        var source = new XeroSyncServiceBadgeSource(kit.Engine, links: actions)
        {
            Prompts = new XeroBadgePrompts((_, _, _) => Task.FromResult(true), (_, _) => Task.FromResult<string?>(null)),
        };
        Assert.True((await actions.UnlinkAsync(Quote)).Done);

        var badge = await ShowAsync(source, Quote, "Q-42");
        Assert.Equal("Xero: Deleted in Xero", badge.Text);
        Assert.Equal(XeroSyncServiceBadgeSource.UnlinkedNote, badge.Presentation!.Detail);
        Assert.True(badge.OffersSendAgain);
        Assert.False(badge.OffersRetry);
        Assert.False(badge.OffersUnlink);
    }

    private static Button FindButton(Control root, string automationName) =>
        root.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == automationName);

    private static async Task ClickAsync(Control root, string automationName)
    {
        var button = FindButton(root, automationName);
        Assert.True(button.IsVisible, $"'{automationName}' is not offered.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
    }
}

/// <summary>A hand-written <see cref="IXeroBadgeSource"/> for the review-board fixes: every new action recorded, prompts scripted.</summary>
internal sealed class ReviewFixBadgeSource : IXeroBadgeSource
{
    private readonly Dictionary<XeroDocumentRef, XeroDocumentSyncStatus> _statuses = [];

    public event Action? Changed;

    public bool HasPrompts { get; init; } = true;

    public Queue<bool> Confirmations { get; } = new();

    public Queue<string?> Answers { get; } = new();

    public List<string> ConfirmMessages { get; } = [];

    public HashSet<XeroDocumentRef> Unlinkable { get; } = [];

    public List<XeroDocumentRef> Unlinked { get; } = [];

    public List<XeroDocumentRef> Checked { get; } = [];

    public List<string> LookedUp { get; } = [];

    public List<XeroNumberMatch> Linked { get; } = [];

    public XeroBadgePrompts? Prompts => HasPrompts
        ? new XeroBadgePrompts(
            (_, message, _) =>
            {
                ConfirmMessages.Add(message);
                return Task.FromResult(Confirmations.Count > 0 && Confirmations.Dequeue());
            },
            (_, _) => Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : null))
        : null;

    public bool CanCheckNow => true;

    public bool CanLinkByNumber => true;

    public void Set(XeroDocumentRef document, XeroSyncStatus status, bool canSendAgain = false, bool cannotTell = false, bool blocked = false) =>
        _statuses[document] = new XeroDocumentSyncStatus(status, XeroSyncService.LabelFor(status.Badge), status.RetryableEntryId is not null, canSendAgain)
        {
            CannotTell = cannotTell,
            Blocked = blocked,
        };

    public void RaiseChanged() => Changed?.Invoke();

    public Task<XeroDocumentSyncStatus> GetStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        Task.FromResult(_statuses.TryGetValue(document, out var status)
            ? status
            : new XeroDocumentSyncStatus(new XeroSyncStatus(XeroSyncBadge.NotSent), XeroSyncService.LabelFor(XeroSyncBadge.NotSent), false, false));

    public Task<XeroBadgeActionResult> RetryAsync(Guid entryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new XeroBadgeActionResult(true, "Queued for Xero again."));

    public Task<XeroBadgeActionResult> SendAgainAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        Task.FromResult(new XeroBadgeActionResult(true, "Queued to be sent to Xero again as a new draft."));

    public Task<XeroBadgeActionResult> SendToXeroAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        Task.FromResult(new XeroBadgeActionResult(true, "Queued for Xero."));

    public bool CanSendToXero(XeroDocumentKind kind) => false;

    public Task<bool> NeedsSendToXeroAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<XeroBadgeActionResult> CheckNowAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        Checked.Add(document);
        return Task.FromResult(new XeroBadgeActionResult(true, "Checked Xero."));
    }

    public Task<bool> CanUnlinkAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) => Task.FromResult(Unlinkable.Contains(document));

    public Task<XeroBadgeActionResult> UnlinkAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        Unlinked.Add(document);
        return Task.FromResult(new XeroBadgeActionResult(true, "Unlinked."));
    }

    public Task<XeroNumberLookup> FindByNumberAsync(XeroDocumentRef document, string number, CancellationToken cancellationToken = default)
    {
        LookedUp.Add(number);
        return Task.FromResult(new XeroNumberLookup(
            new XeroNumberMatch(document, $"xero-{number.ToLowerInvariant()}", number, "DRAFT", "Northern Steel Ltd", 120m, "GBP"), null));
    }

    public Task<XeroBadgeActionResult> LinkByNumberAsync(XeroNumberMatch match, CancellationToken cancellationToken = default)
    {
        Linked.Add(match);
        return Task.FromResult(new XeroBadgeActionResult(true, $"Linked to purchase order {match.Number} in Xero."));
    }
}
