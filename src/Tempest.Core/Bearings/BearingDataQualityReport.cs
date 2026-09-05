using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Bearings;

/// <summary>
/// What <see cref="IBearingValidationService.ValidateCatalogueAsync"/>
/// found across the whole catalogue.
/// </summary>
/// <remarks>
/// A read, never a repair. Nothing here changes a record, and nothing here
/// decides that a record should be released — it reports what a reviewer
/// needs in order to decide that themselves. Mirrors
/// <see cref="Materials.MaterialCatalogReconciliationReport"/>'s own
/// report-don't-fix shape.
/// </remarks>
/// <param name="Findings">One entry per bearing that produced at least one error or warning, ordered by bearing Id. Never <see langword="null"/>.</param>
/// <param name="BearingsExamined">How many registered bearings were examined.</param>
public sealed record BearingDataQualityReport(
    IReadOnlyList<BearingDataQualityFinding> Findings,
    int BearingsExamined)
{
    /// <summary>How many examined bearings produced at least one error.</summary>
    public int BearingsWithErrors => Findings.Count(f => f.Result.Errors.Count > 0);

    /// <summary>How many examined bearings produced at least one warning.</summary>
    public int BearingsWithWarnings => Findings.Count(f => f.Result.Warnings.Count > 0);

    /// <summary>Whether every examined bearing is free of errors. Warnings do not affect this.</summary>
    public bool IsClean => BearingsWithErrors == 0;
}

/// <summary>One bearing's own contribution to a <see cref="BearingDataQualityReport"/>.</summary>
/// <param name="BearingId">The bearing examined.</param>
/// <param name="ValidationState">The state the record was in when examined — the context a finding has to be read in, since a Draft record is expected to be incomplete.</param>
/// <param name="Result">What validating it produced.</param>
public sealed record BearingDataQualityFinding(
    string BearingId,
    BearingValidationState ValidationState,
    IValidationResult Result);
