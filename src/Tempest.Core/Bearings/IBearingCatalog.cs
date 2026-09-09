using Tempest.Core.ReferenceData;

namespace Tempest.Core.Bearings;

/// <summary>
/// The authoritative catalogue of bearing reference data (A4).
/// </summary>
/// <remarks>
/// <para>
/// Register, retrieve, revise, govern, read history and supersede all come
/// from <see cref="IReferenceDataCatalog{TDefinition}"/>, shared with every
/// other Group A library. What is added here is what is genuinely
/// bearing-specific: resolving a record by manufacturer part number, and
/// the bearing query.
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No bearing
/// selection, no life or load calculation, no suitability judgement, and
/// no supplier or price data. A4 supplies the reference evidence those
/// capabilities will consume; the boundaries are documented in
/// `docs/architecture/A4 Bearing Library.md`.
/// </para>
/// </remarks>
public interface IBearingCatalog : IReferenceDataCatalog<BearingDefinition>
{
    /// <summary>
    /// Returns the bearing registered under <paramref name="manufacturer"/>
    /// and <paramref name="partNumber"/> (matched ignoring case and
    /// surrounding whitespace), or <see langword="null"/> if none is.
    /// </summary>
    /// <exception cref="ArgumentException">Either argument is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<BearingDefinition>?> FindByPartNumberAsync(string manufacturer, string partNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered bearing matching <paramref name="query"/>, in the
    /// same order <see cref="IReferenceDataCatalog{TDefinition}.ListAsync"/>
    /// uses. Never <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<BearingDefinition>>> SearchAsync(BearingQuery query, CancellationToken cancellationToken = default);
}
