namespace Tempest.Workspace.Shell;

/// <summary>
/// Whether a navigation destination is backed by a real capability yet.
/// </summary>
/// <remarks>
/// This exists so "not yet implemented" is <b>application state a test can
/// assert</b>, not a caption a view happens to render. The product rule is
/// that every destination must either work or say plainly that it does not
/// — and the only way to keep that rule honest over time is to make the
/// claim checkable.
/// </remarks>
public enum NavigationAvailability
{
    /// <summary>A real destination: the capability behind it exists and works.</summary>
    Implemented,

    /// <summary>
    /// A declared destination whose capability does not exist yet. The
    /// surface is real, project-aware where applicable, and states what is
    /// missing and what tracks it — never a screen pretending to work.
    /// </summary>
    Declared,
}

/// <summary>One global module in the navigation model, and what is actually behind it.</summary>
/// <param name="Area">The module.</param>
/// <param name="Title">Its name in the rail.</param>
/// <param name="Glyph">A single-character glyph for the rail.</param>
/// <param name="Availability">Whether the capability behind it exists.</param>
/// <param name="Note">What the module is for, and — when <see cref="NavigationAvailability.Declared"/> — exactly what is missing.</param>
/// <param name="TrackedBy">The debt item that tracks the missing capability, or <see langword="null"/> when nothing is missing.</param>
public sealed record ShellAreaDescriptor(
    ShellArea Area,
    string Title,
    string Glyph,
    NavigationAvailability Availability,
    string Note,
    string? TrackedBy = null);

