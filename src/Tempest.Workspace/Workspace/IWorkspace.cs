namespace Tempest.App.Workspace;

/// <summary>
/// The assembled, running Workspace instance — the aggregate root exposing
/// every sub-service a view or command needs, and the read-only collection
/// of currently open views. Owns no lifecycle verbs of its own — creation
/// and shutdown belong to <see cref="IWorkspaceManager"/>, mirroring the
/// <see cref="Tempest.Core.Runtime.ITempestHost"/>/
/// <see cref="Tempest.Core.Runtime.ITempestHostBuilder"/> split exactly.
/// </summary>
public interface IWorkspace
{
    /// <summary>Gets the current docking layout.</summary>
    IWorkspaceLayout Layout { get; }

    /// <summary>Gets the current session state.</summary>
    IWorkspaceState State { get; }

    /// <summary>Gets the Workspace-scoped navigation service.</summary>
    INavigationService Navigation { get; }

    /// <summary>Gets the current-selection service.</summary>
    ISelectionService Selection { get; }

    /// <summary>Gets the Project Explorer panel.</summary>
    IProjectExplorer ProjectExplorer { get; }

    /// <summary>Gets the Property Inspector panel.</summary>
    IPropertyInspector PropertyInspector { get; }

    /// <summary>Gets every view currently open in the Document Area, in tab order.</summary>
    IReadOnlyList<IWorkspaceView> OpenViews { get; }

    /// <summary>Gets the currently active (focused) view, or <see langword="null"/> if none is open.</summary>
    IWorkspaceView? ActiveView { get; }
}
