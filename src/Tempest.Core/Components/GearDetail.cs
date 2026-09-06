using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Components;

/// <summary>Which way a helix or a worm thread runs.</summary>
public enum GearHelixHand
{
    /// <summary>Not recorded. Never read as right-hand by default: a mating pair needs opposite hands, and getting it wrong makes the pair unmeshable.</summary>
    Unspecified,

    /// <summary>Right-hand.</summary>
    RightHand,

    /// <summary>Left-hand.</summary>
    LeftHand,

    /// <summary>The family has no helix — a spur gear or a straight bevel.</summary>
    None
}

/// <summary>
/// A gear's own published tooth geometry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Published figures only.</b> A5 does not compute a pitch diameter
/// from module and tooth count, does not derive a centre distance, does
/// not rate a gear for contact or bending stress, and does not check a
/// pair for interference. Those are gear-rating calculations, they depend
/// on load spectra, materials, lubrication and quality that A5 does not
/// hold, and a future calculation capability will consume this as its
/// evidence.
/// </para>
/// <para>
/// <b>Module, not diametral pitch.</b> Module is a length — the platform's
/// Units &amp; Quantities framework holds it directly. Diametral pitch is
/// its inch-series reciprocal and is fully determined by it; a field for
/// each would be two answers to one question, so a source quoting
/// diametral pitch records it in
/// <see cref="ComponentDefinition.SourceClassification"/> or in the
/// designation, verbatim, and the module field stays
/// <see langword="null"/> unless the source itself gave one.
/// </para>
/// </remarks>
/// <param name="NumberOfTeeth">The tooth count. <see langword="null"/> if not recorded, or for a rack, where a length is quoted instead.</param>
/// <param name="Module">The module. <see langword="null"/> if the source quoted none.</param>
/// <param name="PressureAngle">The reference pressure angle. <see langword="null"/> if not recorded.</param>
/// <param name="HelixAngle">The helix angle at the reference cylinder. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="HelixHand">Which way the helix or worm thread runs.</param>
/// <param name="FaceWidth">The width of the toothed face. <see langword="null"/> if not recorded.</param>
/// <param name="PitchDiameter">The reference (pitch) diameter, as the source published it — never computed here. <see langword="null"/> if the source gave none.</param>
/// <param name="OutsideDiameter">The tip diameter. <see langword="null"/> if not recorded.</param>
/// <param name="NumberOfStarts">A worm's own number of thread starts. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="Lead">A worm's own lead, or a rack's own pitch length. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="ProfileShiftCoefficient">The profile shift the source states. <see langword="null"/> if none was stated.</param>
/// <param name="Backlash">The backlash the source states for the gear. <see langword="null"/> if none was stated.</param>
/// <param name="QualityGrade">The accuracy grade designation as the source writes it. <see langword="null"/> if none was given.</param>
/// <param name="ToothHardness">The tooth hardness as the source designates it, verbatim — a scale-specific reading, never a dimensioned quantity. <see langword="null"/> if none was given.</param>
public sealed record GearDetail(
    int? NumberOfTeeth = null,
    ReferenceValue<Length>? Module = null,
    ReferenceValue<PlaneAngle>? PressureAngle = null,
    ReferenceValue<PlaneAngle>? HelixAngle = null,
    GearHelixHand HelixHand = GearHelixHand.Unspecified,
    ReferenceValue<Length>? FaceWidth = null,
    ReferenceValue<Length>? PitchDiameter = null,
    ReferenceValue<Length>? OutsideDiameter = null,
    int? NumberOfStarts = null,
    ReferenceValue<Length>? Lead = null,
    ReferenceValue<Dimensionless>? ProfileShiftCoefficient = null,
    ReferenceValue<Length>? Backlash = null,
    string? QualityGrade = null,
    string? ToothHardness = null);
