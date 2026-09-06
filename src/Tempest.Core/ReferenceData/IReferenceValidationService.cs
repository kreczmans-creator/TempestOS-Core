using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// The validation surface every Group A library offers over its own
/// records.
/// </summary>
/// <remarks>
/// <para>
/// Reuses <see cref="IValidationResult"/>/<see cref="IValidationDiagnostic"/>
/// (`Tempest.Core.EngineeringDomain`) for its own result <em>shape</em>
/// only — the same reuse-respecting choice
/// <see cref="Requirements.IRequirementValidationService"/> already made
/// and for the same structural reason: <see cref="IValidationRule"/> is
/// scoped to <see cref="IEngineeringObject"/>, which no reference record
/// is.
/// </para>
/// <para>
/// <b>Data quality, never engineering judgement.</b> Every rule asks
/// whether a record is internally coherent, dimensionally possible and
/// properly attributed. None asks whether the thing it describes is
/// suitable for anything — that is a future selection capability's job,
/// and putting it here would contaminate reference data with judgement it
/// cannot support.
/// </para>
/// <para>
/// <b>Errors and warnings are different claims.</b> An error means the
/// record states something that cannot be true, or that the library
/// requires. A warning means the record is incomplete or needs a human to
/// look at it — never a claim that the data is wrong.
/// </para>
/// </remarks>
/// <typeparam name="TDefinition">The domain's own engineering description type.</typeparam>
public interface IReferenceValidationService<TDefinition>
    where TDefinition : class
{
    /// <summary>Validates one registered record against every rule that applies to it, including any catalogue-wide rules.</summary>
    /// <exception cref="ReferenceRecordNotFoundException"><paramref name="recordId"/> does not exist.</exception>
    Task<IValidationResult> ValidateAsync(string recordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a definition and provenance that have not been registered
    /// — the check an import or an editor runs before writing anything.
    /// Catalogue-wide rules are not evaluated, since there is no record yet
    /// to compare against.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    Task<IValidationResult> ValidateDefinitionAsync(TDefinition definition, ReferenceProvenance provenance, CancellationToken cancellationToken = default);

    /// <summary>Validates every registered record and reports the result as one data-quality report.</summary>
    Task<ReferenceDataQualityReport> ValidateLibraryAsync(CancellationToken cancellationToken = default);
}
