namespace Tempest.Core.Bearings;

/// <summary>How many rows of rolling elements a bearing carries.</summary>
public enum BearingRowConfiguration
{
    /// <summary>Not recorded. Never a claim that the bearing has no rows.</summary>
    Unspecified,

    /// <summary>One row of rolling elements.</summary>
    SingleRow,

    /// <summary>Two rows of rolling elements.</summary>
    DoubleRow,

    /// <summary>Four rows of rolling elements.</summary>
    FourRow,

    /// <summary>A row count this vocabulary does not name; record the source's own wording in <see cref="BearingConfiguration.ArrangementDesignation"/>.</summary>
    Other
}
