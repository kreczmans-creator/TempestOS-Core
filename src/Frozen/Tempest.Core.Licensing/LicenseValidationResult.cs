namespace Tempest.Core.Licensing;

/// <summary>
/// The outcome of license validation. Mirrors <c>CommandResult</c>'s own
/// success/failure shape.
/// </summary>
/// <param name="IsValid">Whether validation succeeded.</param>
/// <param name="License">The validated license, or <see langword="null"/> if validation failed.</param>
/// <param name="FailureReason">
/// A human-readable, specific description of why validation failed
/// (expired, malformed, missing file), or <see langword="null"/> if
/// validation succeeded.
/// </param>
public sealed record LicenseValidationResult(bool IsValid, ILicense? License, string? FailureReason);
