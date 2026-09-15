using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>One bolt's position in the group's in-plane frame.</summary>
public sealed record BoltPosition(Quantity<Length> X, Quantity<Length> Y);

/// <summary>The inputs to one bolt-group eccentric shear check.</summary>
/// <param name="FastenerGrade">The fastener the allowable shear belongs to, for example "ISO 898-1 class 8.8 M16". Recorded, so the figure is attributable.</param>
/// <param name="Bolts">Every bolt's position. Equal bolt areas are assumed.</param>
/// <param name="LoadX">The in-plane load's x component.</param>
/// <param name="LoadY">The in-plane load's y component.</param>
/// <param name="LoadPointX">Where the load acts, x.</param>
/// <param name="LoadPointY">Where the load acts, y.</param>
/// <param name="AllowableShearPerBolt">The allowable shear force on one bolt.</param>
public sealed record BoltGroupEccentricShearInput(
    string FastenerGrade,
    IReadOnlyList<BoltPosition> Bolts,
    Quantity<Force> LoadX,
    Quantity<Force> LoadY,
    Quantity<Length> LoadPointX,
    Quantity<Length> LoadPointY,
    Quantity<Force> AllowableShearPerBolt);

/// <summary>The result of one bolt-group check. Figures are <see langword="null"/> only when the method was refused; the centroid, moment and polar moment are always reported.</summary>
/// <param name="Outcome">What the check found.</param>
/// <param name="RefusalReason">Why the method was refused, or <see langword="null"/>.</param>
/// <param name="BoltCount">How many bolts share the load.</param>
/// <param name="CentroidX">The group centroid, x.</param>
/// <param name="CentroidY">The group centroid, y.</param>
/// <param name="MomentAboutCentroid">The load's moment about the centroid; counter-clockwise positive.</param>
/// <param name="UnitPolarMoment">The sum of squared distances from the centroid: an area, the bolt area having cancelled.</param>
/// <param name="BoltForces">Each bolt's resultant shear, in input order.</param>
/// <param name="GoverningBoltIndex">The index of the most loaded bolt.</param>
/// <param name="GoverningBoltForce">The most loaded bolt's shear.</param>
/// <param name="Utilisation">Governing bolt force over the allowable.</param>
/// <param name="CriterionMet">Whether the utilisation is at or below one.</param>
public sealed record BoltGroupEccentricShearResult(
    EngineeringCheckOutcome Outcome,
    string? RefusalReason,
    int BoltCount,
    Quantity<Length> CentroidX,
    Quantity<Length> CentroidY,
    Quantity<Torque> MomentAboutCentroid,
    Quantity<Area> UnitPolarMoment,
    IReadOnlyList<Quantity<Force>>? BoltForces,
    int? GoverningBoltIndex,
    Quantity<Force>? GoverningBoltForce,
    double? Utilisation,
    bool? CriterionMet);

