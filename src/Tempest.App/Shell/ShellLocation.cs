namespace Tempest.App.Shell;

/// <summary>
/// Where the user is, as one immutable value: the global module, the
/// project it is scoped to (if any), and the project area within it.
/// </summary>
/// <remarks>
/// <para>
/// Navigation state is a <b>single value</b> rather than several flags
/// spread across views, so "where am I" has exactly one answer and the
/// shell can never render two surfaces that disagree. It is also what
/// makes navigation testable without a UI: a test asserts a
/// <see cref="ShellLocation"/>, not a sequence of control-visibility
/// side effects.
/// </para>
/// <para>
/// The invariant this type enforces: an area that is project-scoped
/// (<see cref="ShellArea.ProjectWorkspace"/>, <see cref="ShellArea.Engineering"/>)
/// always carries a <see cref="ProjectId"/>, and an area that is not
/// never does.
/// </para>
/// </remarks>
/// <param name="Area">The global module.</param>
/// <param name="ProjectId">The project this location is scoped to, or <see langword="null"/> for a cross-project area.</param>
/// <param name="ProjectArea">The area within the project, or <see langword="null"/> outside a project.</param>
public sealed record ShellLocation(ShellArea Area, Guid? ProjectId = null, ProjectArea? ProjectArea = null)
{
    /// <summary>The application's own starting location — the Cockpit, no project scope.</summary>
    public static ShellLocation Home { get; } = new(ShellArea.Home);

    /// <summary>The project browser.</summary>
    public static ShellLocation Projects { get; } = new(ShellArea.Projects);

    /// <summary>Gets whether this location is scoped to a project.</summary>
    public bool IsProjectScoped => Area is ShellArea.ProjectWorkspace or ShellArea.Engineering;

    /// <summary>A project workspace location for <paramref name="projectId"/>, at <paramref name="area"/>.</summary>
    public static ShellLocation ForProject(Guid projectId, ProjectArea area = Shell.ProjectArea.Overview) =>
        new(ShellArea.ProjectWorkspace, projectId, area);

    /// <summary>The Engineering Workspace, entered from <paramref name="projectId"/> — the project scope engineering work happens within.</summary>
    public static ShellLocation ForEngineering(Guid projectId) =>
        new(ShellArea.Engineering, projectId, Shell.ProjectArea.Engineering);
}
