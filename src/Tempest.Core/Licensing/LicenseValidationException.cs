namespace Tempest.Core.Licensing;

/// <summary>
/// Thrown by the Host's own startup sequence when <see cref="ILicenseValidator.Validate"/>
/// reports <see cref="LicenseValidationResult.IsValid"/> as <see langword="false"/> —
/// Host-fatal, per <c>ADR-0013</c>'s existing platform-service-failure
/// classification, applied to Licensing without modification (<c>ADR-0050</c>).
/// </summary>
/// <remarks>
/// Not thrown by <see cref="ILicenseValidator.Validate"/> itself — that
/// method always returns a <see cref="LicenseValidationResult"/>, even
/// for an expired, malformed, or unreadable license file. The Host's own
/// startup sequence is what decides an invalid result is Host-fatal and
/// raises this exception, exactly mirroring how a <c>CommandResult</c>
/// failure does not itself throw.
/// </remarks>
public sealed class LicenseValidationException : LicensingException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="LicenseValidationException"/> class.
    /// </summary>
    /// <param name="failureReason">The human-readable reason validation failed.</param>
    public LicenseValidationException(string failureReason)
        : base($"License validation failed: {failureReason}")
    {
        FailureReason = failureReason;
    }

    /// <summary>Gets the human-readable reason validation failed.</summary>
    public string FailureReason { get; }
}
