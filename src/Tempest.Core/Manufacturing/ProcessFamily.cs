namespace Tempest.Core.Manufacturing;

/// <summary>The broad group a <see cref="ProcessFamily"/> belongs to.</summary>
/// <remarks>
/// A coarser axis over the same taxonomy, so a caller can ask for "every
/// casting process" without enumerating each one and without the library
/// growing a second, drifting list.
/// </remarks>
public enum ProcessGroup
{
    /// <summary>The family is unclassified, so its group is not known.</summary>
    Unspecified,

    /// <summary>Shaping by solidifying a liquid in a mould.</summary>
    Casting,

    /// <summary>Shaping solid stock by plastic deformation in bulk.</summary>
    BulkForming,

    /// <summary>Shaping sheet stock by plastic deformation.</summary>
    SheetForming,

    /// <summary>Shaping by separating material without a chip-forming tool.</summary>
    Cutting,

    /// <summary>Shaping by removing material as chips.</summary>
    Machining,

    /// <summary>Building a part by adding material.</summary>
    Additive,

    /// <summary>Shaping polymer or composite stock in a mould or die.</summary>
    Moulding,

    /// <summary>Consolidating powder into a solid part.</summary>
    PowderProcessing,

    /// <summary>Joining separate parts into an assembly.</summary>
    Joining,

    /// <summary>Changing bulk properties by controlled heating and cooling.</summary>
    HeatTreatment,

    /// <summary>Changing surface properties or adding a surface layer.</summary>
    SurfaceTreatment,

    /// <summary>Improving surface condition or removing unwanted material after shaping.</summary>
    Finishing,

    /// <summary>A group this classification does not cover.</summary>
    Other
}

/// <summary>The controlled classification of manufacturing processes this library recognises (A7).</summary>
/// <remarks>
/// <para>
/// A closed enum, because a process family determines which capabilities
/// and constraints are meaningful — a casting process has a draft angle
/// and a machining process does not — so an unvalidated free-text family
/// would leave <see cref="ProcessFamilyTraits"/> with nothing to stand on.
/// </para>
/// <para>
/// The source's own wording is never lost:
/// <see cref="ProcessDefinition.SourceClassification"/> keeps it verbatim,
/// and a process this taxonomy does not name is recorded as
/// <see cref="Other"/> with that wording rather than forced into the
/// nearest family it is not.
/// </para>
/// </remarks>
public enum ProcessFamily
{
    /// <summary>Not recorded. The honest default — never a claim the process has no family.</summary>
    Unspecified,

    /// <summary>Casting into an expendable sand mould.</summary>
    SandCasting,

    /// <summary>Casting into a ceramic shell formed around an expendable pattern.</summary>
    InvestmentCasting,

    /// <summary>Casting by injecting molten metal into a permanent die under pressure.</summary>
    DieCasting,

    /// <summary>Casting into a permanent mould under gravity.</summary>
    GravityDieCasting,

    /// <summary>Casting in a rotating mould.</summary>
    CentrifugalCasting,

    /// <summary>Forging between flat or simply contoured dies.</summary>
    OpenDieForging,

    /// <summary>Forging in dies that enclose the shape.</summary>
    ClosedDieForging,

    /// <summary>Reducing section by passing stock between rolls.</summary>
    Rolling,

    /// <summary>Forming a continuous profile by forcing stock through a die.</summary>
    Extrusion,

    /// <summary>Reducing section by pulling stock through a die.</summary>
    Drawing,

    /// <summary>Cutting and forming sheet between a punch and a die.</summary>
    Stamping,

    /// <summary>Drawing sheet into a deep hollow form.</summary>
    DeepDrawing,

    /// <summary>Forming sheet by bending about a straight axis.</summary>
    Bending,

    /// <summary>Forming sheet over a rotating mandrel.</summary>
    Spinning,

    /// <summary>Separating material with a focused laser beam.</summary>
    LaserCutting,

