using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The collapsible tree column every area view's own left-hand navigation
/// tree sits inside (`WP 19.10O`, Product Owner: "when I'm not using those
/// menus I get the maximum real estate on the screens for working in") —
/// one shared control, used identically by <see cref="ProjectsAreaView"/>,
/// <see cref="EngineeringAreaView"/> and <see cref="BusinessAreaView"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two independent axes, exactly like <see cref="GlobalNavigationRail"/>'s
/// own compact/collapse pair.</b> <see cref="SetCompact"/> is the existing,
/// unchanged responsive fold every area tree already had (`WP 19.7A`,
/// narrows to <see cref="CompactWidth"/> below
/// <see cref="DesignTokens.CompactShellWidth"/>); <see cref="SetCollapsed"/>
/// is this Work Package's own new manual toggle, folding the tree to a thin,
/// captioned strip (<see cref="CollapsedWidth"/>) at any width and
/// persisting the choice. Below the compact threshold the responsive rule
/// always wins — the tree stays at its narrowed width, never the strip,
/// exactly as the brief specifies — and the column returns to whichever
/// state <see cref="SetCollapsed"/> last set once the window widens past the
/// threshold again.
/// </para>
/// <para>
/// <b>The rotated strip caption follows the codebase's own established
/// pattern</b> (<see cref="Docking.LayoutTabGroupView.BuildStrip"/>'s own
/// "narrow, rotated strip a collapsed or auto-hidden group shows in place
/// of its content") — a <see cref="TextBlock"/> painted with a
/// <see cref="RotateTransform"/>, never a
/// <c>LayoutTransformControl</c>, inside a container that already stretches
/// to fill the strip so the rotated paint never bleeds past its own bounds.
/// </para>
/// <para>
/// <b>Selecting a node never expands the column.</b> Nothing in this class
/// reaches for <see cref="SetCollapsed"/> on its own — a selection made from
/// the header search, a dashboard tile or open-right-up still renders in the
/// right pane while the column stays exactly as the user last left it; only
/// the chevron (or the persisted state this area view restores through it)
/// ever changes <see cref="IsCollapsed"/>.
/// </para>
/// </remarks>
public sealed class CollapsibleColumn : Border
{
    /// <summary>The column's own full width when expanded — the fixed width every area tree used before this Work Package (`WP 19.7A`).</summary>
    public const double ExpandedWidth = 260;

    /// <summary>The column's own narrowed width below <see cref="DesignTokens.CompactShellWidth"/> — unchanged from `WP 19.7A`'s own responsive fold.</summary>
    public const double CompactWidth = 160;

    /// <summary>The column's own width collapsed to a strip — wide enough for the chevron button and the vertical caption alone.</summary>
    public const double CollapsedWidth = 28;

    private readonly string _areaCaption;
    private readonly Border _treeHost;
    private readonly Border _captionHost;
    private readonly Button _chevron;
    private readonly ContentControl _chevronIconHost;
    private bool _isCollapsed;
    private bool _isCompact;

    /// <summary>Raised after <see cref="SetCollapsed"/> changes the column's own collapsed state, carrying the new state — the caller's own cue to persist it, exactly as <see cref="RibbonView.CollapsedChanged"/> already does for `TD-70`.</summary>
    public event Action<bool>? CollapsedChanged;

    /// <summary>Gets whether the column is currently collapsed to its own strip (the manual toggle's own state — independent of whether the responsive rule is separately narrowing it, see <see cref="SetCompact"/>).</summary>
    public bool IsCollapsed => _isCollapsed;

    /// <summary>Gets whether the column is currently narrowed by the shell's own responsive rule.</summary>
    public bool IsCompact => _isCompact;

