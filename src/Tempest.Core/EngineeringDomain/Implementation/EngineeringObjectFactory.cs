using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// One generic factory type serving every Kind — each <em>instance</em> is still responsible for exactly one
/// declared <see cref="Kind"/> (WP8.2B Dependency Rules.md §7), constructed once per Kind by the composition
/// root rather than resolved from any registry (§8: no registry contract is proposed by WP8.2B).
/// </summary>
public sealed class EngineeringObjectFactory<T> : IEngineeringObjectFactory
    where T : EngineeringObjectBase, IRehydratable<T>
{
    private readonly EngineeringDomainContext _context;
    private readonly Func<IEngineeringDocument, IDocumentRevision, T> _constructor;

    public EngineeringObjectFactory(string kind, EngineeringDomainContext context, Func<IEngineeringDocument, IDocumentRevision, T> constructor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(constructor);

        Kind = kind;
        _context = context;
        _constructor = constructor;
    }

    public string Kind { get; }

    public async Task<IEngineeringObject> CreateAsync(string initialContent, CancellationToken cancellationToken = default)
    {
        var document = await _context.Store.CreateAsync(Kind, initialContent, cancellationToken).ConfigureAwait(false);
        var revisions = await _context.Store.GetRevisionHistoryAsync(document.Id, cancellationToken).ConfigureAwait(false);
        var currentRevision = revisions[^1];

        var instance = _constructor(document, currentRevision);
        // `WP 16.4B-R6`. The successor `ReviseAsync` builds is produced by
        // this type's own state reader, given the state captured at the
        // moment of the revision — not by re-running this factory's
        // construction closure, which only ever knew the values passed to
        // *this* call and therefore reverted every type-specific field a
        // caller had changed since. `IRehydratable{T}` is the interface that
        // reader already lives on (`TD-85`), which is why it is required
        // here; every canonical Kind in the platform implements it, because
        // every canonical Kind has to survive a restart.
        instance.AttachSelfFactory((doc, rev, state) => T.Rehydrate(doc, rev, _context, state));
        _context.Repository.Register(instance);

        // `TD-85`. The document alone only ever carried Kind, created-at and
        // prose; everything the caller passed through this factory's own
        // constructor closure — identifier, display name, metadata, and every
        // type-specific field — existed nowhere but in memory. Persisting the
        // object's own state here is what makes the object, rather than only
        // its document, survive a restart. A context composed without a state
        // store (every pre-`TD-85` hand-assembled one) is unaffected — see
        // PersistInitialStateAsync's own no-op branch.
        //
        // Routed through the same per-object write lock as every later
        // mutation (`WP 16.4B-R3`) rather than capturing and saving
        // directly here: this instance is already registered above, so a
        // concurrent caller that finds it through the repository could
        // otherwise race this very save exactly as two mutators could
        // race each other.
        await instance.PersistInitialStateAsync(cancellationToken).ConfigureAwait(false);

        return instance;
    }
}

/// <summary>A uniform <see cref="IEngineeringRelationshipFactory"/> — <see cref="IEngineeringRelationship"/>'s own shape needs no per-Kind specialisation, so one concrete type, instantiated once per named relationship kind, suffices (mirrors <see cref="EngineeringObjectFactory{T}"/>'s own reasoning).</summary>
public sealed class EngineeringRelationshipFactory : IEngineeringRelationshipFactory
{
    private readonly EngineeringDomainContext _context;
    private readonly RelationshipCategory _category;

    public EngineeringRelationshipFactory(string relationshipKind, RelationshipCategory category, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipKind);
        ArgumentNullException.ThrowIfNull(context);

        RelationshipKind = relationshipKind;
        _category = category;
        _context = context;
    }

    public string RelationshipKind { get; }

    public async Task<IEngineeringRelationship> CreateAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default)
    {
        if (sourceId == targetId)
            throw new SelfReferentialRelationshipException(sourceId);

        await _context.Store.LinkAsync(sourceId, targetId, RelationshipKind, cancellationToken).ConfigureAwait(false);

        var relationship = new EngineeringRelationship(sourceId, targetId, RelationshipKind, _category, _context.ResolveCurrentPrincipalId(), DateTimeOffset.UtcNow);
        _context.RelationshipRepository.Record(relationship);

        return relationship;
    }
}
