namespace Tempest.Core.Manufacturing;

/// <summary>The production volume a source associated with a process.</summary>
/// <remarks>
/// <b>Recorded, never recommended.</b> That a source says a process is
/// used at high volume is a fact about what the source said. It is not
/// advice about what volume a particular job should use, which depends on
/// tooling cost, lead time, part complexity and commercial terms that A7
/// does not hold and will not acquire.
/// <para>
/// The bands are deliberately named rather than numeric: sources describe
/// production scale in words, the boundaries between the words differ by
/// industry, and attaching quantities to them here would be TempestOS
/// inventing thresholds no source published.
/// </para>
/// </remarks>
public enum ProductionScale
{
    /// <summary>Not recorded.</summary>
    Unspecified,

    /// <summary>One-off or prototype work.</summary>
    Prototype,

    /// <summary>Low volume, as the source describes it.</summary>
    LowVolume,

    /// <summary>Medium volume, as the source describes it.</summary>
    MediumVolume,

    /// <summary>High volume, as the source describes it.</summary>
    HighVolume,

    /// <summary>Continuous production.</summary>
    Continuous
}
