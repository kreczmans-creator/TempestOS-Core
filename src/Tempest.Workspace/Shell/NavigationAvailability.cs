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

        new(ShellArea.Projects, "Projects", "▤", NavigationAvailability.Implemented,
            "The project catalogue — list, create and open the projects engineering work belongs to."),

        // `WP 18.2A` (`ADR-0148`, `D-028`): evidence is the product — the
        // files, what they are about, the governed references they cite,
        // their declared figures, check and issue. Placed right after
        // Projects, deliberately: evidence belongs beside the project
        // catalogue in the rail, not buried among the five modules that are
        // only Declared.
        new(ShellArea.Evidence, "Evidence", "▧", NavigationAvailability.Implemented,
            "A project's own evidence — files, citations, declared figures, check and issue — plus the Libraries tab over the five governed reference libraries evidence cites."),

        // `WP 19.0A` (`ADR-0150`): the current principal's own weekly
        // timesheet, placed right after Evidence for the same reason
        // Evidence sits right after Projects — real, governed, day-to-day
        // work, not one of the five modules that are only Declared.
        new(ShellArea.Timesheets, "Timesheets", "◷", NavigationAvailability.Implemented,
            "The current principal's own weekly timesheet — record, amend and delete time against a project's own Released rate-card pin, priced and frozen at record time."),

        // `WP 19.1A` part 3 (`ADR-0151`): invoice requests raised from a
        // project's own completed deliverables and unbilled time, placed
        // right after Timesheets for the same reason Timesheets sits right
        // after Evidence — real, governed, day-to-day work, not one of the
        // five modules that are only Declared.
        new(ShellArea.Invoicing, "Invoicing", "▥", NavigationAvailability.Implemented,
            "Every invoice request across open projects (or the open project when one is open), grouped by status — raised from a completed deliverable, sent to a connector, reconciled, and voided, with every outcome shown."),

        // `WP 19.2B`: issued evidence sheets and project documents,
        // filterable by project — placed right after Invoicing, the last
        // of the day-to-day project areas, and before Engineering
        // Calculations.
        new(ShellArea.Reports, "Reports", "▤", NavigationAvailability.Implemented,
            "Every issued evidence sheet and every project document, across open projects — filterable by one project."),

        new(ShellArea.ProjectWorkspace, "Project", "◧", NavigationAvailability.Implemented,
            "One project's own workspace. Reached by opening a project, not from the rail."),

        // `WP 19.2B`: Engineering leaves the rail — it is reached inside a
        // project, as its own Structure tab (`ProjectWorkspaceView`
        // embeds the ribbon and docking surface directly in that tab), or
        // from open-right-up. Standalone engineering (`TD-89`, no project
        // open) has no dedicated rail button either, but is not stranded:
        // Home renders the identical engineering surface (ribbon,
        // explorer, docking), unchanged, so quick calculations and
        // calculation sets with no project stay one click away from where
        // the rail already lands. The descriptor stays, excluded from
        // `RailModules` exactly as `ProjectWorkspace`'s always has been,
        // because `ShellArea.Engineering` remains a real navigator scope
        // other code still names it by (`MainWindow.DescribeModule`/
        // `DescribeLocation`, the Structure tab, open-right-up).
        new(ShellArea.Engineering, "Engineering", "⚙", NavigationAvailability.Implemented,
            "The Engineering Workspace — inside the open project, as its own Structure tab, or standalone for quick calculations and calculation sets."),

        // Rail order is this list's own order, and it is deliberate: an
        // Implemented engineering destination sits beside Engineering,
        // above the five modules that are only Declared. Appended last it
        // sat at the bottom of the rail under those five, which is where
        // the first manual review could not find it. Only the RAIL order
        // moves — `ShellArea`'s own ordinal is untouched, because
        // `ShellLocation` is persisted by it.
        new(ShellArea.EngineeringCalculation, "Engineering Calculations", "∑", NavigationAvailability.Implemented,
            "Governed engineering calculations — populate the reference material library, release a material through review, run a calculation, and read the result with the reference revision it stood on."),

        // `WP 19.2B`: the rail area that replaced the Preferences dialog —
        // placed last, the design system's own convention for a
        // settings/preferences destination.
        new(ShellArea.Settings, "Settings", "⚙", NavigationAvailability.Implemented,
            "Persistence root, principal override, connector authorisation, working pattern, the independent-check toggle, theme, toast duration and confirm-before-delete."),

        // `WP 19.2B` (`TD-81`): Tasks, Commercial, Resources, Knowledge and
        // Administration are removed from the rail, not dimmed — no
        // descriptor for any of the five is declared here any more, so
        // `ShellAreas.For` throws for each exactly as it would for any
        // other undeclared area, and `RailModules` can never offer a
        // button for one. Their `ShellArea` members stay, unused, above
        // (see that enum's own remarks); the surface that used to render
        // them, `DeclaredCapabilityView`, and the rail's own "planned, not
        // yet built" legend and marker are deleted along with them —
        // every module this table now declares is real.
    ];

    /// <summary>Every declared global module, in rail order.</summary>
    public static IReadOnlyList<ShellAreaDescriptor> All => Descriptors;

    /// <summary>
    /// The modules the global navigation rail offers — every declared
    /// module except <see cref="ShellArea.ProjectWorkspace"/> (reached by
    /// opening a project) and <see cref="ShellArea.Engineering"/> (`WP
    /// 19.2B`: reached inside a project, as its own Structure tab, or
    /// standalone from Engineering Calculations — never directly from the
    /// rail).
    /// </summary>
    public static IReadOnlyList<ShellAreaDescriptor> RailModules =>
        Descriptors.Where(d => d.Area is not (ShellArea.ProjectWorkspace or ShellArea.Engineering)).ToList();

    /// <summary>The descriptor for <paramref name="area"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> is not declared here.</exception>
    public static ShellAreaDescriptor For(ShellArea area) =>
        Descriptors.FirstOrDefault(d => d.Area == area)
        ?? throw new ArgumentOutOfRangeException(nameof(area), area, "No descriptor is declared for this module.");

    /// <summary>Whether <paramref name="area"/> is backed by a real capability today.</summary>
    public static bool IsImplemented(ShellArea area) => For(area).Availability == NavigationAvailability.Implemented;
}
