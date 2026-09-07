using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations;

/// <summary>Whether a bracket section meets the criteria it was checked against.</summary>
/// <remarks>
/// <b>Meeting the criteria is not approval.</b> There is no
/// <c>Approved</c> member and there will not be one: approving a design is
/// a governed act by a person, and this enumeration reports only what the
/// arithmetic found.
/// </remarks>
public enum BracketCheckOutcome
{
    /// <summary>Every stated criterion is met. An engineering finding, not an approval.</summary>
    MeetsCriteria,

    /// <summary>At least one stated criterion is not met.</summary>
    DoesNotMeetCriteria,
}

/// <summary>
/// The inputs to one bracket section check, every one of them already
/// resolved.
/// </summary>
/// <remarks>
/// <para>
/// <b>The material properties arrive as values, not as a catalogue.</b>
/// <see cref="ICalculationDefinition{TInput, TResult}.Calculate"/> must be a
/// pure function, so the caller resolves the material beforehand — see
/// <see cref="GovernedBracketCheckService"/>, which is the only thing that
/// should build one of these from a governed record.
/// </para>
/// <para>
/// <b><see cref="MaterialPin"/> is carried through into the result.</b>
/// A stress figure that cannot say which revision of which record supplied
/// its allowable stress is not reproducible, and this is what makes it so:
/// the pin travels with the numbers rather than being recorded alongside
/// them and lost on the way to storage.
/// </para>
/// </remarks>
/// <param name="MaterialPin">The exact reference record and revision the material properties came from.</param>
/// <param name="AllowableStress">The material's allowable direct stress, as taken from the pinned record.</param>
/// <param name="Density">The material's density, as taken from the pinned record.</param>
/// <param name="AppliedLoad">The axial load the section carries.</param>
/// <param name="SectionArea">The minimum cross-sectional area resisting that load.</param>
/// <param name="MemberLength">The length of the member, for the mass estimate.</param>
/// <param name="MassLimit">The heaviest the member is allowed to be.</param>
public sealed record BracketSectionCheckInput(
    ReferencePin MaterialPin,
    Quantity<Pressure> AllowableStress,
    Quantity<MassDensity> Density,
    Quantity<Force> AppliedLoad,
    Quantity<Area> SectionArea,
    Quantity<Length> MemberLength,
    Quantity<Mass> MassLimit);

/// <summary>The result of one bracket section check.</summary>
/// <remarks>
/// The calculated value, the limit it was compared against, and the
/// comparison are three separate fields rather than one verdict, because a
/// reviewer needs to see the numbers that produced the conclusion and not
/// only the conclusion.
/// </remarks>
/// <param name="MaterialPin">The reference record and revision the allowable stress and density came from.</param>
/// <param name="AppliedStress">The direct stress on the section, F / A.</param>
/// <param name="AllowableStress">The allowable stress the applied stress was compared against.</param>
/// <param name="StressMargin">
/// The margin of safety, <c>(allowable / applied) − 1</c>. Zero means the
/// section is exactly at its allowable; negative means it is over.
/// </param>
/// <param name="EstimatedMass">The member's estimated mass, density × area × length.</param>
/// <param name="MassLimit">The mass limit the estimate was compared against.</param>
/// <param name="StressCriterionMet">Whether the applied stress is at or below the allowable stress.</param>
/// <param name="MassCriterionMet">Whether the estimated mass is at or below the mass limit.</param>
/// <param name="Outcome">Whether every criterion was met.</param>
public sealed record BracketSectionCheckResult(
    ReferencePin MaterialPin,
    Quantity<Pressure> AppliedStress,
    Quantity<Pressure> AllowableStress,
    double StressMargin,
    Quantity<Mass> EstimatedMass,
    Quantity<Mass> MassLimit,
    bool StressCriterionMet,
    bool MassCriterionMet,
    BracketCheckOutcome Outcome);

