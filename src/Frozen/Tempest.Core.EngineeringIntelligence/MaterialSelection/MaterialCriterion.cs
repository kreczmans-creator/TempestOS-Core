using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.MaterialSelection;

/// <summary>
/// What role a criterion plays in a selection — and therefore what
/// failing it means.
/// </summary>
/// <remarks>
/// <b>A constraint and a preference are not the same criterion with a
/// different weight.</b> A candidate that violates a constraint is out; a
/// candidate that misses a preference is still a candidate, and may well
/// be the right one. Collapsing the two into a score is the mistake that
/// produces a "recommended" material nobody would actually use.
/// </remarks>
public enum MaterialCriterionRole
{
    /// <summary>
    /// The criterion must be satisfied. A candidate that fails it is
    /// eliminated, whatever else it does well.
    /// </summary>
    Constraint,

    /// <summary>
    /// The criterion is desirable. A candidate that fails it remains a
    /// candidate, and the shortfall is reported rather than scored away.
    /// </summary>
    Preference,

    /// <summary>
    /// The criterion is recorded so the assessment says what was
    /// considered, but is not used to eliminate or rank anything. The
    /// honest role for something an engineer wants visible without it
    /// driving the outcome.
    /// </summary>
    Informational
}

/// <summary>
/// One thing a material must, or ought to, satisfy.
/// </summary>
/// <remarks>
/// <para>
/// <b>The criterion belongs to the caller, not to the material and not to
/// a rule.</b> "Yield strength at least 300 MPa" is a property of the
/// application being designed, and putting it in a rule library would make
/// that library specific to one project. A criterion is therefore supplied
/// with the request, alongside whatever released rules also apply.
/// </para>
/// <para>
/// <see cref="RequiredValue"/> is a
/// <see cref="ReferenceQuantityValue"/> rather than a bare number, so the
/// unit is explicit and the origin travels with it: a requirement derived
/// from a customer specification and one an engineer estimated are both
/// legitimate and are not the same thing, and
/// <see cref="ReferenceValueOrigin"/> keeps them apart.
/// </para>
/// </remarks>
/// <param name="PropertyName">The `A1` property this criterion is about. Required.</param>
/// <param name="Comparator">How the material's own recorded value must compare against the requirement.</param>
/// <param name="RequiredValue">The requirement, carrying its own unit and origin. Required.</param>
/// <param name="Role">Whether failing this eliminates a candidate or merely counts against it.</param>
/// <param name="Description">What the criterion is for, in the engineer's own words. <see langword="null"/> to derive one from the comparison.</param>
public sealed record MaterialCriterion(
    string PropertyName,
    QuantityComparator Comparator,
    ReferenceQuantityValue RequiredValue,
    MaterialCriterionRole Role = MaterialCriterionRole.Constraint,
    string? Description = null)
{
    /// <summary>The `A1` property this criterion is about.</summary>
    public string PropertyName { get; } = string.IsNullOrWhiteSpace(PropertyName)
        ? throw new ArgumentException("A material criterion must name the property it is about.", nameof(PropertyName))
        : PropertyName.Trim();

    /// <summary>The requirement, carrying its own unit and origin.</summary>
    public ReferenceQuantityValue RequiredValue { get; } = RequiredValue ?? throw new ArgumentNullException(nameof(RequiredValue));

    /// <summary>What the criterion is for, in plain engineering language.</summary>
    public string Describe() => Description ?? Expression.Describe();

    /// <summary>
    /// The criterion as a rule condition, so one engine evaluates both
    /// project criteria and released rules and neither can drift from the
    /// other's semantics.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public QuantityComparisonExpression Expression =>
        new(PropertyName, Comparator, RuleThreshold.FromValue(RequiredValue));
}

/// <summary>
/// A criterion that cannot be settled from recorded property values and
/// needs a person to supply evidence.
/// </summary>
/// <remarks>
/// Corrosion resistance in a particular service fluid, compatibility with
/// a joining process, availability in the required form — real selection
/// criteria that no property in `A1` answers. Modelling them explicitly is
/// what stops them dropping out of an assessment altogether, and every
/// candidate reports <see cref="AssessmentOutcome.EvidenceRequired"/>
/// against them rather than a silent pass.
/// </remarks>
/// <param name="Description">What must be evidenced. Required.</param>
/// <param name="Role">Whether failing to evidence this eliminates a candidate or merely counts against it.</param>
public sealed record MaterialEvidenceCriterion(string Description, MaterialCriterionRole Role = MaterialCriterionRole.Constraint)
{
    /// <summary>What must be evidenced.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("An evidence criterion must say what must be evidenced.", nameof(Description))
        : Description.Trim();

    /// <summary>The criterion as a rule condition, so one engine evaluates it alongside every other.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public EvidenceRequiredExpression Expression => new(Description);
}
