using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Workspace;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Tasks;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Invoicing;
using Tempest.Core.Quotations;
using Tempest.Desktop;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Views.Dashboards;

/// <summary>
/// The Home dashboard (`WP 19.7B`, Product Owner comment item 6, sheets
/// 1-2): five task tiles, a commercial snapshot, the project status
/// totals as a chart, the next ten milestones, a task list, and the right
/// rail — Continue, Recent, Favourite and Recently changed, the identical
/// four concepts <see cref="CockpitView"/> already reads from
/// <see cref="EngineeringCockpit"/>, rearranged here rather than
/// reimplemented, since Home no longer renders the engineering surface
/// <see cref="CockpitView"/> is permanently docked inside (see that
/// class's own remarks) — Home is its own surface now.
/// </summary>
/// <remarks>
/// One read per source, on entry and on <see cref="IWorkspaceChanges"/>:
/// <see cref="ITasksReadModel"/> (the tiles, milestones, task list),
/// <see cref="IProjectStatusReadModel"/> (the project status chart),
/// <see cref="IAccountsReadModel"/> (the commercial snapshot's own
/// invoices figures) and one direct, sibling read of every live
/// <see cref="Quotation"/> (the commercial snapshot's own quotes figures —
/// mirrors <see cref="ProjectsAreaView"/>'s own identical "read the domain
/// directly" shape for a figure no read model carries), plus
/// <see cref="EngineeringCockpit.PrimeAsync"/> for the right rail. Every
/// panel with nothing behind it says why — the commercial snapshot's own
/// invoices figures read "unavailable" (never a lying zero) exactly when
/// <see cref="AccountsSnapshot.IsAvailable"/> is <see langword="false"/>.
/// </remarks>
public sealed class HomeDashboardView : UserControl
{
    private static readonly TaskBucket[] TileBuckets = [TaskBucket.Overdue, TaskBucket.DueToday, TaskBucket.DueThisWeek, TaskBucket.Approvals, TaskBucket.Finance];

    private readonly ITasksReadModel _tasksReadModel;
    private readonly IProjectStatusReadModel _projectStatusReadModel;
    private readonly IAccountsReadModel _accountsReadModel;
    private readonly EngineeringDomainContext _domainContext;
    private readonly EngineeringCockpit _cockpit;
    private readonly FavouriteObjectsState? _favourites;
    private readonly Func<Guid, string, Task> _openObjectRightUp;
    private readonly Action _openTasks;
    private readonly Func<int, Task> _onOpenRecent;
    private readonly Action<Guid, string>? _onOpenFavourite;
    private readonly Func<int, Task>? _onOpenRecentlyChanged;
    private readonly Action? _onNewProject;

    // `WP 20.10A` (Product Owner finding D1: "Lets add a 'New Project'
    // button on the home page").
    private readonly Button _newProjectButton = new() { Content = "New Project…", MinHeight = DesignTokens.ControlSizeMedium };

    private readonly WrapPanel _tiles = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _commercialText = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _statusChartHost = new();
    private readonly StackPanel _milestonesList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _taskList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _continueList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _recentList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _favouriteList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _recentlyChangedList = new() { Spacing = DesignTokens.SpaceXs };

    private readonly WorkspaceChangesSubscription _workspaceChanges;

