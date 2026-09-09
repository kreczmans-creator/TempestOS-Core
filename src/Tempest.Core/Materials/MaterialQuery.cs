using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Materials;

/// <summary>A deterministic reference-data filter over the material catalogue.</summary>
/// <remarks>
/// Every criterion is a predicate, every unset criterion matches
/// everything, criteria combine with AND, and results come back in
/// ascending record-Id order — the same contract every Group A library
/// offers. Property ranges compare in the property's own base unit, and a
/// material that does not record the property a range filters on does not
/// match it: an unrecorded value is never read as zero.
/// </remarks>
public sealed record MaterialQuery
{
    /// <summary>Matches any material whose name contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? NameContains { get; init; }

    /// <summary>Matches any material whose designation contains this text, ignoring case. A material with no designation never matches. <see langword="null"/> to match any.</summary>
    public string? DesignationContains { get; init; }

    /// <summary>Matches <see cref="MaterialDefinition.Grade"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? Grade { get; init; }

    /// <summary>Matches <see cref="MaterialDefinition.Condition"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? Condition { get; init; }

    /// <summary>Matches <see cref="MaterialDefinition.Supplier"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? Supplier { get; init; }

    /// <summary>Matches any of these families. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<MaterialFamily> Families { get; init; } = [];

    /// <summary>Matches any of these validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];

    /// <summary>Matches any material citing a standard whose designation contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CitesStandardContaining { get; init; }

    /// <summary>Matches any material recording every one of these property names. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<string> RecordsProperties { get; init; } = [];

    /// <summary>Inclusive lower bound on <see cref="MaterialPropertyNames.Density"/>. <see langword="null"/> for no lower bound.</summary>
    public Quantity<MassDensity>? DensityMinimum { get; init; }

    /// <summary>Inclusive upper bound on <see cref="MaterialPropertyNames.Density"/>. <see langword="null"/> for no upper bound.</summary>
    public Quantity<MassDensity>? DensityMaximum { get; init; }

    /// <summary>Inclusive lower bound on <see cref="MaterialPropertyNames.YieldStrength"/>. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Pressure>? YieldStrengthMinimum { get; init; }

    /// <summary>Inclusive lower bound on <see cref="MaterialPropertyNames.UltimateTensileStrength"/>. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Pressure>? UltimateTensileStrengthMinimum { get; init; }

    /// <summary>Inclusive lower bound on <see cref="MaterialPropertyNames.YoungsModulus"/>. <see langword="null"/> for no lower bound.</summary>
    public Quantity<Pressure>? YoungsModulusMinimum { get; init; }

    /// <summary>Inclusive upper bound on <see cref="MaterialPropertyNames.YoungsModulus"/>. <see langword="null"/> for no upper bound.</summary>
    public Quantity<Pressure>? YoungsModulusMaximum { get; init; }
}
