using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Fasteners;

/// <summary>
/// A fastener's own dimensions, as a source stated them.
/// </summary>
/// <remarks>
/// One record covering every family rather than a type per family: the
/// dimensions genuinely overlap (a nut and a bolt head are both measured
/// across flats), and <see cref="FastenerFamilyTraits"/> already says which
/// are meaningful for which family, so a per-family split would restate
/// that knowledge in a second place. Every field stays
/// <see langword="null"/> where the source supplied nothing; none is ever
/// defaulted to zero.
/// </remarks>
/// <param name="NominalLength">The fastener's own nominal length, measured as the family's own convention measures it. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="HeadDiameter">The head's own outside diameter, or across-corners dimension for a polygonal head. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="HeadHeight">The head's own height. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="WidthAcrossFlats">The spanner size across the flats of a hexagonal or square head or nut. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="WidthAcrossCorners">The dimension across the corners of a polygonal head or nut. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="DriveSize">The size of the driving feature (a socket size, a recess size). <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="Height">The overall height of a nut, or the thickness of a washer or retaining ring. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="InsideDiameter">The inside diameter of a washer, insert or retaining ring. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="OutsideDiameter">The outside diameter of a washer, insert or retaining ring. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="ShankDiameter">The diameter of an unthreaded shank, where the fastener has one. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="GripRange">The material thickness range the fastener is stated to clamp. <see langword="null"/> if the source stated none.</param>
public sealed record FastenerDimensions(
    ReferenceValue<Length>? NominalLength = null,
    ReferenceValue<Length>? HeadDiameter = null,
    ReferenceValue<Length>? HeadHeight = null,
    ReferenceValue<Length>? WidthAcrossFlats = null,
    ReferenceValue<Length>? WidthAcrossCorners = null,
    ReferenceValue<Length>? DriveSize = null,
    ReferenceValue<Length>? Height = null,
    ReferenceValue<Length>? InsideDiameter = null,
    ReferenceValue<Length>? OutsideDiameter = null,
    ReferenceValue<Length>? ShankDiameter = null,
    ReferenceRange<Length>? GripRange = null)
{
    /// <summary>Whether any dimension at all is recorded.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRecorded =>
        NominalLength is not null || HeadDiameter is not null || HeadHeight is not null
        || WidthAcrossFlats is not null || WidthAcrossCorners is not null || DriveSize is not null
        || Height is not null || InsideDiameter is not null || OutsideDiameter is not null
        || ShankDiameter is not null || GripRange is not null;
}
