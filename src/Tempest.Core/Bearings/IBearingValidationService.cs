using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Bearings;

/// <summary>
/// Bearing-specific data-quality validation — the rules a bearing
/// reference record must satisfy to be trustworthy engineering data.
/// </summary>
/// <remarks>
/// <para>
/// Reuses <see cref="IValidationResult"/>/<see cref="IValidationDiagnostic"/>
/// (`Tempest.Core.EngineeringDomain`) for its own result *shape* only —
/// the same reuse-respecting choice
/// <see cref="Requirements.IRequirementValidationService"/> already made
/// and for the same structural reason: <see cref="IValidationRule"/> is
/// scoped to <see cref="IEngineeringObject"/>, which no bearing record is.
/// </para>
/// <para>
/// <b>Data quality, never engineering judgement.</b> Every rule here asks
/// whether a record is internally coherent, dimensionally possible and
/// properly attributed. None asks whether a bearing is suitable for
/// anything — that is a future selection capability's job, and putting it
/// here would contaminate reference data with judgement it cannot support.
/// </para>
/// <para>
/// <b>Errors and warnings are different claims.</b> An error means the
/// record states something that cannot be true (an outside diameter inside
/// the bore) or that this library requires (a family). A warning means the
/// record is incomplete or needs a human to look at it (no designation, a
/// derived value present, a material reference that does not resolve) —
/// never a claim that the data is wrong.
/// </para>
/// </remarks>
public interface IBearingValidationService
{
    /// <summary>
    /// Validates one bearing record against every rule in
    /// <see cref="BearingValidationRules"/> that applies to it, including
    /// the catalogue-wide duplicate-part-number check.
    /// </summary>
    /// <exception cref="BearingNotFoundException"><paramref name="bearingId"/> does not exist.</exception>
    Task<IValidationResult> ValidateAsync(string bearingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a definition that has not been registered — the check an
    /// import or an editor runs before writing anything. Catalogue-wide
    /// rules (duplicate part numbers) are not evaluated here, since there
    /// is no record yet to compare.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    Task<IValidationResult> ValidateDefinitionAsync(BearingDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates every registered bearing and reports the result as one
    /// data-quality report — what a reviewer reads before deciding a
    /// dataset is fit to release.
    /// </summary>
    Task<BearingDataQualityReport> ValidateCatalogueAsync(CancellationToken cancellationToken = default);
}
