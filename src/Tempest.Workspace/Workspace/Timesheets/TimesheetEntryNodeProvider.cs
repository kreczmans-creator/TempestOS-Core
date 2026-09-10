using Tempest.Core.EngineeringDomain;
using Tempest.Core.Timesheets;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Timesheets;

/// <summary>
/// Populates the Project Explorer's own Timesheets area: project → week →
/// entry (`WP 19.0A`, `ADR-0150`). Mirrors
/// <c>Evidence.EvidenceNodeProvider</c>'s own "rooted at every live
/// Project, grouped by a value that is not itself a structural parent"
/// shape — grouped by <see cref="TimesheetWeek.WeekOf"/> instead of a
/// fixed classification, since a week has no closed set to enumerate up
/// front.
/// </summary>
public sealed class TimesheetEntryNodeProvider : IProjectExplorerNodeProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="TimesheetEntryNodeProvider"/> class.</summary>
    public TimesheetEntryNodeProvider(string kind, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);

        Kind = kind;
        _context = context;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var projects = await LiveProjectsAsync(cancellationToken).ConfigureAwait(false);

        var nodes = new List<ProjectExplorerNode>();
        foreach (var project in projects.OrderBy(DisplayNameOf, StringComparer.Ordinal))
            nodes.Add(await ToProjectNodeAsync(project, cancellationToken).ConfigureAwait(false));

        return nodes;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Timesheets node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var candidate = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (candidate is not null && string.Equals(candidate.Kind, MechanicalObjectFactoryRegistry.Project, StringComparison.Ordinal) && IsLive(candidate))
        {
            var underProject = await LiveEntriesUnderProjectAsync(nodeId, cancellationToken).ConfigureAwait(false);

            var weeks = underProject
                .Select(e => TimesheetWeek.WeekOf(e.Date))
                .Distinct()
                .OrderDescending();

            return weeks
                .Select(week => new ProjectExplorerNode(
                    GroupNodeId(nodeId, week), $"Week of {week:yyyy-MM-dd}", null, true, ProjectExplorerNodeType.Category))
                .ToList();
        }

        var projects = await LiveProjectsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var project in projects)
        {
            var underProject = await LiveEntriesUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false);
            var weeks = underProject.Select(e => TimesheetWeek.WeekOf(e.Date)).Distinct();

            foreach (var week in weeks)
            {
                if (GroupNodeId(project.Id, week) != nodeId)
                    continue;

                return underProject
                    .Where(e => TimesheetWeek.WeekOf(e.Date) == week)
                    .OrderByDescending(e => e.Date)
                    .Select(ToEntryNode)
                    .ToList();
            }
        }

        throw new ArgumentException($"'{nodeId}' is not a known Timesheets node.", nameof(nodeId));
    }

    /// <summary>Walks <paramref name="objectId"/>'s own ancestry, root first — the project, then (for an entry) its own week group.</summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false) is not TimesheetEntry entry)
            return [];

        var ancestry = new List<ProjectExplorerNode>();

        if (await _context.Repository.FindAsync(entry.ProjectId, cancellationToken).ConfigureAwait(false) is { } project)
        {
            ancestry.Add(await ToProjectNodeAsync(project, cancellationToken).ConfigureAwait(false));

            var week = TimesheetWeek.WeekOf(entry.Date);
            ancestry.Add(new ProjectExplorerNode(GroupNodeId(entry.ProjectId, week), $"Week of {week:yyyy-MM-dd}", null, true, ProjectExplorerNodeType.Category));
        }

        return ancestry;
    }

    private async Task<List<IEngineeringObject>> LiveProjectsAsync(CancellationToken cancellationToken) =>
        (await _context.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(false))
        .Where(IsLive)
        .ToList();

    private async Task<List<TimesheetEntry>> LiveEntriesUnderProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false))
        .OfType<TimesheetEntry>()
        .Where(IsLive)
        .ToList();

    private async Task<ProjectExplorerNode> ToProjectNodeAsync(IEngineeringObject project, CancellationToken cancellationToken)
    {
        var hasEntries = (await LiveEntriesUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false)).Count > 0;
        return new ProjectExplorerNode(
            project.Id, DisplayNameOf(project), project.Kind, hasEntries, ProjectExplorerNodeType.Object,
            Identifier: (project as IHasBusinessIdentifier)?.Identifier);
    }

    private static ProjectExplorerNode ToEntryNode(TimesheetEntry entry) =>
        new(entry.Id, $"{entry.Date:yyyy-MM-dd} — {entry.Hours}h — {entry.TaskDescription}", entry.Kind, false, ProjectExplorerNodeType.Object, Identifier: entry.Identifier);

    /// <summary>A deterministic, non-persisted node id for <paramref name="week"/>'s own group under <paramref name="projectId"/>, varied by the week's own stable day number.</summary>
    private static Guid GroupNodeId(Guid projectId, DateOnly week)
    {
        var bytes = projectId.ToByteArray();
        var dayNumberBytes = BitConverter.GetBytes(week.DayNumber);

        for (var i = 0; i < dayNumberBytes.Length && i < bytes.Length; i++)
            bytes[i] ^= dayNumberBytes[i];

        bytes[^1] ^= 0b0110_1001;

        return new Guid(bytes);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
