using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Workspace;
using Tempest.Core.Navigation;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Engineering Cockpit (`WP 10.1A`) — the complete graphical
/// realisation of <see cref="EngineeringCockpit"/>, the Workspace's own
/// default landing screen (`ADR-0069`). Every one of the twenty named
/// regions this Work Package's own controlling instruction lists is a
/// <see cref="CockpitCardControl"/> bound directly to an
/// <see cref="EngineeringCockpit"/> member — real data wherever that
/// member already is (`WP 8.1C`–`WP 9.5A`) or was upgraded to be by this
/// Work Package; an honest, disclosed placeholder wherever no platform
/// capability exists to back it, never fabricated content
/// (`WP 10.1A`'s own explicit instruction).
/// </summary>
internal sealed class CockpitView : UserControl
{
    private readonly EngineeringCockpit _cockpit;
    private readonly IReadOnlyList<NavigationItem> _areas;
    private readonly Action _onContinue;
    private readonly Action<int> _onOpenRecent;
    private readonly Action _onOpenCommandPalette;
    private readonly Action<string> _onSwitchArea;
    private readonly FavouriteObjectsState? _favourites;
    private readonly Action<Guid, string>? _onOpenFavourite;
    private readonly WrapPanel _cards = new();

    /// <summary>Initialises a new instance of the <see cref="CockpitView"/> class.</summary>
    /// <param name="favourites">
    /// The Desktop-local "any object" favourites list (`WP 10.6A`) — the
    /// "Favourite Projects" card's own real source since `WP 10.7A`
    /// (Feature Completion), filtered to Kind <c>"Project"</c>.
    /// <see langword="null"/> (any existing caller/test that never
    /// threads it through) leaves the card at its own honest, pre-`WP
    /// 10.7A` empty-capability message — never a crash. Deliberately
    /// Desktop-layer, not <see cref="EngineeringCockpit"/>'s own concern —
    /// see <see cref="FavouriteObjectsState"/>'s own remarks on why this
    /// is genuinely a different concept from the App-layer's still-unbuilt
    /// <see cref="EngineeringCockpit.FavouriteProjects"/>.
    /// </param>
    /// <param name="onOpenFavourite">Opens a favourited Project as a document tab — required whenever <paramref name="favourites"/> is non-null.</param>
    public CockpitView(
        EngineeringCockpit cockpit, IReadOnlyList<NavigationItem> areas, Action onContinue, Action<int> onOpenRecent, Action onOpenCommandPalette, Action<string> onSwitchArea,
        FavouriteObjectsState? favourites = null, Action<Guid, string>? onOpenFavourite = null)
    {
        ArgumentNullException.ThrowIfNull(cockpit);
        ArgumentNullException.ThrowIfNull(areas);
        _cockpit = cockpit;
        _areas = areas;
        _onContinue = onContinue ?? throw new ArgumentNullException(nameof(onContinue));
        _onOpenRecent = onOpenRecent ?? throw new ArgumentNullException(nameof(onOpenRecent));
        _onOpenCommandPalette = onOpenCommandPalette ?? throw new ArgumentNullException(nameof(onOpenCommandPalette));
        _onSwitchArea = onSwitchArea ?? throw new ArgumentNullException(nameof(onSwitchArea));
        _favourites = favourites;
        _onOpenFavourite = onOpenFavourite;

        var root = new DockPanel();

        var searchBar = new Button
        {
            Content = "🔎  Search / Command Palette  (Ctrl+K)",
            Margin = new Avalonia.Thickness(12, 12, 12, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        searchBar.Click += (_, _) => _onOpenCommandPalette();
        DockPanel.SetDock(searchBar, Dock.Top);
        root.Children.Add(searchBar);

        var scroll = new ScrollViewer { Content = _cards, Margin = new Avalonia.Thickness(6) };
        root.Children.Add(scroll);

        Content = root;

        Refresh();
    }

    /// <summary>Rebuilds every card from a fresh, live read of <see cref="EngineeringCockpit"/> — called on first show and after any action taken from the Cockpit itself.</summary>
    public void Refresh()
    {
        _cards.Children.Clear();

        AddWelcomeCard();
        AddRecentProjectsCard();
        AddFavouriteProjectsCard();
        AddAttentionCard();
        AddOpenDecisionsCard();
        AddBlockedItemsCard();
        AddOverdueActionsCard();
        AddHealthDashboardCard();
        AddKpiCard("📋", "Requirements KPIs", _cockpit.RequirementsKpiCards);
        AddKpiCard("🧮", "Calculations KPIs", _cockpit.CalculationsKpiCards);
        AddKpiCard("✔", "Verification KPIs", _cockpit.VerificationKpiCards);
        AddKpiCard("📄", "Documentation KPIs", _cockpit.DocumentsKpiCards);
        AddKpiCard("🏭", "Manufacturing KPIs", _cockpit.ManufacturingKpiCards);
        AddDigitalThreadCard();
        AddRiskSummaryCard();
        AddUpcomingMilestonesCard();
        AddRecentActivityCard();
        AddWorkspaceStatusCard();
        AddQuickActionsCard();
        AddNavigationShortcutsCard();
    }

    private void AddWelcomeCard()
    {
        var card = new CockpitCardControl("👋", "Welcome / Continue Working");
        card.AddLine($"Project: {_cockpit.ProjectName}");

        if (_cockpit.ContinueWhereILeftOff is { } item)
            card.AddAction($"Continue: {IconRegistry.Resolve(item.Kind)} {item.Title}", () => { _onContinue(); Refresh(); });
        else
            card.AddLine("Nothing to continue yet — open an object to begin.", 0.7);

        _cards.Children.Add(card);
    }

    private void AddRecentProjectsCard()
    {
        var card = new CockpitCardControl("📁", "Recent Projects");

        if (_cockpit.RecentProjects.Count == 0)
            card.AddLine("No projects yet.", 0.7);
        else
            foreach (var name in _cockpit.RecentProjects)
                card.AddLine(name);

        _cards.Children.Add(card);
    }

    /// <summary>
    /// The "Favourite Projects" card — real since `WP 10.7A` (Feature
    /// Completion), sourced from <see cref="FavouriteObjectsState"/>
    /// (Desktop-local, already-shipped `WP 10.6A`), filtered to Kind
    /// <c>"Project"</c>. Falls back to the original, honest "no platform
    /// capability" message only if <see cref="_favourites"/> was never
    /// threaded through (any existing caller/test that constructs this
    /// View without it) — never a crash. An empty filtered list (the
    /// user has favourited other objects, just no Project yet) gets its
    /// own distinct, accurate message, never conflated with "the
    /// capability doesn't exist."
    /// </summary>
    private void AddFavouriteProjectsCard()
    {
        var card = new CockpitCardControl("⭐", "Favourite Projects");

        if (_favourites is null)
        {
            card.AddLine("No platform capability exists yet for favouriting a project — honest placeholder, not fabricated data.", 0.6);
            _cards.Children.Add(card);
            return;
        }

        var favouriteProjects = _favourites.Entries.Where(e => e.Kind == "Project").ToList();
        if (favouriteProjects.Count == 0)
        {
            card.AddLine("No favourite Projects yet — star one from the Project Explorer's own context menu.", 0.7);
        }
        else
        {
            foreach (var entry in favouriteProjects)
                // No inline Refresh() here (`TD-58`): the open callback is
                // WorkspaceViewCoordinator.NavigateToObject, whose
                // NavigateToObjectAsync already refreshes this Cockpit —
                // the inline call made every favourite open rebuild all
                // twenty cards twice.
                card.AddAction($"{IconRegistry.Resolve(entry.Kind)} {entry.DisplayName}", () => _onOpenFavourite?.Invoke(entry.Id, entry.Kind));
        }

        _cards.Children.Add(card);
    }

    private void AddAttentionCard()
    {
        var card = new CockpitCardControl("⚠", "What Needs Attention");

        foreach (var item in _cockpit.AttentionItems)
            card.AddLine($"{item.Title} — {item.Detail}");

        _cards.Children.Add(card);
    }

    private void AddOpenDecisionsCard()
    {
        var card = new CockpitCardControl("🗳", "Open Decisions");

        if (_cockpit.OpenDecisions.Count == 0)
            card.AddLine("No live Decisions recorded yet.", 0.7);
        else
            foreach (var decision in _cockpit.OpenDecisions)
                card.AddLine(decision);

        _cards.Children.Add(card);
    }

    private void AddBlockedItemsCard()
    {
        var card = new CockpitCardControl("⛔", "Blocked Items", HealthColors.Resolve(EngineeringHealthStatus.Blocked));

        if (_cockpit.BlockedItems.Count == 0)
            card.AddLine("Nothing blocked right now.", 0.7);
        else
            foreach (var blocked in _cockpit.BlockedItems)
                card.AddLine(blocked);

        _cards.Children.Add(card);
    }

    private void AddOverdueActionsCard()
    {
        var card = new CockpitCardControl("⏰", "Overdue Actions");

        // Real overdue work. This card carried an honest placeholder for
        // as long as it existed — "no due-date field exists on any
        // Task/Action Domain object yet" — which was true until `TD-81`
        // gave EngineeringTask a due date and a work state. Nothing
        // overdue is now a finding rather than an absence of capability,
        // so the empty case says so plainly.
        var overdue = _cockpit.OverdueActionLines;

        if (overdue.Count == 0)
            card.AddLine("Nothing is overdue.", 0.8);

        foreach (var line in overdue)
            card.AddLine(line);

        card.AddLine($"Open Tasks/Actions: {_cockpit.OpenTaskCount}", 0.8);
        _cards.Children.Add(card);
    }

    private void AddHealthDashboardCard()
    {
        var card = new CockpitCardControl("💚", "Project Health Dashboard", HealthColors.Resolve(_cockpit.Health));
        card.AddLine($"Overall: {HealthColors.Label(_cockpit.Health)}");
        card.AddLine(_cockpit.HealthScoreDisplay, 0.8);

        var grid = new WrapPanel();
        foreach (var (label, status) in new (string, EngineeringHealthStatus)[]
        {
            ("Requirements", _cockpit.RequirementsStatus),
            ("Calculations", _cockpit.CalculationStatus),
            ("Verification", _cockpit.VerificationStatus),
            ("Documentation", _cockpit.DocumentationStatus),
            ("Manufacturing", _cockpit.ManufacturingStatus),
            ("Review", _cockpit.ReviewStatus),
        })
        {
            grid.Children.Add(new Border
            {
                BorderBrush = HealthColors.Resolve(status),
                BorderThickness = new Avalonia.Thickness(0, 0, 0, 2),
                Margin = new Avalonia.Thickness(2),
                // The status text itself is now coloured too (`WP 10.5C`,
                // "coloured health indicators"), not only its own
                // underline — colour is still never the only signal
                // (`HealthColors`'s own documented accessibility rule):
                // the real word ("Healthy"/"Attention"/"Blocked") is
                // always present alongside it.
                Child = new TextBlock { Text = $"{label}: {HealthColors.Label(status)}", FontSize = 11, Foreground = HealthColors.Resolve(status), FontWeight = FontWeight.SemiBold },
            });
        }

        card.AddContent(grid);
        _cards.Children.Add(card);
    }

    private void AddKpiCard(string glyph, string title, IReadOnlyList<CockpitKpiCard> kpis)
    {
        var card = new CockpitCardControl(glyph, title);

        // A real progress bar for every genuine coverage KPI
        // (`CockpitKpiCard.PercentValue`, `WP 10.5C`) — every other KPI
        // (a raw count) still renders as the identical plain text line
        // this card always has, via `AddKpiRow`'s own disclosed fallback.
        foreach (var kpi in kpis)
            card.AddKpiRow(kpi.Label, kpi.Value, kpi.PercentValue, kpi.IsPlaceholder);

        _cards.Children.Add(card);
    }

    private void AddDigitalThreadCard()
    {
        var card = new CockpitCardControl("🔗", "Digital Thread Summary");
        card.AddLine(_cockpit.DigitalThreadSummary);
        _cards.Children.Add(card);
    }

    private void AddRiskSummaryCard()
    {
        var card = new CockpitCardControl("🛡", "Risk Summary");
        card.AddLine(_cockpit.RiskSummary);
        _cards.Children.Add(card);
    }

    private void AddUpcomingMilestonesCard()
    {
        var card = new CockpitCardControl("🏁", "Upcoming Milestones");

        if (_cockpit.UpcomingMilestones.Count == 0)
            card.AddLine("No upcoming Milestones recorded.", 0.7);
        else
            foreach (var milestone in _cockpit.UpcomingMilestones)
                card.AddLine(milestone);

        _cards.Children.Add(card);
    }

    private void AddRecentActivityCard()
    {
        var card = new CockpitCardControl("🕒", "Recent Engineering Activity");

        if (_cockpit.RecentActivity.Count == 0)
        {
            card.AddLine("Nothing opened yet this session.", 0.7);
        }
        else
        {
            for (var i = 0; i < _cockpit.RecentActivity.Count; i++)
            {
                var item = _cockpit.RecentActivity[i];
                var index = i + 1;
                card.AddAction($"{IconRegistry.Resolve(item.Kind)} {item.Title} — {item.OpenedAt:HH:mm:ss}", () => { _onOpenRecent(index); Refresh(); });
            }
        }

        _cards.Children.Add(card);
    }

    private void AddWorkspaceStatusCard()
    {
        var card = new CockpitCardControl("🖥", "Workspace Status");
        card.AddLine($"Areas registered: {_cockpit.AreaCount}");
        card.AddLine($"Documents open: {_cockpit.OpenDocumentCount}");
        _cards.Children.Add(card);
    }

    private void AddQuickActionsCard()
    {
        var card = new CockpitCardControl("⚡", "Quick Actions");

        foreach (var hint in _cockpit.QuickActions)
            card.AddLine(hint);

        foreach (var action in _cockpit.OpenActions)
            card.AddLine($"▸ {action.Title} ({action.Owner})");

        if (_cockpit.QuickActions.Count == 0 && _cockpit.OpenActions.Count == 0)
            card.AddLine("Nothing to do right now.", 0.7);

        _cards.Children.Add(card);
    }

    private void AddNavigationShortcutsCard()
    {
        var card = new CockpitCardControl("🧭", "Navigation Shortcuts");

        if (_areas.Count == 0)
            card.AddLine("No areas registered yet.", 0.7);
        else
            foreach (var area in _areas)
                card.AddAction($"{IconRegistry.Resolve(area.Icon)} {area.Title}", () => _onSwitchArea(area.Id));

        _cards.Children.Add(card);
    }
}
