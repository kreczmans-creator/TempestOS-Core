namespace Tempest.App.Shell;

/// <summary>
/// The top level of the TempestOS navigation model — a global module.
/// </summary>
/// <remarks>
/// The product hierarchy is <c>Module → Project → Workspace → Engineering
/// Object</c>. This enum is the first level: where the user is in the
/// application, independent of which project (if any) is open. Deliberately
/// a closed enum rather than a registry: these are the product's own fixed
/// modules, not extensible plugin surface, and making them a type means the
/// shell's navigation state is exhaustively checkable at compile time.
/// </remarks>
public enum ShellArea
{
    /// <summary>The Engineering Cockpit — the cross-project landing surface.</summary>
    Home,

    /// <summary>The project browser — list, open and create projects.</summary>
    Projects,

    /// <summary>A single project's own workspace, scoped by <see cref="ShellLocation.ProjectId"/>.</summary>
    ProjectWorkspace,

    /// <summary>The Engineering Workspace, entered from — and scoped by — the current project.</summary>
    Engineering,
}
