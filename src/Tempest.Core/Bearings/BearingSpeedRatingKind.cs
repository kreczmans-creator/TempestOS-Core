namespace Tempest.Core.Bearings;

/// <summary>
/// Which speed a <see cref="BearingSpeedRating"/> states.
/// </summary>
/// <remarks>
/// Deliberately not collapsed into a single "maximum RPM" field: a
/// reference speed and a limiting speed are different engineering
/// quantities, derived differently, and a grease-lubricated figure and an
/// oil-lubricated figure for the same bearing are different again. Flattening
/// them would destroy exactly the meaning this library exists to preserve.
/// </remarks>
public enum BearingSpeedRatingKind
{
    /// <summary>The kind is not recorded.</summary>
    Unspecified,

    /// <summary>A thermally-derived reference speed.</summary>
    ReferenceSpeed,

    /// <summary>A mechanically-limited maximum speed.</summary>
    LimitingSpeed,

    /// <summary>A speed rating stated for grease lubrication.</summary>
    GreaseLubricatedSpeed,

    /// <summary>A speed rating stated for oil lubrication.</summary>
    OilLubricatedSpeed,

    /// <summary>A speed limit imposed by the sealing arrangement rather than by the bearing itself.</summary>
    SealLimitedSpeed,

    /// <summary>A manufacturer-specific rating this vocabulary does not name; keep the source's own label in <see cref="BearingSpeedRating.ManufacturerDesignation"/>.</summary>
    ManufacturerSpecified
}
