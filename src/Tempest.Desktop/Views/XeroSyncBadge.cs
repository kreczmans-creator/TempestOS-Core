using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

// ============================================================================
// `v0.24.0` U3 (`ADR-0162`; design §4, §6, §11 U3): the one Xero badge shown
// on a quote, invoice, purchase order or expense, with its actions, and the
// small helpers the issuing views use to keep the rendered PDF on the record
// (so X6's `IXeroDocumentFileSource` can upload exactly what was exported).
// Read-only over X6: every badge comes from the link store and the outbox
// alone (`XeroSyncService.GetDocumentStatusAsync`) — never a network call, so
// a badge renders offline and never blocks the UI thread.
// ============================================================================

/// <summary>What one badge action (Retry, Send again, Send to Xero) did, in the words shown to the person.</summary>
/// <param name="Succeeded">Whether the action queued anything.</param>
/// <param name="Message">What to show: the confirmation, or why nothing was queued.</param>
public sealed record XeroBadgeActionResult(bool Succeeded, string Message);

/// <summary>
/// Where a <see cref="XeroSyncBadgeControl"/> reads its badge from and sends
/// its actions to (`v0.24.0` U3). The real shell uses
/// <see cref="XeroSyncServiceBadgeSource"/> over X6's engine; headless tests
/// use a fake.
/// </summary>
public interface IXeroBadgeSource
{
    /// <summary>
    /// Raised, on any thread, whenever a badge may have changed (an engine
    /// cycle finished, or an action was taken); badges reload themselves on
    /// the UI thread.
    /// </summary>
    event Action? Changed;

    /// <summary>The badge, its words and actions for <paramref name="document"/> — from local state alone, never a network call.</summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<XeroDocumentSyncStatus> GetStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default);

    /// <summary><em>Retry</em> on the Failed outbox entry <paramref name="entryId"/> (<see cref="IXeroOutbox.RetryAsync"/>, through the engine so it is audited and woken).</summary>
    /// <param name="entryId">The Failed entry (<see cref="XeroSyncStatus.RetryableEntryId"/>).</param>
    /// <param name="cancellationToken">Cancels the retry.</param>
    Task<XeroBadgeActionResult> RetryAsync(Guid entryId, CancellationToken cancellationToken = default);

    /// <summary><em>Send again</em> on a purchase order or expense whose Xero record TempestOS made was deleted there (X5, <see cref="XeroPurchasingSendAgain"/>).</summary>
    /// <param name="document">The purchase order or expense.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<XeroBadgeActionResult> SendAgainAsync(XeroDocumentRef document, CancellationToken cancellationToken = default);

    /// <summary><em>Send to Xero</em> on a record raised before Xero sync began (Q8): records the person's request (audited) and queues its writes.</summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<XeroBadgeActionResult> SendToXeroAsync(XeroDocumentRef document, CancellationToken cancellationToken = default);

    /// <summary>Whether <em>Send to Xero</em> exists for records of <paramref name="kind"/> (X3 quotes; X5 purchase orders and expenses). X4 exposes none for invoices: an invoice goes to Xero through its own <em>Send</em>.</summary>
    /// <param name="kind">The kind of record.</param>
    bool CanSendToXero(XeroDocumentKind kind);
}

/// <summary>
/// The <see cref="IXeroBadgeSource"/> over X6's <see cref="XeroSyncService"/>
/// and the X3/X5 planners' own <em>Send to Xero</em> (Q8). Every action
/// wakes the engine, so what was queued is sent on its next drain.
/// </summary>
public sealed class XeroSyncServiceBadgeSource : IXeroBadgeSource
{
    private readonly XeroSyncService _engine;
    private readonly XeroQuotePlanner? _quotes;
    private readonly XeroPurchaseOrderPlanner? _orders;
    private readonly XeroExpenseBillPlanner? _expenses;

