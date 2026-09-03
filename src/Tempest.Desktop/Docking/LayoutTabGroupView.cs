using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Workspace.Layout;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Docking;

/// <summary>
/// Renders one <see cref="LayoutTabGroupNode"/>: a tab strip, the group's
/// own chrome, and the selected panel's content (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// The only leaf the renderer knows how to draw. A single docked panel is
/// a tab group of one, so there is no separate "one panel" control and no
/// second chrome implementation to keep in step — which is exactly what
/// made "drag a panel onto another to tab them together" an ordinary
/// operation rather than a special case.
/// </para>
/// <para>
/// This view raises intent and renders state. It never mutates the layout:
/// every gesture becomes an event the host turns into a pure operation on
/// the tree. That is what keeps the arrangement testable without a UI, and
/// what stops the visual tree and the model from drifting apart.
/// </para>
/// </remarks>
public sealed class LayoutTabGroupView : UserControl
{
    /// <summary>The width, or height, a collapsed or auto-hidden group's own strip occupies.</summary>
    public const double StripSize = 32;

    private readonly LayoutTabGroupNode _node;
    private readonly WorkspacePanelRegistry _registry;
    private readonly WorkspaceLayoutTree _tree;

    /// <summary>Raised when the user picks a different tab.</summary>
    public event Action<Guid>? PanelSelected;

    /// <summary>Raised when the user closes a panel out of the layout.</summary>
    public event Action<Guid>? PanelClosed;

    /// <summary>Raised when the user toggles this group's own collapsed state, carrying the panel it applies to.</summary>
    public event Action<Guid, bool>? CollapseToggled;

    /// <summary>Raised when the user pins or auto-hides a panel.</summary>
    public event Action<Guid, bool>? PinToggled;

    /// <summary>Raised when the user begins dragging a tab, carrying the panel and the pointer event.</summary>
    public event Action<Guid, PointerPressedEventArgs>? TabDragStarted;

    /// <summary>Raised when the user clicks an auto-hidden group's own strip, asking for its flyout.</summary>
    public event Action<Guid>? FlyoutRequested;

    /// <summary>Initialises a new instance of the <see cref="LayoutTabGroupView"/> class.</summary>
    public LayoutTabGroupView(LayoutTabGroupNode node, WorkspacePanelRegistry registry, WorkspaceLayoutTree tree)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(tree);

        _node = node;
        _registry = registry;
        _tree = tree;

