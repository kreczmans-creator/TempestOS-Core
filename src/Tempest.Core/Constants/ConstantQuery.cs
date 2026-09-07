using Tempest.Core.ReferenceData;

namespace Tempest.Core.Constants;

/// <summary>A deterministic reference-data filter over the constants library.</summary>
/// <remarks>
/// Every criterion is a predicate, every unset criterion matches
/// everything, criteria combine with AND, and results come back in
/// ascending record-Id order — the same contract every Group A library
/// offers.
/// </remarks>
public sealed record ConstantQuery
{
    /// <summary>Matches any constant whose symbol or alternative symbol contains this text, case-sensitively — a constant's symbol is case-significant. <see langword="null"/> to match any.</summary>
    public string? SymbolContains { get; init; }

    /// <summary>Matches any constant whose name contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? NameContains { get; init; }

    /// <summary>Matches any of these categories. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ConstantCategory> Categories { get; init; } = [];

    /// <summary>Matches any constant whose value carries this dimension, by the name <see cref="ReferenceQuantityCodec.DimensionNameOf"/> gives it. A constant with no recorded value never matches. <see langword="null"/> to match any.</summary>
    public string? DimensionName { get; init; }

    /// <summary>Matches any of these uncertainty kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ConstantUncertaintyKind> UncertaintyKinds { get; init; } = [];

    /// <summary>Matches any of these validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];

    /// <summary>Matches any constant citing a standard whose designation contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CitesStandardContaining { get; init; }

    /// <summary>Matches any constant whose recorded applicability contains this text, ignoring case. A constant recording no applicability never matches. <see langword="null"/> to match any.</summary>
    public string? ApplicabilityContains { get; init; }
}
