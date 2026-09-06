namespace Tempest.Core.ReferenceData;

/// <summary>
/// One registered reference-data record: its domain engineering
/// description, plus the catalogue governance around it.
/// </summary>
/// <remarks>
/// <para>
/// The split between <typeparamref name="TDefinition"/> and everything
/// else on this interface is the central modelling decision of Group A
/// (`ADR-0124`). The definition is what a source said about the thing —
/// engineering content. The rest is how TempestOS governs that content:
/// its identity here, how far it has been checked, what replaced it, and
/// which revision of which document holds it. Mixing the two would make
/// every domain model carry lifecycle fields and every lifecycle question
/// depend on a domain type.
/// </para>
/// <para>
/// Every P01 record is backed by, and traceable directly to, a single
/// <see cref="EngineeringData.IEngineeringDocument"/> — an indexed, typed
/// view over that shared store, never a second storage mechanism
/// (`ADR-0053`, `ADR-0055`, `ADR-0072`).
/// </para>
/// </remarks>
/// <typeparam name="TDefinition">The domain's own engineering description type.</typeparam>
public interface IReferenceRecord<out TDefinition>
    where TDefinition : class
{
    /// <summary>The caller-assigned TempestOS identity this record was registered under. Stable — never changes, and never derived from a designation or part number.</summary>
    string Id { get; }

    /// <summary>The record's own domain engineering description.</summary>
    TDefinition Definition { get; }

    /// <summary>Where the record's own data came from, and how far it can be trusted.</summary>
    ReferenceProvenance Provenance { get; }

    /// <summary>The record's own position in the Group A validation lifecycle.</summary>
    ReferenceValidationState ValidationState { get; }

    /// <summary>
    /// The record that replaced this one, if it has been superseded.
    /// <see langword="null"/> otherwise — including for a record
    /// superseded without a stated replacement, which is why this is
    /// nullable even when <see cref="ValidationState"/> is
    /// <see cref="ReferenceValidationState.Superseded"/>.
    /// </summary>
    string? SupersededByRecordId { get; }

    /// <summary>The Id of the <see cref="EngineeringData.IEngineeringDocument"/> this record is backed by — use it directly with <see cref="EngineeringData.IEngineeringDocumentStore"/> for revision history and typed document references the catalogue does not itself duplicate.</summary>
    Guid UnderlyingDocumentId { get; }

    /// <summary>The underlying document's current revision number — advances on every catalogue write that changes this record.</summary>
    int RevisionNumber { get; }
}
