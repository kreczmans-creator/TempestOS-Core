using System.Text.Json;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

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
/// <b>Why this class keeps a durable index.</b> The platform's object
/// graph repository is in-memory by design (`ADR-0077`): engineering
/// <i>documents</i> are durable, but the reconstructed objects over them
/// are not, so nothing read only through
/// <see cref="IEngineeringObjectRepository"/> survives a restart. A
/// project is the one object the product shell must be able to reopen by
/// name after the application closes, so this class maintains its own
/// small, durable <c>projectId → identity</c> index in
/// <see cref="IPersistenceStore"/> — the identical, already-approved
/// pattern <c>MaterialCatalog</c> uses for exactly the same reason
/// (`ADR-0055`), never a second storage mechanism.
/// </para>
/// <para>
/// The live domain object is always authoritative when it exists; the
/// index is the fallback that makes a project findable after a restart.
/// The underlying durability split itself is disclosed debt, not fixed
/// here (`TD-85`).
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

    /// <summary>The <see cref="IPersistenceStore"/> collection this directory's own durable project index lives in.</summary>
    public const string IndexCollectionName = "Projects.Index";

    private readonly EngineeringDomainContext _context;
    private readonly IPersistenceStore _persistenceStore;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="ProjectDirectory"/> class.</summary>
    /// <param name="context">The engineering domain context every project is read from and created through.</param>
    /// <param name="persistenceStore">The store this directory's own durable project index is held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="persistenceStore"/> is <see langword="null"/>.</exception>
    public ProjectDirectory(EngineeringDomainContext context, IPersistenceStore persistenceStore, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(persistenceStore);

        _context = context;
        _persistenceStore = persistenceStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var live = (await _context.Repository.ListByKindAsync(ProjectKind, cancellationToken).ConfigureAwait(false))
            .OfType<IProject>()
            .Select(ToSummary)
            .ToDictionary(p => p.Id);

        // Every indexed project, preferring its live object when the object
        // graph still holds one (post-restart it will not).
        foreach (var key in await _persistenceStore.ListKeysAsync(IndexCollectionName, cancellationToken).ConfigureAwait(false))
        {
            if (!Guid.TryParseExact(key, "N", out var projectId) || live.ContainsKey(projectId))
                continue;

            if (await ReadIndexAsync(projectId, cancellationToken).ConfigureAwait(false) is { } indexed)
                live[projectId] = indexed;
        }

        return live.Values
            .OrderBy(p => p.Identifier ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(p => p.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ProjectSummary?> FindAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        // The live object is authoritative — it carries the current
        // lifecycle state and name.
        var found = await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (found is IProject project)
            return ToSummary(project);

        // Otherwise the durable index, which is what makes a project
        // reopenable after a restart.
        return await ReadIndexAsync(projectId, cancellationToken).ConfigureAwait(false);
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

        var summary = ToSummary(created);
        await WriteIndexAsync(summary, cancellationToken).ConfigureAwait(false);

        _logger?.Information($"Project created: '{identifier}' — '{displayName}' ({created.Id}).");

        return summary;
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

    private Task WriteIndexAsync(ProjectSummary project, CancellationToken cancellationToken) =>
        _persistenceStore.WriteAsync(
            IndexCollectionName,
            project.Id.ToString("N"),
            JsonSerializer.Serialize(new ProjectIndexDto(project.Identifier, project.DisplayName, project.Status, project.ProgrammeId)),
            cancellationToken);

    private async Task<ProjectSummary?> ReadIndexAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var json = await _persistenceStore.ReadAsync(IndexCollectionName, projectId.ToString("N"), cancellationToken).ConfigureAwait(false);
        if (json is null)
            return null;

        ProjectIndexDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ProjectIndexDto>(json);
        }
        catch (JsonException)
        {
            // A corrupted index entry reads as "no such project" rather
            // than failing every project listing (`TD-60`'s discipline).
            _logger?.Warning($"Project index entry '{projectId}' is unreadable and was skipped.");
            return null;
        }

        return dto is null ? null : new ProjectSummary(projectId, dto.Identifier, dto.DisplayName, dto.Status, dto.ProgrammeId);
    }

    /// <summary>The durable identity snapshot one project is indexed by — never a copy of anything the live object owns beyond what reopening requires.</summary>
    private sealed record ProjectIndexDto(string? Identifier, string DisplayName, LifecycleState Status, Guid? ProgrammeId);
}
