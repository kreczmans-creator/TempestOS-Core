namespace Tempest.Core.Bearings;

/// <summary>
/// A bearing as this library holds it: its canonical engineering
/// description, plus the catalogue governance around it.
/// </summary>
/// <remarks>
/// Backed by, and traceable directly to, a single
/// <see cref="EngineeringData.IEngineeringDocument"/> of
/// <c>Kind = "BearingReference"</c> — an indexed, typed view over that
/// shared store, not a second storage mechanism. Mirrors
/// <see cref="Materials.IMaterialSpecification"/>'s own established shape
/// (`ADR-0055`, itself continuing `ADR-0053`).
/// </remarks>
public interface IBearing
{
    /// <summary>The caller-assigned TempestOS identity this bearing was registered under. Stable — never changes, and never derived from a manufacturer part number.</summary>
    string BearingId { get; }

    /// <summary>The bearing's own canonical engineering description.</summary>
    BearingDefinition Definition { get; }

    /// <summary>The record's own position in this library's own validation lifecycle.</summary>
    BearingValidationState ValidationState { get; }

    /// <summary>
    /// The bearing that replaced this one, if this record has been
    /// superseded. <see langword="null"/> otherwise — including for a
    /// record superseded without a stated replacement, which is why this
    /// is nullable even when <see cref="ValidationState"/> is
    /// <see cref="BearingValidationState.Superseded"/>.
    /// </summary>
    string? SupersededByBearingId { get; }

    /// <summary>The Id of the <see cref="EngineeringData.IEngineeringDocument"/> this record is backed by — use it directly with <see cref="EngineeringData.IEngineeringDocumentStore"/> for revision history and typed document references this catalogue does not itself duplicate.</summary>
    Guid UnderlyingDocumentId { get; }

    /// <summary>The underlying document's current revision number — advances on every catalogue write that changes this record.</summary>
    int RevisionNumber { get; }
}
