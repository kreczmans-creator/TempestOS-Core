namespace Tempest.Core.Bearings;

/// <summary>
/// Where a bearing record's own data came from, and how far it can be
/// trusted. Every bearing carries one — never optionally.
/// </summary>
/// <remarks>
/// Provenance is never fabricated and never inferred. A field the source
/// did not supply stays <see langword="null"/>, and
/// <see cref="VerificationStatus"/> stays
/// <see cref="BearingVerificationStatus.NotVerified"/> until a person
/// actually verifies the record — importing data does not verify it.
/// <see cref="IBearingCatalog.SetValidationStateAsync"/> enforces the
/// consequences: a record cannot leave
/// <see cref="BearingValidationState.Draft"/> without a named source
/// organisation and document, and cannot reach
/// <see cref="BearingValidationState.Released"/> without a named reviewer,
/// a verification date, and
/// <see cref="BearingVerificationStatus.VerifiedAgainstSource"/>.
/// </remarks>
/// <param name="SourceOrganisation">The organisation the data came from (a manufacturer, a standards body). <see langword="null"/> if unknown.</param>
/// <param name="SourceDocument">The document the data came from (a catalogue, a datasheet, a standard). <see langword="null"/> if unknown.</param>
/// <param name="SourceRevision">The revision or edition of <paramref name="SourceDocument"/>. <see langword="null"/> if unknown or not applicable.</param>
/// <param name="SourceDate">The publication date of that revision. <see langword="null"/> if unknown.</param>
/// <param name="SourceLocation">Where in the document the data appears (page, section, table). <see langword="null"/> if not recorded.</param>
/// <param name="ExtractionMethod">How the values got from the source into TempestOS.</param>
/// <param name="VerificationStatus">Whether the values have been checked back against the source.</param>
/// <param name="ReviewerPrincipalId">The principal who verified the record. <see langword="null"/> until one has.</param>
/// <param name="VerificationDate">When the verification happened. <see langword="null"/> until it has.</param>
/// <param name="Notes">Free-text notes not captured by any other field. <see langword="null"/> if none.</param>
public sealed record BearingProvenance(
    string? SourceOrganisation = null,
    string? SourceDocument = null,
    string? SourceRevision = null,
    DateOnly? SourceDate = null,
    string? SourceLocation = null,
    BearingExtractionMethod ExtractionMethod = BearingExtractionMethod.Unknown,
    BearingVerificationStatus VerificationStatus = BearingVerificationStatus.NotVerified,
    string? ReviewerPrincipalId = null,
    DateOnly? VerificationDate = null,
    string? Notes = null)
{
    /// <summary>
    /// The honest default when nothing about a record's own origin is
    /// known — every field <see langword="null"/> or its own "not
    /// assessed" member, never a guessed value. Mirrors
    /// <see cref="Materials.MaterialPropertyProvenance.Unknown"/>.
    /// </summary>
    public static BearingProvenance Unknown { get; } = new();

    /// <summary>Whether a source organisation and document are both named — the minimum for a record to leave <see cref="BearingValidationState.Draft"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IdentifiesASource =>
        !string.IsNullOrWhiteSpace(SourceOrganisation) && !string.IsNullOrWhiteSpace(SourceDocument);

    /// <summary>Whether a named reviewer verified the record against its own source on a recorded date — the minimum for a record to be released.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsVerified =>
        VerificationStatus == BearingVerificationStatus.VerifiedAgainstSource
        && !string.IsNullOrWhiteSpace(ReviewerPrincipalId)
        && VerificationDate is not null;
}
