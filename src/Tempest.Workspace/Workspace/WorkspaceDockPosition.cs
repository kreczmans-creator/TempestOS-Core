namespace Tempest.App.Workspace;

/// <summary>
/// Where a <see cref="IWorkspacePanel"/> is docked. Deliberately has no
/// <c>Floating</c> value — undocking a panel into its own top-level window
/// remains explicitly deferred (`WP8.0A Workspace Architecture Document.md`
/// §"Deliberately Out of Scope").
/// </summary>
public enum WorkspaceDockPosition
{
    /// <summary>Docked to the left edge — the Project Explorer's own default position.</summary>
    Left,

    /// <summary>Docked to the right edge — the Property Inspector's own default position.</summary>
    Right,

    /// <summary>Docked to the bottom edge.</summary>
    Bottom,
}
