using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Workspace.Shell;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The global navigation rail (`TD-84`) — the first level of the
/// TempestOS navigation model, <c>Module → Project → Workspace</c>.
/// </summary>
/// <remarks>
/// <para>
/// A <b>view over <see cref="IShellNavigator"/></b>, never a second
/// navigation model: it raises intent and renders the navigator's own
/// current location, exactly as <see cref="RibbonView"/> is a view over
/// <c>ICommandRegistry</c>. Which module is highlighted is therefore
/// always the navigator's answer, so the rail cannot disagree with the
/// rest of the shell about where the user is.
/// </para>
/// <para>
/// <b>Every module the rail shows is real.</b> `WP 19.2B` (`TD-81`): an
/// undelivered module is removed from <see cref="ShellAreas.RailModules"/>
/// rather than shown dimmed — there is no "planned, not yet built" marker
/// left to draw, because nothing in <see cref="ShellAreas"/> claims a
/// capability that does not exist. The rail is still built from
/// <see cref="ShellAreas.RailModules"/>, so which modules exist is
/// declared once, in application state, not decided here.
/// </para>
/// <para>
/// <b>Visual language.</b> The design system's rail: a sunken instrument
/// surface, UPPERCASE section labels, one monochrome vector icon per
/// module, and the current module marked by a 2px accent rule on its left
/// edge over the 12% selection fill — never by colour alone (the title is
/// also set in the heading weight). Below <see cref="DesignTokens.CompactShellWidth"/>
/// the rail folds to its icons (<see cref="SetCompact"/>), keeping every
/// module reachable in a narrow window with its title in the tooltip. A
/// chevron at the rail's own foot (`WP 19.10O`) folds it the same way on
/// demand, at any width, and the choice is persisted (<see cref="SetCollapsed"/>).
/// </para>
/// </remarks>
public sealed class GlobalNavigationRail : UserControl
{
    private readonly IShellNavigator _navigator;
    private readonly StackPanel _buttons = new() { Spacing = DesignTokens.SpaceXs };
    private readonly List<ModuleItem> _modules = [];
    private readonly TextBlock _sectionLabel;
    private readonly Button _collapseChevron;
    private readonly ContentControl _collapseChevronIconHost;
    private bool _compact;
    private bool _collapsedByUser;

    /// <summary>Raised after the user picks a module, so the shell can render it.</summary>
    public event Action? NavigationRequested;

    /// <summary>Raised after <see cref="SetCollapsed"/> changes the rail's own manually-collapsed state, carrying the new state — the caller's own cue to persist it (`WP 19.10O`), exactly as <see cref="RibbonView.CollapsedChanged"/> already does for `TD-70`.</summary>
    public event Action<bool>? CollapsedChanged;

    /// <summary>Initialises a new instance of the <see cref="GlobalNavigationRail"/> class.</summary>
    /// <param name="navigator">The shell navigator this rail is a view over.</param>
    /// <exception cref="ArgumentNullException"><paramref name="navigator"/> is <see langword="null"/>.</exception>
    public GlobalNavigationRail(IShellNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        _navigator = navigator;

        Width = DesignTokens.RailWidth;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, BrandPalette.SunkenBackgroundBrushKey);

        _sectionLabel = Label("MODULES");
        _sectionLabel.Margin = new Thickness(DesignTokens.SpaceLg + DesignTokens.SpaceSm, DesignTokens.SpaceXl, DesignTokens.SpaceLg, DesignTokens.SpaceMd);

        // `WP 19.7A`: every rail button is a plain global module now —
        // `ShellArea.Engineering`'s own scope-aware verbs
        // (`GoToEngineeringAsync`) are reached from
        // `EngineeringDepartment`'s own Modules → Mechanical node instead
        // of from this rail directly (see `ShellArea.EngineeringDepartment`'s
        // own remarks), so every button here goes through the one plain
        // `GoToModuleAsync`.
        foreach (var module in ShellAreas.RailModules)
        {
            var area = module.Area;
            AddModule(module, () => _navigator.GoToModuleAsync(area));
        }

        // `WP 19.10O`: a collapse control at the rail's own foot — a
        // chevron toggling the manual collapse independent of the
        // responsive fold above, matching `RibbonView`'s own `TD-70`
        // minimise affordance rather than inventing a second interaction
        // language for the same idea.
        _collapseChevronIconHost = new ContentControl
        {
            Width = 14,
            Height = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _collapseChevron = new Button
        {
            Content = _collapseChevronIconHost,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            MinHeight = DesignTokens.ControlSizeMedium,
            Margin = new Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceSm),
        };
        _collapseChevron.Classes.Add(ChromeStyles.Flat);
        _collapseChevron.Click += (_, _) => ToggleCollapsed();

