namespace Tempest.Core.Components;

/// <summary>
/// Which parts of a component record are meaningful for a given
/// <see cref="ComponentFamily"/> — this library's own type-aware modelling
/// rule, and the single place it is stated.
/// </summary>
/// <remarks>
/// Load-bearing here in a way it is not in the other Group A libraries:
/// A5 holds three different typed detail records under one taxonomy, and
/// this table is what decides which of them a family may carry. A gear
/// detail on a spring is a modelling error, not a data gap, and the
/// distinction is made here rather than in three places.
/// </remarks>
public static class ComponentFamilyTraits
{
    /// <summary>The broad group <paramref name="family"/> belongs to.</summary>
    public static ComponentGroup GroupOf(ComponentFamily family) => family switch
    {
        ComponentFamily.CompressionSpring or ComponentFamily.ExtensionSpring or ComponentFamily.TorsionSpring
            or ComponentFamily.DiscSpring or ComponentFamily.ConstantForceSpring or ComponentFamily.GasSpring
            => ComponentGroup.Spring,

        ComponentFamily.SpurGear or ComponentFamily.HelicalGear or ComponentFamily.BevelGear
            or ComponentFamily.Worm or ComponentFamily.WormWheel or ComponentFamily.InternalGear
            or ComponentFamily.GearRack
            => ComponentGroup.Gear,

        ComponentFamily.TimingPulley or ComponentFamily.TimingBelt or ComponentFamily.VeeBelt
            or ComponentFamily.VeePulley or ComponentFamily.RollerChain or ComponentFamily.Sprocket
            => ComponentGroup.DriveElement,

        ComponentFamily.ShaftCoupling or ComponentFamily.ShaftCollar or ComponentFamily.ShaftKey
            => ComponentGroup.ShaftElement,

        ComponentFamily.PlainBearing or ComponentFamily.LinearGuide or ComponentFamily.BallScrew
            => ComponentGroup.MotionElement,

        ComponentFamily.RadialShaftSeal => ComponentGroup.Sealing,
        ComponentFamily.Other => ComponentGroup.Other,
        _ => ComponentGroup.Unspecified
    };

    /// <summary>Whether a <see cref="SpringDetail"/> is a meaningful part of this family's own description.</summary>
    public static bool HasSpringDetail(ComponentFamily family) => GroupOf(family) == ComponentGroup.Spring;

    /// <summary>Whether a <see cref="GearDetail"/> is a meaningful part of this family's own description.</summary>
    public static bool HasGearDetail(ComponentFamily family) => GroupOf(family) == ComponentGroup.Gear;

    /// <summary>Whether a <see cref="DriveElementDetail"/> is a meaningful part of this family's own description.</summary>
    public static bool HasDriveElementDetail(ComponentFamily family) => GroupOf(family) == ComponentGroup.DriveElement;

    /// <summary>
    /// Whether the family's own spring rate is a torque per unit angle
    /// rather than a force per unit deflection. A torsion spring's rate is
    /// the only one that is, and recording it as a translational stiffness
    /// would be a category error the units framework cannot catch on its
    /// own.
    /// </summary>
    public static bool HasTorsionalRate(ComponentFamily family) => family is ComponentFamily.TorsionSpring;

    /// <summary>Whether a coil count and a wire diameter describe the family. A disc spring, a constant-force spring and a gas spring are all springs without coils.</summary>
    public static bool IsHelicalSpring(ComponentFamily family) =>
        family is ComponentFamily.CompressionSpring or ComponentFamily.ExtensionSpring or ComponentFamily.TorsionSpring;

    /// <summary>Whether the family rotates in service, and so can carry a speed rating.</summary>
    public static bool Rotates(ComponentFamily family) => GroupOf(family) switch
    {
        ComponentGroup.Gear or ComponentGroup.DriveElement => true,
        ComponentGroup.ShaftElement => family is not ComponentFamily.ShaftKey,
        ComponentGroup.Sealing => true,
        ComponentGroup.MotionElement => family is not ComponentFamily.LinearGuide,
        _ => false
    };

    /// <summary>Whether the family transmits torque, and so can carry a torque rating.</summary>
    public static bool TransmitsTorque(ComponentFamily family) => GroupOf(family) switch
    {
        ComponentGroup.Gear or ComponentGroup.DriveElement or ComponentGroup.ShaftElement => true,
        _ => family is ComponentFamily.BallScrew
    };

    /// <summary>Whether a bore fitted to a shaft is a meaningful dimension for the family.</summary>
    public static bool HasBore(ComponentFamily family) => GroupOf(family) switch
    {
        ComponentGroup.Gear => family is not ComponentFamily.GearRack,
        ComponentGroup.DriveElement => family is ComponentFamily.TimingPulley or ComponentFamily.VeePulley or ComponentFamily.Sprocket,
        ComponentGroup.ShaftElement => family is not ComponentFamily.ShaftKey,
        ComponentGroup.Sealing or ComponentGroup.MotionElement => true,
        _ => false
    };

    /// <summary>
    /// Whether this table can speak for <paramref name="family"/> at all.
    /// <see cref="ComponentFamily.Unspecified"/> and
    /// <see cref="ComponentFamily.Other"/> are unclassified by
    /// construction: every answer above is conservative for them and must
    /// be read as "not known to apply", never "known not to apply".
    /// </summary>
    public static bool IsApplicabilityKnown(ComponentFamily family) =>
        family is not (ComponentFamily.Unspecified or ComponentFamily.Other);

    /// <summary>Every family in <paramref name="group"/>, in declaration order.</summary>
    public static IReadOnlyList<ComponentFamily> FamiliesIn(ComponentGroup group) =>
        Enum.GetValues<ComponentFamily>().Where(family => GroupOf(family) == group).ToList();
}
