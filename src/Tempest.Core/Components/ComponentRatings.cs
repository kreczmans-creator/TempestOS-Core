using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Components;

/// <summary>
/// The limits a source published for a component.
/// </summary>
/// <remarks>
/// <para>
/// <b>A published limit is not a permission.</b> Recording that a source
/// rated a coupling to a torque says what the source said; it does not say
/// the coupling is suitable for any particular drive, which depends on
/// service factors, duty, shock, alignment and temperature that A5 does
/// not hold. Nothing here is a recommendation.
/// </para>
/// <para>
/// A rating is only meaningful with the conditions it was published under,
/// which is why each is a
/// <see cref="ReferenceValue{TDimension}"/> carrying its own
/// <see cref="ReferenceValue{TDimension}.Conditions"/> rather than a bare
/// quantity.
/// </para>
/// </remarks>
/// <param name="MaximumSpeed">The greatest rotational speed the source states. <see langword="null"/> if none was stated or the family does not rotate.</param>
/// <param name="RatedTorque">The torque the source rates the component at. <see langword="null"/> if none was stated.</param>
/// <param name="MaximumTorque">The greatest torque the source states, where it distinguishes a peak from a rating. <see langword="null"/> if none was stated.</param>
/// <param name="RatedPower">The power the source rates the component at. <see langword="null"/> if none was stated.</param>
/// <param name="MaximumAxialLoad">The greatest axial force the source states. <see langword="null"/> if none was stated.</param>
/// <param name="MaximumRadialLoad">The greatest radial force the source states. <see langword="null"/> if none was stated.</param>
/// <param name="OperatingTemperatureRange">The temperature range the source states the component for. <see langword="null"/> if none was stated.</param>
public sealed record ComponentRatings(
    ReferenceValue<RotationalSpeed>? MaximumSpeed = null,
    ReferenceValue<Torque>? RatedTorque = null,
    ReferenceValue<Torque>? MaximumTorque = null,
    ReferenceValue<Power>? RatedPower = null,
    ReferenceValue<Force>? MaximumAxialLoad = null,
    ReferenceValue<Force>? MaximumRadialLoad = null,
    ReferenceRange<Temperature>? OperatingTemperatureRange = null)
{
    /// <summary>Whether any rating at all is recorded.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRecorded =>
        MaximumSpeed is not null || RatedTorque is not null || MaximumTorque is not null
        || RatedPower is not null || MaximumAxialLoad is not null || MaximumRadialLoad is not null
        || OperatingTemperatureRange is not null;
}
