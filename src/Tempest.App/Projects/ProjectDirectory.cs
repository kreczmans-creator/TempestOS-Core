using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;

namespace Tempest.App.Projects;

/// <summary>
/// The concrete <see cref="IProjectDirectory"/> — a thin read/create
/// surface over the real <see cref="IProject"/> engineering objects.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CreateAsync"/> goes through the same
/// <see cref="EngineeringObjectFactory{T}"/> every discipline module
/// already uses — so a project created from the product shell is
/// indistinguishable from one created by a module, and inherits
/// revisions, lifecycle, audit and principal attribution for free.
/// </para>
/// <para>
/// <b>No index of its own (`TD-85`).</b> This class previously kept a
/// second, durable <c>Projects.Index</c> collection, because the object
/// graph did not survive a restart (`ADR-0077`) and a project is the one
/// object the product shell must be able to reopen by name. That gap is
/// now closed at its real source: engineering object state is durable, and
/// <see cref="EngineeringObjectRehydrationService"/> reconstructs every
/// persisted object — projects included — into
/// <see cref="IEngineeringObjectRepository"/> at startup, through the
/// normal factory architecture. The index was therefore removed rather
/// than left in place as a second, competing answer to "what projects
/// exist?": this class now reads the one object graph, and the one
/// persistence authority behind it.
/// </para>
/// <para>
/// The practical consequence is that a rehydrated project is a real
/// <see cref="IProject"/>, not a summary snapshot — so its lifecycle
/// state, relationships, revisions and contents are all live after a
/// restart, which a name-and-status index could never have provided.
/// </para>
/// </remarks>
public sealed class ProjectDirectory : IProjectDirectory
{
    /// <summary>
    /// The <see cref="Tempest.Core.EngineeringData.IEngineeringDocument.Kind"/>
    /// every project's own backing document carries — referenced from the
    /// Mechanical discipline's own registered constant rather than
    /// redeclared, per `ADR-0105` (a vocabulary value is declared once).
    /// </summary>
    public static string ProjectKind => Workspace.Mechanical.MechanicalObjectFactoryRegistry.Project;

    private readonly EngineeringDomainContext _context;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="ProjectDirectory"/> class.</summary>
    /// <param name="context">The engineering domain context every project is read from and created through.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ProjectDirectory(EngineeringDomainContext context, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _context.Repository.ListByKindAsync(ProjectKind, cancellationToken).ConfigureAwait(false);

        return projects
            .OfType<IProject>()
            .Select(ToSummary)
            .OrderBy(p => p.Identifier ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(p => p.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ProjectSummary?> FindAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var found = await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false);

        return found is IProject project ? ToSummary(project) : null;
    }

    /// <inheritdoc />
    public async Task<ProjectSummary> CreateAsync(string identifier, string displayName, string? description = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        // Business-identifier uniqueness is not enforced by
        // EngineeringObjectFactory itself (`TD-38`) — the product shell
        // enforces it here rather than letting two projects share the
        // identifier every other surface labels them by.
        var existing = await ListAsync(cancellationToken).ConfigureAwait(false);
        if (existing.Any(p => string.Equals(p.Identifier, identifier, StringComparison.OrdinalIgnoreCase)))
            throw new DuplicateProjectIdentifierException(identifier);

        var factory = new EngineeringObjectFactory<Project>(
            ProjectKind,
            _context,
            (doc, rev) => new Project(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty));

        var created = (Project)await factory
            .CreateAsync(description ?? $"Project {identifier} — {displayName}.", cancellationToken)
            .ConfigureAwait(false);

        _logger?.Information($"Project created: '{identifier}' — '{displayName}' ({created.Id}).");

        return ToSummary(created);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListProjectContentsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);

        return all
            .OfType<IHasParent>()
            .Where(o => o.ParentId == projectId)
            .Select(o => o.Id)
            .ToList();
    }

    private static ProjectSummary ToSummary(IProject project) =>
        new(project.Id, project.Identifier, project.DisplayName, project.Status, project.ProgrammeId);
}
