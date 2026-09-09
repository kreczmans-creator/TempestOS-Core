using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Manufacturing;

/// <summary>A deterministic reference-data filter over the manufacturing process library.</summary>
/// <remarks>
/// Every criterion is a predicate, every unset criterion matches
/// everything, criteria combine with AND, and results come back in
/// ascending record-Id order — the same contract every Group A library
/// offers.
/// <para>
/// <b>A filter, not a process selector.</b> Narrowing to the processes
/// whose published bands could contain a value is not choosing a process
/// for a part, and this query offers no criterion that would require it to
/// be. A capability criterion asks "does the source's own band cover
/// this?", never "is this process right for my job?".
/// </para>
/// </remarks>
public sealed record ProcessQuery
{
    /// <summary>Matches any process whose name contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? NameContains { get; init; }

    /// <summary>Matches any of these families. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ProcessFamily> Families { get; init; } = [];

    /// <summary>Matches any process whose family is in one of these groups — "every casting process", without enumerating each. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ProcessGroup> Groups { get; init; } = [];

    /// <summary>Matches any of these validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];

    /// <summary>
    /// Matches any process a source associated with this material family
    /// as suitable or conditionally suitable. A family a source explicitly
    /// called unsuitable never matches. <see langword="null"/> to match
    /// any.
    /// </summary>
    public MaterialFamily? ProcessesMaterialFamily { get; init; }

    /// <summary>Matches any process a source associated with this registered material. <see langword="null"/> to match any.</summary>
    public string? ProcessesMaterialId { get; init; }

    /// <summary>Matches any process a source associated with at least one of these production scales. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ProductionScale> ProductionScales { get; init; } = [];

    /// <summary>Matches any process recording a constraint of at least one of these kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ProcessConstraintKind> ConstraintKinds { get; init; } = [];

    /// <summary>Matches any process citing a standard whose designation contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CitesStandardContaining { get; init; }

    /// <summary>Matches any process whose published tolerance band contains this value. A process recording no such band never matches. <see langword="null"/> to match any.</summary>
    public Quantity<Length>? ToleranceBandContains { get; init; }

    /// <summary>Matches any process whose published surface roughness band contains this value. A process recording no such band never matches. <see langword="null"/> to match any.</summary>
    public Quantity<Length>? SurfaceRoughnessBandContains { get; init; }

    /// <summary>Matches any process whose published wall thickness band contains this value. A process recording no such band never matches. <see langword="null"/> to match any.</summary>
    public Quantity<Length>? WallThicknessBandContains { get; init; }

    /// <summary>Matches any process whose published part-size band contains this value. A process recording no such band never matches. <see langword="null"/> to match any.</summary>
    public Quantity<Length>? PartSizeBandContains { get; init; }

    /// <summary>Matches any process whose published part-mass band contains this value. A process recording no such band never matches. <see langword="null"/> to match any.</summary>
    public Quantity<Mass>? PartMassBandContains { get; init; }
}
