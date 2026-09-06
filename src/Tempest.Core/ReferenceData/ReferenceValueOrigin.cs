namespace Tempest.Core.ReferenceData;

/// <summary>
/// Where a single reference engineering value came from — the distinction
/// P01's own charter makes mandatory across every library: source data
/// must never be confusable with a value TempestOS itself computed.
/// </summary>
public enum ReferenceValueOrigin
{
    /// <summary>The origin is not recorded. The honest default.</summary>
    Unknown,

    /// <summary>Taken from a manufacturer's own technical catalogue or datasheet.</summary>
    ManufacturerCatalogue,

    /// <summary>Taken from a recognised international or national standard.</summary>
    Standard,

    /// <summary>Taken from a test report traceable to a specimen or batch.</summary>
    TestReport,

    /// <summary>Taken from a recognised engineering handbook or authoritative textbook.</summary>
    EngineeringReference,

    /// <summary>
    /// Computed by TempestOS from other recorded values. Never
    /// interchangeable with the four above, and never presented as source
    /// reference data.
    /// </summary>
    DerivedByTempestOS
}
