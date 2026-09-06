namespace Tempest.Core.Components;

/// <summary>
/// The controlled classification of the mechanical components this library
/// holds (A5) — springs, gears, drive elements and standard machine
/// components, in one taxonomy.
/// </summary>
/// <remarks>
/// <para>
/// <b>One taxonomy rather than three libraries.</b> A spring, a gear and a
/// shaft coupling are described by different engineering content, but
/// governed identically: same lifecycle, same provenance, same
/// supersession, same search. Splitting them would triple the
/// infrastructure to express a difference that lives entirely in the
/// engineering detail — which is why the detail is per-family and typed
/// (<see cref="ComponentDefinition.Spring"/>,
/// <see cref="ComponentDefinition.Gear"/>,
/// <see cref="ComponentDefinition.DriveElement"/>) while the record around
/// it is shared.
/// </para>
/// <para>
/// Retaining rings, washers and threaded inserts are deliberately absent:
/// they are fasteners and belong to A3. Rolling bearings are equally
/// absent: they are A4's. One component has one home.
/// </para>
/// </remarks>
public enum ComponentFamily
{
    /// <summary>Not recorded. The honest default — never a claim the component has no family.</summary>
    Unspecified,

    /// <summary>A helical spring loaded in compression.</summary>
    CompressionSpring,

    /// <summary>A helical spring loaded in extension.</summary>
    ExtensionSpring,

    /// <summary>A helical spring loaded in torsion about its own axis.</summary>
    TorsionSpring,

    /// <summary>A conical disc spring (Belleville washer).</summary>
    DiscSpring,

    /// <summary>A spring whose force is substantially independent of deflection.</summary>
    ConstantForceSpring,

    /// <summary>A gas-charged spring or strut.</summary>
    GasSpring,

    /// <summary>A cylindrical gear with teeth parallel to its own axis.</summary>
    SpurGear,

    /// <summary>A cylindrical gear with teeth on a helix.</summary>
    HelicalGear,

    /// <summary>A gear transmitting motion between intersecting axes.</summary>
    BevelGear,

    /// <summary>The screw member of a worm drive.</summary>
    Worm,

    /// <summary>The wheel member of a worm drive.</summary>
    WormWheel,

    /// <summary>A gear with teeth on its own inside diameter.</summary>
    InternalGear,

    /// <summary>A linear gear member meshing with a pinion.</summary>
    GearRack,

    /// <summary>A toothed pulley for a synchronous belt.</summary>
    TimingPulley,

    /// <summary>A synchronous toothed belt.</summary>
    TimingBelt,

    /// <summary>A friction-drive vee belt.</summary>
    VeeBelt,

    /// <summary>A grooved pulley for a vee belt.</summary>
    VeePulley,

    /// <summary>A roller chain.</summary>
    RollerChain,

    /// <summary>A toothed wheel driving a chain.</summary>
    Sprocket,

    /// <summary>A coupling joining two shafts.</summary>
    ShaftCoupling,

    /// <summary>A collar clamped or set to a shaft.</summary>
    ShaftCollar,

    /// <summary>A parallel or woodruff key.</summary>
    ShaftKey,

    /// <summary>A rotary lip seal for a shaft.</summary>
    RadialShaftSeal,

    /// <summary>A plain sliding bearing or bushing.</summary>
    PlainBearing,

    /// <summary>A profiled linear guide rail and carriage.</summary>
    LinearGuide,

    /// <summary>A ball or lead screw.</summary>
    BallScrew,

    /// <summary>A component this taxonomy does not classify. <see cref="ComponentDefinition.SourceClassification"/> must then record the source's own wording.</summary>
    Other
}

/// <summary>The broad group a <see cref="ComponentFamily"/> belongs to.</summary>
/// <remarks>
/// A coarser axis over the same taxonomy, so a caller can ask for "every
/// spring" without enumerating six families and without the library
/// growing a second, drifting list.
/// </remarks>
public enum ComponentGroup
{
    /// <summary>The family is unclassified, so its group is not known.</summary>
    Unspecified,

    /// <summary>An elastic energy-storing element.</summary>
    Spring,

    /// <summary>A toothed element transmitting motion by conjugate action.</summary>
    Gear,

    /// <summary>A belt, chain, pulley or sprocket transmitting motion between shafts.</summary>
    DriveElement,

    /// <summary>An element joining, locating or retaining a shaft.</summary>
    ShaftElement,

    /// <summary>An element carrying or guiding relative motion by sliding or rolling contact.</summary>
    MotionElement,

    /// <summary>An element excluding contamination or retaining lubricant.</summary>
    Sealing,

    /// <summary>A group this classification does not cover.</summary>
    Other
}
