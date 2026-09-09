namespace Tempest.Core.Evidence;

/// <summary>What kind of engineering record a piece of <see cref="Evidence"/> is.</summary>
/// <remarks>
/// `ADR-0148`. Deliberately five values, chosen to cover what an
/// engineering consultancy actually produces and sends to a client — not
/// an open, caller-extensible vocabulary. A classification a consultancy
/// genuinely needs and this enum lacks is a finding for a future Work
/// Package, not a string field left open for anything.
/// </remarks>
public enum EvidenceClassification
{
    /// <summary>A calculation — a workbook, a hand sheet, or a calculation package produced outside Tempest.</summary>
    Calculation,

    /// <summary>A drawing.</summary>
    Drawing,

    /// <summary>A report.</summary>
    Report,

    /// <summary>A test record.</summary>
    Test,

    /// <summary>Anything the four named classifications do not fit.</summary>
    Other,
}
