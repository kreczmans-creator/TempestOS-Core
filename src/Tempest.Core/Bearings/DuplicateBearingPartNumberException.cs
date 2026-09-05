namespace Tempest.Core.Bearings;

/// <summary>
/// Thrown when a registration or revision would leave two bearing records
/// sharing one manufacturer and manufacturer part number.
/// </summary>
/// <remarks>
/// Distinct from <see cref="DuplicateBearingException"/>, which is about
/// TempestOS identity. This one guards the commercial identity: the same
/// manufacturer's own part number must not describe two different records,
/// or a downstream consumer resolving a part number has no way to know
/// which record it got.
/// </remarks>
public sealed class DuplicateBearingPartNumberException : BearingsException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateBearingPartNumberException"/> class.
    /// </summary>
    /// <param name="manufacturer">The manufacturer.</param>
    /// <param name="partNumber">The manufacturer part number.</param>
    /// <param name="existingBearingId">The bearing already registered under that manufacturer and part number.</param>
    public DuplicateBearingPartNumberException(string manufacturer, string partNumber, string existingBearingId)
        : base($"Manufacturer '{manufacturer}' part number '{partNumber}' is already registered as bearing '{existingBearingId}'.")
    {
        Manufacturer = manufacturer;
        PartNumber = partNumber;
        ExistingBearingId = existingBearingId;
    }

    /// <summary>Gets the manufacturer.</summary>
    public string Manufacturer { get; }

    /// <summary>Gets the manufacturer part number.</summary>
    public string PartNumber { get; }

    /// <summary>Gets the bearing already registered under that manufacturer and part number.</summary>
    public string ExistingBearingId { get; }
}
