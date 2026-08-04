namespace Tempest.App.Workspace;

/// <summary>
/// The concrete <see cref="IWorkspaceContext"/> implementation — a plain,
/// internally-mutable holder. Externally read-only (only two properties are
/// exposed through the interface, and neither has a public setter);
/// internally mutated directly by <see cref="SelectionService"/> and
/// <see cref="Workspace"/>, both in the same assembly. Never grows a method
/// that resolves or returns a service reference — see `WP8.0B Dependency
/// Rules.md` §5.
/// </summary>
internal sealed class WorkspaceContext : IWorkspaceContext
{
    /// <inheritdoc />
    public WorkspaceSelection? CurrentSelection { get; internal set; }

    /// <inheritdoc />
    public Guid? ActiveViewId { get; internal set; }
}
