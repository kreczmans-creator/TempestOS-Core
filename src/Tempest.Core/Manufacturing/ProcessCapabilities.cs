using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Manufacturing;

/// <summary>
/// What a source published that a process can achieve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every capability is a range, and every range carries its own
/// origin.</b> Process capability is published as a band, not a point —
/// "this process holds tolerances between these limits" — and recording
/// only a midpoint would invent a figure nobody published while losing the
/// fact that the two ends belong to one thing.
/// <see cref="ReferenceRange{TDimension}"/> gives both, plus the
/// <see cref="ReferenceRange{TDimension}.Conditions"/> a capability is
/// meaningless without: an achievable tolerance depends on the feature,
/// the material and the equipment, and a band separated from those is a
/// number rather than reference data.
/// </para>
/// <para>
/// <b>A published capability is not a promise.</b> Recording that a source
/// says a process reaches a tolerance says what the source said. It does
/// not say a particular supplier will reach it, that a particular feature
/// can be made that way, or that the process should be chosen. Process
/// planning and supplier capability are not A7's, and never become so by
/// accumulating enough of these bands.
/// </para>
/// <para>
/// An open end is genuinely open, and an absent field means nobody
/// recorded the capability — never that the process has none.
/// </para>
/// </remarks>
/// <param name="AchievableTolerance">The dimensional tolerance band the source states the process holds. <see langword="null"/> if none was stated.</param>
/// <param name="SurfaceRoughness">The surface roughness band the source states the process leaves. <see langword="null"/> if none was stated, or the process leaves no surface of its own.</param>
/// <param name="WallThickness">The wall thickness band the source states the process can produce. <see langword="null"/> if none was stated or the process has no wall thickness.</param>
/// <param name="PartSize">The band of largest part dimension the source states the process handles. <see langword="null"/> if none was stated.</param>
/// <param name="PartMass">The band of part mass the source states the process handles. <see langword="null"/> if none was stated.</param>
/// <param name="MinimumFeatureSize">The smallest feature the source states the process resolves. <see langword="null"/> if none was stated.</param>
/// <param name="DraftAngle">The draft angle band the source states a moulded or die-formed part needs. <see langword="null"/> if none was stated or the process uses no mould or die.</param>
/// <param name="CornerRadius">The internal corner radius band the source states. <see langword="null"/> if none was stated.</param>
/// <param name="HoleDiameter">The band of hole diameter the source states the process produces. <see langword="null"/> if none was stated.</param>
/// <param name="AspectRatio">The band of feature aspect ratio the source states the process reaches. <see langword="null"/> if none was stated.</param>
/// <param name="ProcessTemperature">The temperature band the source states the process runs at. <see langword="null"/> if none was stated or the process has no controlled temperature.</param>
/// <param name="CycleTime">The cycle time band the source states. <see langword="null"/> if none was stated.</param>
public sealed record ProcessCapabilities(
    ReferenceRange<Length>? AchievableTolerance = null,
    ReferenceRange<Length>? SurfaceRoughness = null,
    ReferenceRange<Length>? WallThickness = null,
    ReferenceRange<Length>? PartSize = null,
    ReferenceRange<Mass>? PartMass = null,
    ReferenceRange<Length>? MinimumFeatureSize = null,
    ReferenceRange<PlaneAngle>? DraftAngle = null,
    ReferenceRange<Length>? CornerRadius = null,
    ReferenceRange<Length>? HoleDiameter = null,
    ReferenceRange<Dimensionless>? AspectRatio = null,
    ReferenceRange<Temperature>? ProcessTemperature = null,
    ReferenceRange<Duration>? CycleTime = null)
{
    /// <summary>Whether any capability at all is recorded.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRecorded =>
        AchievableTolerance is not null || SurfaceRoughness is not null || WallThickness is not null
        || PartSize is not null || PartMass is not null || MinimumFeatureSize is not null
        || DraftAngle is not null || CornerRadius is not null || HoleDiameter is not null
        || AspectRatio is not null || ProcessTemperature is not null || CycleTime is not null;
}
