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

    // The five modules below were declared-but-unimplemented mock-up
    // modules, present only so the rail could state plainly what was
    // missing. `WP 19.2B` (`TD-81`): removed from the rail rather than
    // dimmed — their descriptors leave `ShellAreas` entirely, so nothing
    // in application state claims they exist any more. The members stay,
    // unused, because `ShellLocation` is persisted by ordinal and a
    // member once shipped is never removed or renumbered; a session
    // restored pointing at one of them now falls back to Home
    // (`MainWindow.RenderCurrentModuleAsync`).

    /// <summary>Removed from the rail (`WP 19.2B`, `TD-81`) — was "Engineering and project task management".</summary>
    Tasks,

    /// <summary>Removed from the rail (`WP 19.2B`, `TD-81`) — was "Quotes, invoices, budget and cashflow".</summary>
    Commercial,

    /// <summary>Removed from the rail (`WP 19.2B`, `TD-81`) — was "People, workload and equipment planning".</summary>
    Resources,

    /// <summary>Removed from the rail (`WP 19.2B`, `TD-81`) — was "Standards, reference data and engineering knowledge".</summary>
    Knowledge,

    /// <summary>Removed from the rail (`WP 19.2B`, `TD-81`) — was "Users, roles, permissions and platform settings".</summary>
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

    /// <summary>
    /// The Invoicing area (`WP 19.1A` part 3, `ADR-0151`): every
    /// <c>InvoiceRequest</c> across open projects (or the open project when
    /// one is open), grouped by status — raised from a project's own
    /// completed deliverables, sent to a connector, reconciled, and voided.
    /// </summary>
    /// <remarks>
    /// Appended, not inserted — see <see cref="EngineeringCalculation"/>'s
    /// own identical remark; <c>ShellLocation</c> is persisted by ordinal.
    /// </remarks>
    Invoicing,

    /// <summary>
    /// The Reports area (`WP 19.2B`): every issued evidence sheet and
    /// every project document, across open projects, filterable by
    /// project.
    /// </summary>
    /// <remarks>
    /// Appended, not inserted — see <see cref="EngineeringCalculation"/>'s
    /// own identical remark; <c>ShellLocation</c> is persisted by ordinal.
    /// </remarks>
    Reports,

    /// <summary>
    /// The Settings area (`WP 19.2B`): persistence root, principal
    /// override, connector authorisation, working pattern, the
    /// independent-check toggle, theme, toast duration and
    /// confirm-before-delete — the rail area that replaced the Preferences
    /// dialog.
    /// </summary>
    /// <remarks>
    /// Appended, not inserted — see <see cref="EngineeringCalculation"/>'s
    /// own identical remark; <c>ShellLocation</c> is persisted by ordinal.
    /// </remarks>
    Settings,

    /// <summary>
    /// The Quotes area (`WP 19.5B`, `ADR-0152`): every quotation across
    /// open projects, as New (Draft), Sent and Outstanding (Sent longer
    /// than seven days ago) — opened right up in the project's own Quote
    /// tab. `WP 19.7A` will move this registration under a Business
    /// module; kept as a single rail entry here in the meantime.
    /// </summary>
    /// <remarks>
    /// Appended, not inserted — see <see cref="EngineeringCalculation"/>'s
    /// own identical remark; <c>ShellLocation</c> is persisted by ordinal.
    /// </remarks>
    Quotes,

    /// <summary>
    /// The Business module (`WP 19.7A`, Product Owner IA sketches items 6
    /// and 7): Dashboard &amp; Reports, Quotes, Invoices, Timesheets and
    /// Subscriptions, as one tree with a right pane over the selected
    /// node.
    /// </summary>
    /// <remarks>
    /// A brand-new member, never <see cref="Commercial"/>'s own ordinal —
    /// <see cref="Commercial"/>'s doc comment records that ordinals are
    /// never renumbered or reused for a different meaning once shipped.
    /// Appended, not inserted — see <see cref="EngineeringCalculation"/>'s
    /// own identical remark; <c>ShellLocation</c> is persisted by ordinal.
    /// </remarks>
    Business,

    /// <summary>
    /// The Engineering module's own rail destination (`WP 19.7A`): a tree
    /// (Dashboard + Reports, Tasks, Modules, Reference data) with a right
    /// pane over the selected node — distinct from <see cref="Engineering"/>
    /// itself, which stays the ribbon-and-docking engineering surface's own
    /// scope-aware location (a project's Structure tab, or standalone).
    /// Selecting Modules → Mechanical from this tree's own content
    /// navigates on to <see cref="Engineering"/> exactly as it always has;
    /// this member exists only so the rail can offer a real landing page
    /// first, per the Product Owner's IA sketches, without touching
    /// <see cref="Engineering"/>'s own long-established behaviour.
    /// </summary>
    /// <remarks>
    /// Appended, not inserted — see <see cref="EngineeringCalculation"/>'s
    /// own identical remark; <c>ShellLocation</c> is persisted by ordinal.
    /// </remarks>
    EngineeringDepartment,
}
