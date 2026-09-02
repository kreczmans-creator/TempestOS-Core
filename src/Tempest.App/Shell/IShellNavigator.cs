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
    /// Navigates to a global module — including one that is declared but
    /// not yet implemented, which lands on a real surface stating what is
    /// missing rather than silently doing nothing.
    /// </summary>
    /// <remarks>
    /// Does not close the current project: a global module is a different
    /// place, not a reason to discard the project the user is in.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> is <see cref="ShellArea.ProjectWorkspace"/>, which is reached by opening a project, or <see cref="ShellArea.Engineering"/>, which has its own scope-aware verbs.</exception>
    Task GoToModuleAsync(ShellArea area, CancellationToken cancellationToken = default);

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
    /// Enters the Engineering Workspace in the scope the user is already
    /// in: within the current project when one is open, standalone when
    /// none is.
    /// </summary>
    /// <remarks>
    /// Never throws for want of a project. TempestOS is project-centric,
    /// but quick calculations and calculation sets are a first-class
    /// workflow that does not require one — so "no project open" selects
    /// the standalone scope rather than blocking the move.
    /// </remarks>
    Task GoToEngineeringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enters the Engineering Workspace with no project scope, whatever is
    /// currently open — the explicit "quick calculation" entry point.
    /// </summary>
    /// <remarks>
    /// Deliberately does <b>not</b> close the current project: standalone
    /// work is a different scope, not a reason to discard the project the
    /// user is in. Returning to that project afterwards is a plain
    /// <see cref="ReturnToProjectAsync"/>.
    /// </remarks>
    Task GoToStandaloneEngineeringAsync(CancellationToken cancellationToken = default);

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
