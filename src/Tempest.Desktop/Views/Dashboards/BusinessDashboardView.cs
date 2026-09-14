using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Quotations;
using Tempest.Desktop;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Views.Dashboards;

/// <summary>
/// The Business dashboard (`WP 19.7B`, Product Owner comment item 6,
/// sheet 5): four tiles, accounts receivable and payable, quotes open
/// value with a chase list, and a 12-week cash-flow chart — the tree's own
/// "Dashboard &amp; Reports" node in <see cref="BusinessAreaView"/>.
/// </summary>
/// <remarks>
/// One read per source, on entry and on <see cref="Tempest.Core.Events.IWorkspaceChanges"/>
/// (via the parent <see cref="BusinessAreaView"/>'s own subscription):
/// <see cref="IAccountsReadModel"/> (tiles, receivable, payable, cash
/// flow) and one direct, sibling read of every live <see cref="Quotation"/>
/// (the quotes panel — mirrors <see cref="ProjectsAreaView"/>'s own
/// identical "read the domain directly" shape for a figure no read model
/// carries). Every payable/receivable figure reads "unavailable" with the
/// reason — never a lying zero — exactly when
/// <see cref="AccountsSnapshot.IsAvailable"/> is <see langword="false"/>;
/// the quotes panel stays real regardless, since it never depends on the
/// accounting package (Product Owner comment item 8: only bills,
/// subscriptions and cash come from there).
/// </remarks>
public sealed class BusinessDashboardView : UserControl
{
    private readonly IAccountsReadModel _accountsReadModel;
    private readonly EngineeringDomainContext _domainContext;
    private readonly Action<Guid, string> _openObjectRightUp;

    private readonly WrapPanel _tiles = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _accountsStatus = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly StackPanel _receivableList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _payableList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBlock _quotesSummary = new() { FontSize = DesignTokens.FontSizeBody };
    private readonly StackPanel _quotesChaseList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly ContentControl _cashFlowHost = new();
    private readonly TextBlock _cashFlowStatus = new() { FontSize = DesignTokens.FontSizeCaption };

    /// <summary>Initialises a new instance of the <see cref="BusinessDashboardView"/> class.</summary>
    public BusinessDashboardView(IAccountsReadModel accountsReadModel, EngineeringDomainContext domainContext, Action<Guid, string> openObjectRightUp)
    {
        ArgumentNullException.ThrowIfNull(accountsReadModel);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(openObjectRightUp);

        _accountsReadModel = accountsReadModel;
        _domainContext = domainContext;
        _openObjectRightUp = openObjectRightUp;

        ThemeReactiveBrush.Bind(_accountsStatus, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        ThemeReactiveBrush.Bind(_cashFlowStatus, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);

        var page = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceXl };
        page.Children.Add(PageHeading.Label("BUSINESS"));
        page.Children.Add(PageHeading.Title("Dashboard"));
        page.Children.Add(_accountsStatus);
        page.Children.Add(Section("Invoiced", _tiles));
        page.Children.Add(Section("Accounts receivable — due 30 days and overdue", _receivableList));
        page.Children.Add(Section("Accounts payable — subscriptions within 60 days, bills within 30", _payableList));
        page.Children.Add(Section("Quotes", _quotesSummary));
        page.Children.Add(Section("Quotes to chase — sent over 7 days ago", _quotesChaseList));

        var cashFlowBody = new StackPanel { Spacing = DesignTokens.SpaceSm };
        cashFlowBody.Children.Add(_cashFlowStatus);
        cashFlowBody.Children.Add(_cashFlowHost);
        page.Children.Add(Section("Cash flow — next 12 weeks", cashFlowBody));

        AutomationProperties.SetName(this, "Business dashboard");
        Content = new ScrollViewer { Content = page };
    }

    /// <summary>Re-reads every source and rebuilds every region.</summary>
    public async Task RefreshAsync()
    {
        var accountsTask = _accountsReadModel.ReadAsync();
        var quotationsTask = ReadQuotationsAsync();
        await Task.WhenAll(accountsTask, quotationsTask).ConfigureAwait(true);

        var accounts = accountsTask.Result;
        var quotations = quotationsTask.Result;

        RenderTiles(accounts);
        RenderReceivable(accounts);
        RenderPayable(accounts);
        RenderQuotes(quotations);
        RenderCashFlow(accounts);
    }

    private void RenderTiles(AccountsSnapshot accounts)
    {
        _tiles.Children.Clear();

        if (!accounts.IsAvailable)
        {
            _accountsStatus.Text = $"Accounts reading unavailable — {accounts.UnavailableReason ?? "no accounts reading yet"}"
                + (accounts.UnavailableSince is { } since ? $" (since {since:g})." : ".");
            _tiles.Children.Add(Tile("Invoiced", "unavailable"));
            _tiles.Children.Add(Tile("Overdue", "unavailable"));
            _tiles.Children.Add(Tile("Due 30", "unavailable"));
            _tiles.Children.Add(Tile("Due 90", "unavailable"));
            return;
        }

        _accountsStatus.Text = $"Read from {accounts.Connector} at {accounts.ReadAt:g}.";
        _tiles.Children.Add(Tile("Invoiced", MoneyDisplay.Format(accounts.InvoicedTotal)));
        _tiles.Children.Add(Tile("Overdue", MoneyDisplay.Format(accounts.OverdueTotal)));
        _tiles.Children.Add(Tile("Due 30", MoneyDisplay.Format(accounts.Due30Total)));
        _tiles.Children.Add(Tile("Due 90", MoneyDisplay.Format(accounts.Due90Total)));
    }

