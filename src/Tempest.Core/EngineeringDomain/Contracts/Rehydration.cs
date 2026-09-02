using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// A canonical object type that knows how to reconstruct itself from its
/// own persisted <see cref="EngineeringObjectState"/> (`TD-85`).
/// </summary>
/// <remarks>
/// <para>
/// The symmetric other half of
/// <c>EngineeringObjectBase.CaptureTypeState</c>: a type writes its own
/// fields on the way out and reads its own fields on the way back in.
/// Neither direction is knowledge held by any other class — there is no
/// central switch over Kind, and no service that must be edited whenever a
/// new canonical type is added.
/// </para>
/// <para>
/// Declared as a <c>static abstract</c> member so
/// <see cref="EngineeringObjectRehydrator{T}"/> can invoke it generically:
/// a type that has not implemented it simply cannot be registered, which
/// is a compile error rather than a runtime surprise at startup.
/// </para>
/// </remarks>
/// <typeparam name="TSelf">The implementing type itself.</typeparam>
public interface IRehydratable<TSelf>
    where TSelf : EngineeringObjectBase
{
    /// <summary>
    /// Reconstructs an instance from <paramref name="state"/>. Only
    /// constructor-carried state belongs here — identifier, display name,
    /// metadata and this type's own fields. Everything mutable
    /// (<see cref="IHasLifecycle.Status"/> and its history,
    /// <see cref="IHasParent.ParentId"/>, deletion, BOM line, attachments)
    /// is restored immediately afterwards by
    /// <c>EngineeringObjectBase.RestoreState</c>, so an implementation
    /// never needs to handle it.
    /// </summary>
    static abstract TSelf Rehydrate(
        IEngineeringDocument document,
        IDocumentRevision currentRevision,
        EngineeringDomainContext context,
        EngineeringObjectState state);
}

/// <summary>
/// Reconstructs one Kind's canonical objects from persisted state
/// (`TD-85`) — the rehydrating counterpart of
/// <see cref="IEngineeringObjectFactory"/>, which only ever creates new
/// ones.
/// </summary>
public interface IEngineeringObjectRehydrator
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> this rehydrator reconstructs.</summary>
    string Kind { get; }

    /// <summary>The concrete type this rehydrator constructs — used to detect a genuine Kind collision between two different types.</summary>
    Type ObjectType { get; }

    /// <summary>
    /// Reconstructs the object <paramref name="state"/> describes, fully
    /// restored and ready to use — including its self-factory, so the
    /// rehydrated object can still revise itself.
    /// </summary>
    IEngineeringObject Rehydrate(EngineeringObjectState state, IEngineeringDocument document, IDocumentRevision currentRevision);
}

/// <summary>
/// The Kind-to-rehydrator map startup rehydration resolves through
/// (`TD-85`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a Domain-layer registry now exists, when `WP8.2B Dependency
/// Rules.md` §8 proposed none.</b> The creation path never needed one:
/// a caller who wants a new Part already knows it wants a Part, and
/// constructs <see cref="EngineeringObjectFactory{T}"/> directly.
/// Rehydration is the opposite problem — the only thing the platform has
/// on startup is a Kind string read from disk, and something must map it
/// back to a type. That map has to live where the types live, so it is
/// declared here rather than bolted onto an unrelated service.
/// </para>
/// <para>
/// <b>Kind vocabulary still belongs to its declaring class (`ADR-0105`).</b>
/// This registry never declares a Kind string of its own; each Kind's own
/// declaring class registers it, passing the same named constant it
/// already owns.
/// </para>
/// </remarks>
public interface IEngineeringObjectRehydratorRegistry
{
    /// <summary>
    /// Registers <paramref name="rehydrator"/> for its own Kind.
    /// Registering the identical type for the same Kind twice is a no-op,
    /// so a composition root that runs a discipline's registration more
    /// than once is not punished for it; registering a <em>different</em>
    /// type for an already-claimed Kind throws.
    /// </summary>
    /// <exception cref="DuplicateRehydratorRegistrationException">A different type is already registered for the same Kind.</exception>
    void Register(IEngineeringObjectRehydrator rehydrator);

    /// <summary>The rehydrator for <paramref name="kind"/>, or <see langword="null"/> when that Kind cannot be rehydrated.</summary>
    IEngineeringObjectRehydrator? Find(string kind);

    /// <summary>Every registered Kind, ordered — the honest answer to "what can this platform bring back?".</summary>
    IReadOnlyList<string> RegisteredKinds { get; }
}