    /// <summary>The change feed this view re-reads every source from while shown — settable by the composition root exactly as every sibling rail view's identical property already is.</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="HomeDashboardView"/> class.</summary>
    /// <param name="onNewProject">
    /// Runs the identical New Project flow every other entry point uses,
    /// and opens the created project right up (`WP 20.10A`, D1).
    /// <see langword="null"/> (any test that constructs this view directly)
    /// leaves the New Project button honestly inert rather than run
    /// without asking — the identical "not threaded through stays
    /// honestly unavailable" discipline every other optional collaborator
    /// across this platform's Desktop views already follows.
    /// </param>
    public HomeDashboardView(
        ITasksReadModel tasksReadModel, IProjectStatusReadModel projectStatusReadModel, IAccountsReadModel accountsReadModel,
        EngineeringDomainContext domainContext, EngineeringCockpit cockpit, FavouriteObjectsState? favourites,
        Func<Guid, string, Task> openObjectRightUp, Action openTasks, Func<int, Task> onOpenRecent,
        Action<Guid, string>? onOpenFavourite = null, Func<int, Task>? onOpenRecentlyChanged = null, Action? onNewProject = null)
    {
        ArgumentNullException.ThrowIfNull(tasksReadModel);
        ArgumentNullException.ThrowIfNull(projectStatusReadModel);
        ArgumentNullException.ThrowIfNull(accountsReadModel);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(cockpit);
        ArgumentNullException.ThrowIfNull(openObjectRightUp);
        ArgumentNullException.ThrowIfNull(openTasks);
        ArgumentNullException.ThrowIfNull(onOpenRecent);

        _tasksReadModel = tasksReadModel;
        _projectStatusReadModel = projectStatusReadModel;
        _accountsReadModel = accountsReadModel;
        _domainContext = domainContext;
        _cockpit = cockpit;
        _favourites = favourites;
        _openObjectRightUp = openObjectRightUp;
        _openTasks = openTasks;
        _onOpenRecent = onOpenRecent;
        _onOpenFavourite = onOpenFavourite;
        _onOpenRecentlyChanged = onOpenRecentlyChanged;
        _onNewProject = onNewProject;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        ThemeReactiveBrush.Bind(_commercialText, TextBlock.ForegroundProperty, BrandPalette.BodyTextBrushKey);

        // `WP 20.10A` (D1): the cockpit's own header row — title on the
        // left, the New Project action beside it, mirroring every other
        // page-level action row across this platform's Desktop views
        // (`ProjectBrowserView`'s own "New Project…" button, the same New
        // Project flow, reached here without a detour through Projects).
        _newProjectButton.Classes.Add(ChromeStyles.Primary);
        AutomationProperties.SetName(_newProjectButton, "New Project…");
        _newProjectButton.Click += (_, _) => _onNewProject?.Invoke();
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titleStack = new StackPanel();
        titleStack.Children.Add(PageHeading.Label("HOME"));
        titleStack.Children.Add(PageHeading.Title("Home"));
        Grid.SetColumn(titleStack, 0);
        header.Children.Add(titleStack);
        Grid.SetColumn(_newProjectButton, 1);
        _newProjectButton.VerticalAlignment = VerticalAlignment.Bottom;
        header.Children.Add(_newProjectButton);

        var main = new StackPanel { Spacing = DesignTokens.SpaceXl };
        main.Children.Add(header);
        main.Children.Add(Section("Your tasks", _tiles));
        main.Children.Add(Section("Commercial snapshot", _commercialText));
        main.Children.Add(Section("Project status", _statusChartHost));
        main.Children.Add(Section("Upcoming milestones", _milestonesList));
        main.Children.Add(Section("Task list", _taskList));

        var rail = new StackPanel { Spacing = DesignTokens.SpaceLg, Width = 280 };
        rail.Children.Add(Section("Continue", _continueList));
        rail.Children.Add(Section("Recent", _recentList));
        rail.Children.Add(Section("Favourite", _favouriteList));
        rail.Children.Add(Section("Recently changed", _recentlyChangedList));

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var mainHost = new ScrollViewer { Content = main, Margin = new Thickness(0, 0, DesignTokens.SpaceXl, 0) };
        Grid.SetColumn(mainHost, 0);
        body.Children.Add(mainHost);
        var railHost = new ScrollViewer { Content = rail };
        Grid.SetColumn(railHost, 1);
        body.Children.Add(railHost);

        var page = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceLg };
        page.Children.Add(body);

