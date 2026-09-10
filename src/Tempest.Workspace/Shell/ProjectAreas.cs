namespace Tempest.Workspace.Shell;

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

        // `WP 19.2B`: this tab's own title changed from "Engineering" to
        // "Structure" — the surface itself is unchanged (still the real
        // Engineering Workspace, ribbon and docking, scoped to this
        // project), but it no longer swaps the whole shell module out
        // from under the project workspace to show it: `ProjectWorkspaceView`
        // embeds it directly, so the project's own header and tab strip
        // stay on screen. `ShellArea.Engineering` remains the navigator's
        // own scope for it.
        new(ProjectArea.Engineering, "Structure", "⚙", NavigationAvailability.Implemented,
            "The project's own engineering objects — the ribbon and docking surface of the Engineering Workspace, embedded here with this project as its scope."),

        new(ProjectArea.Documents, "Documents", "📄", NavigationAvailability.Implemented,
            "This project's own documents and drawings, resolved transitively through project membership, with every file held against them openable in the document viewer."),

        new(ProjectArea.Requirements, "Requirements", "◎", NavigationAvailability.Implemented,
            "The requirements allocated to this project's engineering objects, each showing its declared status alongside what its verification history actually records."),

        new(ProjectArea.Tasks, "Tasks", "☑", NavigationAvailability.Implemented,
            "This project's own tasks and actions, created, assigned, prioritised, dated and moved through their own work states, as a list or a status board."),

        new(ProjectArea.Risks, "Risks", "⚠", NavigationAvailability.Implemented,
            "This project's own risks, issues and decisions — raised, scored, owned, and moved through their own status vocabularies, with the decision log recording who decided what, when, and why."),

        new(ProjectArea.Timeline, "Timeline", "▦", NavigationAvailability.Implemented,
            "This project's own milestones in date order, the deliverables due against each, and the tasks and actions contributing to them. A dated register, not a Gantt chart: there is no scheduling engine, no dependency graph and no critical path."),

        // `WP 19.0A` (`ADR-0150`): every milestone Deliverable of the
        // project with its own completion state — placed right after
        // Timeline, where the deliverables it completes are due.
        new(ProjectArea.Deliverables, "Deliverables", "◈", NavigationAvailability.Implemented,
            "This project's own deliverables, each against the milestone it is due on, with its completion — when, by whom, on what evidence and documents, and a fixed-price value where it is billed that way rather than by time."),

        // `WP 19.2B` (`TD-81`): the Reports and Settings tabs are removed
        // from this tab strip, not dimmed — `DeclaredCapabilityView`,
        // which was the only thing either ever rendered, is deleted.
        // Reports over a project's own evidence is delivered by the
        // rail's own Reports area instead (`ShellArea.Reports`), which
        // filters by project — the identical capability, reached from the
        // rail rather than duplicated per project. A project's own
        // settings (customer, manager, dates, budget) remain undesigned;
        // no substitute surface exists yet, and none is claimed here. The
        // `ProjectArea` members stay, unused, because `ShellLocation` is
        // persisted by ordinal (see that enum's own remarks).
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
