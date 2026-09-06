using Tempest.Core.UnitsAndQuantities;

using Tempest.Core.ReferenceData;

namespace Tempest.Core.Bearings;

/// <summary>
/// A deterministic reference-data filter over the bearing catalogue.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a search engine: no ranking, no relevance, no scoring,
/// no free-text index. Every criterion below is a predicate, every
/// unset criterion matches everything, and criteria combine with AND —
/// so the same query always returns the same set, in the same order
/// (ascending <see cref="IBearing.BearingId"/>, ordinal). That
/// determinism is what makes this usable as engineering evidence.
/// </para>
/// <para>
/// Dimensional ranges compare in each dimension's own base unit, so a
/// bore range expressed in inches correctly matches a bearing recorded in
/// millimetres. A bearing that does not record the dimension a range
/// filters on does not match that range — an unrecorded value is never
/// treated as zero, and never assumed to fall inside a range.
/// </para>
/// </remarks>
public sealed record BearingQuery
{
    /// <summary>Matches <see cref="BearingIdentity.Manufacturer"/> exactly, ignoring case and surrounding whitespace. <see langword="null"/> to match any.</summary>
    public string? Manufacturer { get; init; }

    /// <summary>Matches any bearing whose <see cref="BearingIdentity.ManufacturerPartNumber"/> contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? PartNumberContains { get; init; }

    /// <summary>Matches any bearing whose <see cref="BearingIdentity.Designation"/> contains this text, ignoring case. A bearing with no designation never matches. <see langword="null"/> to match any.</summary>
    public string? DesignationContains { get; init; }

    /// <summary>Matches <see cref="BearingIdentity.Series"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? Series { get; init; }

    /// <summary>Matches any of these families. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<BearingFamily> Families { get; init; } = [];

    /// <summary>Matches any of these validation states. Never <see langword="null"/>; empty matches any. The filter that separates released reference data from drafts and unverified imports.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];

    /// <summary>Inclusive lower bound on <see cref="BearingGeometry.Bore"/>. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Length>? BoreMinimum { get; init; }

    /// <summary>Inclusive upper bound on <see cref="BearingGeometry.Bore"/>. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Length>? BoreMaximum { get; init; }

    /// <summary>Inclusive lower bound on <see cref="BearingGeometry.OutsideDiameter"/>. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Length>? OutsideDiameterMinimum { get; init; }

    /// <summary>Inclusive upper bound on <see cref="BearingGeometry.OutsideDiameter"/>. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Length>? OutsideDiameterMaximum { get; init; }

    /// <summary>Inclusive lower bound on <see cref="BearingGeometry.Width"/>. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Length>? WidthMinimum { get; init; }

    /// <summary>Inclusive upper bound on <see cref="BearingGeometry.Width"/>. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Length>? WidthMaximum { get; init; }

    /// <summary>Inclusive lower bound on <see cref="BearingLoadRatings.BasicDynamicRadial"/> (C). <see langword="null"/> for no lower bound.</summary>
    public Quantity<Force>? BasicDynamicRadialMinimum { get; init; }

    /// <summary>Inclusive lower bound on <see cref="BearingLoadRatings.BasicStaticRadial"/> (C0). <see langword="null"/> for no lower bound.</summary>
    public Quantity<Force>? BasicStaticRadialMinimum { get; init; }

    /// <summary>Inclusive upper bound on <see cref="BearingDefinition.Mass"/>. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Mass>? MassMaximum { get; init; }

    /// <summary>
    /// Inclusive lower bound on a speed rating of <see cref="SpeedRatingKind"/>
    /// (or, when that is <see langword="null"/>, on the highest speed
    /// rating of any kind the bearing records). <see langword="null"/> for
    /// no speed filter.
    /// </summary>
    public Quantity<RotationalSpeed>? SpeedMinimum { get; init; }

    /// <summary>Which kind of speed rating <see cref="SpeedMinimum"/> applies to. <see langword="null"/> to consider every kind the bearing records.</summary>
    public BearingSpeedRatingKind? SpeedRatingKind { get; init; }

    /// <summary>Matches this sealing classification. A bearing with no recorded sealing arrangement never matches. <see langword="null"/> to match any.</summary>
    public BearingSealingType? Sealing { get; init; }

    /// <summary>Matches <see cref="BearingConfiguration.InternalClearanceClass"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? InternalClearanceClass { get; init; }

    /// <summary>Matches <see cref="BearingConfiguration.PrecisionClass"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? PrecisionClass { get; init; }

    /// <summary>Matches any bearing referencing this <c>materialId</c> in any of its own material fields. <see langword="null"/> to match any.</summary>
    public string? ReferencesMaterialId { get; init; }

    /// <summary>Matches this construction class. <see langword="null"/> to match any.</summary>
    public BearingConstructionClass? ConstructionClass { get; init; }
}
