using Avalonia.Controls;

namespace Tempest.Desktop.Docking;

/// <summary>
/// One surface that can take part in the workspace layout (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// The extension point. A future surface — a Drawing Viewer, a Materials
/// table, a Tasks board, a Calculation editor — participates in docking,
/// tabbing, splitting, floating and persistence by registering one of
/// these and nothing else. There is no per-panel code in the layout model,
/// the renderer, or the drag logic, and no privileged slot to compete for:
/// the document area is a descriptor exactly like every other.
/// </para>
/// <para>
/// <see cref="Content"/> is a live control rather than a factory, because
/// the workspace's own surfaces are long-lived singletons that hold
/// selection and scroll state. Re-rendering the layout reparents them
/// rather than rebuilding them, so a dock or a tab change never costs the
/// user what they had open.
/// </para>
/// </remarks>
/// <param name="Id">The panel's own stable identity — the same value the layout tree stores.</param>
/// <param name="Title">The name shown on its tab and its collapsed strip.</param>
/// <param name="Content">The live control this panel shows.</param>
/// <param name="CanClose">Whether the user may close it out of the layout entirely.</param>
/// <param name="CanFloat">Whether the user may undock it into its own window.</param>
public sealed record WorkspacePanelDescriptor(
    Guid Id,
    string Title,
    Control Content,
    bool CanClose = true,
    bool CanFloat = true);

/// <summary>
/// The panels a <see cref="WorkspaceLayoutHost"/> can render (`TD-72`).
/// </summary>
/// <remarks>
/// Deliberately separate from the layout tree. The tree records
/// <em>where</em> panels are as pure data that persists across sessions;
/// this records <em>what</em> they are, which is live UI that does not.
/// Keeping them apart is what lets a saved layout name a panel that no
/// longer exists without the layout becoming unloadable — the missing
/// panel is simply dropped on render.
/// </remarks>
public sealed class WorkspacePanelRegistry
{
    private readonly Dictionary<Guid, WorkspacePanelDescriptor> _panels = [];

    /// <summary>Registers, or replaces, <paramref name="descriptor"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    public void Register(WorkspacePanelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _panels[descriptor.Id] = descriptor;
    }

    /// <summary>The descriptor for <paramref name="panelId"/>, or <see langword="null"/> when no such panel is registered.</summary>
    public WorkspacePanelDescriptor? Find(Guid panelId) =>
        _panels.TryGetValue(panelId, out var descriptor) ? descriptor : null;

    /// <summary>Every registered panel.</summary>
    public IReadOnlyCollection<WorkspacePanelDescriptor> All => _panels.Values;

    /// <summary>Whether <paramref name="panelId"/> is registered.</summary>
    public bool Contains(Guid panelId) => _panels.ContainsKey(panelId);
}
