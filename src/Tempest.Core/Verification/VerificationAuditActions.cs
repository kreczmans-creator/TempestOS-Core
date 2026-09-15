namespace Tempest.Core.Verification;

/// <summary>
/// The <see cref="Audit.IAuditRecord.Action"/> value <see cref="VerificationService"/>
/// writes (`WP 21.6A`, OSA-15) — named in the same style
/// <see cref="EngineeringDomain.EngineeringAuditActions"/> and
/// <see cref="Requirements.RequirementsAuditActions"/> already established.
/// </summary>
public static class VerificationAuditActions
{
    /// <summary>A verification record was created against a subject document.</summary>
    public const string Recorded = "verification.recorded";
}
