using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.Invoicing;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Business tree's own Subscriptions node (`WP 19.7A`,
/// `po-comments.md` items 6 and 8): repeating bills due within 60 days,
/// grouped Hardware/Software/Premises/Other, loaded bills due within 30
/// days, the reading's own time and connector, a Refresh now action, and
/// the honest unavailable state — a read-only view over <see cref="IAccountsReadModel"/>
/// (`WP 19.8B`). Nothing here is entered in TempestOS: every figure is
/// read from the accounting package, never stored beyond the one cached
/// reading `WP 19.8B` already keeps.
/// </summary>
public sealed class SubscriptionsView : UserControl
{
    private static readonly AccountsCategory[] CategoryOrder = [AccountsCategory.Hardware, AccountsCategory.Software, AccountsCategory.Premises, AccountsCategory.Other];

    private readonly IAccountsReadModel _readModel;
    private readonly AccountsRefreshService? _refreshService;

    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly StackPanel _totalsRow = new() { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceLg };
    private readonly StackPanel _subscriptionsList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _billsList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _refreshButton = new() { Content = "Refresh now", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly EmptyStateView _unavailable = new("◈", "Subscriptions unavailable", "The accounting package has not been read yet.") { IsVisible = false };
    private readonly StackPanel _content = new() { Spacing = DesignTokens.SpaceLg };

    /// <summary>Initialises a new instance of the <see cref="SubscriptionsView"/> class.</summary>
    /// <param name="readModel">The cached accounts reading (`WP 19.8B`) this view renders, never entered here.</param>
    /// <param name="refreshService">Drives the "Refresh now" action. <see langword="null"/> leaves the button disabled — an honest state for a host with no refresh loop wired (mirrors <see cref="SettingsView"/>'s own identical accounts section).</param>
    public SubscriptionsView(IAccountsReadModel readModel, AccountsRefreshService? refreshService)
    {
        ArgumentNullException.ThrowIfNull(readModel);

        _readModel = readModel;
        _refreshService = refreshService;

        AutomationProperties.SetName(_refreshButton, "Refresh now");
        _refreshButton.Classes.Add(ChromeStyles.Subtle);
        _refreshButton.IsEnabled = refreshService is not null;
        _refreshButton.Click += async (_, _) => await RefreshNowAsync().ConfigureAwait(true);

        ThemeReactiveBrush.Bind(_status, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);

        _content.Children.Add(SectionHeading("Subscriptions by category"));
        _content.Children.Add(_totalsRow);
        _content.Children.Add(SectionHeading("Due within 60 days"));
        _content.Children.Add(_subscriptionsList);
        _content.Children.Add(SectionHeading("Bills due within 30 days"));
        _content.Children.Add(_billsList);

        var body = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceLg, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        body.Children.Add(PageHeading.Label("BUSINESS · SUBSCRIPTIONS"));
        body.Children.Add(PageHeading.Title("Subscriptions"));
        body.Children.Add(PageHeading.Lead("Hardware, Software and Premises subscriptions and loaded bills, read from the accounting package — never entered here (Product Owner comment item 8)."));
        body.Children.Add(_refreshButton);
        body.Children.Add(_status);
        body.Children.Add(_unavailable);
        body.Children.Add(_content);

        AutomationProperties.SetName(this, "Subscriptions");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Re-reads the cached accounts reading.</summary>
    public async Task RefreshAsync()
    {
        var snapshot = await _readModel.ReadAsync().ConfigureAwait(true);
        Render(snapshot);
    }

    private void Render(AccountsSnapshot snapshot)
    {
        _unavailable.IsVisible = !snapshot.IsAvailable;
        _content.IsVisible = snapshot.IsAvailable;

        if (!snapshot.IsAvailable)
        {
            _status.Text = string.Empty;
            var since = snapshot.UnavailableSince is { } at ? $" (since {at:g})" : string.Empty;
            _unavailable.SetMessage("Subscriptions unavailable", $"{snapshot.UnavailableReason ?? "No accounts reading yet."}{since}");
            return;
        }

        _status.Text = $"Read from {snapshot.Connector} at {snapshot.ReadAt:g}.";

        _totalsRow.Children.Clear();
        foreach (var category in CategoryOrder)
        {
            var total = snapshot.SubscriptionTotalsByCategory.TryGetValue(category, out var money) ? money : default;
            var card = new StackPanel { Spacing = 2 };
            card.Children.Add(new TextBlock { Text = category.ToString(), FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75 });
            card.Children.Add(new TextBlock { Text = total.ToString(), FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeTitle });
            _totalsRow.Children.Add(card);
        }

        _subscriptionsList.Children.Clear();
        if (snapshot.SubscriptionsDueWithin60.Count == 0)
        {
            _subscriptionsList.Children.Add(new TextBlock { Text = "Nothing due within 60 days.", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 });
        }
        else
        {
            foreach (var item in snapshot.SubscriptionsDueWithin60)
                _subscriptionsList.Children.Add(new TextBlock
                {
                    Text = $"{item.Bill.NextDue:d}  —  {item.Bill.Supplier}: {item.Bill.Description}  ({item.Category})  {item.Bill.Amount}",
                    FontSize = DesignTokens.FontSizeBody,
                });
        }

        _billsList.Children.Clear();
        if (snapshot.BillsDueWithin30.Count == 0)
        {
            _billsList.Children.Add(new TextBlock { Text = "Nothing due within 30 days.", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 });
        }
        else
        {
            foreach (var bill in snapshot.BillsDueWithin30)
                _billsList.Children.Add(new TextBlock { Text = $"{bill.Due:d}  —  {bill.Supplier} ({bill.Reference}): {bill.Amount}  [{bill.Status}]", FontSize = DesignTokens.FontSizeBody });
        }
    }

    private async Task RefreshNowAsync()
    {
        if (_refreshService is null)
            return;

        _status.Text = "Refreshing…";
        await _refreshService.RefreshNowAsync(CancellationToken.None).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private static TextBlock SectionHeading(string text) => new()
    {
        Text = text,
        FontFamily = DesignTokens.TitleFont,
        FontWeight = DesignTokens.WeightHeading,
        FontSize = DesignTokens.FontSizeBody + 2,
    };
}