    private static Control Tile(string label, string value)
    {
        var tile = new Border { MinWidth = 150, Padding = DesignTokens.CardPadding, Margin = new Thickness(0, 0, DesignTokens.SpaceMd, DesignTokens.SpaceMd), CornerRadius = new CornerRadius(DesignTokens.PanelCornerRadius), BorderThickness = new Thickness(1) };
        ThemeReactiveBrush.Bind(tile, Border.BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);
        ThemeReactiveBrush.Bind(tile, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);

        var content = new StackPanel { Spacing = DesignTokens.SpaceXs };
        content.Children.Add(new TextBlock { Text = value, FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading });
        content.Children.Add(new TextBlock { Text = label, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75 });
        tile.Child = content;

        AutomationProperties.SetName(tile, $"{label}: {value}");
        return tile;
    }

    private void RenderReceivable(AccountsSnapshot accounts)
    {
        _receivableList.Children.Clear();

        if (!accounts.IsAvailable)
        {
            _receivableList.Children.Add(Muted("Accounts reading unavailable — see above."));
            return;
        }

        var rows = accounts.Overdue.Concat(accounts.Due30).OrderBy(r => r.DueDate).ToList();
        if (rows.Count == 0)
        {
            _receivableList.Children.Add(Muted("Nothing due within 30 days or overdue."));
            return;
        }

        foreach (var row in rows)
        {
            var label = $"{row.DueDate:d}  —  Client {row.ClientOrganisationId}: {MoneyDisplay.Format(row.Amount)}";
            _receivableList.Children.Add(Row(label, row.RequestId, InvoiceRequest.CanonicalKind));
        }
    }

    private void RenderPayable(AccountsSnapshot accounts)
    {
        _payableList.Children.Clear();

        if (!accounts.IsAvailable)
        {
            _payableList.Children.Add(Muted("Accounts reading unavailable — see above."));
            return;
        }

        if (accounts.SubscriptionsDueWithin60.Count == 0 && accounts.BillsDueWithin30.Count == 0)
        {
            _payableList.Children.Add(Muted("Nothing due."));
            return;
        }

        foreach (var subscription in accounts.SubscriptionsDueWithin60.OrderBy(s => s.Bill.NextDue))
            _payableList.Children.Add(Muted($"{subscription.Bill.NextDue:d}  —  {subscription.Bill.Supplier}: {subscription.Bill.Description}  ({subscription.Category})  {MoneyDisplay.Format(subscription.Bill.Amount)}"));

        foreach (var bill in accounts.BillsDueWithin30.OrderBy(b => b.Due))
            _payableList.Children.Add(Muted($"{bill.Due:d}  —  {bill.Supplier} ({bill.Reference}): {MoneyDisplay.Format(bill.Amount)}  [{bill.Status}]"));
    }

    private void RenderQuotes(IReadOnlyList<Quotation> sentQuotations)
    {
        var openValue = sentQuotations.Count == 0
            ? Money.Zero(CurrencyCode.Gbp)
            : Money.Sum(sentQuotations.Select(q => q.Total), sentQuotations[0].Total.Currency);

        _quotesSummary.Text = $"{sentQuotations.Count} open quote(s), {MoneyDisplay.Format(openValue)} total value.";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var toChase = sentQuotations
            .Where(q => q.SentOn is { } sentOn && today.DayNumber - sentOn.DayNumber > Tempest.Workspace.Tasks.TaskEquations.QuoteChaseAfterDays)
            .OrderBy(q => q.SentOn)
            .ToList();

        _quotesChaseList.Children.Clear();
        if (toChase.Count == 0)
        {
            _quotesChaseList.Children.Add(Muted("Nothing to chase."));
            return;
        }

        foreach (var quote in toChase)
            _quotesChaseList.Children.Add(Row($"{quote.SentOn:d}  —  {quote.DisplayName}: {MoneyDisplay.Format(quote.Total)}", quote.Id, Quotation.CanonicalKind));
    }

    private void RenderCashFlow(AccountsSnapshot accounts)
    {
        if (!accounts.IsAvailable)
        {
            _cashFlowStatus.Text = "Accounts reading unavailable — see above.";
            _cashFlowHost.Content = Muted("No cash-flow series to draw.");
            return;
        }

        _cashFlowStatus.Text = $"Read from {accounts.Connector} at {accounts.ReadAt:g}.";

        var points = accounts.CashFlowSeries
            .Select(week => new DashboardChart.Point(week.WeekStart.ToString("d", CultureInfo.InvariantCulture), week.ClosingCash.Amount, MoneyDisplay.Format(week.ClosingCash)))
            .ToList();

        _cashFlowHost.Content = DashboardChart.Line(points);
    }

    private Control Row(string text, Guid objectId, string kind)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, FontSize = DesignTokens.FontSizeBody };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var open = new Button { Content = "Open", MinHeight = DesignTokens.ControlSizeSmall };
        open.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(open, $"Open {text}");
        open.Click += (_, _) => _openObjectRightUp(objectId, kind);
        Grid.SetColumn(open, 1);
        grid.Children.Add(open);

        return grid;
    }

    private async Task<IReadOnlyList<Quotation>> ReadQuotationsAsync()
    {
        var all = await _domainContext.Repository.ListByKindAsync(Quotation.CanonicalKind).ConfigureAwait(true);
        return all.OfType<Quotation>()
            .Where(q => q is not IDeletable { IsDeleted: true } && q.Status == QuotationStatus.Sent)
            .ToList();
    }

    private static TextBlock Muted(string text) => new() { Text = text, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    private static StackPanel Section(string title, Control content)
    {
        var section = new StackPanel { Spacing = DesignTokens.SpaceSm };
        var heading = new TextBlock { Text = title, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2 };
        ThemeReactiveBrush.Bind(heading, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        section.Children.Add(heading);
        section.Children.Add(content);
        return section;
    }
}
