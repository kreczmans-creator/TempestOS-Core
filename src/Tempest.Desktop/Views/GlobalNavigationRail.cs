using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Shell;
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
/// <b>Every module the product designs is shown, and every one of them
/// goes somewhere real.</b> The rail is built from
/// <see cref="ShellAreas.RailModules"/>, so which modules exist and which
/// are backed by a capability is declared once, in application state, not
/// decided here. A module whose capability is not built yet is marked in
/// the rail and lands on a surface that says exactly what is missing and
/// what tracks it — never a dead button, and never a screen pretending to
/// work.
/// </para>
/// <para>
/// <b>Visual language.</b> The design system's rail: a sunken instrument
/// surface, UPPERCASE section labels, one monochrome vector icon per
/// module, and the current module marked by a 2px accent rule on its left
/// edge over the 12% selection fill — never by colour alone (the title is
/// also set in the heading weight). Below <see cref="DesignTokens.CompactShellWidth"/>
/// the rail folds to its icons (<see cref="SetCompact"/>), keeping every
/// module reachable in a narrow window with its title in the tooltip.
/// </para>
/// </remarks>
public sealed class GlobalNavigationRail : UserControl
{
    private readonly IShellNavigator _navigator;
    private readonly StackPanel _buttons = new() { Spacing = DesignTokens.SpaceXs };
    private readonly List<ModuleItem> _modules = [];
    private readonly TextBlock _sectionLabel;
    private readonly TextBlock _plannedLegend;
    private bool _compact;

    /// <summary>Raised after the user picks a module, so the shell can render it.</summary>
    public event Action? NavigationRequested;

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

        foreach (var module in ShellAreas.RailModules)
        {
            var area = module.Area;

            // Engineering is the one module with a scope of its own: it
            // enters the open project when there is one, and the
            // standalone workflow when there is not. Both are real
            // destinations (`TD-89`).
            Func<Task> navigate = area == ShellArea.Engineering
                ? () => _navigator.GoToEngineeringAsync()
                : () => _navigator.GoToModuleAsync(area);

            AddModule(module, navigate);
        }

        // The legend for the planned-module marker, so the meaning of the
        // violet dot is stated on the surface rather than left to be
        // guessed.
        _plannedLegend = new TextBlock
        {
            Text = "●  planned, not yet built",
            FontSize = DesignTokens.FontSizeLabel,
            Margin = new Thickness(DesignTokens.SpaceLg + DesignTokens.SpaceSm, DesignTokens.SpaceLg, DesignTokens.SpaceLg, DesignTokens.SpaceLg),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = ShellAreas.RailModules.Any(m => m.Availability == NavigationAvailability.Declared),
        };
        ThemeReactiveBrush.Bind(_plannedLegend, TextBlock.ForegroundProperty, BrandPalette.FaintTextBrushKey);

        var body = new DockPanel();
        DockPanel.SetDock(_sectionLabel, Dock.Top);
        DockPanel.SetDock(_plannedLegend, Dock.Bottom);
        body.Children.Add(_sectionLabel);
        body.Children.Add(_plannedLegend);
        body.Children.Add(new ScrollViewer { Content = _buttons, Padding = new Thickness(DesignTokens.SpaceMd, 0) });

        var frame = new Border { Child = body, BorderThickness = new Thickness(0, 0, 1, 0) };
        ThemeReactiveBrush.Bind(frame, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        Content = frame;

        ActualThemeVariantChanged += (_, _) => RefreshSelection();
        RefreshSelection();
    }

    /// <summary>Gets whether the rail is currently folded to its icons.</summary>
    public bool IsCompact => _compact;

    /// <summary>Folds the rail to icons only, or restores its titles — the shell calls this from its own width, so a narrow window keeps every module reachable.</summary>
    public void SetCompact(bool compact)
    {
        if (_compact == compact)
            return;

        _compact = compact;
        Width = compact ? DesignTokens.RailCompactWidth : DesignTokens.RailWidth;
        _sectionLabel.IsVisible = !compact;
        _plannedLegend.IsVisible = !compact && ShellAreas.RailModules.Any(m => m.Availability == NavigationAvailability.Declared);

        foreach (var item in _modules)
        {
            item.Title.IsVisible = !compact;
            item.Marker.IsVisible = !compact && item.IsDeclaredOnly;
            item.Button.HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            item.Button.Padding = compact ? new Thickness(0, DesignTokens.SpaceMd) : new Thickness(DesignTokens.SpaceLg, DesignTokens.SpaceMd);
        }
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
        var isDeclaredOnly = module.Availability == NavigationAvailability.Declared;

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
            Opacity = isDeclaredOnly ? 0.7 : 1.0,
        };

        // A module with no capability behind it is visibly distinguished in
        // the rail (the brand's violet, strictly secondary, as a small
        // dot), and says so again on the surface it opens — the user
        // learns what TempestOS is without being misled about what it can
        // do today.
        var marker = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsVisible = isDeclaredOnly,
            Margin = new Thickness(DesignTokens.SpaceSm, 0, 0, 0),
        };
        ThemeReactiveBrush.Bind(marker, Border.BackgroundProperty, BrandPalette.SecondaryAccentBrushKey);

        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        title.Margin = new Thickness(DesignTokens.SpaceLg, 0, 0, 0);
        Grid.SetColumn(iconHost, 0);
        Grid.SetColumn(title, 1);
        Grid.SetColumn(marker, 2);
        content.Children.Add(iconHost);
        content.Children.Add(title);
        content.Children.Add(marker);

        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = DesignTokens.ControlSizeMedium + 2,
            Padding = new Thickness(DesignTokens.SpaceLg, DesignTokens.SpaceMd),
            Tag = module.Area,
        };
        button.Classes.Add(ChromeStyles.Flat);

        AutomationProperties.SetName(button, module.Title);
        AutomationProperties.SetHelpText(button, isDeclaredOnly ? $"{module.Title} — {DeclaredCapabilityView.NotImplementedBadge}. {module.Note}" : module.Note);
        ToolTip.SetTip(button, isDeclaredOnly ? $"{module.Title} — {DeclaredCapabilityView.NotImplementedBadge}\n{module.Note}" : $"{module.Title}\n{module.Note}");
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
        _modules.Add(new ModuleItem(module.Area, button, frame, rule, title, iconHost, marker, isDeclaredOnly));
    }

    /// <summary>The vector icon for <paramref name="area"/> — one per designed module, falling back to the module's own declared text glyph for a module this set does not yet know.</summary>
    private static StreamGeometry IconFor(ShellArea area) => area switch
    {
        ShellArea.Home => IconGeometry.Home,
        ShellArea.Projects => IconGeometry.Folder,
        ShellArea.ProjectWorkspace => IconGeometry.Folder,
        ShellArea.Engineering => IconGeometry.Gear,
        ShellArea.Tasks => IconGeometry.CheckSquare,
        ShellArea.Commercial => IconGeometry.Currency,
        ShellArea.Resources => IconGeometry.People,
        ShellArea.Knowledge => IconGeometry.Book,
        ShellArea.Administration => IconGeometry.Shield,
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

    private sealed record ModuleItem(ShellArea Area, Button Button, Border Frame, Border Rule, TextBlock Title, ContentControl Icon, Border Marker, bool IsDeclaredOnly);
}
