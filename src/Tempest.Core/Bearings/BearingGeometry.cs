using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Bearings;

/// <summary>
/// A bearing's own dimensional data, as dimensioned
/// <see cref="Quantity{TDimension}"/> values — never strings, and never
/// bare numbers with an implied unit.
/// </summary>
/// <remarks>
/// <para>
/// Values are stored in the unit the source itself quoted (bearing
/// catalogues quote millimetres; a source quoting inches is kept in
/// inches), so a record stays a faithful transcription of its source.
/// Ordering and range comparison convert to the dimension's own base unit
/// at the point of comparison — see <see cref="BearingQuery"/> — and
/// display conversion happens at the presentation boundary. No unit
/// arithmetic of any kind is reimplemented here; all of it is
/// <see cref="Quantity{TDimension}.ConvertTo"/>'s job.
/// </para>
/// <para>
/// <see cref="Bore"/>, <see cref="OutsideDiameter"/> and
/// <see cref="Width"/> are the three dimensions essentially every bearing
/// family states, so they are named. Everything else a particular family
/// or source needs — cone and cup widths, flange and shoulder diameters,
/// roller and raceway dimensions — goes in <see cref="AdditionalDimensions"/>
/// keyed by the source's own symbol, rather than this record inventing a
/// fixed superset of every family's own drawing.
/// </para>
/// </remarks>
/// <param name="Bore">Bore (inner) diameter, conventionally <c>d</c>. <see langword="null"/> if not recorded.</param>
/// <param name="OutsideDiameter">Outside diameter, conventionally <c>D</c>. <see langword="null"/> if not recorded.</param>
/// <param name="Width">Nominal width or height, conventionally <c>B</c> (or <c>H</c> for a thrust bearing). <see langword="null"/> if not recorded.</param>
/// <param name="OverallWidth">Overall assembled width where it differs from <paramref name="Width"/> — a tapered roller bearing's own <c>T</c>, for instance. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="ChamferMinimum">Minimum chamfer dimension, conventionally <c>r min</c>. <see langword="null"/> if not recorded.</param>
/// <param name="AdditionalDimensions">
/// Every further dimension the source stated, keyed by the source's own
/// symbol (e.g. <c>"da min"</c>, <c>"Da max"</c>, <c>"C"</c>). Never
/// <see langword="null"/>; empty if none.
/// </param>
public sealed record BearingGeometry(
    Quantity<Length>? Bore = null,
    Quantity<Length>? OutsideDiameter = null,
    Quantity<Length>? Width = null,
    Quantity<Length>? OverallWidth = null,
    Quantity<Length>? ChamferMinimum = null,
    IReadOnlyDictionary<string, Quantity<Length>>? AdditionalDimensions = null)
{
    /// <summary>Every further dimension the source stated, keyed by the source's own symbol. Never <see langword="null"/>.</summary>
    public IReadOnlyDictionary<string, Quantity<Length>> AdditionalDimensions { get; init; } =
        AdditionalDimensions ?? new Dictionary<string, Quantity<Length>>();

    /// <summary>
    /// Returns <paramref name="length"/> in metres (this dimension's own
    /// base unit), or <see langword="null"/> if it was not recorded — the
    /// one conversion helper this record offers, so dimensional rules and
    /// range queries can compare a millimetre bore against an inch one
    /// without each reimplementing the conversion.
    /// </summary>
    public static double? ToMetres(Quantity<Length>? length) =>
        length is null ? null : length.Value.Value * length.Value.Unit.ToBaseUnitFactor;
}
