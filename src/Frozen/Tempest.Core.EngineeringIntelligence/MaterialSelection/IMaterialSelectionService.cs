using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.MaterialSelection;

/// <summary>
/// Assesses material candidates against an application's requirements
/// (`B1`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Assessment, not selection.</b> The name of the work package is
/// "Material Selection Logic" and the logic is exactly what this provides:
/// which candidates satisfy which criteria, why, and against which
/// revision of which record. It does not choose. Choosing weighs cost,
/// lead time, supply, prior use, customer preference and the engineer's
/// own experience — none of which is in a criterion set.
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No ranking, no
/// score, no "best" candidate, no derived allowable stress, no safety
/// factor, and no commercial data. A single satisfying candidate is
/// reported as a single satisfying candidate, not as a recommendation.
/// </para>
/// </remarks>
public interface IMaterialSelectionService
{
    /// <summary>
    /// Assesses <paramref name="candidates"/> against
    /// <paramref name="requirements"/>, together with every released rule
    /// that applies to each.
    /// </summary>
    /// <param name="requirements">What the application needs.</param>
    /// <param name="candidates">The material records to assess, as read from `A1`.</param>
    /// <param name="cancellationToken">Cancels the assessment.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    Task<MaterialSelectionResult> AssessAsync(
        MaterialRequirementSet requirements,
        IReadOnlyList<IReferenceRecord<MaterialDefinition>> candidates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assesses every material the `A1` catalogue holds that
    /// <paramref name="requirements"/> permits as a candidate.
    /// </summary>
    /// <remarks>
    /// The convenience path, and it narrows only by what the requirements
    /// state: family acceptability and, where
    /// <see cref="MaterialRequirementSet.RequireReleasedMaterials"/> is
    /// set, validation state. It does not pre-filter on the property
    /// criteria — a candidate eliminated by a criterion is reported as
    /// eliminated, with the reason, rather than quietly omitted.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="requirements"/> is <see langword="null"/>.</exception>
    Task<MaterialSelectionResult> AssessCatalogueAsync(
        MaterialRequirementSet requirements,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-runs an assessment against the exact revisions a previous result
    /// pinned, so a historical conclusion can be reproduced rather than
    /// merely believed.
    /// </summary>
    /// <remarks>
    /// The reproducibility path. Every pin is resolved back through the
    /// owning catalogue's own revision history, so the assessment reads
    /// the values as they stood, whatever has happened to those records
    /// since.
    /// </remarks>
    /// <param name="requirements">The requirements the original assessment used.</param>
    /// <param name="pins">The material revisions the original assessment pinned.</param>
    /// <param name="cancellationToken">Cancels the reproduction.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A pin names a library other than Materials.</exception>
    /// <exception cref="ReferenceRecordNotFoundException">A pinned record no longer exists.</exception>
    Task<MaterialSelectionResult> ReproduceAsync(
        MaterialRequirementSet requirements,
        IReadOnlyList<ReferencePin> pins,
        CancellationToken cancellationToken = default);
}
