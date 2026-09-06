using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Reconstructs one Kind's canonical objects by calling that type's own
/// <see cref="IRehydratable{TSelf}.Rehydrate"/> (`TD-85`).
/// </summary>
/// <remarks>
/// The rehydrating mirror of <see cref="EngineeringObjectFactory{T}"/>:
/// one generic type serving every Kind, each <em>instance</em> responsible
/// for exactly one declared <see cref="Kind"/>. It holds no per-type
/// knowledge at all — <typeparamref name="T"/> supplies its own
/// reconstruction, and this class only supplies the shared plumbing every
/// Kind needs identically (restore mutable state, attach a self-factory so
/// the rehydrated object can still revise itself).
/// </remarks>
public sealed class EngineeringObjectRehydrator<T> : IEngineeringObjectRehydrator
    where T : EngineeringObjectBase, IRehydratable<T>
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="EngineeringObjectRehydrator{T}"/> class.</summary>
    /// <param name="kind">The Kind this rehydrator answers for — supplied by that Kind's own declaring class (`ADR-0105`), never invented here.</param>
    /// <param name="context">The shared domain collaborators a rehydrated object needs.</param>
    public EngineeringObjectRehydrator(string kind, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);

        Kind = kind;
        _context = context;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public Type ObjectType => typeof(T);

    /// <inheritdoc />
    public IEngineeringObject Rehydrate(EngineeringObjectState state, IEngineeringDocument document, IDocumentRevision currentRevision)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(currentRevision);

        var instance = T.Rehydrate(document, currentRevision, _context, state);

        // The same self-factory every created object gets, so a rehydrated
        // object is not a second-class one: it can still revise itself, and
        // its successor is correctly typed.
        //
        // `WP 16.4B-R6`: the state comes from the caller at revision time,
        // not from this closure. Closing over `state` meant a successor was
        // reconstructed from the record as it stood *at rehydration*, so
        // every type-specific field mutated during this lifetime was
        // silently reverted by an ordinary `ReviseAsync`.
        instance.AttachSelfFactory((doc, rev, revisionState) => T.Rehydrate(doc, rev, _context, revisionState));

        // Everything a constructor cannot carry — lifecycle, history,
        // parent, deletion, BOM line, attachments.
        instance.RestoreState(state);

        return instance;
    }
}

/// <summary>The concrete <see cref="IEngineeringObjectRehydratorRegistry"/> (`TD-85`).</summary>
public sealed class EngineeringObjectRehydratorRegistry : IEngineeringObjectRehydratorRegistry
{
    private readonly Dictionary<string, IEngineeringObjectRehydrator> _byKind = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(IEngineeringObjectRehydrator rehydrator)
    {
        ArgumentNullException.ThrowIfNull(rehydrator);

        if (_byKind.TryGetValue(rehydrator.Kind, out var existing))
        {
            if (existing.ObjectType == rehydrator.ObjectType)
                return; // Idempotent — the identical registration, run twice.

            throw new DuplicateRehydratorRegistrationException(rehydrator.Kind, existing.ObjectType, rehydrator.ObjectType);
        }

        _byKind.Add(rehydrator.Kind, rehydrator);
    }

    /// <inheritdoc />
    public IEngineeringObjectRehydrator? Find(string kind) =>
        kind is not null && _byKind.TryGetValue(kind, out var rehydrator) ? rehydrator : null;

    /// <inheritdoc />
    public IReadOnlyList<string> RegisteredKinds => _byKind.Keys.Order(StringComparer.Ordinal).ToList();
}

/// <summary>Thrown when two different canonical types claim the same Kind for rehydration (`TD-85`).</summary>
public sealed class DuplicateRehydratorRegistrationException : EngineeringDomainException
{
    /// <summary>Initialises a new instance of the <see cref="DuplicateRehydratorRegistrationException"/> class.</summary>
    public DuplicateRehydratorRegistrationException(string kind, Type existingType, Type attemptedType)
        : base($"Kind '{kind}' is already registered for rehydration as '{existingType.Name}' — '{attemptedType.Name}' cannot also claim it.")
    {
        Kind = kind;
        ExistingType = existingType;
        AttemptedType = attemptedType;
    }

    /// <summary>The contested Kind.</summary>
    public string Kind { get; }

    /// <summary>The type already registered for <see cref="Kind"/>.</summary>
    public Type ExistingType { get; }

    /// <summary>The type that attempted to claim <see cref="Kind"/>.</summary>
    public Type AttemptedType { get; }
}

/// <summary>The one-line registration form every Kind's own declaring class uses (`TD-85`).</summary>
public static class EngineeringObjectRehydratorRegistryExtensions
{
    /// <summary>
    /// Registers <typeparamref name="T"/> as the type <paramref name="kind"/>
    /// comes back as.
    /// </summary>
    /// <remarks>
    /// <paramref name="kind"/> is always the caller's own named Kind
    /// constant (`ADR-0105`) — the registry never invents or duplicates a
    /// Kind string of its own.
    /// </remarks>
    public static void Register<T>(this IEngineeringObjectRehydratorRegistry registry, string kind, EngineeringDomainContext context)
        where T : EngineeringObjectBase, IRehydratable<T>
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(new EngineeringObjectRehydrator<T>(kind, context));
    }
}
