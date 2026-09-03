using Avalonia;
using Avalonia.Automation;
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
/// default landing screen (`ADR-0069`). Every region is bound directly to
/// an <see cref="EngineeringCockpit"/> member — real data wherever that
/// member already is (`WP 8.1C`–`WP 9.5A`) or was upgraded to be; an
/// honest, disclosed placeholder wherever no platform capability exists
/// to back it, never fabricated content (`WP 10.1A`'s own explicit
/// instruction).
/// </summary>
/// <remarks>
/// <para>
/// <b>Information architecture, since the Desktop brand alignment.</b>
/// The Cockpit answers the user's questions in the order they ask them,
/// top to bottom: <em>where am I and is it healthy</em> (the hero: the
/// project, the health readout, the one thing to continue);
/// <em>what needs my attention</em> (four state tiles — attention,
/// blocked, overdue, open decisions — each a count with its first
/// items); <em>how is each discipline doing</em> (a strip of six
/// discipline chips, each a real navigation shortcut); then the detail
/// cards — KPIs with real coverage bars, recent work, favourites,
/// milestones, risk, the digital thread, workspace status and quick
/// actions. The same twenty regions as before; the hierarchy is new.
/// </para>
/// <para>
/// Every card is a <see cref="CockpitCardControl"/>, rebuilt on
/// <see cref="Refresh"/> from one live read scope (`WP-E`).
/// </para>
/// </remarks>
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

    private readonly StackPanel _page = new() { Spacing = DesignTokens.SpaceXl };
    private readonly WrapPanel _hero = new() { Orientation = Orientation.Horizontal };
    private readonly WrapPanel _tiles = new() { Orientation = Orientation.Horizontal };
    private readonly WrapPanel _disciplines = new() { Orientation = Orientation.Horizontal };
    private readonly WrapPanel _cards = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(-DesignTokens.SpaceMd) };

    /// <summary>Initialises a new instance of the <see cref="CockpitView"/> class.</summary>
    /// <param name="favourites">
    /// The Desktop-local "any object" favourites list (`WP 10.6A`) — the
    /// "Favourite Projects" card's own real source since `WP 10.7A`
    /// (Feature Completion), filtered to Kind <c>"Project"</c>.
    /// <see langword="null"/> (any existing caller/test that never
    /// threads it through) leaves the card at its own honest, pre-`WP
    /// 10.7A` empty-capability message — never a crash.
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

        _page.Margin = DesignTokens.PagePadding;
        _page.MaxWidth = 1480;
        _page.HorizontalAlignment = HorizontalAlignment.Left;

        _page.Children.Add(_hero);
        _page.Children.Add(Section("NEEDS ATTENTION", _tiles));
        _page.Children.Add(Section("DISCIPLINES", _disciplines));
        _page.Children.Add(Section("DETAIL", _cards));

        var scroll = new ScrollViewer { Content = _page, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        ThemeReactiveBrush.Bind(this, BackgroundProperty, BrandPalette.PageBackgroundBrushKey);
        Content = scroll;

        Refresh();
    }

    /// <summary>Rebuilds every region from a fresh, live read of <see cref="EngineeringCockpit"/> — called on first show and after any action taken from the Cockpit itself.</summary>
    /// <remarks>
    /// <b>`WP-E`.</b> The whole rebuild runs inside one
    /// <see cref="EngineeringCockpit.BeginReadScope"/> pass. The read is
    /// still fresh — the scope is opened here and closed on the way out,
    /// so each rebuild re-reads everything — but each underlying
    /// persistence read now happens once for the pass instead of once per
    /// card that needs it. It also makes the cards agree with each other:
    /// a KPI total and the coverage percentage beside it are now computed
    /// from the same snapshot rather than from two separate reads.
    /// </remarks>
    public void Refresh()
    {
        using var readScope = _cockpit.BeginReadScope();

        _hero.Children.Clear();
        _tiles.Children.Clear();
        _disciplines.Children.Clear();
        _cards.Children.Clear();

        BuildHero();

        // What needs attention — four state tiles.
        AddAttentionTile();
        AddBlockedTile();
        AddOverdueTile();
        AddOpenDecisionsTile();

        BuildDisciplineStrip();

        // The detail cards.
        AddRecentActivityCard();

        // `WP-Z4` Productisation Phase 1 (P1) — `EngineeringCockpit.KpiCards`,
        // the one real cross-discipline aggregate (Requirements/
        // Verification/Calculations/Documentation/Review/Risks totals,
        // `ADR-0103`), was fully computed but never rendered anywhere:
        // only the five per-discipline KPI sets below ever reached this
        // view. Placed first among the KPI cards — the summary before the
        // detail, the same order the Cockpit's own information
        // architecture already uses everywhere else (hero, then tiles,
        // then per-discipline detail).
        AddKpiCard(IconGeometry.Chart, "Engineering Overview", _cockpit.KpiCards);
        AddKpiCard(IconGeometry.Requirement, "Requirements KPIs", _cockpit.RequirementsKpiCards);
        AddKpiCard(IconGeometry.Calculator, "Calculations KPIs", _cockpit.CalculationsKpiCards);
        AddKpiCard(IconGeometry.CheckCircle, "Verification KPIs", _cockpit.VerificationKpiCards);
        AddKpiCard(IconGeometry.Document, "Documentation KPIs", _cockpit.DocumentsKpiCards);
        AddKpiCard(IconGeometry.Factory, "Manufacturing KPIs", _cockpit.ManufacturingKpiCards);
        AddFavouriteProjectsCard();
        AddRecentProjectsCard();
        AddUpcomingMilestonesCard();
        AddRiskSummaryCard();
        AddDigitalThreadCard();
        AddQuickActionsCard();
        AddNavigationShortcutsCard();
        AddWorkspaceStatusCard();
    }

    // ------------------------------------------------------------
    // Hero — where am I, is it healthy, what do I continue?
    // ------------------------------------------------------------

    private void BuildHero()
    {
        var left = new StackPanel { Spacing = DesignTokens.SpaceSm, VerticalAlignment = VerticalAlignment.Center, MinWidth = 320, MaxWidth = 720, Margin = new Thickness(0, 0, DesignTokens.SpaceXxl, DesignTokens.SpaceMd) };

        left.Children.Add(Label("ENGINEERING COCKPIT"));

        var title = new TextBlock
        {
            Text = _cockpit.ProjectName,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeDisplay + 4,
            FontWeight = DesignTokens.WeightHeading,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
        };
        ThemeReactiveBrush.Bind(title, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        left.Children.Add(title);

        var subtitle = new TextBlock
        {
            Text = $"{_cockpit.AreaCount} discipline area(s) registered  ·  {_cockpit.OpenDocumentCount} document(s) open  ·  {_cockpit.DigitalThreadSummary}",
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeReactiveBrush.Bind(subtitle, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        left.Children.Add(subtitle);

        var actions = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };

        if (_cockpit.ContinueWhereILeftOff is { } item)
        {
            var resume = new Button { Content = $"Continue  {IconRegistry.Resolve(item.Kind)} {item.Title}", MinHeight = DesignTokens.ControlSizeMedium };
            resume.Classes.Add(ChromeStyles.Primary);
            AutomationProperties.SetName(resume, "Continue working");
            ToolTip.SetTip(resume, $"Reopen {item.Title}, the last object you worked on");
            resume.Click += (_, _) => { _onContinue(); Refresh(); };
            resume.Margin = new Thickness(0, 0, DesignTokens.SpaceMd, DesignTokens.SpaceMd);
            actions.Children.Add(resume);
        }
        else
        {
            var hint = new TextBlock { Text = "Nothing to continue yet — open an object to begin.", FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center };
            ThemeReactiveBrush.Bind(hint, TextBlock.ForegroundProperty, BrandPalette.FaintTextBrushKey);
            hint.Margin = new Thickness(0, 0, DesignTokens.SpaceXl, DesignTokens.SpaceMd);
            actions.Children.Add(hint);
        }

        var search = new Button { MinHeight = DesignTokens.ControlSizeMedium };
        var searchContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        searchContent.Children.Add(IconGeometry.Build(IconGeometry.Search, 13));
        searchContent.Children.Add(new TextBlock { Text = "Search / Command Palette", VerticalAlignment = VerticalAlignment.Center });
        searchContent.Children.Add(new TextBlock { Text = "CTRL K", FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeLabel, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 });
        search.Content = searchContent;
        search.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(search, "Search / Command Palette");
        search.Click += (_, _) => _onOpenCommandPalette();
        search.Margin = new Thickness(0, 0, DesignTokens.SpaceMd, DesignTokens.SpaceMd);
        actions.Children.Add(search);

        left.Children.Add(actions);
        _hero.Children.Add(left);

        // The health readout — the one number the whole Cockpit exists to
        // answer, in the readout face, coloured by the platform's own
        // health colour language and always paired with its word.
        var health = _cockpit.Health;
        var readout = new CockpitCardControl(IconGeometry.Activity, "Project Health", HealthColors.Resolve(health))
        {
            MinWidth = 240,
            MaxWidth = 320,
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceMd),
            VerticalAlignment = VerticalAlignment.Top,
        };
        readout.AddReadout(HealthColors.Label(health), _cockpit.HealthScoreDisplay, HealthColors.Resolve(health));
        _hero.Children.Add(readout);
    }

    // ------------------------------------------------------------
    // What needs attention — four tiles.
    // ------------------------------------------------------------

    private void AddAttentionTile()
    {
        var items = _cockpit.AttentionItems;
        var tile = Tile(IconGeometry.Warning, "What Needs Attention", items.Count, "item(s) across every discipline", items.Count == 0 ? null : HealthColors.Resolve(EngineeringHealthStatus.Attention));

        foreach (var item in items.Take(4))
            tile.AddLine($"{item.Title} — {item.Detail}");
        if (items.Count > 4)
            tile.AddMeta($"+{items.Count - 4} more");

        _tiles.Children.Add(tile);
    }

    private void AddBlockedTile()
    {
        var blocked = _cockpit.BlockedItems;
        var tile = Tile(IconGeometry.Blocked, "Blocked Items", blocked.Count, blocked.Count == 0 ? "nothing blocked right now" : "blocked, needing a decision or an input", blocked.Count == 0 ? null : HealthColors.Resolve(EngineeringHealthStatus.Blocked));

        if (blocked.Count == 0)
            tile.AddLine("Nothing blocked right now.", 0.7);
        foreach (var line in blocked.Take(4))
            tile.AddLine(line);
        if (blocked.Count > 4)
            tile.AddMeta($"+{blocked.Count - 4} more");

        _tiles.Children.Add(tile);
    }

    private void AddOverdueTile()
    {
        // Real overdue work. This card carried an honest placeholder for
        // as long as it existed — "no due-date field exists on any
        // Task/Action Domain object yet" — which was true until `TD-81`
        // gave EngineeringTask a due date and a work state. Nothing
        // overdue is now a finding rather than an absence of capability,
        // so the empty case says so plainly.
        var overdue = _cockpit.OverdueActionLines;
        var tile = Tile(IconGeometry.Clock, "Overdue Actions", overdue.Count, $"overdue  ·  {_cockpit.OpenTaskCount} open task(s)/action(s)", overdue.Count == 0 ? null : HealthColors.Resolve(EngineeringHealthStatus.Blocked));

        if (overdue.Count == 0)
            tile.AddLine("Nothing is overdue.", 0.8);
        foreach (var line in overdue.Take(4))
            tile.AddLine(line);
        if (overdue.Count > 4)
            tile.AddMeta($"+{overdue.Count - 4} more");

        tile.AddLine($"Open Tasks/Actions: {_cockpit.OpenTaskCount}", 0.8);
        _tiles.Children.Add(tile);
    }

    private void AddOpenDecisionsTile()
    {
        var decisions = _cockpit.OpenDecisions;
        var tile = Tile(IconGeometry.Decision, "Open Decisions", decisions.Count, decisions.Count == 0 ? "no live Decisions recorded yet" : "live Decision(s) awaiting an outcome", null);

        if (decisions.Count == 0)
            tile.AddLine("No live Decisions recorded yet.", 0.7);
        foreach (var decision in decisions.Take(4))
            tile.AddLine(decision);
        if (decisions.Count > 4)
            tile.AddMeta($"+{decisions.Count - 4} more");

        _tiles.Children.Add(tile);
    }

    private static CockpitCardControl Tile(StreamGeometry icon, string title, int count, string caption, IBrush? accent)
    {
        var tile = new CockpitCardControl(icon, title, accent) { MinWidth = 250, MaxWidth = 340 };
        tile.AddReadout(count.ToString(System.Globalization.CultureInfo.InvariantCulture), caption);
        return tile;
    }

    // ------------------------------------------------------------
    // Disciplines — six chips, each a real navigation shortcut.
    // ------------------------------------------------------------

    private void BuildDisciplineStrip()
    {
        foreach (var (label, status, areaKeyword) in new (string, EngineeringHealthStatus, string)[]
        {
            ("Mechanical", EngineeringHealthStatus.Unknown, "Mechanical"),
            ("Requirements", _cockpit.RequirementsStatus, "Requirement"),
            ("Calculations", _cockpit.CalculationStatus, "Calculation"),
            ("Verification", _cockpit.VerificationStatus, "Verification"),
            ("Documentation", _cockpit.DocumentationStatus, "Document"),
            ("Manufacturing", _cockpit.ManufacturingStatus, "Manufacturing"),
            ("Review", _cockpit.ReviewStatus, "Review"),
        })
        {
            var area = _areas.FirstOrDefault(a => a.Title.Contains(areaKeyword, StringComparison.OrdinalIgnoreCase));

            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = DisciplineColors.Resolve(label), VerticalAlignment = VerticalAlignment.Center });

            var name = new TextBlock { Text = label, FontSize = DesignTokens.FontSizeBody, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
            ThemeReactiveBrush.Bind(name, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
            content.Children.Add(name);

            // The status text itself is coloured too (`WP 10.5C`,
            // "coloured health indicators") — colour is still never the
            // only signal (`HealthColors`'s own documented accessibility
            // rule): the real word is always present alongside it.
            var state = new TextBlock
            {
                Text = HealthColors.Label(status).ToUpperInvariant(),
                FontFamily = DesignTokens.TitleFont,
                FontSize = DesignTokens.FontSizeLabel,
                FontWeight = DesignTokens.WeightLabel,
                LetterSpacing = DesignTokens.LabelTracking,
                Foreground = HealthColors.Resolve(status),
                VerticalAlignment = VerticalAlignment.Center,
            };
            content.Children.Add(state);

            var chip = new Button
            {
                Content = content,
                MinHeight = DesignTokens.ControlSizeMedium,
                Padding = new Thickness(DesignTokens.SpaceLg, DesignTokens.SpaceSm),
                Margin = new Thickness(0, 0, DesignTokens.SpaceMd, DesignTokens.SpaceMd),
                IsEnabled = area is not null,
            };
            chip.Classes.Add(ChromeStyles.Subtle);
            AutomationProperties.SetName(chip, $"{label} discipline");
            ToolTip.SetTip(chip, area is not null ? $"Open the {area.Title} area" : $"No {label} area is registered in this workspace yet.");
            if (area is not null)
            {
                var areaId = area.Id;
                chip.Click += (_, _) => _onSwitchArea(areaId);
            }

            _disciplines.Children.Add(chip);
        }
    }

    // ------------------------------------------------------------
    // Detail cards.
    // ------------------------------------------------------------

    private void AddRecentProjectsCard()
    {
        var card = new CockpitCardControl(IconGeometry.Folder, "Recent Projects");

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
        var card = new CockpitCardControl(IconGeometry.Star, "Favourite Projects");

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
                // NavigateToObjectAsync already refreshes this Cockpit.
                card.AddAction($"{IconRegistry.Resolve(entry.Kind)} {entry.DisplayName}", () => _onOpenFavourite?.Invoke(entry.Id, entry.Kind));
        }

        _cards.Children.Add(card);
    }

    private void AddKpiCard(StreamGeometry icon, string title, IReadOnlyList<CockpitKpiCard> kpis)
    {
        var card = new CockpitCardControl(icon, title);

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
        var card = new CockpitCardControl(IconGeometry.Link, "Digital Thread Summary");
        card.AddLine(_cockpit.DigitalThreadSummary);
        _cards.Children.Add(card);
    }

    private void AddRiskSummaryCard()
    {
        var card = new CockpitCardControl(IconGeometry.Shield, "Risk Summary");
        card.AddLine(_cockpit.RiskSummary);
        _cards.Children.Add(card);
    }

    private void AddUpcomingMilestonesCard()
    {
        var card = new CockpitCardControl(IconGeometry.Flag, "Upcoming Milestones");

        if (_cockpit.UpcomingMilestones.Count == 0)
            card.AddLine("No upcoming Milestones recorded.", 0.7);
        else
            foreach (var milestone in _cockpit.UpcomingMilestones)
                card.AddLine(milestone);

        _cards.Children.Add(card);
    }

    private void AddRecentActivityCard()
    {
        var card = new CockpitCardControl(IconGeometry.Clock, "Recent Engineering Activity");

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
        var card = new CockpitCardControl(IconGeometry.Monitor, "Workspace Status");
        card.AddLine($"Areas registered: {_cockpit.AreaCount}");
        card.AddLine($"Documents open: {_cockpit.OpenDocumentCount}");
        _cards.Children.Add(card);
    }

    private void AddQuickActionsCard()
    {
        var card = new CockpitCardControl(IconGeometry.Bolt, "Quick Actions");

        foreach (var hint in _cockpit.QuickActions)
            card.AddLine(hint);

        foreach (var action in _cockpit.OpenActions)
            card.AddLine($"→ {action.Title} ({action.Owner})");

        if (_cockpit.QuickActions.Count == 0 && _cockpit.OpenActions.Count == 0)
            card.AddLine("Nothing to do right now.", 0.7);

        _cards.Children.Add(card);
    }

    private void AddNavigationShortcutsCard()
    {
        var card = new CockpitCardControl(IconGeometry.Compass, "Navigation Shortcuts");

        if (_areas.Count == 0)
            card.AddLine("No areas registered yet.", 0.7);
        else
            foreach (var area in _areas)
                card.AddAction($"{IconRegistry.Resolve(area.Icon)} {area.Title}", () => _onSwitchArea(area.Id));

        _cards.Children.Add(card);
    }

    // ------------------------------------------------------------

    private static StackPanel Section(string label, Control content)
    {
        var section = new StackPanel { Spacing = DesignTokens.SpaceMd };
        section.Children.Add(Label(label));
        section.Children.Add(content);
        return section;
    }

    private static TextBlock Label(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeLabel,
            FontWeight = DesignTokens.WeightLabel,
            LetterSpacing = DesignTokens.LabelTracking,
        };
        ThemeReactiveBrush.Bind(label, TextBlock.ForegroundProperty, BrandPalette.FaintTextBrushKey);
        return label;
    }
}
