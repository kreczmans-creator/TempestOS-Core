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
/// "Dashboard" node in <see cref="BusinessAreaView"/>. Since `v0.24.0` X7
/// also the forward cash picture: six calendar months of opening cash,
/// money in, money out and closing cash, every figure stating its source.
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
/// <para>
/// <b>Forward cash (`v0.24.0` X7)</b> reads <see cref="IForwardCashReadModel"/>
/// from the same read model instance (no new constructor argument): the
/// accounting package's actuals as last read — bank balances, bills,
/// repeating bills — and the invoices TempestOS raised there, combined with
/// accepted quotes not yet invoiced (TempestOS). Each figure's source is in
/// its tooltip and automation name; a figure with no source reading reads
/// "unavailable" with the reason, never zero. Quotes not yet accepted never
/// count.
/// </para>
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
    private readonly TextBlock _forwardCashStatus = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ContentControl _forwardCashTableHost = new();
    private readonly StackPanel _forwardCashDetail = new() { Spacing = DesignTokens.SpaceXs };

    /// <summary>The time zone every time on the dashboard is shown in (`v0.24.0` review-board fix n6): the machine's own unless a test pins one.</summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;

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
        ThemeReactiveBrush.Bind(_forwardCashStatus, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);

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

        var forwardCashBody = new StackPanel { Spacing = DesignTokens.SpaceSm };
        forwardCashBody.Children.Add(_forwardCashStatus);
        forwardCashBody.Children.Add(new ScrollViewer { Content = _forwardCashTableHost, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        forwardCashBody.Children.Add(_forwardCashDetail);
        AutomationProperties.SetName(forwardCashBody, ForwardCashPanelName);
        page.Children.Add(Section(ForwardCashPanelName, forwardCashBody));

        AutomationProperties.SetName(this, "Business dashboard");
        Content = new ScrollViewer { Content = page };
    }

    /// <summary>The forward cash panel's heading and automation name (`v0.24.0` X7).</summary>
    public const string ForwardCashPanelName = "Forward cash — next 6 months";

    /// <summary>Re-reads every source and rebuilds every region.</summary>
    public async Task RefreshAsync()
    {
        var accountsTask = _accountsReadModel.ReadAsync();
        var quotationsTask = ReadQuotationsAsync();
        var forwardCashTask = _accountsReadModel is IForwardCashReadModel forwardCash
            ? ReadForwardCashAsync(forwardCash)
            : Task.FromResult<ForwardCashPicture?>(null);
        await Task.WhenAll(accountsTask, quotationsTask, forwardCashTask).ConfigureAwait(true);

        var accounts = accountsTask.Result;
        var quotations = quotationsTask.Result;

        RenderTiles(accounts);
        RenderReceivable(accounts);
        RenderPayable(accounts);
        RenderQuotes(quotations);
        RenderCashFlow(accounts);
        RenderForwardCash(forwardCashTask.Result);
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

    private static async Task<ForwardCashPicture?> ReadForwardCashAsync(IForwardCashReadModel readModel) =>
        await readModel.ReadForwardCashAsync().ConfigureAwait(true);

    private void RenderForwardCash(ForwardCashPicture? picture)
    {
        _forwardCashDetail.Children.Clear();

        if (picture is null)
        {
            _forwardCashStatus.Text = "Forward cash unavailable — this accounts read model does not provide it.";
            _forwardCashTableHost.Content = null;
            return;
        }

        var status = new List<string>();
        if (picture.AccountsUnavailableReason is { } reason)
        {
            status.Add($"Bank balances, bills and repeating bills: unavailable — {reason}");
        }
        else
        {
            status.Add($"Bank balances, bills and repeating bills: {picture.AccountsConnector}, read at {Local(picture.AccountsReadAt!.Value)}.");
            if (picture.RefreshFailureReason is { } failure)
            {
                var when = picture.RefreshFailedAtUtc is { } failedAt ? $" at {Local(failedAt)}" : string.Empty;
                status.Add($"The latest refresh failed{when} ({failure}); showing the last reading.");
            }
        }

        status.Add("Money in: invoices raised in TempestOS, as last read back from the accounting package, and accepted quotes not yet invoiced (TempestOS). Quotes not yet accepted are not counted.");
        status.Add(picture.VatBasis);
        _forwardCashStatus.Text = string.Join(Environment.NewLine, status);

        _forwardCashTableHost.Content = ForwardCashTable(picture);

        foreach (var month in picture.Months.Where(m => m.Items.Count > 0))
        {
            _forwardCashDetail.Children.Add(Muted($"{MonthLabel(month)} — behind the figures:"));
            foreach (var item in month.Items)
                _forwardCashDetail.Children.Add(ForwardCashItemRow(item));
        }

        if (picture.Undated.Count > 0)
        {
            _forwardCashDetail.Children.Add(Muted(
                $"Undated — accepted, not invoiced, no planned or target date: {MoneyDisplay.Format(picture.UndatedTotal)} (TempestOS; in no month above)."));
            foreach (var item in picture.Undated)
                _forwardCashDetail.Children.Add(ForwardCashItemRow(item));
        }

        if (picture.Later.Count > 0)
        {
            _forwardCashDetail.Children.Add(Muted($"After {MonthLabel(picture.Months[^1])} (in no month above):"));
            foreach (var item in picture.Later)
                _forwardCashDetail.Children.Add(ForwardCashItemRow(item));
        }

        foreach (var exclusion in picture.Exclusions)
        {
            var total = exclusion.Total is { } amount ? $", {MoneyDisplay.Format(amount)}" : string.Empty;
            _forwardCashDetail.Children.Add(Muted($"Not counted: {exclusion.Reason} ({exclusion.Count} item(s){total})."));
        }
    }

    private Grid ForwardCashTable(ForwardCashPicture picture)
    {
        var rows = new (string Label, Func<ForwardCashMonth, ForwardCashFigure> Figure, bool Total)[]
        {
            ("Opening cash", m => m.OpeningCash, true),
            ("Invoices due (Xero)", m => m.InvoicesDue, false),
            ("Expected milestone invoices (TempestOS)", m => m.ExpectedMilestones, false),
            ("Money in", m => m.MoneyIn, true),
            ("Bills due (Xero)", m => m.BillsDue, false),
            ("Repeating bills (Xero)", m => m.RepeatingBills, false),
            ("Money out", m => m.MoneyOut, true),
            ("Closing cash", m => m.ClosingCash, true),
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto," + string.Join(',', picture.Months.Select(_ => "Auto"))) };
        for (var r = 0; r <= rows.Length; r++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AutomationProperties.SetName(grid, "Forward cash table");

        AddCell(grid, 0, 0, new TextBlock { Text = string.Empty });
        for (var c = 0; c < picture.Months.Count; c++)
        {
            var header = new TextBlock { Text = MonthLabel(picture.Months[c]), FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeCaption, HorizontalAlignment = HorizontalAlignment.Right };
            AddCell(grid, 0, c + 1, header);
        }

        for (var r = 0; r < rows.Length; r++)
        {
            var (label, select, total) = rows[r];
            var rowLabel = new TextBlock { Text = label, FontSize = DesignTokens.FontSizeBody, FontWeight = total ? DesignTokens.WeightHeading : Avalonia.Media.FontWeight.Normal };
            AddCell(grid, r + 1, 0, rowLabel);

            for (var c = 0; c < picture.Months.Count; c++)
            {
                var month = picture.Months[c];
                var figure = select(month);
                var value = figure.Amount is { } amount ? MoneyDisplay.Format(amount) : "unavailable";
                var source = figure.IsAvailable ? figure.Source.Describe(TimeZone) : $"{figure.Source.Describe(TimeZone)} — {figure.UnavailableReason}";

                var cell = new TextBlock
                {
                    Text = value,
                    FontSize = DesignTokens.FontSizeBody,
                    FontWeight = total ? DesignTokens.WeightHeading : Avalonia.Media.FontWeight.Normal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Opacity = figure.IsAvailable ? 1.0 : 0.7,
                };
                ToolTip.SetTip(cell, source);
                AutomationProperties.SetName(cell, $"{label}, {MonthLabel(month)}: {value} — {source}");
                AddCell(grid, r + 1, c + 1, cell);
            }
        }

        return grid;
    }

    private static void AddCell(Grid grid, int row, int column, Control content)
    {
        content.Margin = new Thickness(column == 0 ? 0 : DesignTokens.SpaceLg, DesignTokens.SpaceXs, 0, DesignTokens.SpaceXs);
        Grid.SetRow(content, row);
        Grid.SetColumn(content, column);
        grid.Children.Add(content);
    }

    private Control ForwardCashItemRow(ForwardCashItem item)
    {
        var date = item.Date is { } d ? d.ToString("d", CultureInfo.CurrentCulture) : "undated";
        var note = item.Note is { Length: > 0 } ? $" — {item.Note}" : string.Empty;
        var text = $"{date}  —  {KindLabel(item.Kind)}: {item.Description}: {MoneyDisplay.Format(item.Gross)}  ({item.Source.Describe(TimeZone)}){note}";

        return item.ObjectId is { } id && item.ObjectKind is { } kind ? Row(text, id, kind) : Muted(text);
    }

    private static string KindLabel(ForwardCashItemKind kind) => kind switch
    {
        ForwardCashItemKind.InvoiceDue => "Invoice due",
        ForwardCashItemKind.ExpectedMilestone => "Expected milestone invoice",
        ForwardCashItemKind.BillDue => "Bill due",
        ForwardCashItemKind.RepeatingBill => "Repeating bill",
        _ => kind.ToString(),
    };

    private static string MonthLabel(ForwardCashMonth month) =>
        month.From == month.MonthStart
            ? month.MonthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture)
            : $"{month.MonthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture)} (from {month.From.Day.ToString(CultureInfo.InvariantCulture)})";

    /// <summary>`v0.24.0` review-board fix n6: every time on the dashboard in the person's own local time (<see cref="TimeZone"/>).</summary>
    private string Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, TimeZone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

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
        // `TD-88`/`WP 21.5B`: `Status` is a `Quotation`-own field, not on
        // the index row.
        var entries = await _domainContext.Repository.ListByKindAsync(Quotation.CanonicalKind).ConfigureAwait(true);
        var all = await _domainContext.Repository.MaterialiseAsync<Quotation>(entries).ConfigureAwait(true);
        return all
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
