namespace Tempest.App.Workspace;

/// <summary>
/// One entry in <see cref="NavigationService"/>'s own "recently opened
/// objects" list — the Workspace's own "recent items" surface
/// (`WP8.0C Navigation Maps.md` §5), deliberately global rather than
/// per-project (`WP8.1B Implementation Report.md` names this as a
/// disclosed simplification of `WP8.0C`'s own richer, project-scoped
/// design). Not one of the twelve `WP8.0B Workspace Contracts.md`
/// interfaces — a genuine, disclosed implementation-phase addition.
/// </summary>
/// <param name="ObjectId">The object's own Id.</param>
/// <param name="Kind">The object's own <c>Kind</c>.</param>
/// <param name="Title">The object's own display title, captured at the time it was opened.</param>
/// <param name="OpenedAt">When this object was most recently opened or jumped to.</param>
internal sealed record RecentNavigationItem(Guid ObjectId, string Kind, string Title, DateTimeOffset OpenedAt);
