using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;

namespace Tempest.Core.EngineeringDomain;

/// <summary>The shared collaborators every <see cref="EngineeringObjectBase"/> instance and factory needs — bundled to keep per-Kind constructors small.</summary>
public sealed class EngineeringDomainContext
{
    public IEngineeringDocumentStore Store { get; }
    public IEngineeringObjectRepository Repository { get; }
    public IEngineeringRelationshipRepository RelationshipRepository { get; }
    public ILifecycleTransitionTable LifecycleTable { get; }
    public IValidationRuleSet ValidationRuleSet { get; }
    public IEvidenceComposer EvidenceComposer { get; }
    public ICurrentPrincipalAccessor CurrentPrincipalAccessor { get; }

    /// <summary>
    /// The durable engineering-object state store (`TD-85`), or
    /// <see langword="null"/> where no rehydration substrate is composed.
    /// </summary>
    /// <remarks>
    /// Optional so every existing hand-assembled context in tests and
    /// samples keeps working unchanged: with no store, objects behave
    /// exactly as they did before `TD-85` (in-memory only). The production
    /// Host always supplies one.
    /// </remarks>
    public IEngineeringObjectStateStore? ObjectStateStore { get; }

    public EngineeringDomainContext(
        IEngineeringDocumentStore store,
        IEngineeringObjectRepository repository,
        IEngineeringRelationshipRepository relationshipRepository,
        ILifecycleTransitionTable lifecycleTable,
        IValidationRuleSet validationRuleSet,
        IEvidenceComposer evidenceComposer,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IEngineeringObjectStateStore? objectStateStore = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(relationshipRepository);
        ArgumentNullException.ThrowIfNull(lifecycleTable);
        ArgumentNullException.ThrowIfNull(validationRuleSet);
        ArgumentNullException.ThrowIfNull(evidenceComposer);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);

        Store = store;
        Repository = repository;
        RelationshipRepository = relationshipRepository;
        LifecycleTable = lifecycleTable;
        ValidationRuleSet = validationRuleSet;
        EvidenceComposer = evidenceComposer;
        CurrentPrincipalAccessor = currentPrincipalAccessor;
        ObjectStateStore = objectStateStore;
    }

    public string ResolveCurrentPrincipalId() =>
        CurrentPrincipalAccessor.Current?.Identity.Id ?? InMemoryEngineeringDocumentStore.UnknownAuthorPrincipalId;
}
