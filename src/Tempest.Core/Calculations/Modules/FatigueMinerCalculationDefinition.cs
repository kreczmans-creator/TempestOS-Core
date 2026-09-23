using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>One block of a variable-amplitude loading spectrum.</summary>
/// <param name="StressRange">The stress range of the block.</param>
/// <param name="Cycles">How many cycles the block applies.</param>
public sealed record FatigueLoadBlock(Quantity<Pressure> StressRange, double Cycles);

/// <summary>The inputs to one fatigue damage calculation.</summary>
/// <param name="MaterialPin">The released material record the reference fatigue strength came from, where the curve is a material's; <see langword="null"/> for a detail category.</param>
/// <param name="CurveReference">Where the S-N curve comes from, for example "EN 1993-1-9 detail category 71". Required when no material pin is given.</param>
/// <param name="ReferenceStressRange">The stress range at the reference point of the curve.</param>
/// <param name="ReferenceCycles">The cycles at the reference point of the curve.</param>
/// <param name="Slope">The inverse slope m of the curve.</param>
/// <param name="EnduranceLimit">The constant-amplitude limit below which no damage accrues, or <see langword="null"/> for none.</param>
/// <param name="Blocks">The loading spectrum.</param>
public sealed record FatigueMinerInput(
    ReferencePin? MaterialPin,
    string CurveReference,
    Quantity<Pressure> ReferenceStressRange,
    double ReferenceCycles,
    double Slope,
    Quantity<Pressure>? EnduranceLimit,
    IReadOnlyList<FatigueLoadBlock> Blocks);

/// <summary>The result of one fatigue damage calculation. Figures are <see langword="null"/> only when the method was refused.</summary>
/// <param name="Outcome">What the check found.</param>
/// <param name="RefusalReason">Why the method was refused, or <see langword="null"/>.</param>
/// <param name="BlockLives">Each block's life in cycles, in input order; <see langword="null"/> where unbounded.</param>
/// <param name="BlockDamages">Each block's damage fraction, in input order.</param>
/// <param name="TotalDamage">The Miner sum.</param>
/// <param name="RepetitionsToFailure">One over the total damage; <see langword="null"/> when no damage accrues.</param>
/// <param name="CriterionMet">Whether the total damage is at or below one.</param>
public sealed record FatigueMinerResult(
    EngineeringCheckOutcome Outcome,
    string? RefusalReason,
    IReadOnlyList<double?>? BlockLives,
    IReadOnlyList<double>? BlockDamages,
    double? TotalDamage,
    double? RepetitionsToFailure,
    bool? CriterionMet);

