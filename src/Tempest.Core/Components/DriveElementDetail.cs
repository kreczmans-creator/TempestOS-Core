using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Components;

/// <summary>
/// A belt, chain, pulley or sprocket's own published geometry.
/// </summary>
/// <remarks>
/// <b>Published figures only.</b> A5 does not select a belt, compute a
/// centre distance, work out a wrap angle or rate a drive for power. Those
/// depend on service factors, drive layout and duty that A5 does not hold.
/// </remarks>
/// <param name="ProfileDesignation">The tooth or section profile as the source designates it, verbatim. <see langword="null"/> if none was given.</param>
/// <param name="Pitch">The tooth or link pitch. <see langword="null"/> if not recorded.</param>
/// <param name="Width">The belt or chain width. <see langword="null"/> if not recorded.</param>
/// <param name="PitchLength">The belt's own pitch length, or the chain's own overall length. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="NumberOfTeeth">The tooth count of a belt, pulley or sprocket. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="NumberOfLinks">A chain's own link count. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="NumberOfGrooves">A vee pulley's own groove count. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="PitchDiameter">The reference diameter of a pulley or sprocket, as the source published it — never computed here. <see langword="null"/> if the source gave none.</param>
/// <param name="OutsideDiameter">The outside diameter of a pulley or sprocket. <see langword="null"/> if not recorded.</param>
/// <param name="MinimumPulleyDiameter">The smallest pulley the source states the belt may run on. <see langword="null"/> if none was stated.</param>
/// <param name="TensileMemberDesignation">The belt's own tensile-member or cord designation, verbatim. <see langword="null"/> if none was given.</param>
public sealed record DriveElementDetail(
    string? ProfileDesignation = null,
    ReferenceValue<Length>? Pitch = null,
    ReferenceValue<Length>? Width = null,
    ReferenceValue<Length>? PitchLength = null,
    int? NumberOfTeeth = null,
    int? NumberOfLinks = null,
    int? NumberOfGrooves = null,
    ReferenceValue<Length>? PitchDiameter = null,
    ReferenceValue<Length>? OutsideDiameter = null,
    ReferenceValue<Length>? MinimumPulleyDiameter = null,
    string? TensileMemberDesignation = null);
