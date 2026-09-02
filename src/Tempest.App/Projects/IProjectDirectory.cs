namespace Tempest.App.Projects;

/// <summary>
/// The catalogue of projects — the read/create surface the product shell
/// browses before any project is open.
/// </summary>
/// <remarks>
/// Reads and writes the real <see cref="Tempest.Core.EngineeringDomain.IProject"/>
/// engineering objects through the existing
/// <see cref="Tempest.Core.EngineeringDomain.EngineeringDomainContext"/> —
/// it owns no storage of its own, so a project created here is the same
/// object the Engineering Workspace, Digital Thread, audit trail and
/// Project Explorer already understand.
/// </remarks>
public interface IProjectDirectory
{
    /// <summary>Lists every project, ordered by identifier then display name.</summary>
    Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds one project by its own engineering-object Id, or <see langword="null"/> if no such project exists.</summary>
    Task<ProjectSummary?> FindAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a real project engineering object and returns its snapshot.
    /// </summary>
    /// <param name="identifier">The human-readable project identifier (for example <c>P-0027</c>).</param>
    /// <param name="displayName">The project's own display name.</param>
    /// <param name="description">The initial description stored as the project document's own first revision content.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> or <paramref name="displayName"/> is null, empty, or whitespace.</exception>
    /// <exception cref="DuplicateProjectIdentifierException">A project already carries <paramref name="identifier"/>.</exception>
    Task<ProjectSummary> CreateAsync(string identifier, string displayName, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every engineering object structurally owned by
    /// <paramref name="projectId"/> — the project's own contents, through
    /// the existing <see cref="Tempest.Core.EngineeringDomain.IHasParent"/>
    /// edge that already parents engineering objects to a project.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListProjectContentsAsync(Guid projectId, CancellationToken cancellationToken = default);
}
