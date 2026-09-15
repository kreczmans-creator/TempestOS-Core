using Avalonia;
using Avalonia.Controls;
using Tempest.Workspace.Layout;

namespace Tempest.Desktop.Docking;

/// <summary>
/// A workspace panel, or a subtree of them, living in its own top-level
/// window (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// A real <see cref="Window"/>, not an in-window overlay pretending to
/// float. That is what makes multi-monitor engineering work possible at
/// all: the operating system places it, the user drags it onto whichever
/// display they like, and its screen position is what gets persisted —
/// so the arrangement is restored onto the same display next session.
/// </para>
/// <para>
/// It hosts its content through the same <see cref="WorkspaceLayoutHost"/>
/// the main window uses, so a floating window is not a lesser surface: it
/// can itself contain tabs and splits, and behaves identically.
/// </para>
/// </remarks>
public sealed class FloatingPanelWindow : Window
{
    private readonly WorkspaceLayoutHost _host;

    /// <summary>Raised when the user moves or resizes this window, carrying its new screen rectangle.</summary>
    public event Action<Guid, double, double, double, double>? GeometryChanged;

    /// <summary>Raised when the user closes this window, so its panels can be returned to the docked layout or removed.</summary>
    public event Action<Guid>? WindowClosed;

    /// <summary>Initialises a new instance of the <see cref="FloatingPanelWindow"/> class.</summary>
    public FloatingPanelWindow(FloatingLayoutWindow model, WorkspacePanelRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(registry);

        WindowId = model.Id;
        _host = new WorkspaceLayoutHost(registry);

        Title = registry.Find(model.Content.Panels.First())?.Title ?? "Panel";
        Width = model.Width;
        Height = model.Height;
        Position = new PixelPoint((int)model.X, (int)model.Y);
        ShowInTaskbar = false;
        Content = _host;

        Update(model);

        PositionChanged += (_, _) => RaiseGeometryChanged();
        SizeChanged += (_, _) => RaiseGeometryChanged();
        Closed += (_, _) => WindowClosed?.Invoke(WindowId);
    }

    /// <summary>This window's own identity in the layout model.</summary>
    public Guid WindowId { get; }

    /// <summary>The layout host rendering this window's own content.</summary>
    public WorkspaceLayoutHost Host => _host;

    /// <summary>Re-renders this window from <paramref name="model"/>.</summary>
    public void Update(FloatingLayoutWindow model)
    {
        ArgumentNullException.ThrowIfNull(model);

        // The floating window renders its own subtree as a complete little
        // arrangement, carrying the panel presentations with it so a
        // collapsed panel stays collapsed when it is undocked.
        _host.Update(new WorkspaceLayoutTree(model.Content, [], LayoutPanels));
    }

    /// <summary>The panel presentations this window's own host renders with — set by the controller that owns the whole arrangement.</summary>
    public IReadOnlyDictionary<Guid, PanelPresentation> LayoutPanels { get; set; } = new Dictionary<Guid, PanelPresentation>();

    /// <summary>
    /// Keeps this window's own current position and size reachable — see
    /// <see cref="FloatingWindowPlacement.Clamp"/> (`WP 20.10D`, PO finding
    /// T4). Called by <see cref="WorkspaceLayoutController"/> right before
    /// every <see cref="Show()"/>, whether this window was just created from
    /// a drag or restored from a saved arrangement, so neither path can ever
    /// place it somewhere no screen actually is. A no-op when this window
    /// reports no screens at all — nothing to clamp against.
    /// </summary>
    internal void ClampToScreen()
    {
        var screens = Screens?.All;
        if (screens is null || screens.Count == 0)
            return;

        var primary = (Screens!.Primary ?? screens[0]).WorkingArea;
        var requested = new PixelRect(Position.X, Position.Y, (int)Math.Max(0, Width), (int)Math.Max(0, Height));

        var clamped = FloatingWindowPlacement.Clamp(requested, screens.Select(s => s.WorkingArea).ToList(), primary);

        Position = clamped.Position;
        Width = clamped.Width;
        Height = clamped.Height;
    }

    private void RaiseGeometryChanged() =>
        GeometryChanged?.Invoke(WindowId, Position.X, Position.Y, Width, Height);
}
