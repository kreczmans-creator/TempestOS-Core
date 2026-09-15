namespace Tempest.Core.ReferenceData;

/// <summary>
/// The <see cref="Audit.IAuditRecord.Action"/> values <see cref="ReferenceDataCatalog{TDefinition}"/>
/// writes (`WP 21.6A`, OSA-15) — named in the same style
/// <see cref="EngineeringDomain.EngineeringAuditActions"/>,
/// <see cref="Requirements.RequirementsAuditActions"/> and
/// <see cref="Verification.VerificationAuditActions"/> already established.
/// </summary>
/// <remarks>
/// Only the three raw-registration write paths this class itself commits —
/// <see cref="ReferenceDataCatalog{TDefinition}.RegisterAsync(string,TDefinition,ReferenceProvenance,SourceCitation?,CancellationToken)"/>,
/// its revise path, and <see cref="ReferenceDataCatalog{TDefinition}.SupersedeAsync"/> —
/// are covered here. <c>SetValidationStateAsync</c> (check/release) is
/// deliberately not: its only production callers,
/// <c>Review.ReferenceReviewService.CheckAsync</c>/<c>ReleaseAsync</c>,
/// already call <see cref="Audit.IAuditRecorder.RecordAsync"/> themselves
/// (`WP 21.5F`'s own audit confirmed this directly) — adding a second row
/// here would duplicate, not close, a gap.
/// </remarks>
public static class ReferenceDataAuditActions
{
    /// <summary>A new reference record was registered.</summary>
    public const string RecordAdded = "referencedata.record-added";

    /// <summary>A reference record's definition was revised.</summary>
    public const string Revised = "referencedata.revised";

    /// <summary>A reference record was superseded by a replacement record.</summary>
    public const string Superseded = "referencedata.superseded";
}
