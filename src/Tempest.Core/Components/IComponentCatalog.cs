using Tempest.Core.ReferenceData;

namespace Tempest.Core.Components;

/// <summary>
/// The authoritative catalogue of mechanical component reference data (A5)
/// — springs, gears, drive elements and standard machine components.
/// </summary>
/// <remarks>
/// <para>
/// Register, retrieve, revise, govern, read history and supersede all come
/// from <see cref="IReferenceDataCatalog{TDefinition}"/>, shared with every
/// other Group A library. What is added here is component-specific:
/// resolving a component by designation or part number, and the component
/// query.
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No spring
/// design, no gear rating, no drive selection, no ratio or centre-distance
/// calculation, no life prediction, no suitability judgement, and no
/// commercial data. A5 supplies the reference evidence those capabilities
/// will consume.
/// </para>
/// </remarks>
public interface IComponentCatalog : IReferenceDataCatalog<ComponentDefinition>
{
    /// <summary>
    /// Returns the component registered under
    /// <paramref name="designation"/> (and, where the record describes a
    /// specific supplier's product, <paramref name="manufacturer"/>), or
    /// <see langword="null"/> if none is.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="designation"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<ComponentDefinition>?> FindByDesignationAsync(
        string designation,
        string? manufacturer = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the component registered under
    /// <paramref name="manufacturer"/> and <paramref name="partNumber"/>,
    /// or <see langword="null"/> if none is.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="partNumber"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<ComponentDefinition>?> FindByPartNumberAsync(
        string manufacturer,
        string partNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered component matching <paramref name="query"/>, in the
    /// same order <see cref="IReferenceDataCatalog{TDefinition}.ListAsync"/>
    /// uses. Never <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<ComponentDefinition>>> SearchAsync(ComponentQuery query, CancellationToken cancellationToken = default);
}
