using Tempest.App.Workspace;

namespace Tempest.Desktop.Docking;

/// <summary>
/// The Output panel (`WP 10.2B`) — a genuine, fourth <see cref="IWorkspacePanel"/>
/// implementer, docked at <see cref="WorkspaceDockPosition.Bottom"/> (never
/// wired to any real dock surface before this Work Package). Desktop-local
/// only, never registered against <see cref="IWorkspace.ProjectExplorer"/>/
/// <see cref="IWorkspace.PropertyInspector"/>'s own fixed-property shape on
/// <see cref="IWorkspace"/> (one of the frozen twelve `WP8.0B` contracts) —
/// implementing the same, already-open <see cref="IWorkspacePanel"/>
/// interface a fourth time is exactly the extensibility that contract
/// already documents ("the Project Explorer and Property Inspector both
/// implement this"), not a change to it.
/// </summary>
public sealed class OutputPanel : IWorkspacePanel
{
    /// <summary>
    /// A fixed, well-known Id — mirroring <c>ProjectExplorer.WellKnownId</c>/
    /// <c>PropertyInspector.WellKnownId</c>'s own identical precedent
    /// (`TD-35`, `WP 10.0B`), so a future persisted reference to this panel
    /// (should one ever be added) would resolve correctly across restarts.
    /// </summary>
    public static readonly Guid WellKnownId = new("4a1bd248-7e78-42f8-b072-c09fb0cecc48");

    /// <inheritdoc />
    public Guid Id { get; } = WellKnownId;

    /// <inheritdoc />
    public string Title => "Output";

    /// <inheritdoc />
    public WorkspaceDockPosition DockPosition => WorkspaceDockPosition.Bottom;

    /// <inheritdoc />
    public bool IsVisible { get; private set; }

    /// <inheritdoc />
    public Task ShowAsync(CancellationToken cancellationToken = default)
    {
        IsVisible = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HideAsync(CancellationToken cancellationToken = default)
    {
        IsVisible = false;
        return Task.CompletedTask;
    }
}
