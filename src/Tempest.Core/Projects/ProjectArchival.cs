using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Projects;

/// <summary>
/// A project's own listing group — Open, Closed or Archive — and whether a
/// project is closed or archived at all (`WP 19.5C`, Product Owner comment
/// item 6). Derived, never stored: <see cref="Project.ClosedOn"/> is the
/// one fact recorded; every question this class answers is computed from
/// it and an "as of" instant, so there is exactly one fact to keep honest
/// rather than a flag that can drift from it.
/// </summary>
public static class ProjectArchival
{
    /// <summary>
    /// How many days after <see cref="Project.ClosedOn"/> a Closed project
    /// becomes Archive — read-only, reference data only (Product Owner
    /// comment item 6, sheets 6-7: "Closed (closed less than 90 days ago)
    /// ... Archive (reference data for older projects, &gt; 90 days)").
    /// </summary>
    public const int ArchiveAfterDays = 90;

    /// <summary>Whether <paramref name="project"/> has been signed off and closed. A held project is still Open unless it is also closed.</summary>
    public static bool IsClosed(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.ClosedOn is not null;
    }

    /// <summary>
    /// Whether <paramref name="project"/> is Closed and has gone read-only
    /// as Archive as of <paramref name="asOf"/> — closed
    /// <see cref="ArchiveAfterDays"/> days or more ago.
    /// </summary>
    public static bool IsArchived(Project project, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.ClosedOn is { } closedOn && DateOnly.FromDateTime(asOf.UtcDateTime) >= closedOn.AddDays(ArchiveAfterDays);
    }

    /// <summary>This project's own listing group, as of <paramref name="asOf"/> — the Projects tree's own Open/Closed/Archive split (Product Owner comment item 6, sheets 6-7).</summary>
    public static ProjectListingGroup ListingGroupOf(Project project, DateTimeOffset asOf)
    {
        if (!IsClosed(project))
            return ProjectListingGroup.Open;

        return IsArchived(project, asOf) ? ProjectListingGroup.Archive : ProjectListingGroup.Closed;
    }
}

/// <summary>A project's own listing group in the Projects tree (`WP 19.5C`) — see <see cref="ProjectArchival.ListingGroupOf"/>.</summary>
public enum ProjectListingGroup
{
    /// <summary>Not closed.</summary>
    Open,

    /// <summary>Closed less than <see cref="ProjectArchival.ArchiveAfterDays"/> days ago.</summary>
    Closed,

    /// <summary>Closed <see cref="ProjectArchival.ArchiveAfterDays"/> days or more ago — read-only, reference data only.</summary>
    Archive,
}