        var footer = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = _collapseChevron };
        ThemeReactiveBrush.Bind(footer, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        DockPanel.SetDock(footer, Dock.Bottom);

        var body = new DockPanel();
        DockPanel.SetDock(_sectionLabel, Dock.Top);
        body.Children.Add(_sectionLabel);
        body.Children.Add(footer);
        body.Children.Add(new ScrollViewer { Content = _buttons, Padding = new Thickness(DesignTokens.SpaceMd, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });

        var frame = new Border { Child = body, BorderThickness = new Thickness(0, 0, 1, 0) };
        ThemeReactiveBrush.Bind(frame, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        Content = frame;

        ActualThemeVariantChanged += (_, _) => RefreshSelection();
        RefreshSelection();
        ApplyFoldState();
    }

    /// <summary>Gets whether the rail is currently folded to its icons — either the responsive rule (<see cref="IsCompact"/>) or the manual toggle (<see cref="IsCollapsed"/>), or both.</summary>
    public bool IsFolded => _compact || _collapsedByUser;

    /// <summary>Gets whether the shell's own responsive rule is currently narrowing the rail (below <see cref="DesignTokens.CompactShellWidth"/>).</summary>
    public bool IsCompact => _compact;

    /// <summary>Gets whether the rail is manually collapsed to its icon width (`WP 19.10O`) — independent of <see cref="IsCompact"/>.</summary>
    public bool IsCollapsed => _collapsedByUser;

    /// <summary>Folds the rail to icons only below the shell's own compact threshold, or restores its titles above it — the shell calls this from its own width, so a narrow window keeps every module reachable. Below the threshold this always wins over <see cref="SetCollapsed"/>'s own manual state (`WP 19.10O`, "the responsive rule wins").</summary>
    public void SetCompact(bool compact)
    {
        if (_compact == compact)
            return;

        _compact = compact;
        ApplyFoldState();
    }

    /// <summary>Manually collapses the rail to its icon width, or restores it — reachable at any window width above the compact threshold (`WP 19.10O`), independent of <see cref="SetCompact"/>.</summary>
    public void SetCollapsed(bool collapsed)
    {
        if (_collapsedByUser == collapsed)
            return;

        _collapsedByUser = collapsed;
        ApplyFoldState();
        CollapsedChanged?.Invoke(collapsed);
    }

    /// <summary>Toggles <see cref="IsCollapsed"/> — the footer chevron's own target, and the Command Palette's "Collapse navigation" shell action's own target.</summary>
    public void ToggleCollapsed() => SetCollapsed(!_collapsedByUser);

    private void ApplyFoldState()
    {
        var folded = IsFolded;
        Width = folded ? DesignTokens.RailCompactWidth : DesignTokens.RailWidth;
        _sectionLabel.IsVisible = !folded;

        foreach (var item in _modules)
        {
            item.Title.IsVisible = !folded;
            item.Button.HorizontalContentAlignment = folded ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            item.Button.Padding = folded ? new Thickness(0, DesignTokens.SpaceMd) : new Thickness(DesignTokens.SpaceLg, DesignTokens.SpaceMd);
        }

        // The chevron's own name/tooltip/icon reflect the manual toggle's
        // own next action — not whether the rail happens to be folded right
        // now for the unrelated, responsive reason (`CollapsibleColumn`'s
        // own identical distinction).
        var actionName = _collapsedByUser ? "Expand navigation" : "Collapse navigation";
        AutomationProperties.SetName(_collapseChevron, actionName);
        ToolTip.SetTip(_collapseChevron, $"{actionName} (Ctrl+B)");
        _collapseChevronIconHost.Content = IconGeometry.Build(_collapsedByUser ? IconGeometry.ChevronRight : IconGeometry.ChevronLeft, 14);
    }

    /// <summary>Re-highlights whichever module the navigator currently reports — called after every shell move, and again on a theme switch so the state brushes re-resolve for the new variant.</summary>
    public void RefreshSelection()
    {
        var current = _navigator.Current.Area;

        foreach (var item in _modules)
        {
            var isCurrent = item.Area == current
                || (item.Area == ShellArea.Projects && current == ShellArea.ProjectWorkspace);

            item.Title.FontWeight = isCurrent ? DesignTokens.WeightHeading : DesignTokens.WeightBody;
            item.Rule.IsVisible = isCurrent;
            item.Frame.Background = isCurrent ? BrandPalette.Brush(BrandPalette.SelectedBackgroundBrushKey) : Brushes.Transparent;
            item.Title.Foreground = BrandPalette.Brush(isCurrent ? BrandPalette.HeadingTextBrushKey : BrandPalette.BodyTextBrushKey);
            item.Icon.Foreground = BrandPalette.Brush(isCurrent ? BrandPalette.AccentBrushKey : BrandPalette.MutedTextBrushKey);
        }
    }

    private void AddModule(ShellAreaDescriptor module, Func<Task> navigate)
    {
        // The icon inherits its Foreground from this host, which the
        // selection state paints — so one binding tints the vector.
        var iconHost = new ContentControl
        {
            Content = IconGeometry.Build(IconFor(module.Area), DesignTokens.ChromeIconSize),
            Width = 20,
            Height = 20,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var title = new TextBlock
        {
            Text = module.Title,
            FontSize = DesignTokens.FontSizeBody + 1,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        title.Margin = new Thickness(DesignTokens.SpaceLg, 0, 0, 0);
        // The title may never ask for more than the rail has left beside the
        // icon: rail width, less the scroll padding, the button padding, the
        // icon and the title margin. Without this ceiling the measure
        // reaches the text at infinite width and "Engineering Calculations"
        // pushed its button 14 px past the rail (found by the layout walk,
        // WP 19.3A). The tooltip has the full name.
        title.MaxWidth = DesignTokens.RailWidth - 2 * DesignTokens.SpaceMd - 2 * DesignTokens.SpaceLg - 20 - DesignTokens.SpaceLg;
        Grid.SetColumn(iconHost, 0);
        Grid.SetColumn(title, 1);
        content.Children.Add(iconHost);
        content.Children.Add(title);

        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            // Stretch, not Left: a left-aligned content presenter measures its
            // content at infinite width, so a long title (Engineering
            // Calculations) never trimmed and pushed the button 14 px past the
            // rail. The content grid keeps the title left through its columns.
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = DesignTokens.ControlSizeMedium + 2,
            Padding = new Thickness(DesignTokens.SpaceLg, DesignTokens.SpaceMd),
            Tag = module.Area,
        };
        button.Classes.Add(ChromeStyles.Flat);

        AutomationProperties.SetName(button, module.Title);
        AutomationProperties.SetHelpText(button, module.Note);
        ToolTip.SetTip(button, $"{module.Title}\n{module.Note}");
        button.Click += async (_, _) =>
        {
            await navigate().ConfigureAwait(true);
            RefreshSelection();
            NavigationRequested?.Invoke();
        };

        // The 2px selection rule on the left edge, over the selection
        // fill — the design system's own list/rail selection treatment.
        var rule = new Border { Width = DesignTokens.RuleThickness, HorizontalAlignment = HorizontalAlignment.Left, IsVisible = false };
        ThemeReactiveBrush.Bind(rule, Border.BackgroundProperty, BrandPalette.AccentBrushKey);

        var layers = new Panel();
        layers.Children.Add(button);
        layers.Children.Add(rule);

        var frame = new Border
        {
            Child = layers,
            CornerRadius = new CornerRadius(DesignTokens.ControlCornerRadius),
            ClipToBounds = true,
            Background = Brushes.Transparent,
        };

        _buttons.Children.Add(frame);
        _modules.Add(new ModuleItem(module.Area, button, frame, rule, title, iconHost));
    }

    /// <summary>The vector icon for <paramref name="area"/> — one per designed module, falling back to the module's own declared text glyph for a module this set does not yet know.</summary>
    private static StreamGeometry IconFor(ShellArea area) => area switch
    {
        ShellArea.Home => IconGeometry.Home,
        ShellArea.Projects => IconGeometry.Folder,
        ShellArea.ProjectWorkspace => IconGeometry.Folder,
        ShellArea.Tasks => IconGeometry.CheckSquare,
        ShellArea.Engineering => IconGeometry.Gear,
        ShellArea.EngineeringDepartment => IconGeometry.Gear,
        ShellArea.Business => IconGeometry.Currency,
        ShellArea.Settings => IconGeometry.Sliders,
        _ => IconGeometry.Dot,
    };

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

    private sealed record ModuleItem(ShellArea Area, Button Button, Border Frame, Border Rule, TextBlock Title, ContentControl Icon);
}
