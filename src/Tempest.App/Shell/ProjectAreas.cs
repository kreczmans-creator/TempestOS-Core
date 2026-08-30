namespace Tempest.App.Shell;

/// <summary>One area of a project workspace, and what is actually behind it.</summary>
/// <param name="Area">The area.</param>
/// <param name="Title">Its name in the project tab strip.</param>
/// <param name="Glyph">A single-character glyph.</param>
/// <param name="Availability">Whether the capability behind it exists.</param>
/// <param name="Note">What the area is for, and — when <see cref="NavigationAvailability.Declared"/> — exactly what is missing.</param>
/// <param name="TrackedBy">The debt item that tracks the missing capability, or <see langword="null"/> when nothing is missing.</param>
public sealed record ProjectAreaDescriptor(
    ProjectArea Area,
    string Title,
    string Glyph,
    NavigationAvailability Availability,
    string Note,
    string? TrackedBy = null);

/// <summary>
/// The project-area table — the single declaration of which project
/// workspace areas exist, and which of them the platform can genuinely
/// serve.
/// </summary>
/// <remarks>
/// The project-level counterpart of <see cref="ShellAreas"/>, and for the
/// same reason: a declared area gets a real, project-aware surface that
/// names the open project and states plainly what is missing, so the tab
/// strip matches the designed product without any tab pretending to work.
/// </remarks>
public static class ProjectAreas
{
    private static readonly IReadOnlyList<ProjectAreaDescriptor> Descriptors =
    [
        new(ProjectArea.Overview, "Overview", "◉", NavigationAvailability.Implemented,
            "The project's own identity, lifecycle and real engineering contents."),

        new(ProjectArea.Engineering, "Engineering", "⚙", NavigationAvailability.Implemented,
            "The project's own engineering objects, opened in the Engineering Workspace with this project as its scope."),

        new(ProjectArea.Documents, "Documents", "📄", NavigationAvailability.Implemented,
            "This project's own documents and drawings, resolved transitively through project membership, with every file held against them openable in the document viewer."),

        new(ProjectArea.Requirements, "Requirements", "◎", NavigationAvailability.Implemented,
            "The requirements allocated to this project's engineering objects, each showing its declared status alongside what its verification history actually records."),

        new(ProjectArea.Tasks, "Tasks", "☑", NavigationAvailability.Implemented,
            "This project's own tasks and actions, created, assigned, prioritised, dated and moved through their own work states, as a list or a status board."),

        new(ProjectArea.Risks, "Risks", "⚠", NavigationAvailability.Implemented,
            "This project's own risks, issues and decisions — raised, scored, owned, and moved through their own status vocabularies, with the decision log recording who decided what, when, and why."),

        new(ProjectArea.Timeline, "Timeline", "▦", NavigationAvailability.Declared,
            "Schedule, milestones and deliverables. Milestone and Deliverable are real canonical objects; no scheduling model, Gantt or timeline surface exists.",
            "TD-81"),

        new(ProjectArea.Reports, "Reports", "▤", NavigationAvailability.Declared,
            "Reports over this project's own engineering evidence. Evidence composition and traceability are real and queryable; report definition, generation and export are not built.",
            "TD-81"),

        new(ProjectArea.Settings, "Settings", "⚙", NavigationAvailability.Declared,
            "This project's own settings. Identity and lifecycle are real and editable through the domain; customer, manager, dates and budget fields do not exist on the Project object yet.",
            "TD-76"),
    ];

    /// <summary>Every declared project area, in tab-strip order.</summary>
    public static IReadOnlyList<ProjectAreaDescriptor> All => Descriptors;

    /// <summary>The descriptor for <paramref name="area"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> is not declared here.</exception>
    public static ProjectAreaDescriptor For(ProjectArea area) =>
        Descriptors.FirstOrDefault(d => d.Area == area)
        ?? throw new ArgumentOutOfRangeException(nameof(area), area, "No descriptor is declared for this project area.");

    /// <summary>Whether <paramref name="area"/> is backed by a real capability today.</summary>
    public static bool IsImplemented(ProjectArea area) => For(area).Availability == NavigationAvailability.Implemented;
}
