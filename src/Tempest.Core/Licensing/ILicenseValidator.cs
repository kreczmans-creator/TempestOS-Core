namespace Tempest.Core.Licensing;

/// <summary>
/// Validates a license at Host startup, before the DI container exists —
/// mirroring Configuration's own pre-container construction. Invalid
/// results are Host-fatal (<c>ADR-0013</c>), not isolated.
/// </summary>
public interface ILicenseValidator
{
    /// <summary>Validates the current license.</summary>
    LicenseValidationResult Validate();
}
