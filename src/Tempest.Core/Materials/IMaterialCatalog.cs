using Tempest.Core.ReferenceData;

namespace Tempest.Core.Materials;

/// <summary>
/// The authoritative catalogue of engineering material reference data (A1).
/// </summary>
/// <remarks>
/// <para>
/// Register, retrieve, revise, govern, read history and supersede all come
/// from <see cref="IReferenceDataCatalog{TDefinition}"/>, shared with every
/// other Group A library. What is added here is materials-specific:
/// resolving a material by its own designation, and the material query.
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No material
/// selection, no allowable-stress derivation, no suitability judgement, and
/// no supplier commercial data. A1 supplies the reference evidence those
/// capabilities will consume.
/// </para>
/// </remarks>
public interface IMaterialCatalog : IReferenceDataCatalog<MaterialDefinition>
{
    /// <summary>
    /// Returns the material registered under <paramref name="designation"/>
    /// (and, where the material is a specific supplier's product,
    /// <paramref name="supplier"/>), or <see langword="null"/> if none is.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="designation"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<MaterialDefinition>?> FindByDesignationAsync(
        string designation,
        string? supplier = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered material matching <paramref name="query"/>, in the
    /// same order <see cref="IReferenceDataCatalog{TDefinition}.ListAsync"/>
    /// uses. Never <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<MaterialDefinition>>> SearchAsync(MaterialQuery query, CancellationToken cancellationToken = default);
}
