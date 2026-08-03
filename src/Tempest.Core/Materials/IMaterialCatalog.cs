namespace Tempest.Core.Materials;

/// <summary>
/// A catalogue of material specifications. Each specification is itself an
/// <see cref="EngineeringData.IEngineeringDocument"/> of
/// <c>Kind = "MaterialSpecification"</c> — this catalogue is an indexed,
/// typed view over that shared store, not a second storage mechanism.
/// </summary>
public interface IMaterialCatalog
{
    /// <summary>Registers a new material specification.</summary>
    /// <param name="materialId">The caller-assigned identity to register this material under.</param>
    /// <param name="name">The material's own display name.</param>
    /// <param name="properties">Every engineering property this material is registered with, each carrying its own provenance.</param>
    /// <param name="category">An open, caller-assigned classification (e.g. "Metal"). <see langword="null"/> if uncategorised.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="DuplicateMaterialException"><paramref name="materialId"/> is already registered.</exception>
    Task<IMaterialSpecification> RegisterAsync(
        string materialId,
        string name,
        IReadOnlyDictionary<string, MaterialProperty> properties,
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the specification, or <see langword="null"/> if none is registered under <paramref name="materialId"/>.</summary>
    Task<IMaterialSpecification?> FindAsync(string materialId, CancellationToken cancellationToken = default);

    /// <summary>Every registered material specification. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IMaterialSpecification>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new revision of an existing material's own properties,
    /// mirroring <see cref="EngineeringData.IEngineeringDocumentStore.ReviseAsync"/>'s
    /// own revision-controlled model. <see cref="IMaterialSpecification.MaterialId"/>,
    /// <see cref="IMaterialSpecification.Name"/>, and <see cref="IMaterialSpecification.Category"/>
    /// are unaffected — only <paramref name="properties"/> changes.
    /// </summary>
    /// <exception cref="MaterialNotFoundException"><paramref name="materialId"/> does not exist.</exception>
    Task<IMaterialSpecification> ReviseAsync(
        string materialId,
        IReadOnlyDictionary<string, MaterialProperty> properties,
        string? changeSummary,
        CancellationToken cancellationToken = default);
}
