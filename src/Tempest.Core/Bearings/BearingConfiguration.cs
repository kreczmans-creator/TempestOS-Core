using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Bearings;

/// <summary>
/// A bearing's own configuration: sealing, internal clearance, precision,
/// row arrangement and contact angle.
/// </summary>
/// <remarks>
/// Clearance, preload and precision classes are held as the source's own
/// designation plus the standard that designation belongs to, rather than
/// as an enum: <c>C3</c> means something only against the standard that
/// defines it, and there is no manufacturer-neutral numeric scale these
/// classes could be normalised onto without inventing one. Recording the
/// designation and its governing standard keeps them traceable and
/// comparable without pretending a universal scale exists.
/// </remarks>
/// <param name="Sealing">The sealing or shielding arrangement. <see langword="null"/> if not recorded.</param>
/// <param name="InternalClearanceClass">The internal clearance class designation as the source writes it (e.g. <c>"C3"</c>). <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="InternalClearanceStandard">The standard <paramref name="InternalClearanceClass"/> is defined by. <see langword="null"/> if the source did not state one.</param>
/// <param name="RadialInternalClearanceMinimum">The minimum radial internal clearance, where the source gives a figure rather than only a class. <see langword="null"/> if not recorded.</param>
/// <param name="RadialInternalClearanceMaximum">The maximum radial internal clearance. <see langword="null"/> if not recorded.</param>
/// <param name="PreloadClass">The preload class designation as the source writes it. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="PrecisionClass">The precision/tolerance class designation as the source writes it (e.g. <c>"P5"</c>, <c>"ABEC 7"</c>). <see langword="null"/> if not recorded.</param>
/// <param name="PrecisionStandard">The standard <paramref name="PrecisionClass"/> is defined by. <see langword="null"/> if the source did not state one.</param>
/// <param name="Rows">The row configuration.</param>
/// <param name="ContactAngle">The nominal contact angle. <see langword="null"/> if not recorded, or not applicable to the family (see <see cref="BearingFamilyTraits.HasContactAngle"/>).</param>
/// <param name="ArrangementDesignation">The bearing-arrangement or set designation as the source writes it (e.g. a matched-pair code). <see langword="null"/> if none was given.</param>
public sealed record BearingConfiguration(
    BearingSealingArrangement? Sealing = null,
    string? InternalClearanceClass = null,
    string? InternalClearanceStandard = null,
    Quantity<Length>? RadialInternalClearanceMinimum = null,
    Quantity<Length>? RadialInternalClearanceMaximum = null,
    string? PreloadClass = null,
    string? PrecisionClass = null,
    string? PrecisionStandard = null,
    BearingRowConfiguration Rows = BearingRowConfiguration.Unspecified,
    Quantity<PlaneAngle>? ContactAngle = null,
    string? ArrangementDesignation = null);
