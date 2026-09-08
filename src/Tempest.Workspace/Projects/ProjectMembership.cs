using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Projects;

/// <summary>
/// Resolves which project an engineering object belongs to — the single
/// definition of project membership in TempestOS.
/// </summary>
/// <remarks>
/// <para>
/// <b>Membership is the structural parent chain, not a new field.</b>
/// <see cref="IHasParent"/>/<c>MoveAsync</c> already is the edge that makes
/// an object belong somewhere (`WP 9.0A`), and it is durable (`TD-85`). An
/// object belongs to a project when walking that chain upwards reaches a
/// <see cref="ProjectDirectory.ProjectKind"/> object; it is
/// <b>standalone</b> when the walk ends without reaching one. No second
/// ownership mechanism, no <c>ProjectId</c> column bolted onto the domain.
/// </para>
/// <para>
/// Both answers matter equally. TempestOS is project-centric, but quick
/// calculations and calculation sets are a first-class workflow with no
/// project (`TD-89`), so "belongs to no project" is a real, supported
/// state — never an error or an orphan.
/// </para>
/// <para>
/// The walk is bounded by a visited set. <c>MoveAsync</c> already rejects
/// a cycle at write time, but a graph rebuilt from a store this class does
/// not own must not be able to hang the shell.
/// </para>
/// </remarks>
public static class ProjectMembership
{
    /// <summary>
    /// The Id of the project <paramref name="objectId"/> belongs to, or
    /// <see langword="null"/> when it belongs to none (standalone) or does
    /// not exist.
    /// </summary>
    public static async Task<Guid?> ResolveOwningProjectAsync(
        IEngineeringObjectRepository repository, Guid objectId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var visited = new HashSet<Guid>();
        var current = objectId;

        while (visited.Add(current))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var found = await repository.FindAsync(current, cancellationToken).ConfigureAwait(false);
            if (found is null)
                return null;

            if (string.Equals(found.Kind, ProjectDirectory.ProjectKind, StringComparison.Ordinal))
                return found.Id;

            if (found is not IHasParent { ParentId: { } parentId })
                return null;

            current = parentId;
        }

        return null;
    }

    /// <summary>
    /// Every live object belonging to <paramref name="projectId"/>,
    /// transitively — an Assembly's own Parts are in the project, not only
    /// the Assembly. The project itself is excluded.
    /// </summary>
    public static Task<IReadOnlyList<IEngineeringObject>> ListProjectMembersAsync(
        IEngineeringObjectRepository repository, Guid projectId, CancellationToken cancellationToken = default) =>
        ListWhereAsync(repository, owner => owner == projectId, cancellationToken);

    /// <summary>
    /// Every live object belonging to no project at all — the standalone
    /// engineering estate (`TD-89`). Projects themselves are excluded.
    /// </summary>
    public static Task<IReadOnlyList<IEngineeringObject>> ListStandaloneAsync(
        IEngineeringObjectRepository repository, CancellationToken cancellationToken = default) =>
        ListWhereAsync(repository, owner => owner is null, cancellationToken);

    private static async Task<IReadOnlyList<IEngineeringObject>> ListWhereAsync(
        IEngineeringObjectRepository repository, Func<Guid?, bool> matches, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var all = await repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var members = new List<IEngineeringObject>();

        foreach (var candidate in all)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A project is its own context, never a member of one — and a
            // project is never standalone engineering work either. This is
            // also what keeps the project itself out of its own contents.
            if (string.Equals(candidate.Kind, ProjectDirectory.ProjectKind, StringComparison.Ordinal))
                continue;

            // Deleted objects are not contents (`WP 9.0A` soft delete).
            if (candidate is IDeletable { IsDeleted: true })
                continue;

            var owner = await ResolveOwningProjectAsync(repository, candidate.Id, cancellationToken).ConfigureAwait(false);
            if (matches(owner))
                members.Add(candidate);
        }

        return members;
    }
}
