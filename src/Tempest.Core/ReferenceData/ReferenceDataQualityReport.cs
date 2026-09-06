using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// What validating a whole reference library found.
/// </summary>
/// <remarks>
/// A read, never a repair. Nothing here changes a record, and nothing here
/// decides that a record should be released — it reports what a reviewer
/// needs in order to decide that themselves. Mirrors
/// <see cref="Materials.MaterialCatalogReconciliationReport"/>'s own
/// report-don't-fix shape.
/// </remarks>
/// <param name="Library">The library examined.</param>
/// <param name="Findings">One entry per record that produced at least one error or warning, ordered by record Id. Never <see langword="null"/>.</param>
/// <param name="RecordsExamined">How many registered records were examined.</param>
public sealed record ReferenceDataQualityReport(
    string Library,
    IReadOnlyList<ReferenceDataQualityFinding> Findings,
    int RecordsExamined)
{
    /// <summary>How many examined records produced at least one error.</summary>
    public int RecordsWithErrors => Findings.Count(f => f.Result.Errors.Count > 0);

    /// <summary>How many examined records produced at least one warning.</summary>
    public int RecordsWithWarnings => Findings.Count(f => f.Result.Warnings.Count > 0);

    /// <summary>Whether every examined record is free of errors. Warnings do not affect this.</summary>
    public bool IsClean => RecordsWithErrors == 0;
}

/// <summary>One record's own contribution to a <see cref="ReferenceDataQualityReport"/>.</summary>
/// <param name="RecordId">The record examined.</param>
/// <param name="ValidationState">The state the record was in when examined — the context a finding has to be read in, since a Draft record is expected to be incomplete.</param>
/// <param name="Result">What validating it produced.</param>
public sealed record ReferenceDataQualityFinding(
    string RecordId,
    ReferenceValidationState ValidationState,
    IValidationResult Result);