        AutomationProperties.SetName(this, "Home dashboard");
        Content = new ScrollViewer { Content = page };
    }

    /// <summary>Test-only (`WP 19.7C`, <c>WorkspaceChangesReattachTests</c>): counts every <see cref="RefreshAsync"/> call, proving a reattached view's subscription still reaches <see cref="OnWorkspaceChanged"/>.</summary>
    internal int RefreshCount { get; private set; }

    /// <summary>Re-reads every source and rebuilds every region.</summary>
    public async Task RefreshAsync()
    {
        RefreshCount++;

        var tasksTask = _tasksReadModel.ReadAsync();
        var projectsTask = _projectStatusReadModel.ReadAsync();
        var accountsTask = _accountsReadModel.ReadAsync();
        var quotesTask = ReadOpenQuotationsAsync();
        await Task.WhenAll(tasksTask, projectsTask, accountsTask, quotesTask).ConfigureAwait(true);
        await _cockpit.PrimeAsync().ConfigureAwait(true);

        RenderTiles(tasksTask.Result);
        RenderCommercial(quotesTask.Result, accountsTask.Result);
        RenderProjectStatus(projectsTask.Result);
        RenderMilestones(tasksTask.Result);
        RenderTaskList(tasksTask.Result);
        RenderRightRail();
    }

    private void RenderTiles(TasksSnapshot snapshot)
    {
        _tiles.Children.Clear();
        foreach (var bucket in TileBuckets)
        {
            var count = snapshot.Counts.GetValueOrDefault(bucket);
            var button = new Button { MinWidth = 150, MinHeight = 64, Margin = new Thickness(0, 0, DesignTokens.SpaceMd, DesignTokens.SpaceMd) };
            button.Classes.Add(ChromeStyles.Subtle);
            var content = new StackPanel { Spacing = DesignTokens.SpaceXs };
            content.Children.Add(new TextBlock { Text = count.ToString(CultureInfo.InvariantCulture), FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeDisplay, FontWeight = DesignTokens.WeightHeading });
            content.Children.Add(new TextBlock { Text = TileLabel(bucket), FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75 });
            button.Content = content;
            AutomationProperties.SetName(button, $"{TileLabel(bucket)}: {count} — open Tasks");
            button.Click += (_, _) => _openTasks();
            _tiles.Children.Add(button);
        }
    }

    private static string TileLabel(TaskBucket bucket) => bucket switch
    {
        TaskBucket.Overdue => "Overdue",
        TaskBucket.DueToday => "Due today",
        TaskBucket.DueThisWeek => "Due this week",
        TaskBucket.Approvals => "Approvals",
        TaskBucket.Finance => "Finance",
        _ => bucket.ToString(),
    };

    private void RenderCommercial(IReadOnlyList<Quotation> openQuotations, AccountsSnapshot accounts)
    {
        var quoteValue = Sum(openQuotations.Select(q => q.Total));
        var quotesLine = $"Quotes: {openQuotations.Count} open, {MoneyDisplay.Format(quoteValue)} total value.";

        // `WP 19.10P` (D1): `AccountsSnapshot.UnavailableReason` already
        // ends with its own trailing period ("No accounts reading yet.")
        // and is never actually null once `IsAvailable` is false — used
        // as it is, rather than appending a second period or falling back
        // to dead text a real reading can never reach.
        var invoicesLine = accounts.IsAvailable
            ? $"Invoices: {accounts.Overdue.Count + accounts.Due30.Count + accounts.Due90.Count} sent, {MoneyDisplay.Format(accounts.InvoicedTotal)} outstanding, {MoneyDisplay.Format(accounts.OverdueTotal)} overdue."
            : $"Invoices: unavailable — {accounts.UnavailableReason}";

        _commercialText.Text = $"{quotesLine}\n{invoicesLine}";
    }

    private void RenderProjectStatus(ProjectStatusSnapshot snapshot)
    {
        _statusChartHost.Children.Clear();

        DashboardChart.Bar[] bars =
        [
            new("On track", snapshot.Counts.GetValueOrDefault(ProjectHealthStatus.OnTrack), BrandPalette.SuccessBrushKey),
            new("At risk", snapshot.Counts.GetValueOrDefault(ProjectHealthStatus.AtRisk), BrandPalette.WarningBrushKey),
            new("Overdue", snapshot.Counts.GetValueOrDefault(ProjectHealthStatus.Overdue), BrandPalette.DangerBrushKey),
            new("On hold", snapshot.Counts.GetValueOrDefault(ProjectHealthStatus.OnHold), BrandPalette.MutedTextBrushKey),
        ];

        _statusChartHost.Children.Add(DashboardChart.HorizontalBars(bars));

        var total = bars.Sum(b => b.Value);
        var totals = new TextBlock { Text = $"{total} open project(s) in total.", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0) };
        _statusChartHost.Children.Add(totals);
    }

    private void RenderMilestones(TasksSnapshot snapshot)
    {
        _milestonesList.Children.Clear();

        if (snapshot.UpcomingMilestones.Count == 0)
        {
            _milestonesList.Children.Add(Muted("No upcoming milestones."));
            return;
        }

        foreach (var milestone in snapshot.UpcomingMilestones)
            _milestonesList.Children.Add(Row($"{milestone.TargetDate:d}  —  {milestone.Title}", milestone.MilestoneId, "Milestone"));
    }

    private void RenderTaskList(TasksSnapshot snapshot)
    {
        _taskList.Children.Clear();

        var top = snapshot.OpenTasks
            .OrderBy(t => t.DueDate ?? DateOnly.MaxValue)
            .Take(20)
            .ToList();

        if (top.Count == 0)
        {
            _taskList.Children.Add(Muted("Nothing due."));
            return;
        }

        foreach (var item in top)
        {
            var label = item.DueDate is { } due ? $"{due:d}  —  {item.Title}" : item.Title;
            _taskList.Children.Add(Row(label, item.ObjectId, item.Kind));
        }
    }

    private void RenderRightRail()
    {
        _continueList.Children.Clear();
        var recentProjects = _cockpit.RecentProjects.Take(3).ToList();
        if (recentProjects.Count == 0)
            _continueList.Children.Add(Muted("No projects yet."));
        else
            foreach (var name in recentProjects)
                _continueList.Children.Add(new TextBlock { Text = name, FontSize = DesignTokens.FontSizeBody });

        _recentList.Children.Clear();
        var recentActivity = _cockpit.RecentActivity.Take(5).ToList();
        if (recentActivity.Count == 0)
        {
            _recentList.Children.Add(Muted("Nothing opened yet this session."));
        }
        else
        {
            for (var i = 0; i < recentActivity.Count; i++)
            {
                var item = recentActivity[i];
                var index = i + 1;
                _recentList.Children.Add(ActionRow(item.Title, () => _onOpenRecent(index)));
            }
        }

        _favouriteList.Children.Clear();
        var favourites = _favourites?.Entries.TakeLast(5).Reverse().ToList() ?? [];
        if (favourites.Count == 0)
        {
            _favouriteList.Children.Add(Muted(_favourites is null
                ? "No platform capability exists yet for favouriting an object."
                : "Nothing favourited yet."));
        }
        else
        {
            foreach (var entry in favourites)
                _favouriteList.Children.Add(ActionRow(entry.DisplayName, () => { _onOpenFavourite?.Invoke(entry.Id, entry.Kind); return Task.CompletedTask; }));
        }

        _recentlyChangedList.Children.Clear();
        var changes = _cockpit.RecentlyChanged.Take(5).ToList();
        if (changes.Count == 0)
        {
            _recentlyChangedList.Children.Add(Muted("Nothing has changed yet."));
        }
        else
        {
            for (var i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                var index = i + 1;
                _recentlyChangedList.Children.Add(ActionRow($"{change.Title} — {change.ChangeType}", () => _onOpenRecentlyChanged?.Invoke(index) ?? Task.CompletedTask));
            }
        }
    }

    private Control Row(string text, Guid objectId, string kind)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = DesignTokens.FontSizeBody };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        var open = new Button { Content = "Open", MinHeight = DesignTokens.ControlSizeSmall };
        open.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(open, $"Open {text}");
        open.Click += (_, _) => _ = _openObjectRightUp(objectId, kind);
        Grid.SetColumn(open, 1);
        row.Children.Add(open);

        return row;
    }

    private static Control ActionRow(string text, Func<Task> onOpen)
    {
        var button = new Button { Content = text, HorizontalContentAlignment = HorizontalAlignment.Left, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(button, text);
        button.Click += (_, _) => _ = onOpen();
        return button;
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 };
        return block;
    }

    private static StackPanel Section(string title, Control content)
    {
        var section = new StackPanel { Spacing = DesignTokens.SpaceSm };
        section.Children.Add(Heading(title));
        section.Children.Add(content);
        return section;
    }

    private static TextBlock Heading(string text)
    {
        var block = new TextBlock { Text = text, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2 };
        ThemeReactiveBrush.Bind(block, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        return block;
    }

    private async Task<IReadOnlyList<Quotation>> ReadOpenQuotationsAsync()
    {
        var all = await _domainContext.Repository.ListByKindAsync(Quotation.CanonicalKind).ConfigureAwait(true);
        return all.OfType<Quotation>()
            .Where(q => q is not IDeletable { IsDeleted: true } && q.Status == QuotationStatus.Sent)
            .ToList();
    }

    private static Money Sum(IEnumerable<Money> amounts)
    {
        var list = amounts.ToList();
        return list.Count == 0 ? Money.Zero(CurrencyCode.Gbp) : Money.Sum(list, list[0].Currency);
    }

    private void OnWorkspaceChanged(WorkspaceChange change) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Best-effort background refresh — mirrors every sibling
                // rail view's own identical "the next real entry is the
                // backstop" shape (`ProjectsAreaView.OnWorkspaceChanged`).
            }
        });
}
