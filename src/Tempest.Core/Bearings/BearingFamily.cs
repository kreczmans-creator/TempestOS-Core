namespace Tempest.Core.Bearings;

/// <summary>
/// The controlled classification of bearing types this library recognises.
/// </summary>
/// <remarks>
/// <para>
/// A closed enum, unlike <see cref="Materials.IMaterialSpecification.Category"/>'s
/// own deliberately-open string: a bearing's own family genuinely does
/// determine which engineering properties are meaningful (see
/// <see cref="BearingFamilyTraits"/>), so an unvalidated free-text family
/// would leave that applicability model with nothing to stand on.
/// </para>
/// <para>
/// <b>Extensible without redesign.</b> Introducing another family is two
/// purely additive edits — a member here and its row in
/// <see cref="BearingFamilyTraits"/> — and nothing else in this namespace
/// switches exhaustively on this enum. <see cref="Other"/> exists so a
/// genuinely unrecognised bearing can still be recorded honestly, paired
/// with <see cref="BearingIdentity.FamilyDesignation"/> for the source's
/// own wording, rather than being forced into the nearest wrong member.
/// </para>
/// </remarks>
public enum BearingFamily
{
    /// <summary>The family is not recorded. The honest default — never a claim that the bearing has no family.</summary>
    Unspecified,

    /// <summary>Radial ball bearing with deep, uninterrupted raceway grooves.</summary>
    DeepGrooveBall,

    /// <summary>Ball bearing whose raceways are displaced relative to each other, giving a nominal contact angle.</summary>
    AngularContactBall,

    /// <summary>Ball bearing with a spherical outer raceway, tolerating shaft-to-housing misalignment.</summary>
    SelfAligningBall,

    /// <summary>Roller bearing with cylindrical rolling elements.</summary>
    CylindricalRoller,

    /// <summary>Roller bearing with tapered rolling elements and a tapered raceway, carrying combined loads.</summary>
    TaperedRoller,

    /// <summary>Roller bearing with barrel-shaped rolling elements on a spherical outer raceway.</summary>
    SphericalRoller,

    /// <summary>Roller bearing with long, small-diameter cylindrical rolling elements.</summary>
    NeedleRoller,

    /// <summary>Ball bearing whose raceways are washers, carrying axial load only.</summary>
    ThrustBall,

    /// <summary>Roller bearing whose raceways are washers, carrying axial load (cylindrical, tapered, spherical and needle thrust variants alike).</summary>
    ThrustRoller,

    /// <summary>A bearing carrying load through a sliding surface rather than rolling elements (bush, sleeve, journal, thrust washer).</summary>
    Plain,

    /// <summary>A recognised bearing that this taxonomy does not yet name. Record the source's own wording in <see cref="BearingIdentity.FamilyDesignation"/>.</summary>
    Other
}