    /// <summary>Initialises a new instance of the <see cref="XeroSyncServiceBadgeSource"/> class.</summary>
    /// <param name="engine">X6's engine: badges, Retry and Send again.</param>
    /// <param name="quotes">X3's planner, for a quotation's <em>Send to Xero</em>; <see langword="null"/> offers none.</param>
    /// <param name="orders">X5's purchase-order planner, for <em>Send to Xero</em>; <see langword="null"/> offers none.</param>
    /// <param name="expenses">X5's expense-bill planner, for <em>Send to Xero</em>; <see langword="null"/> offers none.</param>
    public XeroSyncServiceBadgeSource(
        XeroSyncService engine, XeroQuotePlanner? quotes = null, XeroPurchaseOrderPlanner? orders = null, XeroExpenseBillPlanner? expenses = null)
    {
        ArgumentNullException.ThrowIfNull(engine);

        _engine = engine;
        _quotes = quotes;
        _orders = orders;
        _expenses = expenses;
        _engine.CycleCompleted += _ => Changed?.Invoke();
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>
    /// The source over the Xero services the host registered, or
    /// <see langword="null"/> when Xero is not the configured connector (no
    /// engine is registered) — the views then show no Xero badge at all.
    /// </summary>
    /// <param name="services">The host's service provider.</param>
    public static XeroSyncServiceBadgeSource? TryCreate(ITempestServiceProvider? services)
    {
        if (services is null || TryResolve<XeroSyncService>(services) is not { } engine)
            return null;

        return new XeroSyncServiceBadgeSource(
            engine, TryResolve<XeroQuotePlanner>(services), TryResolve<XeroPurchaseOrderPlanner>(services), TryResolve<XeroExpenseBillPlanner>(services));
    }

    /// <inheritdoc />
    public Task<XeroDocumentSyncStatus> GetStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        _engine.GetDocumentStatusAsync(document, cancellationToken);

    /// <inheritdoc />
    public async Task<XeroBadgeActionResult> RetryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var retried = await _engine.RetryAsync(entryId, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();
        return retried
            ? new XeroBadgeActionResult(true, "Queued for Xero again.")
            : new XeroBadgeActionResult(false, "Nothing to retry: that write is no longer failed.");
    }

    /// <inheritdoc />
    public async Task<XeroBadgeActionResult> SendAgainAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        var result = await _engine.SendAgainAsync(document, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();
        return result.Queued
            ? new XeroBadgeActionResult(true, "Queued to be sent to Xero again as a new draft.")
            : new XeroBadgeActionResult(false, result.Reason ?? "Nothing was queued.");
    }

    /// <inheritdoc />
    public async Task<XeroBadgeActionResult> SendToXeroAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!Guid.TryParse(document.TempestKey, out var id))
            return new XeroBadgeActionResult(false, "This record cannot be sent to Xero.");

        (bool Queued, string? Reason) result = document.Kind switch
        {
            XeroDocumentKind.Quote when _quotes is not null => From(await _quotes.SendToXeroAsync(id, cancellationToken).ConfigureAwait(false)),
            XeroDocumentKind.PurchaseOrder when _orders is not null => From(await _orders.SendToXeroAsync(id, cancellationToken).ConfigureAwait(false)),
            XeroDocumentKind.ExpenseBill when _expenses is not null => From(await _expenses.SendToXeroAsync(id, cancellationToken).ConfigureAwait(false)),
            _ => (false, "Send to Xero is not offered for this record."),
        };

        if (result.Queued)
            _engine.Signal();
        Changed?.Invoke();

