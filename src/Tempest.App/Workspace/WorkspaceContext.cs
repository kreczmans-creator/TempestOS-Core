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
    /// <summary>The live, mutable backing list for <see cref="SelectedItems"/> — mutated in place by <see cref="SelectionService"/> so every held <see cref="SelectedItems"/> reference observer sees the same instance's own current contents; replaced only via <see cref="ReplaceSelectedItems"/>.</summary>
    private List<WorkspaceSelection> _selectedItems = [];

    /// <inheritdoc />
    public WorkspaceSelection? CurrentSelection { get; internal set; }

    /// <inheritdoc />
    public IReadOnlyList<WorkspaceSelection> SelectedItems => _selectedItems;

    /// <inheritdoc />
    public Guid? ActiveViewId { get; internal set; }

    /// <summary>Replaces the live selection set wholesale — the only mutation path <see cref="SelectionService"/> uses, keeping every read of <see cref="SelectedItems"/> a plain, un-mutating snapshot.</summary>
    internal void ReplaceSelectedItems(IEnumerable<WorkspaceSelection> items) => _selectedItems = [.. items];
}
