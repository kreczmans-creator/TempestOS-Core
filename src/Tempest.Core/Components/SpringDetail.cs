using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Components;

/// <summary>How a helical spring's own ends are formed.</summary>
public enum SpringEndType
{
    /// <summary>Not recorded.</summary>
    Unspecified,

    /// <summary>Open ends, not ground.</summary>
    Open,

    /// <summary>Open ends, ground square.</summary>
    OpenGround,

    /// <summary>Closed ends, not ground.</summary>
    Closed,

    /// <summary>Closed ends, ground square.</summary>
    ClosedGround,

    /// <summary>An extension spring's own loop or hook end.</summary>
    LoopOrHook,

    /// <summary>A torsion spring's own straight or formed leg.</summary>
    Leg,

    /// <summary>An end form this taxonomy does not classify.</summary>
    Other
}

/// <summary>Which way a helical spring is wound.</summary>
public enum SpringWindingDirection
{
    /// <summary>Not recorded. Never read as right-hand by default — a spring wound the wrong way for its application unwinds under load.</summary>
    Unspecified,

    /// <summary>Right-hand wound.</summary>
    RightHand,

    /// <summary>Left-hand wound.</summary>
    LeftHand
}

/// <summary>
/// A spring's own published geometry and rate.
/// </summary>
/// <remarks>
/// <para>
/// <b>Published figures only.</b> A5 does not compute a rate from wire
/// diameter, coil diameter and active coils, does not derive a solid
/// length from a coil count, and does not calculate a stress at any
/// deflection. Every value here is what a source stated, and a spring
/// designer's own calculations are a future capability that will consume
/// this rather than live in it.
/// </para>
/// <para>
/// <see cref="Rate"/> and <see cref="TorsionalRate"/> are separate,
/// separately typed fields rather than one field of ambiguous meaning: a
/// torsion spring's rate is a torque per unit angle and a compression
/// spring's is a force per unit deflection, and
/// <see cref="ComponentFamilyTraits.HasTorsionalRate"/> says which a
/// family may carry.
/// </para>
/// </remarks>
/// <param name="Rate">The published rate as force per unit deflection. <see langword="null"/> where the family's rate is torsional, or where none is recorded.</param>
/// <param name="TorsionalRate">The published rate as torque per unit angle. <see langword="null"/> where the family's rate is translational, or where none is recorded.</param>
/// <param name="FreeLength">The length or angular position at rest. <see langword="null"/> if not recorded.</param>
/// <param name="SolidLength">The length at which the coils close. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="OutsideDiameter">The coil's own outside diameter. <see langword="null"/> if not recorded.</param>
/// <param name="InsideDiameter">The coil's own inside diameter. <see langword="null"/> if not recorded.</param>
/// <param name="WireDiameter">The wire or strip thickness. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="TotalCoils">The total number of coils. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="ActiveCoils">The number of coils contributing to the rate. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="MaximumDeflection">The greatest deflection the source states the spring for. <see langword="null"/> if none was stated.</param>
/// <param name="MaximumLoad">The greatest force the source states the spring for. <see langword="null"/> if none was stated.</param>
/// <param name="MaximumTorque">The greatest torque the source states a torsion spring for. <see langword="null"/> if none was stated.</param>
/// <param name="InitialTension">An extension spring's own initial tension. <see langword="null"/> if not recorded or not applicable.</param>
/// <param name="EndType">How the ends are formed.</param>
/// <param name="WindingDirection">Which way the spring is wound.</param>
public sealed record SpringDetail(
    ReferenceValue<Stiffness>? Rate = null,
    ReferenceValue<TorsionalStiffness>? TorsionalRate = null,
    ReferenceValue<Length>? FreeLength = null,
    ReferenceValue<Length>? SolidLength = null,
    ReferenceValue<Length>? OutsideDiameter = null,
    ReferenceValue<Length>? InsideDiameter = null,
    ReferenceValue<Length>? WireDiameter = null,
    ReferenceValue<Dimensionless>? TotalCoils = null,
    ReferenceValue<Dimensionless>? ActiveCoils = null,
    ReferenceValue<Length>? MaximumDeflection = null,
    ReferenceValue<Force>? MaximumLoad = null,
    ReferenceValue<Torque>? MaximumTorque = null,
    ReferenceValue<Force>? InitialTension = null,
    SpringEndType EndType = SpringEndType.Unspecified,
    SpringWindingDirection WindingDirection = SpringWindingDirection.Unspecified);
