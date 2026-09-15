using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>The rolling element type, which selects the life exponent.</summary>
public enum RollingBearingType
{
    /// <summary>Ball bearing: life exponent 3.</summary>
    Ball,

    /// <summary>Roller bearing: life exponent 10/3.</summary>
    Roller,
}

/// <summary>The inputs to one bearing rating life calculation.</summary>
/// <param name="BearingPin">The bearing record the dynamic load rating came from, where one exists.</param>
/// <param name="BearingDesignation">The bearing the rating belongs to, for example "6208". Recorded, so the figure is attributable.</param>
/// <param name="BearingType">Ball or roller.</param>
/// <param name="BasicDynamicLoadRating">The basic dynamic load rating C.</param>
/// <param name="RadialLoad">The radial load.</param>
/// <param name="AxialLoad">The axial load.</param>
/// <param name="RadialFactor">The catalogue's radial factor X.</param>
/// <param name="AxialFactor">The catalogue's axial factor Y.</param>
/// <param name="Speed">The running speed.</param>
/// <param name="ReliabilityFactor">ISO 281's reliability factor a1: 1 at 90 %, 0.64 at 95 %, 0.55 at 96 %, 0.47 at 97 %, 0.37 at 98 %, 0.25 at 99 %.</param>
/// <param name="RequiredLife">The life the modified life is compared against; zero for no criterion.</param>
public sealed record BearingRatingLifeInput(
    ReferencePin? BearingPin,
    string BearingDesignation,
    RollingBearingType BearingType,
    Quantity<Force> BasicDynamicLoadRating,
    Quantity<Force> RadialLoad,
    Quantity<Force> AxialLoad,
    double RadialFactor,
    double AxialFactor,
    Quantity<RotationalSpeed> Speed,
    double ReliabilityFactor,
    Quantity<Duration> RequiredLife);

/// <summary>The result of one bearing rating life calculation. The equivalent load and load ratio are always reported; every other figure is <see langword="null"/> only when the method was refused.</summary>
/// <param name="Outcome">What the calculation found.</param>
/// <param name="RefusalReason">Why the method was refused, or <see langword="null"/>.</param>
/// <param name="EquivalentDynamicLoad">X times radial plus Y times axial.</param>
/// <param name="LoadRatio">Equivalent load over the dynamic load rating, the ratio the refusal is decided on.</param>
/// <param name="BasicRatingLifeMillionRevolutions">L10 in millions of revolutions.</param>
/// <param name="BasicRatingLife">L10 at the running speed.</param>
/// <param name="ModifiedRatingLifeMillionRevolutions">a1 times L10, in millions of revolutions.</param>
/// <param name="ModifiedRatingLife">a1 times L10 at the running speed.</param>
/// <param name="CriterionMet">Whether the modified life reaches the required life, or no criterion was set.</param>
public sealed record BearingRatingLifeResult(
    EngineeringCheckOutcome Outcome,
    string? RefusalReason,
    Quantity<Force> EquivalentDynamicLoad,
    double LoadRatio,
    double? BasicRatingLifeMillionRevolutions,
    Quantity<Duration>? BasicRatingLife,
    double? ModifiedRatingLifeMillionRevolutions,
    Quantity<Duration>? ModifiedRatingLife,
    bool? CriterionMet);