/// <summary>
/// A first-order direct-stress and mass check on one bracket section.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a hand calculation, and says so.</b> It is the closed-form
/// check an engineer does on the back of a drawing: divide the load by the
/// area, compare against the material's allowable, and weigh the part. It
/// is not a stress analysis. It cannot see bending, buckling, stress
/// concentration, bearing at the fixings, or fatigue, and the constraints
/// below say so rather than leaving a reader to assume otherwise.
/// </para>
/// <para>
/// <b>The method is the one the bracket's own calculation pack already
/// declares.</b> <c>TDE-CPK-001</c> states <c>sigma = F / A</c> and
/// <c>margin = (Rp0.2 / sigma) - 1</c>, and the worked example
/// <c>WEX-MAT-001</c> teaches from exactly those equations against the
/// 6082-T6 record. Implementing anything else would have meant the
/// platform's documentation describing one calculation and its code
/// performing another.
/// </para>
/// <para>
/// <b>Why mass is here too.</b> The bracket is carried, so it has two
/// acceptance criteria and always did — the integration phase's material
/// selection screened on a strength floor and a density ceiling together.
/// Mass is arithmetic over the same inputs, not a second calculation
/// family, and including it is what makes the missing 5083 density bite in
/// the calculation exactly as it bit in selection.
/// </para>
/// <para>
/// <b>Pure and deterministic.</b> No clock, no randomness, no catalogue
/// lookup, no shared state. The same inputs give the same result forever,
/// which is the property the whole reproducibility claim rests on.
/// </para>
/// </remarks>
public sealed class BracketSectionCheckCalculationDefinition
    : ICalculationDefinition<BracketSectionCheckInput, BracketSectionCheckResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.bracket-section-check";

    /// <summary>
    /// The relative slack the acceptance comparisons allow, so that a
    /// section sitting exactly on its limit is not failed by unit-conversion
    /// rounding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is not a fudge factor.</b> A section of 60 mm2 carrying
    /// 15.6 kN is exactly at a 260 MPa allowable, in decimal. In binary
    /// floating point it is not: 60 mm2 converts to
    /// 5.9999999999999995e-05 m2, so the computed stress lands one unit in
    /// the last place <em>above</em> 260 MPa and a bare
    /// <c>applied &lt;= allowable</c> reports the bracket as failing by
    /// about 1e-14 percent. That is a wrong answer, and an engineer told
    /// their design fails by a hundred-trillionth of a percent would
    /// rightly stop trusting the tool.
    /// </para>
    /// <para>
    /// <b>Why this magnitude.</b> One part in a billion is roughly seven
    /// orders of magnitude larger than the double-precision rounding it
    /// exists to absorb, and roughly seven orders of magnitude smaller than
    /// the tightest tolerance any real material property is known to — a
    /// specified minimum proof stress is quoted to three significant
    /// figures at best. It cannot change an engineering conclusion; it can
    /// only stop arithmetic noise from changing one.
    /// </para>
    /// <para>
    /// It is public, and named, because a comparison tolerance buried as a
    /// literal inside a method is exactly the kind of unexplained constant
    /// that makes engineering software impossible to review.
    /// </para>
    /// </remarks>
    public const double AcceptanceRelativeTolerance = 1e-9;

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Bracket Section Check",
        Description:
            "First-order direct stress and mass check on a bracket's minimum section. "
            + "sigma = F / A; margin = (allowable / sigma) - 1; mass = density x area x length. "
            + "Meets criteria when margin >= 0 and mass <= limit.",
        Category: "Structural",
        Assumptions:
        [
            new CalculationAssumption(
                "Loading is static and purely axial across the minimum section.",
                "The closed-form method assumes it. A bending or fatigue case needs a different check."),
            new CalculationAssumption(
                "The section area given is the minimum area resisting the load, net of holes and cut-outs.",
                "The calculation cannot see geometry; it divides by the area it is given."),
            new CalculationAssumption(
                "The delivered material meets the specified minimum properties for the size band actually supplied.",
                "Published proof stresses are specification minima for a stated product form and size band, "
                + "not measured properties of a particular bar."),
            new CalculationAssumption(
                "The member is a prism of constant section over its stated length, for the mass estimate.",
                "Mass is density x area x length. A tapered or featured part weighs less than this estimate."),
            new CalculationAssumption(
                "Acceptance comparisons allow one part in a billion of relative slack.",
                "So that a section exactly on its limit is not failed by unit-conversion rounding. Far below "
                + "the precision any material property is known to, so it cannot change a conclusion."),
        ],
        Constraints:
        [
            new CalculationConstraint("Applied load must be positive."),
            new CalculationConstraint("Section area must be positive."),
            new CalculationConstraint("Member length must be positive."),
            new CalculationConstraint("Allowable stress must be positive."),
            new CalculationConstraint("Density must be positive."),
            new CalculationConstraint("Mass limit must be positive."),
            new CalculationConstraint("Does not consider bending, buckling, stress concentration, bearing at fixings, or fatigue."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any input that must be positive is zero or negative.</exception>
    public BracketSectionCheckResult Calculate(BracketSectionCheckInput input, CalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        // Every quantity is taken in its own dimension's base unit — newtons,
        // square metres, pascals, kilograms per cubic metre, metres. The unit
        // the caller happened to state a value in never reaches the
        // arithmetic, so mixing MPa with Pa or mm2 with m2 is impossible
        // rather than merely discouraged.
        var loadNewtons = input.AppliedLoad.BaseValue;
        var areaSquareMetres = input.SectionArea.BaseValue;
        var lengthMetres = input.MemberLength.BaseValue;
        var allowablePascals = input.AllowableStress.BaseValue;
        var densityKilogramsPerCubicMetre = input.Density.BaseValue;
        var massLimitKilograms = input.MassLimit.BaseValue;

        Require(context, "Applied load must be positive.", loadNewtons > 0, $"{loadNewtons:0.###} N");
        Require(context, "Section area must be positive.", areaSquareMetres > 0, $"{areaSquareMetres:0.#########} m2");
        Require(context, "Member length must be positive.", lengthMetres > 0, $"{lengthMetres:0.######} m");
        Require(context, "Allowable stress must be positive.", allowablePascals > 0, $"{allowablePascals:0.###} Pa");
        Require(context, "Density must be positive.", densityKilogramsPerCubicMetre > 0, $"{densityKilogramsPerCubicMetre:0.###} kg/m3");
        Require(context, "Mass limit must be positive.", massLimitKilograms > 0, $"{massLimitKilograms:0.######} kg");

        var appliedPascals = loadNewtons / areaSquareMetres;
        var stressMargin = (allowablePascals / appliedPascals) - 1.0;
        var massKilograms = densityKilogramsPerCubicMetre * areaSquareMetres * lengthMetres;

        var appliedStress = new Quantity<Pressure>(appliedPascals, PressureUnits.Pascal).ConvertTo(PressureUnits.Megapascal);
        var estimatedMass = new Quantity<Mass>(massKilograms, MassUnits.Kilogram);

        context.ReferenceMaterial(input.MaterialPin.RecordId);
        context.RecordIntermediate("Applied stress (MPa)", appliedStress.Value);
        context.RecordIntermediate("Allowable stress (MPa)", input.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value);
        context.RecordIntermediate("Stress margin of safety", stressMargin);
        context.RecordIntermediate("Estimated mass (kg)", massKilograms);
        context.RecordIntermediate("Material reference", input.MaterialPin.ToString());

        // Compared against the limit with AcceptanceRelativeTolerance of
        // slack, for the reason set out on that constant: without it, a
        // section exactly on its limit fails on a rounding artefact.
        var stressMet = appliedPascals <= allowablePascals * (1.0 + AcceptanceRelativeTolerance);
        var massMet = massKilograms <= massLimitKilograms * (1.0 + AcceptanceRelativeTolerance);

        // Acceptance criteria are recorded as constraint checks so an
        // unmet one carries into the engine's own validation outcome as
        // Conditional. The engineering conclusion below is separate from
        // the arithmetic above, and neither is an approval.
        context.RecordConstraintCheck(
            "Applied stress must not exceed the material's allowable stress.",
            stressMet,
            $"Applied {appliedStress.Value:0.###} MPa against allowable "
            + $"{input.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value:0.###} MPa; margin {stressMargin:0.####}.");

        context.RecordConstraintCheck(
            "Estimated mass must not exceed the stated mass limit.",
            massMet,
            $"Estimated {massKilograms:0.#####} kg against limit {massLimitKilograms:0.#####} kg.");

        return new BracketSectionCheckResult(
            input.MaterialPin,
            appliedStress,
            input.AllowableStress,
            stressMargin,
            estimatedMass,
            input.MassLimit,
            stressMet,
            massMet,
            stressMet && massMet ? BracketCheckOutcome.MeetsCriteria : BracketCheckOutcome.DoesNotMeetCriteria);
    }

    private static void Require(CalculationContext context, string constraint, bool satisfied, string actual)
    {
        context.RecordConstraintCheck(constraint, satisfied, $"Received {actual}.");

        if (!satisfied)
            throw new CalculationInputInvalidException($"{constraint} Received {actual}.");
    }
}
