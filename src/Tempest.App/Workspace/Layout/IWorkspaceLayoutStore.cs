namespace Tempest.App.Workspace.Layout;

/// <summary>
/// Durable storage for the workspace arrangement (`TD-72`).
/// </summary>
/// <remarks>
/// Application session state, not project domain data — so it lives on the
/// established <see cref="Tempest.Core.Settings.ISettingsProvider"/>
/// substrate (`ADR-0064`), alongside the shell location and the current
/// project, and deliberately <b>not</b> in the engineering persistence
/// authority (`TD-85`). Where a user has put their panels is not
/// engineering data.
/// </remarks>
public interface IWorkspaceLayoutStore
{
    /// <summary>Writes <paramref name="tree"/> as the arrangement to restore next session.</summary>
    Task SaveAsync(WorkspaceLayoutTree tree, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the saved arrangement, or <see langword="null"/> when none
    /// was saved or what was saved is unreadable. Never throws: a corrupt
    /// layout costs the user their panel positions, never their session.
    /// </summary>
    Task<WorkspaceLayoutTree?> LoadAsync(CancellationToken cancellationToken = default);
}
