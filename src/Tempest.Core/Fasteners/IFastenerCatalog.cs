using Tempest.Core.ReferenceData;

namespace Tempest.Core.Fasteners;

/// <summary>
/// The authoritative catalogue of fastener reference data (A3).
/// </summary>
/// <remarks>
/// <para>
/// Register, retrieve, revise, govern, read history and supersede all come
/// from <see cref="IReferenceDataCatalog{TDefinition}"/>, shared with every
/// other Group A library. What is added here is fastener-specific:
/// resolving a fastener by its own designation or part number, and the
/// fastener query.
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No joint
/// analysis, no preload or clamp-load calculation, no thread-engagement
/// check, no torque TempestOS worked out, no fastener selection, and no
/// commercial data. A3 supplies the reference evidence those capabilities
/// will consume.
/// </para>
/// </remarks>
public interface IFastenerCatalog : IReferenceDataCatalog<FastenerDefinition>
{
    /// <summary>
    /// Returns the fastener registered under
    /// <paramref name="designation"/> (and, where the record describes a
    /// specific supplier's product, <paramref name="manufacturer"/>), or
    /// <see langword="null"/> if none is.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="designation"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<FastenerDefinition>?> FindByDesignationAsync(
        string designation,
        string? manufacturer = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the fastener registered under
    /// <paramref name="manufacturer"/> and
    /// <paramref name="partNumber"/>, or <see langword="null"/> if none is.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="partNumber"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<FastenerDefinition>?> FindByPartNumberAsync(
        string manufacturer,
        string partNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered fastener matching <paramref name="query"/>, in the
    /// same order <see cref="IReferenceDataCatalog{TDefinition}.ListAsync"/>
    /// uses. Never <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<FastenerDefinition>>> SearchAsync(FastenerQuery query, CancellationToken cancellationToken = default);
}
