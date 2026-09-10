namespace Tempest.Workspace.Shell;

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

    /// <summary>The Engineering Workspace — scoped by the current project, or standalone when none is open.</summary>
    Engineering,

    // The modules below are declared but not yet implemented. They are
    // present so the shell shows the product's real module set and can
    // state plainly what is missing, rather than hiding it — see
    // `ShellAreas`, which is the single place that says which of these the
    // platform can genuinely serve. New members are appended, never
    // inserted: `ShellLocation` is persisted by ordinal.

    /// <summary>Engineering and project task management (`TD-81`).</summary>
    Tasks,

    /// <summary>Quotes, invoices, budget and cashflow (`TD-81`).</summary>
    Commercial,

    /// <summary>People, workload and equipment planning (`TD-81`).</summary>
    Resources,

    /// <summary>Standards, reference data and engineering knowledge (`TD-79`).</summary>
    Knowledge,

    /// <summary>Users, roles, permissions and platform settings (`TD-81`).</summary>
    Administration,

    /// <summary>
    /// The governed engineering calculation surface — pick a released
    /// reference, enter the inputs, run the real calculation, and read the
    /// result with the reference revision it stood on.
    /// </summary>
    /// <remarks>
    /// Appended, not inserted, because <c>ShellLocation</c> is persisted by
    /// ordinal and inserting would silently relocate every saved session.
    /// </remarks>
    EngineeringCalculation,

    /// <summary>
    /// The Evidence workspace (`WP 18.2A`, `ADR-0148`): the open project's
    /// own evidence — records, citations, declared figures, check and
    /// issue — and the Libraries tab over the five governed reference
    /// libraries evidence cites.
    /// </summary>
    /// <remarks>
    /// Appended, not inserted — see <see cref="EngineeringCalculation"/>'s
    /// own identical remark; <c>ShellLocation</c> is persisted by ordinal.
    /// </remarks>
    Evidence,

    /// <summary>
    /// The Timesheets area (`WP 19.0A`, `ADR-0150`): the current
    /// principal's own weekly timesheet — record, amend and delete time
    /// against a project's own Released rate-card pin.
    /// </summary>
    /// <remarks>
    /// Appended, not inserted — see <see cref="EngineeringCalculation"/>'s
    /// own identical remark; <c>ShellLocation</c> is persisted by ordinal.
    /// </remarks>
    Timesheets,
}