    /// <summary>Separating material with a high-pressure abrasive water jet.</summary>
    WaterjetCutting,

    /// <summary>Separating material with a plasma arc.</summary>
    PlasmaCutting,

    /// <summary>Machining a rotating workpiece with a single-point tool.</summary>
    Turning,

    /// <summary>Machining with a rotating multi-point cutter.</summary>
    Milling,

    /// <summary>Producing holes with a rotating drill.</summary>
    Drilling,

    /// <summary>Machining with a bonded abrasive wheel.</summary>
    Grinding,

    /// <summary>Machining with a progressively toothed tool in one pass.</summary>
    Broaching,

    /// <summary>Removing material by controlled electrical discharge.</summary>
    ElectricalDischargeMachining,

    /// <summary>Building a part by fusing powder in a bed.</summary>
    PowderBedFusion,

    /// <summary>Building a part by depositing extruded material.</summary>
    MaterialExtrusionAdditive,

    /// <summary>Building a part by depositing and fusing material at a moving energy source.</summary>
    DirectedEnergyDeposition,

    /// <summary>Building a part by curing photopolymer in a vat.</summary>
    VatPhotopolymerisation,

    /// <summary>Building a part by binding powder, then sintering it.</summary>
    BinderJetting,

    /// <summary>Moulding by injecting molten polymer into a closed mould.</summary>
    InjectionMoulding,

    /// <summary>Moulding by compressing charge in a heated mould.</summary>
    CompressionMoulding,

    /// <summary>Moulding a hollow part by inflating a parison in a mould.</summary>
    BlowMoulding,

    /// <summary>Forming heated sheet over a mould.</summary>
    Thermoforming,

    /// <summary>Consolidating powder by pressing and sintering.</summary>
    PressAndSinter,

    /// <summary>Moulding a powder-binder feedstock, then debinding and sintering it.</summary>
    MetalInjectionMoulding,

    /// <summary>Joining by fusion under an electric arc.</summary>
    ArcWelding,

    /// <summary>Joining by fusion under resistance heating and pressure.</summary>
    ResistanceWelding,

    /// <summary>Joining by melting a filler above 450 degrees Celsius without melting the parent.</summary>
    Brazing,

    /// <summary>Joining by melting a filler below 450 degrees Celsius without melting the parent.</summary>
    Soldering,

    /// <summary>Joining with a structural adhesive.</summary>
    AdhesiveBonding,

    /// <summary>Softening and relieving by controlled heating and slow cooling.</summary>
    Annealing,

    /// <summary>Hardening by quenching, then tempering to the required condition.</summary>
    QuenchAndTemper,

    /// <summary>Hardening a surface layer while leaving the core tough.</summary>
    CaseHardening,

    /// <summary>Dissolving and retaining a phase by solution treatment and ageing.</summary>
    SolutionTreatAndAge,

    /// <summary>Relieving residual stress by controlled heating.</summary>
    StressRelieving,

    /// <summary>Growing a controlled oxide layer electrolytically.</summary>
    Anodising,

    /// <summary>Depositing a metal layer electrolytically.</summary>
    Electroplating,

    /// <summary>Applying an organic coating.</summary>
    Painting,

    /// <summary>Depositing a coating from a heated particle stream.</summary>
    ThermalSpraying,

    /// <summary>Inducing compressive surface stress by controlled impact.</summary>
    ShotPeening,

    /// <summary>Removing burrs and sharp edges left by an earlier operation.</summary>
    Deburring,

    /// <summary>Improving surface finish with a fine abrasive.</summary>
    Polishing,

    /// <summary>Finishing a bore with a bonded abrasive stone.</summary>
    Honing,

    /// <summary>Finishing a flat or formed surface with a loose abrasive.</summary>
    Lapping,

    /// <summary>A process this taxonomy does not classify. <see cref="ProcessDefinition.SourceClassification"/> must then record the source's own wording.</summary>
    Other
}
