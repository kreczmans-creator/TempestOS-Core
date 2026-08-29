namespace Tempest.App.Shell;

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

        new(ShellArea.ProjectWorkspace, "Project", "◧", NavigationAvailability.Implemented,
            "One project's own workspace. Reached by opening a project, not from the rail."),

        new(ShellArea.Engineering, "Engineering", "⚙", NavigationAvailability.Implemented,
            "The Engineering Workspace — inside the open project, or standalone for quick calculations and calculation sets."),

        new(ShellArea.Tasks, "Tasks", "☑", NavigationAvailability.Declared,
            "Engineering and project task management. The Task and Action canonical objects exist in the domain and are durable, but no task surface, assignment workflow or board has been built.",
            "TD-81"),

        new(ShellArea.Commercial, "Commercial", "£", NavigationAvailability.Declared,
            "Quotes, invoices, budget and cashflow. No commercial domain exists yet — this module has no implementation in any layer.",
            "TD-81"),

        new(ShellArea.Resources, "Resources", "⚗", NavigationAvailability.Declared,
            "People, workload and equipment planning. No resourcing domain exists yet.",
            "TD-81"),

        new(ShellArea.Knowledge, "Knowledge", "◫", NavigationAvailability.Declared,
            "Standards, reference data and engineering knowledge. Materials, units and calculation templates exist as real platform services, but no knowledge surface aggregates them.",
            "TD-79"),

        new(ShellArea.Administration, "Administration", "⚙", NavigationAvailability.Declared,
            "Users, roles, permissions and platform settings. Identity, roles and permissions are real, enforced platform services — the administrative surface over them is not built.",
            "TD-81"),
    ];

    /// <summary>Every declared global module, in rail order.</summary>
    public static IReadOnlyList<ShellAreaDescriptor> All => Descriptors;

    /// <summary>The modules the global navigation rail offers — every module except the project workspace, which is reached by opening a project.</summary>
    public static IReadOnlyList<ShellAreaDescriptor> RailModules =>
        Descriptors.Where(d => d.Area != ShellArea.ProjectWorkspace).ToList();

    /// <summary>The descriptor for <paramref name="area"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="area"/> is not declared here.</exception>
    public static ShellAreaDescriptor For(ShellArea area) =>
        Descriptors.FirstOrDefault(d => d.Area == area)
        ?? throw new ArgumentOutOfRangeException(nameof(area), area, "No descriptor is declared for this module.");

    /// <summary>Whether <paramref name="area"/> is backed by a real capability today.</summary>
    public static bool IsImplemented(ShellArea area) => For(area).Availability == NavigationAvailability.Implemented;
}
