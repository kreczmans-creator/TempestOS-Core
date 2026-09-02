using Tempest.App.Projects;

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
/// <b>The invariant this type enforces.</b>
/// <see cref="ShellArea.ProjectWorkspace"/> always carries a
/// <see cref="ProjectId"/> — a project workspace with no project is not a
/// thing. <see cref="ShellArea.Home"/> and <see cref="ShellArea.Projects"/>
/// never carry one. <see cref="ShellArea.Engineering"/> is the one area
/// that is legitimately either: engineering work happens inside a project
/// <em>or</em> standalone, and both are first-class.
/// </para>
/// <para>
/// <b>Standalone engineering is a real scope, not an absence of one.</b>
/// The authoritative product decision is that TempestOS is
/// project-centric <em>and</em> that quick calculations and calculation
/// sets remain a first-class workflow with no project. That makes
/// <see cref="ProjectId"/> the scope itself: <see langword="null"/> means
/// "standalone", read by the Engineering surface as real navigation state
/// rather than inferred from what the UI happens to be showing.
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

    /// <summary>The Engineering Workspace with no project scope — quick calculations and calculation sets (`TD-89`).</summary>
    public static ShellLocation StandaloneEngineering { get; } = new(ShellArea.Engineering);

    /// <summary>
    /// Gets whether this location claims to be inside a project — and
    /// therefore whether <see cref="IProjectContext"/> must have that
    /// project open for the shell to be self-consistent.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="ProjectId"/>, not from <see cref="Area"/>:
    /// standalone Engineering carries no project and so claims nothing,
    /// while project Engineering carries one and must agree with the
    /// context. A location that claims no project can never disagree with
    /// one.
    /// </remarks>
    public bool IsProjectScoped => ProjectId is not null;

    /// <summary>Gets whether this is the Engineering Workspace with no project — the standalone workflow.</summary>
    public bool IsStandaloneEngineering => Area is ShellArea.Engineering && ProjectId is null;

    /// <summary>A project workspace location for <paramref name="projectId"/>, at <paramref name="area"/>.</summary>
    public static ShellLocation ForProject(Guid projectId, ProjectArea area = Shell.ProjectArea.Overview) =>
        new(ShellArea.ProjectWorkspace, projectId, area);

    /// <summary>
    /// The Engineering Workspace scoped to <paramref name="projectId"/>,
    /// or standalone when <paramref name="projectId"/> is
    /// <see langword="null"/> — both are valid, and the difference is
    /// carried here rather than decided by a view.
    /// </summary>
    public static ShellLocation ForEngineering(Guid? projectId) =>
        projectId is { } id
            ? new ShellLocation(ShellArea.Engineering, id, Shell.ProjectArea.Engineering)
            : StandaloneEngineering;
}
