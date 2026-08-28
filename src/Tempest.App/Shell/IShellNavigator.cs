using Tempest.App.Projects;

namespace Tempest.App.Shell;

/// <summary>
/// The TempestOS navigation model: <c>Module → Project → Workspace</c>,
/// as explicit, persisted, testable state.
/// </summary>
/// <remarks>
/// Every navigation the product supports is a verb here, so the shell's
/// views raise intent and this service owns the transition — the same
/// "views raise intent, the owner dispatches" shape
/// <c>ProjectExplorerView</c> already established. Navigating into a
/// project-scoped area opens that project in
/// <see cref="IProjectContext"/> as part of the same move, which is what
/// makes it impossible to be "in" a project workspace without a current
/// project.
/// </remarks>
public interface IShellNavigator
{
    /// <summary>Gets where the user currently is.</summary>
    ShellLocation Current { get; }

    /// <summary>Navigates to the cross-project Cockpit. Does not close the current project.</summary>
    Task GoHomeAsync(CancellationToken cancellationToken = default);

    /// <summary>Navigates to the project browser. Does not close the current project.</summary>
    Task GoToProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens <paramref name="projectId"/> — making it the current project —
    /// and navigates to its workspace at <paramref name="area"/>.
    /// </summary>
    /// <exception cref="ProjectNotFoundException">No project exists with that Id.</exception>
    Task OpenProjectAsync(Guid projectId, ProjectArea area = ProjectArea.Overview, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves to another area of the already-open project.
    /// </summary>
    /// <exception cref="InvalidOperationException">No project is currently open.</exception>
    Task GoToProjectAreaAsync(ProjectArea area, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enters the Engineering Workspace within the current project — the
    /// only way in, because engineering work belongs to a project.
    /// </summary>
    /// <exception cref="InvalidOperationException">No project is currently open.</exception>
    Task GoToEngineeringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns from Engineering to the current project's workspace,
    /// preserving the project context.
    /// </summary>
    /// <exception cref="InvalidOperationException">No project is currently open.</exception>
    Task ReturnToProjectAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the current project and returns to the project browser.</summary>
    Task CloseProjectAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the current location so the next session can restore it.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the location this navigator was last saved with, opening
    /// the project it was scoped to. Resolves to <see cref="ShellLocation.Home"/>
    /// when nothing was saved, the saved value is corrupt, or the project
    /// it referenced no longer exists — never an exception.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
