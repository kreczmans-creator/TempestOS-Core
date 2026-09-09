namespace Tempest.Core.Manufacturing;

/// <summary>
/// Which capabilities and constraints are meaningful for a given
/// <see cref="ProcessFamily"/> — this library's own type-aware modelling
/// rule, and the single place it is stated.
/// </summary>
/// <remarks>
/// The same discipline every other Group A library applies to its own
/// families. Reading applicability from here lets a missing capability be
/// reported as
/// <see cref="ReferenceData.ReferencePropertyAvailability.NotApplicable"/>
/// — a draft angle on a turning operation is not a data gap, there is
/// nothing to record.
/// </remarks>
public static class ProcessFamilyTraits
{
    /// <summary>The broad group <paramref name="family"/> belongs to.</summary>
    public static ProcessGroup GroupOf(ProcessFamily family) => family switch
    {
        ProcessFamily.SandCasting or ProcessFamily.InvestmentCasting or ProcessFamily.DieCasting
            or ProcessFamily.GravityDieCasting or ProcessFamily.CentrifugalCasting
            => ProcessGroup.Casting,

        ProcessFamily.OpenDieForging or ProcessFamily.ClosedDieForging or ProcessFamily.Rolling
            or ProcessFamily.Extrusion or ProcessFamily.Drawing
            => ProcessGroup.BulkForming,

        ProcessFamily.Stamping or ProcessFamily.DeepDrawing or ProcessFamily.Bending or ProcessFamily.Spinning
            => ProcessGroup.SheetForming,

        ProcessFamily.LaserCutting or ProcessFamily.WaterjetCutting or ProcessFamily.PlasmaCutting
            => ProcessGroup.Cutting,

        ProcessFamily.Turning or ProcessFamily.Milling or ProcessFamily.Drilling or ProcessFamily.Grinding
            or ProcessFamily.Broaching or ProcessFamily.ElectricalDischargeMachining
            => ProcessGroup.Machining,

        ProcessFamily.PowderBedFusion or ProcessFamily.MaterialExtrusionAdditive
            or ProcessFamily.DirectedEnergyDeposition or ProcessFamily.VatPhotopolymerisation
            or ProcessFamily.BinderJetting
            => ProcessGroup.Additive,

        ProcessFamily.InjectionMoulding or ProcessFamily.CompressionMoulding
            or ProcessFamily.BlowMoulding or ProcessFamily.Thermoforming
            => ProcessGroup.Moulding,

        ProcessFamily.PressAndSinter or ProcessFamily.MetalInjectionMoulding => ProcessGroup.PowderProcessing,

        ProcessFamily.ArcWelding or ProcessFamily.ResistanceWelding or ProcessFamily.Brazing
            or ProcessFamily.Soldering or ProcessFamily.AdhesiveBonding
            => ProcessGroup.Joining,

        ProcessFamily.Annealing or ProcessFamily.QuenchAndTemper or ProcessFamily.CaseHardening
            or ProcessFamily.SolutionTreatAndAge or ProcessFamily.StressRelieving
            => ProcessGroup.HeatTreatment,

        ProcessFamily.Anodising or ProcessFamily.Electroplating or ProcessFamily.Painting
            or ProcessFamily.ThermalSpraying or ProcessFamily.ShotPeening
            => ProcessGroup.SurfaceTreatment,

        ProcessFamily.Deburring or ProcessFamily.Polishing or ProcessFamily.Honing or ProcessFamily.Lapping
            => ProcessGroup.Finishing,

        ProcessFamily.Other => ProcessGroup.Other,
        _ => ProcessGroup.Unspecified
    };

    /// <summary>
    /// Whether the process gives a part its shape, as opposed to changing
    /// its properties, its surface or its assembly. Only a shaping process
    /// has a geometry capability envelope to record.
    /// </summary>
    public static bool IsShaping(ProcessFamily family) => GroupOf(family) switch
    {
        ProcessGroup.Casting or ProcessGroup.BulkForming or ProcessGroup.SheetForming
            or ProcessGroup.Cutting or ProcessGroup.Machining or ProcessGroup.Additive
            or ProcessGroup.Moulding or ProcessGroup.PowderProcessing => true,
        _ => false
    };

    /// <summary>
    /// Whether the process forms material against a mould or die, and so
    /// has a draft angle to record. A machined or cut part has none.
    /// </summary>
    public static bool UsesAMouldOrDie(ProcessFamily family) => GroupOf(family) switch
    {
        ProcessGroup.Casting or ProcessGroup.Moulding => true,
        ProcessGroup.PowderProcessing => true,
        ProcessGroup.BulkForming => family is ProcessFamily.ClosedDieForging or ProcessFamily.Extrusion or ProcessFamily.Drawing,
        _ => false
    };

    /// <summary>Whether a wall thickness is a meaningful capability of the process.</summary>
    public static bool HasWallThicknessCapability(ProcessFamily family) => GroupOf(family) switch
    {
        ProcessGroup.Casting or ProcessGroup.Moulding or ProcessGroup.SheetForming
            or ProcessGroup.Additive or ProcessGroup.PowderProcessing => true,
        _ => false
    };

    /// <summary>Whether the process leaves a surface whose roughness is a published capability of the process itself.</summary>
    public static bool HasSurfaceRoughnessCapability(ProcessFamily family) => GroupOf(family) switch
    {
        ProcessGroup.HeatTreatment or ProcessGroup.Joining or ProcessGroup.Unspecified => false,
        _ => true
    };

    /// <summary>Whether the process runs at a controlled temperature that is itself a recorded capability.</summary>
    public static bool HasProcessTemperature(ProcessFamily family) => GroupOf(family) switch
    {
        ProcessGroup.HeatTreatment or ProcessGroup.Casting or ProcessGroup.Moulding
            or ProcessGroup.PowderProcessing or ProcessGroup.Joining => true,
        ProcessGroup.Additive => true,
        _ => false
    };

    /// <summary>
    /// Whether the process joins separate parts rather than shaping one —
    /// the distinction that decides whether a part-size envelope describes
    /// the process at all.
    /// </summary>
    public static bool IsJoining(ProcessFamily family) => GroupOf(family) == ProcessGroup.Joining;

    /// <summary>
    /// Whether this table can speak for <paramref name="family"/> at all.
    /// <see cref="ProcessFamily.Unspecified"/> and
    /// <see cref="ProcessFamily.Other"/> are unclassified by construction:
    /// every answer above is conservative for them and must be read as
    /// "not known to apply", never "known not to apply".
    /// </summary>
    public static bool IsApplicabilityKnown(ProcessFamily family) =>
        family is not (ProcessFamily.Unspecified or ProcessFamily.Other);

    /// <summary>Every family in <paramref name="group"/>, in declaration order.</summary>
    public static IReadOnlyList<ProcessFamily> FamiliesIn(ProcessGroup group) =>
        Enum.GetValues<ProcessFamily>().Where(family => GroupOf(family) == group).ToList();
}