        return result.Queued
            ? new XeroBadgeActionResult(true, "Queued for Xero.")
            : new XeroBadgeActionResult(false, result.Reason ?? "Nothing was queued.");
    }

    /// <inheritdoc />
    public bool CanSendToXero(XeroDocumentKind kind) => kind switch
    {
        XeroDocumentKind.Quote => _quotes is not null,
        XeroDocumentKind.PurchaseOrder => _orders is not null,
        XeroDocumentKind.ExpenseBill => _expenses is not null,
        _ => false,
    };

    private static (bool, string?) From(XeroQuoteSendRequest request) => (request.Queued, request.Reason);

    private static (bool, string?) From(XeroPurchasingSendRequest request) => (request.Queued, request.Reason);

    private static T? TryResolve<T>(ITempestServiceProvider services) where T : class
    {
        try
        {
            return services.GetService(typeof(T)) as T;
        }
        catch (ServiceResolutionException)
        {
            return null;
        }
#pragma warning disable CA1031 // Badges are advisory: a Xero service that cannot be built leaves the views without badges, never the shell without views.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}

/// <summary>How a badge is coloured: by what the person needs to do about it.</summary>
public enum XeroBadgeTone
{
    /// <summary>Nothing has happened yet, or nothing needs doing (Not sent, Voided).</summary>
    Neutral,

    /// <summary>On its way, or waiting in Xero for the person (Queued, a draft in Xero).</summary>
    Pending,

    /// <summary>Done (in Xero past draft, Paid).</summary>
    Good,

    /// <summary>Needs the person (Failed, Can't tell, Deleted in Xero, Waiting for authorisation).</summary>
    Attention,
}

/// <summary>A badge's words: its short text, any detail (the reason or Xero's note) and its tone.</summary>
/// <param name="Text">The badge's own words, for example <em>Queued</em> or <em>Draft in Xero — review and send from Xero</em>.</param>
/// <param name="Detail">The reason or note under it; <see langword="null"/> when none.</param>
/// <param name="Tone">How it is coloured.</param>
public sealed record XeroBadgePresentation(string Text, string? Detail, XeroBadgeTone Tone);

/// <summary>
/// Turns X6's <see cref="XeroDocumentSyncStatus"/> into the words the badge
/// shows (`v0.24.0` U3, design §4): one place, so every view says the same.
/// </summary>
public static class XeroBadgeText
{
    /// <summary>Nothing queued or sent.</summary>
    public const string NotSent = "Not sent";

    /// <summary>Queued; Xero has not confirmed it yet.</summary>
    public const string Queued = "Queued";

    /// <summary>A quote, purchase order or bill held as a draft in Xero.</summary>
    public const string InXeroDraft = "In Xero (draft)";

    /// <summary>An invoice held as a draft in Xero (D3, D4): TempestOS never approves or sends it.</summary>
    public const string InvoiceDraft = "Draft in Xero — review and send from Xero";

    /// <summary>A quote or purchase order held in Xero past draft.</summary>
    public const string InXero = "In Xero";

    /// <summary>A quote Xero holds as <c>SENT</c>.</summary>
    public const string QuoteSent = "Sent in Xero";

    /// <summary>A quote Xero holds as <c>ACCEPTED</c>.</summary>
    public const string QuoteAccepted = "Accepted in Xero";

    /// <summary>A quote Xero holds as <c>DECLINED</c>.</summary>
    public const string QuoteDeclined = "Declined in Xero";

    /// <summary>A quote invoiced in Xero (raising it from TempestOS too would bill twice).</summary>
    public const string QuoteInvoiced = "Invoiced in Xero";

    /// <summary>Approved in Xero, not yet paid.</summary>
    public const string AwaitingPayment = "Awaiting payment";

    /// <summary>Paid in full, read back from Xero.</summary>
    public const string Paid = "Paid";

    /// <summary>Voided in Xero.</summary>
    public const string Voided = "Voided";

    /// <summary>Deleted in Xero.</summary>
    public const string Deleted = "Deleted in Xero";

    /// <summary>The last write was refused or blocked.</summary>
    public const string Failed = "Failed";

    /// <summary>A purchase order or bill whose create's answer was lost and cannot be recovered (X5 <em>CannotTell</em>): someone must check Xero.</summary>
    public const string CannotTell = "Can't tell";

    /// <summary>Xero needs (re-)authorising before anything more is sent.</summary>
    public const string WaitingForAuthorisation = "Waiting for authorisation";

    /// <summary>The badge's words for <paramref name="status"/> on a record of <paramref name="kind"/>.</summary>
    /// <param name="kind">The kind of record.</param>
    /// <param name="status">X6's badge for it.</param>
    public static XeroBadgePresentation Describe(XeroDocumentKind kind, XeroDocumentSyncStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var reason = string.IsNullOrWhiteSpace(status.Status.Reason) ? null : status.Status.Reason.Trim();
        var xeroStatus = status.Status.XeroStatus?.Trim().ToUpperInvariant();
        var deletedNote = reason is not null && reason.StartsWith("Deleted in Xero", StringComparison.OrdinalIgnoreCase);

        XeroBadgePresentation Make(string text, XeroBadgeTone tone) =>
            new(text, reason is not null && string.Equals(reason.TrimEnd('.'), text, StringComparison.OrdinalIgnoreCase) ? null : reason, tone);

        switch (status.Status.Badge)
        {
            case XeroSyncBadge.NotSent:
                return status.CanSendAgain ? Make(Deleted, XeroBadgeTone.Attention) : Make(NotSent, XeroBadgeTone.Neutral);

            case XeroSyncBadge.Queued:
                return Make(Queued, XeroBadgeTone.Pending);

            case XeroSyncBadge.InXeroDraft:
                return Make(kind == XeroDocumentKind.Invoice ? InvoiceDraft : InXeroDraft, XeroBadgeTone.Pending);

            case XeroSyncBadge.InXero:
                if (kind == XeroDocumentKind.Quote)
                {
                    var text = xeroStatus switch
                    {
                        "SENT" => QuoteSent,
                        "ACCEPTED" => QuoteAccepted,
                        "DECLINED" => QuoteDeclined,
                        "INVOICED" => QuoteInvoiced,
                        _ => InXero,
                    };
                    return Make(text, xeroStatus == "INVOICED" ? XeroBadgeTone.Attention : XeroBadgeTone.Good);
                }

                return Make(InXero, XeroBadgeTone.Good);

            case XeroSyncBadge.AwaitingPayment:
                return Make(AwaitingPayment, XeroBadgeTone.Pending);

            case XeroSyncBadge.Paid:
                return Make(Paid, XeroBadgeTone.Good);

            case XeroSyncBadge.Voided:
                return deletedNote || xeroStatus == "DELETED" ? Make(Deleted, XeroBadgeTone.Neutral) : Make(Voided, XeroBadgeTone.Neutral);

            case XeroSyncBadge.Failed:
                if (reason is not null && reason.Contains("cannot tell", StringComparison.OrdinalIgnoreCase))
                    return Make(CannotTell, XeroBadgeTone.Attention);
                if (deletedNote || (xeroStatus == "DELETED" && kind == XeroDocumentKind.Quote))
                    return Make(Deleted, XeroBadgeTone.Attention);
                return Make(Failed, XeroBadgeTone.Attention);

            case XeroSyncBadge.NeedsReauthorisation:
                return Make(WaitingForAuthorisation, XeroBadgeTone.Attention);

            default:
                return Make(status.Label, XeroBadgeTone.Neutral);
        }
    }
}

/// <summary>
/// The Xero badge for one quote, invoice, purchase order or expense
/// (`v0.24.0` U3, design §11): X6's status in words
/// (<see cref="XeroBadgeText"/>), its reason or note, and the actions it
/// offers — <b>Retry</b> on a failed write, <b>Send again</b> on a purchase
/// order or bill deleted in Xero (X5), and <b>Send to Xero</b> on a record
/// raised before Xero sync began (Q8). Reads local state only, asynchronously
/// (<see cref="LoadAsync"/>), and reloads itself whenever its source says
/// something changed while it is on screen.
/// </summary>
public sealed class XeroSyncBadgeControl : Border
{
    private readonly IXeroBadgeSource _source;
    private readonly TextBlock _label = new() { FontSize = DesignTokens.FontSizeCaption, VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _detail = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = false };
    private readonly Button _retry = new() { Content = "Retry", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _sendAgain = new() { Content = "Send again", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _sendToXero = new() { Content = "Send to Xero", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Border _badgeBorder;
    private readonly bool _offerSendToXero;
    private int _loadVersion;
    private bool _busy;

    /// <summary>Initialises a new instance of the <see cref="XeroSyncBadgeControl"/> class; call <see cref="LoadAsync"/> to fill it.</summary>
    /// <param name="source">Where the badge is read from and its actions go.</param>
    /// <param name="document">The record.</param>
    /// <param name="reference">The record's own number or name, for the automation names (<c>"Xero status for Q-001"</c>).</param>
    /// <param name="offerSendToXero">Whether this record could be sent to Xero on demand (Q8): it is issued (a quote approved or sent, a purchase order issued) or recorded (an expense). <em>Send to Xero</em> is then offered while the badge reads <em>Not sent</em>.</param>
    public XeroSyncBadgeControl(IXeroBadgeSource source, XeroDocumentRef document, string reference, bool offerSendToXero = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        _source = source;
        Document = document;
        Reference = reference;
        _offerSendToXero = offerSendToXero && source.CanSendToXero(document.Kind);

        _label.Text = "Xero: …";
        AutomationProperties.SetName(this, $"Xero status for {reference}");
        AutomationProperties.SetName(_label, $"Xero status for {reference}");
        AutomationProperties.SetName(_detail, $"Xero status detail for {reference}");
        AutomationProperties.SetName(_retry, $"Retry Xero for {reference}");
        AutomationProperties.SetName(_sendAgain, $"Send {reference} to Xero again");
        AutomationProperties.SetName(_sendToXero, $"Send {reference} to Xero");
        ToolTip.SetTip(_retry, "Queue the failed write for Xero again");
        ToolTip.SetTip(_sendAgain, "Send it to Xero again as a new draft (the deleted one stays deleted)");
        ToolTip.SetTip(_sendToXero, "Raised before Xero sync began: send it to Xero now");
        _retry.Classes.Add(ChromeStyles.Flat);
        _sendAgain.Classes.Add(ChromeStyles.Flat);
        _sendToXero.Classes.Add(ChromeStyles.Flat);

        _retry.Click += async (_, _) => await RunAsync(ct => Status?.Status.RetryableEntryId is { } id
            ? _source.RetryAsync(id, ct)
            : Task.FromResult(new XeroBadgeActionResult(false, "Nothing to retry."))).ConfigureAwait(true);
        _sendAgain.Click += async (_, _) => await RunAsync(ct => _source.SendAgainAsync(Document, ct)).ConfigureAwait(true);
        _sendToXero.Click += async (_, _) => await RunAsync(ct => _source.SendToXeroAsync(Document, ct)).ConfigureAwait(true);

        _badgeBorder = new Border
        {
            Child = _label,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(DesignTokens.BadgeCornerRadius),
            Padding = new Thickness(6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ApplyTone(XeroBadgeTone.Neutral);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        row.Children.Add(_badgeBorder);
        row.Children.Add(_retry);
        row.Children.Add(_sendAgain);
        row.Children.Add(_sendToXero);

        var body = new StackPanel { Spacing = DesignTokens.SpaceXs };
        body.Children.Add(row);
        body.Children.Add(_detail);
        Child = body;

        AttachedToVisualTree += (_, _) => _source.Changed += OnSourceChanged;
        DetachedFromVisualTree += (_, _) => _source.Changed -= OnSourceChanged;
    }

    /// <summary>Raised after an action completes, with what to show — forwarded by each view to its own <c>ActionCompleted</c>.</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>The record this badge is for.</summary>
    public XeroDocumentRef Document { get; }

    /// <summary>The record's own number or name.</summary>
    public string Reference { get; }

    /// <summary>X6's badge as last loaded; <see langword="null"/> before the first load.</summary>
    public XeroDocumentSyncStatus? Status { get; private set; }

    /// <summary>The badge's words as last shown; <see langword="null"/> before the first load.</summary>
    public XeroBadgePresentation? Presentation { get; private set; }

    /// <summary>The badge's text as shown (<c>"Xero: Queued"</c>).</summary>
    public string Text => _label.Text ?? string.Empty;

    /// <summary>Whether <em>Retry</em> is offered.</summary>
    public bool OffersRetry => _retry.IsVisible;

    /// <summary>Whether <em>Send again</em> is offered.</summary>
    public bool OffersSendAgain => _sendAgain.IsVisible;

    /// <summary>Whether <em>Send to Xero</em> is offered.</summary>
    public bool OffersSendToXero => _sendToXero.IsVisible;

    /// <summary>Reads the badge (local state only) and shows it. Never throws for a failed read: the badge says it could not be read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _loadVersion);
        XeroDocumentSyncStatus status;
        try
        {
            status = await _source.GetStatusAsync(Document, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
#pragma warning disable CA1031 // A badge is advisory: a failed local read is shown on the badge, never thrown into the view.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (version == Volatile.Read(ref _loadVersion))
                Show(null, new XeroBadgePresentation("Status unavailable", ex.GetBaseException().Message, XeroBadgeTone.Attention));
            return;
        }

        // A newer load started while this one read: it wins.
        if (version != Volatile.Read(ref _loadVersion))
            return;

        Show(status, XeroBadgeText.Describe(Document.Kind, status));
    }

    private void Show(XeroDocumentSyncStatus? status, XeroBadgePresentation presentation)
    {
        Status = status;
        Presentation = presentation;

        _label.Text = $"Xero: {presentation.Text}";
        AutomationProperties.SetHelpText(_label, presentation.Detail);
        ToolTip.SetTip(_label, presentation.Detail);
        _detail.Text = presentation.Detail;
        _detail.IsVisible = presentation.Detail is not null;
        ApplyTone(presentation.Tone);

        _retry.IsVisible = status?.CanRetry == true;
        _sendAgain.IsVisible = status?.CanSendAgain == true;
        _sendToXero.IsVisible = _offerSendToXero && status is { Status.Badge: XeroSyncBadge.NotSent, CanSendAgain: false };
        SetButtonsEnabled(!_busy);
    }

    private void ApplyTone(XeroBadgeTone tone)
    {
        var brushKey = tone switch
        {
            XeroBadgeTone.Good => ApplicationPalette.HealthTextHealthyBrushKey,
            XeroBadgeTone.Attention => ApplicationPalette.HealthTextBlockedBrushKey,
            XeroBadgeTone.Pending => ApplicationPalette.HealthTextAttentionBrushKey,
            _ => ApplicationPalette.HealthTextUnknownBrushKey,
        };
        ThemeReactiveBrush.Bind(_label, TextBlock.ForegroundProperty, brushKey);
        ThemeReactiveBrush.Bind(_badgeBorder, BorderBrushProperty, brushKey);
    }

    private async Task RunAsync(Func<CancellationToken, Task<XeroBadgeActionResult>> action)
    {
        if (_busy)
            return;

        _busy = true;
        SetButtonsEnabled(false);
        XeroBadgeActionResult result;
        try
        {
            result = await action(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Reported on screen like every other failed action, never thrown out of a click handler.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            result = new XeroBadgeActionResult(false, $"Xero: {ex.GetBaseException().Message}");
        }
        finally
        {
            _busy = false;
        }

        await LoadAsync().ConfigureAwait(true);
        ActionCompleted?.Invoke(result.Message, result.Succeeded ? ActionOutcome.Changed : ActionOutcome.Failed);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _retry.IsEnabled = enabled;
        _sendAgain.IsEnabled = enabled;
        _sendToXero.IsEnabled = enabled;
    }

    private void OnSourceChanged() => Dispatcher.UIThread.Post(async () => await LoadAsync().ConfigureAwait(true));
}

/// <summary>
/// Keeps the PDF a person issued on the record it was issued for (`v0.24.0`
/// U3; design §1, §3 "the same number and the same PDF"): the bytes saved by
/// an Export or an Issue are attached to the quotation, invoice request or
/// purchase order, so X6's <see cref="IXeroDocumentFileSource"/> uploads
/// exactly what was exported. File naming and folders are the caller's,
/// unchanged.
/// </summary>
public static class XeroIssuedPdf
{
    /// <summary>The MIME type issued documents are attached as.</summary>
    public const string ContentType = "application/pdf";

    /// <summary>
    /// Whether <paramref name="quotation"/> has been issued — an approved
    /// revision, or sent and answered — so its exported sheet is the one Xero
    /// should carry. A draft or in-review sheet is never attached: it would
    /// otherwise count as "exported" the moment the quotation is approved
    /// (X3's planner), and Xero would get the draft's PDF.
    /// </summary>
    /// <param name="quotation">The quotation.</param>
    public static bool IsIssued(Tempest.Core.Quotations.Quotation quotation)
    {
        ArgumentNullException.ThrowIfNull(quotation);

        return (quotation.Status == Tempest.Core.Quotations.QuotationStatus.Approved && quotation.RevisionNumber > 0)
               || quotation.Status is Tempest.Core.Quotations.QuotationStatus.Sent
                   or Tempest.Core.Quotations.QuotationStatus.Accepted
                   or Tempest.Core.Quotations.QuotationStatus.Declined;
    }

    /// <summary>
    /// Attaches <paramref name="bytes"/> to <paramref name="record"/> under
    /// <paramref name="fileName"/> — unless the newest PDF already attached is
    /// byte-for-byte the same (an unchanged re-export adds nothing). Never
    /// throws for an attach the platform refuses (no content store, too large,
    /// an archived project): the export itself already succeeded.
    /// </summary>
    /// <param name="record">The issued record.</param>
    /// <param name="fileName">The attachment's name (the exported file's own name).</param>
    /// <param name="bytes">The exact bytes saved.</param>
    /// <param name="cancellationToken">Cancels the attach.</param>
    /// <returns>Whether the PDF is now on the record (newly attached, or already there).</returns>
    public static async Task<bool> AttachAsync(IHasAttachments record, string fileName, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        try
        {
            var attachments = await record.GetAttachmentsAsync(cancellationToken).ConfigureAwait(true);
            var newestPdf = attachments.LastOrDefault(a =>
                string.Equals(a.ContentType, ContentType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetExtension(a.FileName), ".pdf", StringComparison.OrdinalIgnoreCase));
            if (newestPdf is not null)
            {
                var existing = await record.ReadAttachmentContentAsync(newestPdf.Id, cancellationToken).ConfigureAwait(true);
                if (existing.IsAvailable && existing.Bytes.AsSpan().SequenceEqual(bytes.Span))
                    return true;
            }

            await record.AttachContentAsync(fileName, ContentType, bytes, cancellationToken).ConfigureAwait(true);
            return true;
        }
        catch (InvalidOperationException)
        {
            // No attachment content store (a Core-only host), or the record refuses changes.
            return false;
        }
        catch (EngineeringDomainException)
        {
            // Too large, or a domain rule refused the change (an archived project, say).
            return false;
        }
    }
}

/// <summary>
/// Wraps a document renderer and keeps the bytes of its last render, so a
/// caller exporting through <see cref="DocumentExporter"/> can attach exactly
/// the bytes that were saved (`v0.24.0` U3) without changing the exporter or
/// the file it names.
/// </summary>
/// <typeparam name="TModel">The renderer's model.</typeparam>
public sealed class CapturingDocumentRenderer<TModel> : IDocumentRenderer<TModel>
{
    private readonly IDocumentRenderer<TModel> _inner;

    /// <summary>Initialises a new instance of the <see cref="CapturingDocumentRenderer{TModel}"/> class.</summary>
    /// <param name="inner">The renderer to run.</param>
    public CapturingDocumentRenderer(IDocumentRenderer<TModel> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    public string DocumentType => _inner.DocumentType;

    /// <inheritdoc />
    public string TemplateName => _inner.TemplateName;

    /// <summary>The bytes of the last <see cref="Render"/>; <see langword="null"/> before any.</summary>
    public ReadOnlyMemory<byte>? LastRender { get; private set; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Render(TModel model, OrganisationIdentity? identity = null)
    {
        var bytes = _inner.Render(model, identity);
        LastRender = bytes;
        return bytes;
    }
}
