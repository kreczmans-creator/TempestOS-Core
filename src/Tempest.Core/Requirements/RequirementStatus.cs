namespace Tempest.Core.Requirements;

/// <summary>
/// A requirement's own lifecycle position — a workflow state, never
/// derived automatically from a <see cref="Verification.IVerificationRecord"/>'s
/// own <see cref="Verification.VerificationOutcome"/> (<c>WP7.2C
/// Requirement Lifecycle Model.md</c>).
/// </summary>
public enum RequirementStatus
{
    Draft,
    Reviewed,
    Approved,
    Allocated,
    Verified,
    Satisfied,
    Obsolete
}
