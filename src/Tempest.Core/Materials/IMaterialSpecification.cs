namespace Tempest.Core.Materials;

/// <summary>
/// A registered material's own identity, classification, and dimensioned
/// properties. Backed by, and traceable directly to, a single
/// <see cref="EngineeringData.IEngineeringDocument"/> of
/// <c>Kind = "MaterialSpecification"</c> — this is an indexed, typed view
/// over that document, not a second, parallel storage mechanism.
/// </summary>
public interface IMaterialSpecification
{
    /// <summary>The caller-assigned identity this material was registered under. Stable — never changes for the life of this material.</summary>
    string MaterialId { get; }

    /// <summary>The material's own display name.</summary>
    string Name { get; }

    /// <summary>
    /// An open, caller-assigned classification (e.g. "Metal", "Polymer") —
    /// deliberately not a closed enum, since no real discipline requirement
    /// has yet named a fixed taxonomy to validate one against. <see langword="null"/>
    /// if uncategorised. Stable — never changes after registration, mirroring
    /// <see cref="EngineeringData.IEngineeringDocument.Kind"/>'s own immutability.
    /// </summary>
    string? Category { get; }

    /// <summary>Every registered engineering property, each carrying its own provenance. Never <see langword="null"/>; empty if none are registered.</summary>
    IReadOnlyDictionary<string, MaterialProperty> Properties { get; }

    /// <summary>The Id of the <see cref="EngineeringData.IEngineeringDocument"/> this specification is backed by — use this directly with <see cref="EngineeringData.IEngineeringDocumentStore"/> for revision history or typed references (e.g. "derivedFrom" a source standard document) this catalogue does not itself duplicate.</summary>
    Guid UnderlyingDocumentId { get; }

    /// <summary>The underlying document's current revision number — advances each time <see cref="IMaterialCatalog.ReviseAsync"/> is called.</summary>
    int RevisionNumber { get; }
}