    /// <summary>Initialises a new instance of the <see cref="CollapsibleColumn"/> class.</summary>
    /// <param name="areaCaption">Names the area this column belongs to ("Projects", "Engineering", "Business") — shown as the collapsed strip's own vertical caption, and used to build the chevron's own automation name.</param>
    /// <param name="treeContent">The area's own tree — reparented nowhere; this class only ever toggles its visibility, never detaches it.</param>
    public CollapsibleColumn(string areaCaption, Control treeContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaCaption);
        ArgumentNullException.ThrowIfNull(treeContent);

        _areaCaption = areaCaption;

        Width = ExpandedWidth;
        BorderThickness = new Thickness(0, 0, 1, 0);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, BrandPalette.HairlineBrushKey);

        _chevronIconHost = new ContentControl
        {
            Width = 12,
            Height = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _chevron = new Button
        {
            Content = _chevronIconHost,
            HorizontalAlignment = HorizontalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(DesignTokens.SpaceSm),
            Margin = new Thickness(0, DesignTokens.SpaceSm),
        };
        _chevron.Classes.Add(ChromeStyles.Flat);
        _chevron.Click += (_, _) => ToggleCollapsed();

        var header = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = _chevron };
        ThemeReactiveBrush.Bind(header, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        DockPanel.SetDock(header, Dock.Top);

        _treeHost = new Border { Child = treeContent, Padding = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };

        var caption = new TextBlock
        {
            Text = areaCaption,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeLabel,
            FontWeight = DesignTokens.WeightLabel,
            LetterSpacing = DesignTokens.LabelTracking,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = new RotateTransform(-90),
        };
        ThemeReactiveBrush.Bind(caption, TextBlock.ForegroundProperty, BrandPalette.FaintTextBrushKey);
        _captionHost = new Border { Child = caption, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

        var body = new Panel();
        body.Children.Add(_treeHost);
        body.Children.Add(_captionHost);

        var root = new DockPanel();
        root.Children.Add(header);
        root.Children.Add(body);
        Child = root;

        ApplyState();
    }

    /// <summary>Collapses the column to its own strip, or restores it — the manual toggle's own setter, independent of <see cref="SetCompact"/>.</summary>
    public void SetCollapsed(bool collapsed)
    {
        if (_isCollapsed == collapsed)
            return;

        _isCollapsed = collapsed;
        ApplyState();
        CollapsedChanged?.Invoke(collapsed);
    }

    /// <summary>Toggles <see cref="IsCollapsed"/> — the chevron's own target.</summary>
    public void ToggleCollapsed() => SetCollapsed(!_isCollapsed);

    /// <summary>Narrows the column below the shell's own compact threshold, or restores it — the same responsive rule `WP 19.7A` already applied, unchanged; this always wins over <see cref="IsCollapsed"/> while active (the brief's "the responsive rule wins").</summary>
    public void SetCompact(bool compact)
    {
        if (_isCompact == compact)
            return;

        _isCompact = compact;
        ApplyState();
    }

    private void ApplyState()
    {
        Width = _isCompact ? CompactWidth : (_isCollapsed ? CollapsedWidth : ExpandedWidth);

        // The responsive rule wins: below the compact threshold the tree
        // itself stays visible (narrowed, `WP 19.7A`'s own existing
        // behaviour) regardless of the manual toggle — the strip only ever
        // shows above the threshold, when the user asked for it.
        var showTree = _isCompact || !_isCollapsed;
        _treeHost.IsVisible = showTree;
        _captionHost.IsVisible = !showTree;

        // The chevron's own name/tooltip/icon reflect the manual toggle's
        // own next action, exactly as `GlobalNavigationRail`'s own footer
        // chevron does — not whether the tree happens to be visible right
        // now for an unrelated (responsive) reason.
        var actionName = _isCollapsed ? $"Expand {_areaCaption}" : $"Collapse {_areaCaption}";
        AutomationProperties.SetName(_chevron, actionName);
        ToolTip.SetTip(_chevron, actionName);
        _chevronIconHost.Content = IconGeometry.Build(_isCollapsed ? IconGeometry.ChevronRight : IconGeometry.ChevronLeft, 12);
    }
}
