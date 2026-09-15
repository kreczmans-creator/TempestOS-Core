using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>The inputs to one bolted joint preload and clamp force check.</summary>
/// <param name="FastenerPin">The fastener record the proof strength came from, where one exists; <see langword="null"/> when the grade is named by <paramref name="FastenerGrade"/> alone.</param>
/// <param name="FastenerGrade">The fastener property class the proof strength belongs to, for example "ISO 898-1 class 8.8". Recorded, so the figure is attributable.</param>
/// <param name="Preload">The assembly preload in the bolt.</param>
/// <param name="ExternalLoad">The external tensile load on the joint.</param>
/// <param name="BoltStiffness">The bolt's own axial stiffness.</param>
/// <param name="MemberStiffness">The clamped members' axial stiffness.</param>
/// <param name="TensileStressArea">The thread's tensile stress area.</param>
/// <param name="ProofStrength">The fastener grade's proof strength.</param>
public sealed record BoltedJointPreloadInput(
    ReferencePin? FastenerPin,
    string FastenerGrade,
    Quantity<Force> Preload,
    Quantity<Force> ExternalLoad,
    Quantity<Stiffness> BoltStiffness,
    Quantity<Stiffness> MemberStiffness,
    Quantity<Area> TensileStressArea,
    Quantity<Pressure> ProofStrength);

/// <summary>
/// The result of one bolted joint check. Figures are <see langword="null"/>
/// only when the method was refused, except the two load factors, which are
/// also <see langword="null"/> when unbounded (no external load) or
/// undefined (a separated joint).
/// </summary>
/// <param name="Outcome">What the check found.</param>
/// <param name="RefusalReason">Why the method was refused, or <see langword="null"/>.</param>
/// <param name="PreloadToProofRatio">Preload over proof load, the ratio the refusal is decided on.</param>
/// <param name="JointConstant">The fraction of the external load the bolt sees.</param>
/// <param name="BoltLoad">The bolt's total tensile load.</param>
/// <param name="ClampLoad">The compression left in the members; zero once separated.</param>
/// <param name="SeparationLoad">The external load at which the members separate.</param>
/// <param name="SeparationLoadFactor">Separation load over external load; <see langword="null"/> with no external load.</param>
/// <param name="BoltStress">Bolt load over tensile stress area.</param>
/// <param name="YieldLoadFactor">The multiple of the external load at which the bolt reaches proof stress; <see langword="null"/> with no external load or once separated.</param>
/// <param name="IsSeparated">Whether the external load exceeds the separation load.</param>
public sealed record BoltedJointPreloadResult(
    EngineeringCheckOutcome Outcome,
    string? RefusalReason,
    double PreloadToProofRatio,
    double? JointConstant,
    Quantity<Force>? BoltLoad,
    Quantity<Force>? ClampLoad,
    Quantity<Force>? SeparationLoad,
    double? SeparationLoadFactor,
    Quantity<Pressure>? BoltStress,
    double? YieldLoadFactor,
    bool? IsSeparated);