/// <summary>
/// A bolt group under an eccentric in-plane load, by the elastic (vector)
/// method. Specified in
/// <c>docs/engineering/calculations/calc.bolt-group-eccentric-shear.md</c>.
/// </summary>
public sealed class BoltGroupEccentricShearCalculationDefinition
    : ICalculationDefinition<BoltGroupEccentricShearInput, BoltGroupEccentricShearResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.bolt-group-eccentric-shear";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Bolt Group under Eccentric In-Plane Load",
        Description:
            "Direct and torsional shear on every bolt of a group loaded in its plane away from its centroid, "
            + "by the elastic vector method; the most loaded bolt compared against an allowable shear.",
        Category: "Fasteners",
        Assumptions:
        [
            new CalculationAssumption("Every bolt has the same area and the connected plates are rigid.", "The elastic method's own idealisation; each bolt's torsional share is proportional to its distance from the centroid."),
            new CalculationAssumption("Coordinates and forces share one right-handed in-plane frame; a positive moment is counter-clockwise.", "So the vector sum is unambiguous."),
        ],
        Constraints:
        [
            new CalculationConstraint("At least one bolt, no two coincident; the load must have a non-zero component; the allowable shear must be positive."),
            new CalculationConstraint("The group must be able to resist the load's moment (method limit; a single bolt off the load line is refused)."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">No bolts, coincident bolts, a zero load, a non-positive allowable, or a blank fastener grade.</exception>
    public BoltGroupEccentricShearResult Calculate(BoltGroupEccentricShearInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var bolts = input.Bolts ?? [];
        var count = bolts.Count;
        var loadXN = input.LoadX.BaseValue;
        var loadYN = input.LoadY.BaseValue;
        var allowableN = input.AllowableShearPerBolt.BaseValue;

        ModuleGuards.Require(context, "Fastener grade must be named.", !string.IsNullOrWhiteSpace(input.FastenerGrade), $"'{input.FastenerGrade}'");
        ModuleGuards.Require(context, "At least one bolt is required.", count >= 1, $"{count} bolt(s)");
        ModuleGuards.Require(context, "No two bolts may be coincident.", !AnyCoincident(bolts), "coincident bolt positions");
        ModuleGuards.Require(context, "The load must have at least one non-zero component.", loadXN != 0 || loadYN != 0, "a zero load");
        ModuleGuards.Require(context, "Allowable shear per bolt must be positive.", allowableN > 0, $"{allowableN:0.###} N");

        context.RecordIntermediate("Fastener grade", input.FastenerGrade);

        var xs = new double[count];
        var ys = new double[count];
        for (var i = 0; i < count; i++)
        {
            xs[i] = bolts[i].X.BaseValue;
            ys[i] = bolts[i].Y.BaseValue;
        }

        var centroidXM = xs.Average();
        var centroidYM = ys.Average();
        var momentNm = (input.LoadPointX.BaseValue - centroidXM) * loadYN - (input.LoadPointY.BaseValue - centroidYM) * loadXN;

        var polarM2 = 0.0;
        for (var i = 0; i < count; i++)
            polarM2 += (xs[i] - centroidXM) * (xs[i] - centroidXM) + (ys[i] - centroidYM) * (ys[i] - centroidYM);

        var centroidX = new Quantity<Length>(centroidXM, LengthUnits.Metre).ConvertTo(LengthUnits.Millimetre);
        var centroidY = new Quantity<Length>(centroidYM, LengthUnits.Metre).ConvertTo(LengthUnits.Millimetre);
        var moment = new Quantity<Torque>(momentNm, TorqueUnits.NewtonMetre);
        var polar = new Quantity<Area>(polarM2, AreaUnits.SquareMetre).ConvertTo(AreaUnits.SquareMillimetre);

        context.RecordIntermediate("Centroid x", centroidX);
        context.RecordIntermediate("Centroid y", centroidY);
        context.RecordIntermediate("Moment about centroid", moment);
        context.RecordIntermediate("Unit polar moment", polar);

        if (polarM2 <= 0 && momentNm != 0)
        {
            var reason = ModuleGuards.Refuse(
                context,
                "The group must be able to resist the load's moment (method limit; a single bolt off the load line is refused).",
                $"Refused: the load has a moment of {momentNm:0.###} N.m about the group centroid and a single bolt has no lever arm "
                + "to resist it. The elastic method needs at least two bolts, or the load line through the bolt.");

            return new BoltGroupEccentricShearResult(EngineeringCheckOutcome.OutsideMethodLimits, reason, count, centroidX, centroidY, moment, polar, null, null, null, null, null);
        }

        var directXN = loadXN / count;
        var directYN = loadYN / count;
        var forces = new Quantity<Force>[count];
        var governingIndex = 0;
        var governingN = double.NegativeInfinity;

        for (var i = 0; i < count; i++)
        {
            var dx = xs[i] - centroidXM;
            var dy = ys[i] - centroidYM;
            var torsionalXN = polarM2 > 0 ? -momentNm * dy / polarM2 : 0.0;
            var torsionalYN = polarM2 > 0 ? momentNm * dx / polarM2 : 0.0;
            var fx = directXN + torsionalXN;
            var fy = directYN + torsionalYN;
            var resultantN = Math.Sqrt(fx * fx + fy * fy);
            forces[i] = new Quantity<Force>(resultantN, ForceUnits.Newton);

            if (resultantN > governingN)
            {
                governingN = resultantN;
                governingIndex = i;
            }
        }

        var utilisation = governingN / allowableN;
        var met = ModuleGuards.IsMet(utilisation);

        context.RecordIntermediate("Direct shear per bolt", new Quantity<Force>(Math.Sqrt(directXN * directXN + directYN * directYN), ForceUnits.Newton));
        context.RecordIntermediate("Bolt forces", forces);
        context.RecordIntermediate("Governing bolt", governingIndex);
        context.RecordIntermediate("Utilisation", utilisation);

        context.RecordConstraintCheck(
            "The most loaded bolt's shear must not exceed the allowable shear per bolt.",
            met,
            $"Bolt {governingIndex} carries {governingN:0.###} N against allowable {allowableN:0.###} N; utilisation {utilisation:0.####}.");

        return new BoltGroupEccentricShearResult(
            ModuleGuards.OutcomeOf(met),
            null,
            count,
            centroidX,
            centroidY,
            moment,
            polar,
            forces,
            governingIndex,
            forces[governingIndex],
            utilisation,
            met);
    }

    private static bool AnyCoincident(IReadOnlyList<BoltPosition> bolts)
    {
        const double toleranceM = 1e-9;

        for (var i = 0; i < bolts.Count; i++)
        {
            for (var j = i + 1; j < bolts.Count; j++)
            {
                var dx = bolts[i].X.BaseValue - bolts[j].X.BaseValue;
                var dy = bolts[i].Y.BaseValue - bolts[j].Y.BaseValue;
                if (Math.Sqrt(dx * dx + dy * dy) < toleranceM)
                    return true;
            }
        }

        return false;
    }
}
