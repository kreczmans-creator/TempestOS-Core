using Tempest.Core.ReferenceData;

namespace Tempest.Core.Manufacturing;

/// <summary>What kind of limitation a process constraint describes.</summary>
public enum ProcessConstraintKind
{
    /// <summary>Not recorded.</summary>
    Unspecified,

    /// <summary>A limitation on the shapes the process can produce.</summary>
    Geometric,

    /// <summary>A limitation arising from the material being processed.</summary>
    Material,

    /// <summary>A limitation arising from the tooling the process needs.</summary>
    Tooling,

    /// <summary>A limitation on dimensional accuracy or repeatability.</summary>
    Dimensional,

    /// <summary>A limitation on the surface the process leaves.</summary>
    Surface,

    /// <summary>A limitation arising from the process environment or its emissions.</summary>
    Environmental,

    /// <summary>A limitation arising from setup, tooling or volume economics, as the source describes it.</summary>
    Economic,

    /// <summary>A limitation arising from safety requirements.</summary>
    Safety,

    /// <summary>A kind this taxonomy does not classify.</summary>
    Other
}

/// <summary>
/// A limitation a source stated about a process.
/// </summary>
/// <remarks>
/// <see cref="Description"/> is the source's own wording, verbatim, and is
/// deliberately free text: process constraints are stated in prose in
/// every real source, and forcing them into a structured form would either
/// lose what the source said or invent structure it did not have. The
/// <see cref="Kind"/> exists so a reader can filter them, not so the text
/// can be interpreted.
/// </remarks>
/// <param name="Description">The constraint as the source states it, verbatim. Required.</param>
/// <param name="Kind">What kind of limitation it is.</param>
/// <param name="Origin">Who stated the constraint.</param>
public sealed record ProcessConstraint(
    string Description,
    ProcessConstraintKind Kind = ProcessConstraintKind.Unspecified,
    ReferenceValueOrigin Origin = ReferenceValueOrigin.Unknown)
{
    /// <summary>The constraint as the source states it.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A process constraint must describe something.", nameof(Description))
        : Description.Trim();

    /// <summary>Whether TempestOS itself, rather than a source, stated the constraint.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDerived => Origin == ReferenceValueOrigin.DerivedByTempestOS;
}
