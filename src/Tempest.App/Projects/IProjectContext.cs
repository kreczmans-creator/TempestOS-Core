namespace Tempest.App.Projects;

/// <summary>
/// The current project — the organisational context every other surface
/// works within.
/// </summary>
/// <remarks>
/// <para>
/// This is the product spine's own load-bearing service. TempestOS is a
/// project-centric engineering environment: engineering work happens
/// <i>within</i> a project, not alongside one. That makes "which project
/// am I in" real application state with a lifecycle, an event, and
/// persistence — never a caption a view sets on itself.
/// </para>
/// <para>
/// Persisted through the same <see cref="Tempest.Core.Settings.ISettingsProvider"/>
/// substrate <c>WorkspaceState</c> already uses (`ADR-0064`), so reopening
/// the application recovers the project the user was last working in.
/// </para>
/// </remarks>
public interface IProjectContext
{
    /// <summary>Gets the currently open project, or <see langword="null"/> if none is open.</summary>
    ProjectSummary? Current { get; }

    /// <summary>Gets whether a project is currently open.</summary>
    bool HasProject { get; }

    /// <summary>
    /// Opens <paramref name="projectId"/> as the current project,
    /// replacing whatever was open, and publishes
    /// <see cref="ProjectContextChangedEvent"/>.
    /// </summary>
    /// <exception cref="ProjectNotFoundException">No project exists with that Id.</exception>
    Task OpenAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Closes the current project, publishing <see cref="ProjectContextChangedEvent"/>. A no-op when none is open.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-reads the current project from the directory — the hook a
    /// surface calls after mutating the project itself (a rename, a
    /// lifecycle transition), so the context never serves a stale
    /// snapshot. A no-op when no project is open; closes the context if
    /// the project has since been deleted.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the current project Id so the next session can restore it.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the project this context was last saved with. Resolves to
    /// no open project when nothing was saved, or when the saved project
    /// no longer exists — never an exception, mirroring every other
    /// Desktop state loader's own contract.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
