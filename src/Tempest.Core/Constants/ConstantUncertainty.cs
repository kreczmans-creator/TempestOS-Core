using Tempest.Core.ReferenceData;

namespace Tempest.Core.Constants;

/// <summary>What kind of uncertainty statement a source made about a constant.</summary>
/// <remarks>
/// <b>Not recorded is not zero, and zero is not exact.</b> The three are
/// separate members here because they are three separate facts: nobody
/// wrote the uncertainty down, the source stated an uncertainty that
/// happens to be zero, and the constant is exact by definition so no
/// uncertainty exists to state.
/// </remarks>
public enum ConstantUncertaintyKind
{
    /// <summary>The source stated no uncertainty, and nobody has recorded one. Never read as exact.</summary>
    NotRecorded,

    /// <summary>The constant is exact — fixed by definition or adopted by convention — so there is no uncertainty to state.</summary>
    Exact,

    /// <summary>A standard uncertainty, at coverage factor one.</summary>
    Standard,

    /// <summary>An expanded uncertainty, at a stated coverage factor.</summary>
    Expanded,

    /// <summary>A tolerance or bound the source stated without characterising it statistically.</summary>
    Tolerance
}

/// <summary>
/// What a source said about how well a constant's own value is known.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Absolute"/> carries the same dimension as the constant
/// itself and is held as a boxed quantity for the same reason the value
/// is; <see cref="Relative"/> is the dimensionless form sources often
/// quote instead. Both may be present where the source gave both; neither
/// is ever computed from the other, because rounding either way would
/// invent precision the source did not publish.
/// </para>
/// <para>
/// <b>Recorded, never derived.</b> A6 does not propagate uncertainty, does
/// not combine it, and does not convert between coverage factors. Those
/// are measurement-analysis operations belonging to whatever consumes the
/// constant.
/// </para>
/// </remarks>
/// <param name="Kind">What kind of uncertainty statement this is.</param>
/// <param name="Absolute">The uncertainty in the constant's own units. <see langword="null"/> if the source stated none in that form.</param>
/// <param name="Relative">The relative uncertainty, as a dimensionless fraction. <see langword="null"/> if the source stated none in that form.</param>
/// <param name="CoverageFactor">The coverage factor an expanded uncertainty was stated at. <see langword="null"/> for every other kind, and where an expanded uncertainty's factor was not recorded.</param>
/// <param name="Notes">Anything else the source said about the uncertainty, verbatim. <see langword="null"/> if none.</param>
public sealed record ConstantUncertainty(
    ConstantUncertaintyKind Kind = ConstantUncertaintyKind.NotRecorded,
    ReferenceQuantityValue? Absolute = null,
    double? Relative = null,
    double? CoverageFactor = null,
    string? Notes = null)
{
    /// <summary>The honest default when a source stated nothing about how well a constant is known.</summary>
    public static ConstantUncertainty NotRecorded { get; } = new();

    /// <summary>An exact constant, with no uncertainty to state.</summary>
    public static ConstantUncertainty Exact { get; } = new(ConstantUncertaintyKind.Exact);

    /// <summary>Whether the constant is exact rather than measured.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsExact => Kind == ConstantUncertaintyKind.Exact;

    /// <summary>Whether any uncertainty figure at all is recorded.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool StatesAFigure => Absolute is not null || Relative is not null;
}
