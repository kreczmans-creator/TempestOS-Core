using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Shell;
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
/// </remarks>
public sealed class GlobalNavigationRail : UserControl
{
    private readonly IShellNavigator _navigator;
    private readonly StackPanel _buttons = new() { Spacing = DesignTokens.SpaceXs };
    private readonly List<(Button Button, ShellArea Area)> _moduleButtons = [];

    /// <summary>Raised after the user picks a module, so the shell can render it.</summary>
    public event Action? NavigationRequested;

    /// <summary>Initialises a new instance of the <see cref="GlobalNavigationRail"/> class.</summary>
    /// <param name="navigator">The shell navigator this rail is a view over.</param>
    /// <exception cref="ArgumentNullException"><paramref name="navigator"/> is <see langword="null"/>.</exception>
    public GlobalNavigationRail(IShellNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        _navigator = navigator;

        Width = 148;
        Padding = new Thickness(DesignTokens.SpaceSm);
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);

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

        Content = _buttons;
        RefreshSelection();
    }

    /// <summary>Re-highlights whichever module the navigator currently reports — called after every shell move.</summary>
    public void RefreshSelection()
    {
        var current = _navigator.Current.Area;

        foreach (var (button, area) in _moduleButtons)
        {
            var isCurrent = area == current
                || (area == ShellArea.Projects && current == ShellArea.ProjectWorkspace);

            button.FontWeight = isCurrent ? FontWeight.Bold : FontWeight.Normal;
            ThemeReactiveBrush.Bind(
                button,
                BackgroundProperty,
                isCurrent ? ApplicationPalette.AccentPanelBackgroundBrushKey : ApplicationPalette.PanelBackgroundBrushKey);
        }
    }

    private void AddModule(ShellAreaDescriptor module, Func<Task> navigate)
    {
        var isDeclaredOnly = module.Availability == NavigationAvailability.Declared;

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = DesignTokens.SpaceSm,
            Children =
            {
                new TextBlock { Text = module.Glyph, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = module.Title, VerticalAlignment = VerticalAlignment.Center, Opacity = isDeclaredOnly ? 0.55 : 1.0 },
            },
        };

        // A module with no capability behind it is visibly distinguished in
        // the rail, and says so again on the surface it opens — the user
        // learns what TempestOS is without being misled about what it can
        // do today.
        if (isDeclaredOnly)
        {
            content.Children.Add(new TextBlock
            {
                Text = "•",
                FontSize = DesignTokens.FontSizeCaption,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.55,
            });
        }

        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = DesignTokens.MinControlSize,
            Tag = module.Area,
        };

        AutomationProperties.SetName(button, module.Title);
        AutomationProperties.SetHelpText(button, isDeclaredOnly ? $"{module.Title} — {DeclaredCapabilityView.NotImplementedBadge}. {module.Note}" : module.Note);
        ToolTip.SetTip(button, module.Note);
        button.Click += async (_, _) =>
        {
            await navigate().ConfigureAwait(true);
            RefreshSelection();
            NavigationRequested?.Invoke();
        };

        _buttons.Children.Add(button);
        _moduleButtons.Add((button, module.Area));
    }
}