/// <summary>
/// Rolling bearing basic rating life L10 by the ISO 281 method, with the
/// reliability factor a1. Specified in
/// <c>docs/engineering/calculations/calc.bearing-rating-life.md</c>.
/// </summary>
public sealed class BearingRatingLifeCalculationDefinition : ICalculationDefinition<BearingRatingLifeInput, BearingRatingLifeResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.bearing-rating-life";

    /// <summary>The equivalent load, as a fraction of the dynamic load rating, above which the basic rating life equation is refused.</summary>
    public const double MaximumLoadRatio = 0.5;

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Rolling Bearing Rating Life (L10)",
        Description:
            "Basic rating life of a rolling bearing from its dynamic load rating and equivalent dynamic load (ISO 281), "
            + "in millions of revolutions and hours, scaled by the reliability factor and compared against a required life.",
        Category: "Machine Elements",
        Assumptions:
        [
            new CalculationAssumption("The equivalent dynamic load is X times the radial load plus Y times the axial load, with X and Y from the bearing catalogue.", "ISO 281's equivalent load."),
            new CalculationAssumption("The life exponent is 3 for ball bearings and 10/3 for roller bearings.", "ISO 281's basic rating life equation."),
            new CalculationAssumption("Only the reliability factor a1 modifies the life; the life modification factor for lubrication, contamination and fatigue load limit is not applied.", "The basic or a1-modified life only."),
        ],
        Constraints:
        [
            new CalculationConstraint("Rating and speed must be positive; loads and factors must not be negative; the equivalent load must be positive; the reliability factor must lie in (0, 1]; the required life must not be negative."),
            new CalculationConstraint("Equivalent load must not exceed half the dynamic load rating (method limit; refused above it)."),
            new CalculationConstraint("Bearing designation must be named."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any constraint above, other than the method limit, is not met.</exception>
    public BearingRatingLifeResult Calculate(BearingRatingLifeInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var ratingN = input.BasicDynamicLoadRating.BaseValue;
        var radialN = input.RadialLoad.BaseValue;
        var axialN = input.AxialLoad.BaseValue;
        var speedRps = input.Speed.BaseValue;
        var requiredS = input.RequiredLife.BaseValue;

        ModuleGuards.Require(context, "Bearing designation must be named.", !string.IsNullOrWhiteSpace(input.BearingDesignation), $"'{input.BearingDesignation}'");
        ModuleGuards.Require(context, "Basic dynamic load rating must be positive.", ratingN > 0, $"{ratingN:0.###} N");
        ModuleGuards.Require(context, "Radial load must not be negative.", radialN >= 0, $"{radialN:0.###} N");
        ModuleGuards.Require(context, "Axial load must not be negative.", axialN >= 0, $"{axialN:0.###} N");
        ModuleGuards.Require(context, "Radial factor must not be negative.", input.RadialFactor >= 0, $"{input.RadialFactor:0.###}");
        ModuleGuards.Require(context, "Axial factor must not be negative.", input.AxialFactor >= 0, $"{input.AxialFactor:0.###}");
        ModuleGuards.Require(context, "Speed must be positive.", speedRps > 0, $"{speedRps:0.###} r/s");
        ModuleGuards.Require(context, "Reliability factor must be within (0, 1].", input.ReliabilityFactor > 0 && input.ReliabilityFactor <= 1.0, $"{input.ReliabilityFactor:0.###}");
        ModuleGuards.Require(context, "Required life must not be negative.", requiredS >= 0, $"{requiredS:0.###} s");

        var equivalentN = input.RadialFactor * radialN + input.AxialFactor * axialN;
        ModuleGuards.Require(context, "Equivalent dynamic load must be positive (an unloaded bearing has no finite life to report).", equivalentN > 0, $"{equivalentN:0.###} N");

        context.RecordIntermediate("Bearing designation", input.BearingDesignation);
        if (input.BearingPin is { } pin)
            context.RecordIntermediate("Bearing reference", pin.ToString());

        var equivalent = new Quantity<Force>(equivalentN, ForceUnits.Newton);
        var loadRatio = equivalentN / ratingN;
        var exponent = input.BearingType == RollingBearingType.Ball ? 3.0 : 10.0 / 3.0;

        context.RecordIntermediate("Equivalent dynamic load", equivalent);
        context.RecordIntermediate("Load ratio P/C", loadRatio);
        context.RecordIntermediate("Life exponent", exponent);
        context.RecordIntermediate("Reliability factor", input.ReliabilityFactor);

        if (loadRatio > MaximumLoadRatio)
        {
            var reason = ModuleGuards.Refuse(
                context,
                "Equivalent load must not exceed half the dynamic load rating (method limit; refused above it).",
                $"Refused: the equivalent dynamic load of {equivalentN:0.###} N is {loadRatio:0.###} of the dynamic load rating, above half. "
                + "ISO 281's basic rating life equation is stated for loads up to half the rating; the module does not extrapolate beyond it.");

            return new BearingRatingLifeResult(EngineeringCheckOutcome.OutsideMethodLimits, reason, equivalent, loadRatio, null, null, null, null, null);
        }

        var basicMillionRev = Math.Pow(ratingN / equivalentN, exponent);
        var basicHours = basicMillionRev * 1e6 / (speedRps * 3600.0);
        var modifiedMillionRev = input.ReliabilityFactor * basicMillionRev;
        var modifiedHours = input.ReliabilityFactor * basicHours;
        var requiredHours = requiredS / 3600.0;
        var met = requiredHours <= 0 || ModuleGuards.IsMet(requiredHours / modifiedHours);

        var basicLife = new Quantity<Duration>(basicHours, DurationUnits.Hour);
        var modifiedLife = new Quantity<Duration>(modifiedHours, DurationUnits.Hour);

        context.RecordIntermediate("Basic rating life (million revolutions)", basicMillionRev);
        context.RecordIntermediate("Basic rating life", basicLife);
        context.RecordIntermediate("Modified rating life", modifiedLife);

        context.RecordConstraintCheck(
            "The modified rating life must reach the required life (where one is set).",
            met,
            requiredHours > 0
                ? $"Modified life {modifiedHours:0.###} h against required {requiredHours:0.###} h."
                : "No required life set.");

        return new BearingRatingLifeResult(
            ModuleGuards.OutcomeOf(met),
            null,
            equivalent,
            loadRatio,
            basicMillionRev,
            basicLife,
            modifiedMillionRev,
            modifiedLife,
            met);
    }
}