/// <summary>
/// Fatigue under variable amplitude: a single-slope S-N curve with an
/// optional endurance limit and the Palmgren-Miner damage sum. Specified in
/// <c>docs/engineering/calculations/calc.fatigue-miner.md</c>.
/// </summary>
public sealed class FatigueMinerCalculationDefinition : ICalculationDefinition<FatigueMinerInput, FatigueMinerResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.fatigue-miner";

    /// <summary>The block life, in cycles, below which the stress-life curve is refused as low-cycle.</summary>
    public const double MinimumBlockLifeCycles = 1e4;

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Fatigue Damage (S-N Curve with Miner's Rule)",
        Description:
            "Life of each block of a stress-range spectrum from a single-slope S-N curve with an optional endurance limit, "
            + "and the Palmgren-Miner damage sum across the spectrum.",
        Category: "Fatigue",
        Assumptions:
        [
            new CalculationAssumption("The S-N curve is a single straight line in log-log space through the reference point, with the given inverse slope.", "The Basquin form; a second slope is not modelled."),
            new CalculationAssumption("Damage accumulates linearly and independently of the order of the blocks.", "The Palmgren-Miner hypothesis."),
            new CalculationAssumption("A stress range at or below the endurance limit causes no damage.", "The constant-amplitude fatigue limit as a cut-off."),
        ],
        Constraints:
        [
            new CalculationConstraint("Reference stress range, reference cycles and slope must be positive; an endurance limit, when given, must be positive; at least one block, each with a positive range and non-negative cycles; a material pin or a curve reference must be given."),
            new CalculationConstraint("Every block's life must be at least 10,000 cycles (method limit; refused in the low-cycle regime)."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any constraint above, other than the method limit, is not met.</exception>
    public FatigueMinerResult Calculate(FatigueMinerInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var referencePa = input.ReferenceStressRange.BaseValue;
        var limitPa = input.EnduranceLimit?.BaseValue;
        var blocks = input.Blocks ?? [];

        ModuleGuards.Require(context, "A material pin or a curve reference must name where the S-N curve comes from.", input.MaterialPin is not null || !string.IsNullOrWhiteSpace(input.CurveReference), "neither");
        ModuleGuards.Require(context, "Reference stress range must be positive.", referencePa > 0, input, nameof(input.ReferenceStressRange));
        ModuleGuards.Require(context, "Reference cycles must be positive.", input.ReferenceCycles > 0, input, nameof(input.ReferenceCycles));
        ModuleGuards.Require(context, "Slope must be positive.", input.Slope > 0, input, nameof(input.Slope));
        ModuleGuards.Require(context, "Endurance limit must be positive when given.", limitPa is null || limitPa > 0, input, nameof(input.EnduranceLimit));
        ModuleGuards.Require(context, "At least one load block is required.", blocks.Count >= 1, $"{blocks.Count} block(s)");

        for (var i = 0; i < blocks.Count; i++)
        {
            ModuleGuards.Require(context, "Every block's stress range must be positive.", blocks[i].StressRange.BaseValue > 0, $"block {i}: {blocks[i].StressRange}");
            ModuleGuards.Require(context, "Every block's cycle count must not be negative.", blocks[i].Cycles >= 0, $"block {i}: {blocks[i].Cycles} cycles");
        }

        if (input.MaterialPin is { } pin)
        {
            context.ReferenceMaterial(pin.RecordId);
            context.RecordIntermediate("Material reference", pin.ToString());
        }

        if (!string.IsNullOrWhiteSpace(input.CurveReference))
            context.RecordIntermediate("S-N curve reference", input.CurveReference);

        var lives = new double?[blocks.Count];
        var damages = new double[blocks.Count];
        var total = 0.0;

        for (var i = 0; i < blocks.Count; i++)
        {
            var rangePa = blocks[i].StressRange.BaseValue;

            if (limitPa is { } limit && rangePa <= limit)
            {
                lives[i] = null;
                damages[i] = 0.0;
                continue;
            }

            var life = input.ReferenceCycles * Math.Pow(referencePa / rangePa, input.Slope);
            lives[i] = life;
            damages[i] = blocks[i].Cycles / life;
            total += damages[i];
        }

        context.RecordIntermediate("Block lives (cycles)", lives);
        context.RecordIntermediate("Block damages", damages);

        for (var i = 0; i < blocks.Count; i++)
        {
            if (lives[i] is { } life && life < MinimumBlockLifeCycles)
            {
                var reason = ModuleGuards.Refuse(
                    context,
                    "Every block's life must be at least 10,000 cycles (method limit; refused in the low-cycle regime).",
                    $"Refused: block {i} ({blocks[i].StressRange}) has a predicted life of {life:0.###} cycles, in the low-cycle regime below "
                    + $"{MinimumBlockLifeCycles:0} where a stress-life curve is not valid; a strain-life method is needed.");

                return new FatigueMinerResult(EngineeringCheckOutcome.OutsideMethodLimits, reason, null, null, null, null, null);
            }
        }

        var met = ModuleGuards.IsMet(total);
        double? repetitions = total > 0 ? 1.0 / total : null;

        context.RecordIntermediate("Total damage", total);
        context.RecordConstraintCheck(
            "The Miner damage sum must not exceed one.",
            met,
            $"Total damage {total:0.#####}; repetitions of the spectrum to failure {(repetitions is { } r ? r.ToString("0.###") : "unbounded")}.");

        return new FatigueMinerResult(ModuleGuards.OutcomeOf(met), null, lives, damages, total, repetitions, met);
    }
}
