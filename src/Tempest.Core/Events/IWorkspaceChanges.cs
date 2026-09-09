namespace Tempest.Core.Events;

/// <summary>
/// The subscribe-only surface of the platform's change feed (`WP 18.1A`):
/// raised once per committed engineering-domain transaction, after the
/// commit, naming the store sequence it landed at and every object it
/// touched.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not routed through <see cref="IEventBus"/>.</b> The one
/// publisher, <see cref="EngineeringDomain.EngineeringDomainContext"/>,
/// raises <see cref="Changed"/> synchronously, still holding the domain
/// write lock, on whatever thread completed the commit — never the UI
/// thread, since the write path reaches this point through a chain of
/// <c>ConfigureAwait(false)</c> calls. <see cref="IEventBus.PublishAsync{TEvent}"/>'s
/// own sequential, awaited dispatch would instead make every subscriber's
/// own handling part of the commit's critical section, and a subscriber
/// that must run on the UI thread would have no way to opt out of that. A
/// subscriber here does the opposite: it marshals to the UI thread itself
/// (<c>Dispatcher.UIThread.Post</c>) before touching anything UI-owned,
/// exactly as an ordinary background notification would.
/// </para>
/// <para>
/// A subscriber that wants to render what changed takes one
/// <c>Tempest.Workspace.WorkspaceSnapshot</c> read at (at least) the
/// event's own <see cref="WorkspaceChange.Sequence"/> — never composes
/// several independent reads, which could straddle a commit that lands
/// between them.
/// </para>
/// </remarks>
public interface IWorkspaceChanges
{
    /// <summary>Raised once per committed transaction, after the commit.</summary>
    event Action<WorkspaceChange>? Changed;
}

/// <summary>
/// The publish-only surface of the platform's change feed (`WP 18.1A`),
/// held by <see cref="EngineeringDomain.EngineeringDomainContext"/> alone.
/// </summary>
/// <remarks>
/// Split from <see cref="IWorkspaceChanges"/> the same way
/// <c>Tempest.Core.Persistence.IPersistenceStore</c> and
/// <c>IQueryablePersistenceStore</c> sit beside each other on one
/// instance (`ADR-0044`'s dual-registration pattern): a Desktop view
/// resolves <see cref="IWorkspaceChanges"/> and can only subscribe; the
/// one durable-write path resolves this and can only publish. Nothing
/// downstream of a commit can raise its own change.
/// </remarks>
public interface IWorkspaceChangePublisher
{
    /// <summary>Raises <see cref="IWorkspaceChanges.Changed"/> for every current subscriber.</summary>
    /// <param name="change">The commit to announce.</param>
    void Publish(WorkspaceChange change);
}

/// <summary>
/// The one concrete change feed (`WP 18.1A`): a bare, synchronous .NET
/// event, deliberately not the platform's own <see cref="IEventBus"/> —
/// see <see cref="IWorkspaceChanges"/>'s remarks for why.
/// </summary>
public sealed class WorkspaceChangeFeed : IWorkspaceChanges, IWorkspaceChangePublisher
{
    /// <inheritdoc />
    public event Action<WorkspaceChange>? Changed;

    /// <inheritdoc />
    public void Publish(WorkspaceChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        Changed?.Invoke(change);
    }
}