        NodeId = node.Id;
        Content = Build();
    }

    /// <summary>The layout node this view renders — the handle a drop targets.</summary>
    public Guid NodeId { get; }

    /// <summary>The panels in this group, in tab order.</summary>
    public IReadOnlyList<Guid> PanelIds => _node.PanelIds;

    /// <summary>The panel currently selected.</summary>
    public Guid SelectedPanelId => _node.SelectedPanelId;

    /// <summary>Whether this group is showing only its own strip — collapsed in place, or auto-hidden.</summary>
    public bool IsStripShowing { get; private set; }

    private Control Build()
    {
        var presentation = _tree.PresentationOf(_node.SelectedPanelId);
        IsStripShowing = presentation.IsCollapsed || !presentation.IsPinned;

        return IsStripShowing ? BuildStrip() : BuildFull();
    }

    /// <summary>The narrow, rotated strip a collapsed or auto-hidden group shows in place of its content.</summary>
    private Control BuildStrip()
    {
        var descriptor = _registry.Find(_node.SelectedPanelId);
        var title = descriptor?.Title ?? "Panel";
        var presentation = _tree.PresentationOf(_node.SelectedPanelId);

        var label = new TextBlock
        {
            Text = title,
            FontSize = DesignTokens.FontSizeCaption,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = new RotateTransform(-90),
        };

        var button = new Button
        {
            Content = label,
            Width = StripSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0),
        };
        button.Classes.Add(ChromeStyles.Flat);
        ThemeReactiveBrush.Bind(label, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);

        AutomationProperties.SetName(button, $"Expand {title}");

        // A collapsed panel expands in place; an auto-hidden one expands as
        // a flyout over the layout. The distinction is the whole difference
        // between the two, so it is decided here, from the model, rather
        // than by whichever handler happens to run.
        button.Click += (_, _) =>
        {
            if (presentation.IsCollapsed)
                CollapseToggled?.Invoke(_node.SelectedPanelId, false);
            else
                FlyoutRequested?.Invoke(_node.SelectedPanelId);
        };

        var host = new Border { Width = StripSize, Child = button, BorderThickness = new Thickness(0, 0, 1, 0) };
        ThemeReactiveBrush.Bind(host, Border.BackgroundProperty, BrandPalette.SunkenBackgroundBrushKey);
        ThemeReactiveBrush.Bind(host, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        return host;
    }

    private Control BuildFull()
    {
        var root = new DockPanel();
        var strip = BuildTabStrip();
        DockPanel.SetDock(strip, Dock.Top);
        root.Children.Add(strip);

        var descriptor = _registry.Find(_node.SelectedPanelId);
        if (descriptor is not null)
        {
            // Reparenting a long-lived surface, not rebuilding it: detach
            // from whatever previously held it so the selection and scroll
            // state the user had survive a re-render.
            Detach(descriptor.Content);
            var surface = new Border { Child = descriptor.Content };
            ThemeReactiveBrush.Bind(surface, Border.BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);
            root.Children.Add(surface);
        }
        else
        {
            root.Children.Add(new TextBlock
            {
                Text = "This panel is no longer available.",
                Margin = DesignTokens.PanelPadding,
                Opacity = 0.7,
            });
        }

        return root;
    }

    private Control BuildTabStrip()
    {
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs, ClipToBounds = true };

        foreach (var panelId in _node.PanelIds)
        {
            var descriptor = _registry.Find(panelId);
            var isSelected = panelId == _node.SelectedPanelId;

            // A panel tab is an UPPERCASE chrome label (the design system's
            // own panel-title treatment); the selected one carries the
            // heading colour and a 2px accent rule beneath it — never
            // colour alone.
            var tab = new Button
            {
                Content = new TextBlock
                {
                    Text = (descriptor?.Title ?? "Panel").ToUpperInvariant(),
                    FontFamily = DesignTokens.TitleFont,
                    FontSize = DesignTokens.FontSizeLabel + 1,
                    FontWeight = isSelected ? DesignTokens.WeightHeading : DesignTokens.WeightLabel,
                    LetterSpacing = DesignTokens.LabelTracking,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 160,
                },
                MinHeight = DesignTokens.ControlSizeSmall,
                Padding = new Thickness(DesignTokens.SpaceLg, DesignTokens.SpaceXs),
                VerticalAlignment = VerticalAlignment.Stretch,
                Tag = panelId,
            };
            tab.Classes.Add(ChromeStyles.Flat);
            ThemeReactiveBrush.Bind(tab, ForegroundProperty, isSelected ? BrandPalette.HeadingTextBrushKey : BrandPalette.FaintTextBrushKey);

            AutomationProperties.SetName(tab, descriptor?.Title ?? "Panel");

            var captured = panelId;
            tab.Click += (_, _) => PanelSelected?.Invoke(captured);
            tab.AddHandler(PointerPressedEvent, (_, e) => TabDragStarted?.Invoke(captured, e), Avalonia.Interactivity.RoutingStrategies.Tunnel);

            var rule = new Border { Height = DesignTokens.RuleThickness, VerticalAlignment = VerticalAlignment.Bottom, IsVisible = isSelected, IsHitTestVisible = false };
            ThemeReactiveBrush.Bind(rule, Border.BackgroundProperty, BrandPalette.AccentBrushKey);
            var layered = new Panel();
            layered.Children.Add(tab);
            layered.Children.Add(rule);
            tabs.Children.Add(layered);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs, HorizontalAlignment = HorizontalAlignment.Right };
        var selected = _node.SelectedPanelId;
        var presentation = _tree.PresentationOf(selected);
        var selectedDescriptor = _registry.Find(selected);

        actions.Children.Add(ChromeButton(Icons.IconGeometry.Collapse, $"Collapse {selectedDescriptor?.Title ?? "panel"}", () => CollapseToggled?.Invoke(selected, true)));
        actions.Children.Add(ChromeButton(
            presentation.IsPinned ? Icons.IconGeometry.Pin : Icons.IconGeometry.PinOff,
            presentation.IsPinned ? $"Auto-hide {selectedDescriptor?.Title ?? "panel"}" : $"Pin {selectedDescriptor?.Title ?? "panel"}",
            () => PinToggled?.Invoke(selected, !presentation.IsPinned)));

        if (selectedDescriptor?.CanClose != false)
            actions.Children.Add(ChromeButton(Icons.IconGeometry.Close, $"Close {selectedDescriptor?.Title ?? "panel"}", () => PanelClosed?.Invoke(selected)));

        actions.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0);

        var strip = new DockPanel { Height = DesignTokens.ControlSizeSmall + DesignTokens.SpaceSm };
        DockPanel.SetDock(actions, Dock.Right);
        strip.Children.Add(actions);
        strip.Children.Add(tabs);

        // The strip is a sunken instrument surface with a hairline beneath
        // it, so a panel's own title row reads as chrome and its content
        // as the surface — the same two-tier treatment the shell's header
        // and status bar use.
        var frame = new Border { Child = strip, BorderThickness = new Thickness(0, 0, 0, 1) };
        ThemeReactiveBrush.Bind(frame, Border.BackgroundProperty, BrandPalette.SunkenBackgroundBrushKey);
        ThemeReactiveBrush.Bind(frame, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        return frame;
    }

    private static Button ChromeButton(StreamGeometry icon, string name, Action onClick)
    {
        var button = new Button
        {
            Content = Icons.IconGeometry.Build(icon, 12),
            MinHeight = DesignTokens.ControlSizeSmall - DesignTokens.SpaceSm,
            MinWidth = DesignTokens.ControlSizeSmall - DesignTokens.SpaceSm,
            Padding = new Thickness(DesignTokens.SpaceSm, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add(ChromeStyles.Flat);
        ThemeReactiveBrush.Bind(button, ForegroundProperty, BrandPalette.MutedTextBrushKey);

        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>Detaches <paramref name="content"/> from whatever currently holds it, so it can be reparented.</summary>
    internal static void Detach(Control content)
    {
        switch (content.Parent)
        {
            case Panel panel:
                panel.Children.Remove(content);
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, content):
                contentControl.Content = null;
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, content):
                decorator.Child = null;
                break;
        }
    }
}
