namespace Tempest.Core.Bearings;

/// <summary>
/// Where a rated engineering value came from — the distinction §7 of this
/// library's own charter makes mandatory: manufacturer/reference data must
/// never be confusable with a value TempestOS itself computed.
/// </summary>
public enum BearingValueOrigin
{
    /// <summary>The origin is not recorded. The honest default.</summary>
    Unknown,

    /// <summary>Taken from a manufacturer's own technical catalogue or datasheet.</summary>
    ManufacturerCatalogue,

    /// <summary>Taken from a recognised international or national standard.</summary>
    Standard,

    /// <summary>Taken from a test report traceable to a specimen or batch.</summary>
    TestReport,

    /// <summary>
    /// Computed by TempestOS from other recorded values. Never
    /// interchangeable with the four above, and never presented as
    /// manufacturer reference data.
    /// </summary>
    DerivedByTempestOS
}