/// <summary>
/// The global module table — the single declaration of which TempestOS
/// modules exist, and which of them the platform can genuinely serve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why declared-but-unimplemented modules are present at all.</b> An
/// earlier pass omitted them entirely, to avoid decorative navigation. The
/// authoritative product decision is now that the shell must show the
/// product's real module set, with anything unbuilt marked plainly rather
/// than hidden — a user should be able to see what TempestOS is, and be
/// told the truth about what it can do today. Omitting them made the
/// product look smaller than designed; faking them would be worse. This
/// table is the third option: present, navigable, and honest.
/// </para>
/// <para>
/// <see cref="ShellArea.ProjectWorkspace"/> is deliberately absent from
/// <see cref="RailModules"/>: it is reached by opening a project, not by
/// picking a module, and a rail button for it would have nowhere to go
/// with no project open.
/// </para>
/// </remarks>
public static class ShellAreas
{
    private static readonly IReadOnlyList<ShellAreaDescriptor> Descriptors =
    [
        new(ShellArea.Home, "Home", "⌂", NavigationAvailability.Implemented,
            "The cross-project Cockpit — engineering health, attention items and recent work."),

        // `WP 19.7A` (Product Owner IA sketches, `po-comments.md` item 6):
        // the rail's own five modules — Home, Projects, Tasks, Engineering,
        // Business — replace the ten-entry rail `WP 19.2B` left behind.
        // Projects, Tasks, Engineering (via `EngineeringDepartment`) and
        // Business are each a tree over their own designed nodes, with a
        // right pane over whichever node is selected; the former standalone
        // rail entries below (Evidence, Timesheets, Invoicing, Reports,
        // Engineering Calculations, Quotes) are removed from this table
        // exactly as `WP 19.2B` removed Tasks/Commercial/Resources/
        // Knowledge/Administration — their own capability is not gone, it
        // is reached from inside one of these five now (see each area
        // view's own remarks for where).
        new(ShellArea.Projects, "Projects", "▤", NavigationAvailability.Implemented,
            "Dashboard + Reports, and every project grouped Open, Closed (under 90 days) and Archive (90 days and over) — open one to work inside it, or create the next."),

        new(ShellArea.Tasks, "Tasks", "☑", NavigationAvailability.Implemented,
            "Every open task and action across the platform, grouped by when it is due, plus reviews, approvals and finance chasers awaiting attention."),

        // `EngineeringDepartment`, not `Engineering` — see that member's
        // own remarks. This is the rail's own landing tree (Dashboard +
        // Reports, Tasks, Modules, Reference data); `Engineering` itself
        // (below, excluded from the rail exactly as it already was)
        // remains the ribbon-and-docking surface's own scope-aware
        // location, reached from this tree's own Modules → Mechanical
        // node, from a project's Structure tab, or from open-right-up.
        new(ShellArea.EngineeringDepartment, "Engineering", "⚙", NavigationAvailability.Implemented,
            "Dashboard + Reports, Tasks (Calculations, Reviews, Approvals), Modules (Mechanical; Electrical and Structural are future) and Reference data — the governed libraries evidence and calculations cite."),

        // `WP 19.7A` (Product Owner comment item 7, `po-comments.md`):
        // Quotes, Invoices, Timesheets and Subscriptions move under one
        // Business module — see `NavigationAvailability.cs`'s own former
        // `Quotes`/`Timesheets`/`Invoicing` remarks for each capability's
        // history; none of it changed, only where it is reached from.
        new(ShellArea.Business, "Business", "◈", NavigationAvailability.Implemented,
            "Dashboard & Reports, Quotes, Invoices, Timesheets and Subscriptions (read from the accounting package, never entered here)."),

        new(ShellArea.ProjectWorkspace, "Project", "◧", NavigationAvailability.Implemented,
            "One project's own workspace. Reached by opening a project, not from the rail."),

        // `WP 19.2B`: Engineering leaves the rail — it is reached inside a
        // project, as its own Structure tab (`ProjectWorkspaceView`
        // embeds the ribbon and docking surface directly in that tab), or
        // from open-right-up. Standalone engineering (`TD-89`, no project
        // open) has no dedicated rail button either, but is not stranded:
        // Home renders the identical engineering surface (ribbon,
        // explorer, docking), unchanged, and `WP 19.7A`'s own
        // `EngineeringDepartment` tree offers Modules → Mechanical as a
        // second, discoverable route to the same place. The descriptor
        // stays, excluded from `RailModules` exactly as `ProjectWorkspace`'s
        // always has been, because `ShellArea.Engineering` remains a real
        // navigator scope other code still names it by
        // (`MainWindow.DescribeModule`/`DescribeLocation`, the Structure
        // tab, open-right-up).
        new(ShellArea.Engineering, "Engineering", "⚙", NavigationAvailability.Implemented,
            "The Engineering Workspace — inside the open project, as its own Structure tab, or standalone for quick calculations and calculation sets."),

        // `WP 19.2B`: the settings destination — kept declared and
        // reachable (`MainWindow.GoToModuleAsync(ShellArea.Settings)`, the
        // header's own principal chip, `WP 19.7A`), excluded from the rail
        // itself: "Settings behind the header's login details" is this
        // Work Package's own brief, scope item 1.
        new(ShellArea.Settings, "Settings", "⚙", NavigationAvailability.Implemented,
            "Persistence root, principal override, connector authorisation, working pattern, the independent-check toggle, theme, toast duration and confirm-before-delete."),

        // `WP 19.2B` (`TD-81`): Commercial, Resources, Knowledge and
        // Administration are removed from the rail, not dimmed — no
        // descriptor for any is declared here any more, so `ShellAreas.For`
        // throws for each exactly as it would for any other undeclared
        // area, and `RailModules` can never offer a button for one.
        // `WP 19.7A`: Tasks re-joins the declared set above, now genuinely
        // implemented, and Evidence, Timesheets, Invoicing, Reports,
        // EngineeringCalculation and Quotes join Commercial/Resources/
        // Knowledge/Administration in this list — each one's own real
        // capability is reached from inside Projects/Tasks/
        // EngineeringDepartment/Business now (see those area views' own
        // remarks), never lost, only no longer a standalone declared
        // module of its own. A session that persisted one of these as its
        // last location is redirected Home
        // (`MainWindow.RenderCurrentModuleAsync`'s own registry-miss
        // check), exactly as a persisted Tasks/Commercial/Resources/
        // Knowledge/Administration location always has been.
    ];

    /// <summary>Every declared global module, in rail order.</summary>
    public static IReadOnlyList<ShellAreaDescriptor> All => Descriptors;

    /// <summary>
    /// The modules the global navigation rail offers (`WP 19.7A`): exactly
    /// Home, Projects, Tasks, Engineering (<see cref="ShellArea.EngineeringDepartment"/>)
    /// and Business, in that order — every other declared module
    /// (<see cref="ShellArea.ProjectWorkspace"/>, reached by opening a
    /// project; <see cref="ShellArea.Engineering"/>, the scope-aware
    /// engineering surface; <see cref="ShellArea.Settings"/>, reached from
    /// the header) is declared but deliberately excluded here.
    /// </summary>
    public static IReadOnlyList<ShellAreaDescriptor> RailModules =>
        Descriptors.Where(d => d.Area is ShellArea.Home or ShellArea.Projects or ShellArea.Tasks
            or ShellArea.EngineeringDepartment or ShellArea.Business).ToList();

    /// <summary>The descriptor for <paramref name="area"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> is not declared here.</exception>
    public static ShellAreaDescriptor For(ShellArea area) =>
        Descriptors.FirstOrDefault(d => d.Area == area)
        ?? throw new ArgumentOutOfRangeException(nameof(area), area, "No descriptor is declared for this module.");

    /// <summary>Whether <paramref name="area"/> is backed by a real capability today.</summary>
    public static bool IsImplemented(ShellArea area) => For(area).Availability == NavigationAvailability.Implemented;
}
