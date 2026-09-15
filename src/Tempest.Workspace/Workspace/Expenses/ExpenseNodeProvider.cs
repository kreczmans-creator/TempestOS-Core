using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Expenses;

/// <summary>
/// Populates the Project Explorer's own Expenses area: project → category
/// → expense (`WP 21.3B`). Mirrors
/// <c>Quotations.QuotationNodeProvider</c>'s own "rooted at every live
/// Project, grouped by a value that is not itself a structural parent"
/// shape — grouped by <see cref="ExpenseCategory"/>.
/// </summary>
public sealed class ExpenseNodeProvider : IProjectExplorerNodeProvider
{
    private static readonly IReadOnlyList<ExpenseCategory> Categories =
        [ExpenseCategory.Travel, ExpenseCategory.Subsistence, ExpenseCategory.Materials, ExpenseCategory.Subcontract, ExpenseCategory.Other];

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ExpenseNodeProvider"/> class.</summary>
    public ExpenseNodeProvider(string kind, EngineeringDomainContext context)
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
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Expenses node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var candidate = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (candidate is not null && string.Equals(candidate.Kind, MechanicalObjectFactoryRegistry.Project, StringComparison.Ordinal) && IsLive(candidate))
        {
            var underProject = await LiveExpensesUnderProjectAsync(nodeId, cancellationToken).ConfigureAwait(false);

            var groupNodes = new List<ProjectExplorerNode>();
            foreach (var category in Categories)
            {
                var count = underProject.Count(e => e.Category == category);
                groupNodes.Add(new ProjectExplorerNode(GroupNodeId(nodeId, category), category.ToString(), null, count > 0, ProjectExplorerNodeType.Category));
            }

            return groupNodes;
        }

        var projects = await LiveProjectsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var project in projects)
        {
            foreach (var category in Categories)
            {
                if (GroupNodeId(project.Id, category) != nodeId)
                    continue;

                var members = (await LiveExpensesUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false))
                    .Where(e => e.Category == category)
                    .OrderByDescending(e => e.Date)
                    .ThenBy(e => e.DisplayName, StringComparer.Ordinal);

                return members.Select(ToExpenseNode).ToList();
            }
        }

        throw new ArgumentException($"'{nodeId}' is not a known Expenses node.", nameof(nodeId));
    }

    /// <summary>Walks <paramref name="objectId"/>'s own ancestry, root first — the project, then (for an expense) its own category group.</summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false) is not ProjectExpense expense)
            return [];

        var ancestry = new List<ProjectExplorerNode>();

        if (await _context.Repository.FindAsync(expense.ProjectId, cancellationToken).ConfigureAwait(false) is { } project)
        {
            ancestry.Add(await ToProjectNodeAsync(project, cancellationToken).ConfigureAwait(false));
            ancestry.Add(new ProjectExplorerNode(GroupNodeId(expense.ProjectId, expense.Category), expense.Category.ToString(), null, true, ProjectExplorerNodeType.Category));
        }

        return ancestry;
    }

    private async Task<List<IEngineeringObject>> LiveProjectsAsync(CancellationToken cancellationToken) =>
        (await _context.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(false))
        .Where(IsLive)
        .ToList();

    private async Task<List<ProjectExpense>> LiveExpensesUnderProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false))
        .OfType<ProjectExpense>()
        .Where(IsLive)
        .ToList();

    private async Task<ProjectExplorerNode> ToProjectNodeAsync(IEngineeringObject project, CancellationToken cancellationToken)
    {
        var hasExpenses = (await LiveExpensesUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false)).Count > 0;
        return new ProjectExplorerNode(
            project.Id, DisplayNameOf(project), project.Kind, hasExpenses, ProjectExplorerNodeType.Object,
            Identifier: (project as IHasBusinessIdentifier)?.Identifier);
    }

    private static ProjectExplorerNode ToExpenseNode(ProjectExpense expense) =>
        new(expense.Id, $"{expense.Date:yyyy-MM-dd} — {expense.Description} — {expense.GrossAmount}", expense.Kind, false, ProjectExplorerNodeType.Object, Identifier: expense.Identifier);

    /// <summary>A deterministic, non-persisted node id for <paramref name="category"/>'s own group under <paramref name="projectId"/> — varies the project id's own first byte by the category's ordinal, mirroring <c>QuotationNodeProvider.GroupNodeId</c>'s own identical shape.</summary>
    private static Guid GroupNodeId(Guid projectId, ExpenseCategory category)
    {
        var bytes = projectId.ToByteArray();
        bytes[0] = (byte)(bytes[0] ^ (byte)category ^ 0b0110_1101);
        return new Guid(bytes);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
