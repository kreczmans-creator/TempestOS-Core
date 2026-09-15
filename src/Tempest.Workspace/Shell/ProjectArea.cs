namespace Tempest.Workspace.Shell;

/// <summary>
/// The second level of the navigation model — which area of an open
/// project the user is looking at.
/// </summary>
/// <remarks>
/// Mirrors the mock-ups' own project tab strip. Every area the product
/// designs is declared here; which of them are backed by a real capability
/// today is declared once, in <see cref="ProjectAreas"/>, and an area that
/// is not says so on its own surface. New members are appended, never
/// inserted: <see cref="ShellLocation"/> is persisted by ordinal.
/// </remarks>
public enum ProjectArea
{
    /// <summary>The project's own summary — identity, lifecycle, contents and activity.</summary>
    Overview,

    /// <summary>The project's own engineering objects, opened in the Engineering Workspace.</summary>
    Engineering,

    /// <summary>The project's own documents and drawings.</summary>
    Documents,

    /// <summary>The project's own requirements and their verification.</summary>
    Requirements,

    /// <summary>The project's own tasks and actions (`TD-81`).</summary>
    Tasks,

    /// <summary>The project's own risks, issues and decisions (`FCR-0056`).</summary>
    Risks,

    /// <summary>The project's own schedule, milestones and deliverables (`TD-81`).</summary>
    Timeline,

    /// <summary>Removed from the tab strip (`WP 19.2B`, `TD-81`) — was "Reports generated from this project's own engineering evidence"; delivered instead by the rail's own Reports area, filtered by project.</summary>
    Reports,

    /// <summary>Removed from the tab strip (`WP 19.2B`, `TD-81`) — was "This project's own settings — identity, lifecycle and configuration".</summary>
    Settings,

    /// <summary>
    /// This project's own deliverables — every milestone <c>Deliverable</c>
    /// with its completion state (`WP 19.0A`, `ADR-0150`).
    /// </summary>
    /// <remarks>Appended, not inserted: <see cref="ShellLocation"/> is persisted by ordinal.</remarks>
    Deliverables,

    /// <summary>
    /// This project's own quotation(s) — opened with the project, defining
    /// its initial deliverables and requirements once accepted
    /// (`WP 19.5B`, `ADR-0152`).
    /// </summary>
    /// <remarks>Appended, not inserted: <see cref="ShellLocation"/> is persisted by ordinal.</remarks>
    Quote,

    /// <summary>
    /// This project's own evidence — records, citations, declared figures,
    /// check and issue — the existing Evidence surface scoped to this
    /// project (`WP 19.7A`, `po-comments.md` item 6 delta (e)).
    /// </summary>
    /// <remarks>Appended, not inserted: <see cref="ShellLocation"/> is persisted by ordinal.</remarks>
    Evidence,

    /// <summary>
    /// This project's own sign off — the statement box and the Sign off
    /// action (`WP 19.5C`'s lifecycle service), showing the record
    /// afterwards (`WP 19.7A`, `po-comments.md` item 6 delta (c)).
    /// </summary>
    /// <remarks>Appended, not inserted: <see cref="ShellLocation"/> is persisted by ordinal.</remarks>
    SignOff,

    /// <summary>
    /// The project's own identity and Commercial section — client, purchase
    /// order, budget, rate card, dates and project manager (`WP 20.10A`,
    /// Product Owner findings D2/D12/T1) — reachable directly from the
    /// project workspace rather than only through the generic Object
    /// Editor.
    /// </summary>
    /// <remarks>
    /// Appended, not inserted, exactly as every sibling member's own remarks
    /// say: <see cref="ShellLocation"/> is persisted by ordinal. First in the
    /// tab strip regardless — <see cref="ProjectAreas.All"/>'s own
    /// declaration order, not this enum's, decides tab position.
    /// </remarks>
    Details,
}
