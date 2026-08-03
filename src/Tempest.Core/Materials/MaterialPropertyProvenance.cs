namespace Tempest.Core.Materials;

/// <summary>
/// Where a <see cref="MaterialProperty"/>'s own value came from, and how much
/// it can be trusted — every property carries one, never optionally.
/// </summary>
/// <param name="SourceReference">The engineering source the value was taken from (a standard, a datasheet, a test report) — free text, since no fixed vocabulary of sources exists yet. <see langword="null"/> if unknown.</param>
/// <param name="SourceRevision">The revision or edition of <paramref name="SourceReference"/> the value came from, where that source itself has a revision concept. <see langword="null"/> if unknown or not applicable.</param>
/// <param name="ValidationStatus">Whether the value has been independently checked.</param>
/// <param name="ConfidenceLevel">How confidently the value is believed to be accurate.</param>
/// <param name="ApplicableConditions">The conditions the value is valid under (e.g. a temperature range, a loading rate) — free text, since no fixed vocabulary of conditions exists yet. <see langword="null"/> if unknown or not applicable.</param>
/// <param name="Notes">Free-text notes not captured by any other field. <see langword="null"/> if none.</param>
public sealed record MaterialPropertyProvenance(
    string? SourceReference,
    int? SourceRevision,
    MaterialPropertyValidationStatus ValidationStatus,
    MaterialPropertyConfidenceLevel ConfidenceLevel,
    string? ApplicableConditions,
    string? Notes)
{
    /// <summary>
    /// The honest default when nothing about a property's own origin is known —
    /// every field <see langword="null"/> or its own "not assessed" enum member,
    /// never a guessed or invented value.
    /// </summary>
    public static MaterialPropertyProvenance Unknown { get; } = new(
        SourceReference: null,
        SourceRevision: null,
        ValidationStatus: MaterialPropertyValidationStatus.Unvalidated,
        ConfidenceLevel: MaterialPropertyConfidenceLevel.Unknown,
        ApplicableConditions: null,
        Notes: null);
}
