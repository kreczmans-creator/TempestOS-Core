using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Builds an <see cref="EngineeringDomainContext"/> out of the real
/// production components over one
/// <see cref="InMemoryQueryablePersistenceStore"/> (`ADR-0145`).
/// </summary>
/// <remarks>
/// <para>
/// <b>This replaces <c>InMemoryEngineeringDocumentStore</c>.</b> That class
/// existed because a test wanting a document store without a filesystem
/// had to reimplement one: documents, revisions and references kept in
/// dictionaries, with its own copy of the store's rules about revision
/// numbering and reference keys. It was a second implementation of the
/// document store, and it drifted — which is precisely the shape
/// `ADR-0145` set out to remove from the production code.
/// </para>
/// <para>
/// Keeping it as a fourth writer alongside the transaction would have
/// meant teaching it <c>ITransactionalDocumentWriter</c> as well, so that
/// a test double could take part in the atomicity the real store
/// provides. The honest alternative is the one taken here: the real
/// <see cref="EngineeringDocumentStore"/>, the real
/// <see cref="EngineeringObjectStateStore"/> and the real
/// <see cref="AttachmentContentStore"/>, over a persistence store that
/// happens to be in memory. Every test then exercises the shipped write
/// path, and a fault is injected at the store — where a real fault
/// actually occurs — rather than at a hand-written stand-in for one of
/// four writers.
/// </para>
/// </remarks>
internal static class TestEngineeringDomain
{
    /// <summary>Builds a context over a fresh in-memory store.</summary>
    public static EngineeringDomainContext NewContext() => NewContext(out _);

    /// <summary>Builds a context over a fresh in-memory store, and hands the store back.</summary>
    /// <param name="store">The one durable store the context commits through.</param>
    public static EngineeringDomainContext NewContext(out InMemoryQueryablePersistenceStore store)
    {
        store = new InMemoryQueryablePersistenceStore();
        return NewContextOver(store, store);
    }

    /// <summary>
    /// Builds a context that commits through <paramref name="transactional"/>
    /// while its read surfaces are built over <paramref name="backing"/>.
    /// </summary>
    /// <remarks>
    /// The two differ when a test wraps the store to inject a fault — a
    /// <see cref="CommitFailingPersistenceStore"/> around the same
    /// in-memory store — so that the reads a fact makes afterwards go to
    /// the real committed state rather than through the wrapper.
    /// </remarks>
    /// <param name="transactional">The store the context opens transactions on.</param>
    /// <param name="backing">The store every read surface is built over.</param>
    /// <param name="workspaceChanges">
    /// Where a committed write's touched set is announced (`WP 18.1A`).
    /// <see langword="null"/> — the default — is a legitimate no-op, for
    /// the great majority of tests that have no feed to assert against.
    /// </param>
    public static EngineeringDomainContext NewContextOver(
        Core.Persistence.IQueryablePersistenceStore transactional,
        InMemoryQueryablePersistenceStore backing,
        IWorkspaceChangePublisher? workspaceChanges = null)
    {
        ArgumentNullException.ThrowIfNull(transactional);
        ArgumentNullException.ThrowIfNull(backing);

        var principalAccessor = new CurrentPrincipalAccessor();
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);

        return new EngineeringDomainContext(
            transactional,
            new EngineeringDocumentStore(backing, principalAccessor),
            repository,
            relationshipRepository,
            new LifecycleTransitionTable(),
            new ValidationRuleSet(),
            new EvidenceComposer(relationshipDiscovery, repository),
            principalAccessor,
            new EngineeringObjectStateStore(backing),
            new AttachmentContentStore(backing),
            logger: null,
            workspaceChanges: workspaceChanges);
    }
}