/// <summary>
/// Bolted joint preload and clamp force under an external tensile load, by
/// the joint-diagram method. Specified in
/// <c>docs/engineering/calculations/calc.bolted-joint-preload.md</c>.
/// </summary>
public sealed class BoltedJointPreloadCalculationDefinition : ICalculationDefinition<BoltedJointPreloadInput, BoltedJointPreloadResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.bolted-joint-preload";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Bolted Joint Preload and Clamp Force",
        Description:
            "Bolt load, residual clamp load, separation load and the load factors against separation and bolt yielding "
            + "for a preloaded joint under an external tensile load (joint-diagram method).",
        Category: "Fasteners",
        Assumptions:
        [
            new CalculationAssumption("The bolt and the clamped members are linear springs in parallel sharing the external load in proportion to stiffness.", "The joint-diagram idealisation."),
            new CalculationAssumption("The external load is tensile and applied at the joint faces.", "A load introduced inside the members changes the joint constant; a compressive load is outside this method."),
            new CalculationAssumption("Once separated, the bolt carries the whole external load.", "The members can no longer share it."),
        ],
        Constraints:
        [
            new CalculationConstraint("Preload, both stiffnesses, tensile stress area and proof strength must be positive; the external load must not be negative."),
            new CalculationConstraint("Preload must be below the proof load (method limit; refused at or above it)."),
            new CalculationConstraint("Fastener grade must be named."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any input that must be positive is zero or negative, the external load is negative, or the fastener grade is blank.</exception>
    public BoltedJointPreloadResult Calculate(BoltedJointPreloadInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var preloadN = input.Preload.BaseValue;
        var externalN = input.ExternalLoad.BaseValue;
        var boltStiffness = input.BoltStiffness.BaseValue;
        var memberStiffness = input.MemberStiffness.BaseValue;
        var areaM2 = input.TensileStressArea.BaseValue;
        var proofPa = input.ProofStrength.BaseValue;

        ModuleGuards.Require(context, "Fastener grade must be named.", !string.IsNullOrWhiteSpace(input.FastenerGrade), $"'{input.FastenerGrade}'");
        ModuleGuards.Require(context, "Preload must be positive.", preloadN > 0, $"{preloadN:0.###} N");
        ModuleGuards.Require(context, "External load must not be negative (compression is outside this method).", externalN >= 0, $"{externalN:0.###} N");
        ModuleGuards.Require(context, "Bolt stiffness must be positive.", boltStiffness > 0, $"{boltStiffness:0.###} N/m");
        ModuleGuards.Require(context, "Member stiffness must be positive.", memberStiffness > 0, $"{memberStiffness:0.###} N/m");
        ModuleGuards.Require(context, "Tensile stress area must be positive.", areaM2 > 0, $"{areaM2:E3} m^2");
        ModuleGuards.Require(context, "Proof strength must be positive.", proofPa > 0, $"{proofPa:0.###} Pa");

        context.RecordIntermediate("Fastener grade", input.FastenerGrade);
        if (input.FastenerPin is { } pin)
            context.RecordIntermediate("Fastener reference", pin.ToString());

        var proofLoadN = proofPa * areaM2;
        var preloadToProof = preloadN / proofLoadN;
        context.RecordIntermediate("Proof load", new Quantity<Force>(proofLoadN, ForceUnits.Newton));
        context.RecordIntermediate("Preload as a fraction of proof load", preloadToProof);

        if (preloadN >= proofLoadN)
        {
            var reason = ModuleGuards.Refuse(
                context,
                "Preload must be below the proof load (method limit; refused at or above it).",
                $"Refused: the preload of {preloadN:0.###} N is {preloadToProof:0.###} times the proof load of {proofLoadN:0.###} N. "
                + "The bolt yields at assembly and the joint diagram no longer applies.");

            return new BoltedJointPreloadResult(EngineeringCheckOutcome.OutsideMethodLimits, reason, preloadToProof, null, null, null, null, null, null, null, null);
        }

        var jointConstant = boltStiffness / (boltStiffness + memberStiffness);
        var separationLoadN = preloadN / (1.0 - jointConstant);
        var clampedMemberLoadN = preloadN - (1.0 - jointConstant) * externalN;
        var isSeparated = clampedMemberLoadN < 0;

        var boltLoadN = isSeparated ? externalN : preloadN + jointConstant * externalN;
        var clampLoadN = isSeparated ? 0.0 : clampedMemberLoadN;
        var boltStressPa = boltLoadN / areaM2;

        double? separationFactor = externalN > 0 ? separationLoadN / externalN : null;
        double? yieldFactor = externalN > 0 && !isSeparated ? (proofLoadN - preloadN) / (jointConstant * externalN) : null;

        var boltStress = new Quantity<Pressure>(boltStressPa, PressureUnits.Pascal).ConvertTo(PressureUnits.Megapascal);
        var stressMet = ModuleGuards.IsMet(boltStressPa / proofPa);

        context.RecordIntermediate("Joint constant", jointConstant);
        context.RecordIntermediate("Bolt load", new Quantity<Force>(boltLoadN, ForceUnits.Newton));
        context.RecordIntermediate("Clamp load", new Quantity<Force>(clampLoadN, ForceUnits.Newton));
        context.RecordIntermediate("Separation load", new Quantity<Force>(separationLoadN, ForceUnits.Newton));
        context.RecordIntermediate("Bolt stress", boltStress);

        context.RecordConstraintCheck(
            "The external load must not separate the joint.",
            !isSeparated,
            $"External load {externalN:0.###} N against separation load {separationLoadN:0.###} N.");
        context.RecordConstraintCheck(
            "Bolt stress must not exceed the proof strength.",
            stressMet,
            $"Bolt stress {boltStress.Value:0.###} MPa against proof strength {input.ProofStrength.ConvertTo(PressureUnits.Megapascal).Value:0.###} MPa.");

        return new BoltedJointPreloadResult(
            ModuleGuards.OutcomeOf(!isSeparated && stressMet),
            null,
            preloadToProof,
            jointConstant,
            new Quantity<Force>(boltLoadN, ForceUnits.Newton),
            new Quantity<Force>(clampLoadN, ForceUnits.Newton),
            new Quantity<Force>(separationLoadN, ForceUnits.Newton),
            separationFactor,
            boltStress,
            yieldFactor,
            isSeparated);
    }
}
