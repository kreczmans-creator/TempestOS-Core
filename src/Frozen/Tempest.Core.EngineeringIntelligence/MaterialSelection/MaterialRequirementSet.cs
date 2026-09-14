using Tempest.Core.Materials;

namespace Tempest.Core.EngineeringIntelligence.MaterialSelection;

/// <summary>
/// Everything a material is being selected against, for one application.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the engineer's problem statement, not a rule library.</b> It
/// says what this application needs; the rule library says what good
/// practice requires of any material. Both are evaluated, and keeping them
/// separate is what lets one rule library serve every project.
/// </para>
/// <para>
/// <see cref="AcceptableFamilies"/> and <see cref="ExcludedFamilies"/> are
/// deliberately separate. An empty acceptable list means "no family
/// restriction stated", which is not the same as "every family is
/// acceptable" — and an exclusion is a definite engineering statement
/// worth recording in its own right, typically because a family was ruled
/// out for a reason the property criteria do not capture.
/// </para>
/// </remarks>
public sealed record MaterialRequirementSet
{
    /// <summary>What the material is for, in the engineer's own words. Required — an assessment nobody can attribute to an application is not traceable.</summary>
    public required string ApplicationDescription { get; init; }

    /// <summary>The property criteria. Never <see langword="null"/>; empty means none was stated.</summary>
    public IReadOnlyList<MaterialCriterion> Criteria { get; init; } = [];

    /// <summary>The criteria a person must evidence. Never <see langword="null"/>; empty means none was stated.</summary>
    public IReadOnlyList<MaterialEvidenceCriterion> EvidenceCriteria { get; init; } = [];

    /// <summary>
    /// The material families this application will accept. Never
    /// <see langword="null"/>; empty states no family restriction, which is
    /// not the same as accepting every family.
    /// </summary>
    public IReadOnlyList<MaterialFamily> AcceptableFamilies { get; init; } = [];

    /// <summary>The material families this application rules out, and why they matter enough to be stated. Never <see langword="null"/>.</summary>
    public IReadOnlyList<MaterialFamily> ExcludedFamilies { get; init; } = [];

    /// <summary>
    /// Whether a candidate must be a Released `A1` record.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/>. A Draft or Checked material
    /// record holds values nobody has finished verifying, and selecting
    /// against one produces a conclusion whose trustworthiness nobody can
    /// later establish. Set it false deliberately — for exploratory work
    /// against a partially-entered library — and the assessment says so.
    /// </remarks>
    public bool RequireReleasedMaterials { get; init; } = true;

    /// <summary>Free-text notes about the selection. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether this set states anything at all to assess against.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool StatesAnyCriterion =>
        Criteria.Count > 0 || EvidenceCriteria.Count > 0 || AcceptableFamilies.Count > 0 || ExcludedFamilies.Count > 0;

    /// <summary>The criteria that eliminate a candidate when unsatisfied.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<MaterialCriterion> Constraints =>
        Criteria.Where(c => c.Role == MaterialCriterionRole.Constraint).ToList();

    /// <summary>The criteria that count against a candidate without eliminating it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<MaterialCriterion> Preferences =>
        Criteria.Where(c => c.Role == MaterialCriterionRole.Preference).ToList();
}
