namespace Tempest.Core.Bearings;

/// <summary>
/// Lubrication information a source actually supplied for a bearing.
/// </summary>
/// <remarks>
/// Every field is recorded, never inferred. This library does not decide
/// that a bearing is suitable for a lubricant, or that a lubricant is
/// suitable for a duty — that is engineering judgement and belongs to a
/// future selection capability. Temperature limits are deliberately not
/// modelled here; see `docs/architecture/A4 Bearing Library.md`,
/// "Deferred", for why (this framework's own <c>Unit</c> is a purely
/// multiplicative factor and cannot express an affine scale such as
/// degrees Celsius, so a temperature limit cannot yet be stored as a
/// dimensioned quantity, and storing one as a bare number would be the
/// exact loss of engineering meaning A4 exists to prevent).
/// </remarks>
/// <param name="Type">What the bearing is lubricated with, as stated by the source.</param>
/// <param name="LubricantDesignation">The lubricant's own designation as the source writes it (a grease grade, a manufacturer's own fill code). <see langword="null"/> if none was given.</param>
/// <param name="FillDescription">The fill quantity or arrangement as the source states it (e.g. a percentage of free space). Free text — no fixed vocabulary exists. <see langword="null"/> if none was given.</param>
/// <param name="Notes">Free-text notes not captured by any other field. <see langword="null"/> if none.</param>
public sealed record BearingLubrication(
    BearingLubricationType Type,
    string? LubricantDesignation = null,
    string? FillDescription = null,
    string? Notes = null);
