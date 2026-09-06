using Tempest.Core.ReferenceData;

namespace Tempest.Core.Standards;

/// <summary>A deterministic reference-data filter over the standards register.</summary>
/// <remarks>
/// Every criterion is a predicate, every unset criterion matches
/// everything, criteria combine with AND, and results come back in
/// ascending record-Id order — the same contract every Group A library
/// offers.
/// <para>
/// <see cref="PublicationStatuses"/> and <see cref="ValidationStates"/> are
/// separate criteria on purpose, and searching on one never implies
/// anything about the other: "standards their publisher still holds
/// current" and "standard records TempestOS has released" are different
/// questions with different answers.
/// </para>
/// </remarks>
public sealed record StandardQuery
{
    /// <summary>Matches any standard whose designation contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? DesignationContains { get; init; }

    /// <summary>Matches any standard whose title contains this text, ignoring case. A standard with no recorded title never matches. <see langword="null"/> to match any.</summary>
    public string? TitleContains { get; init; }

    /// <summary>Matches any standard whose recorded scope summary contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? ScopeSummaryContains { get; init; }

    /// <summary>Matches <see cref="StandardsBody.Code"/> exactly, ignoring case and surrounding whitespace. <see langword="null"/> to match any.</summary>
    public string? BodyCode { get; init; }

    /// <summary>Matches any of these body kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<StandardsBodyKind> BodyKinds { get; init; } = [];

    /// <summary>Matches any of these classifications. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<StandardClassification> Classifications { get; init; } = [];

    /// <summary>Matches any standard recording at least one of these disciplines. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<StandardDiscipline> Disciplines { get; init; } = [];

    /// <summary>Matches any of these publisher statuses — a fact about the standard. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<StandardPublicationStatus> PublicationStatuses { get; init; } = [];

    /// <summary>Matches any of these record validation states — a fact about TempestOS's own record. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];

    /// <summary>Matches <see cref="StandardDefinition.Edition"/> exactly, ignoring case. <see langword="null"/> to match any.</summary>
    public string? Edition { get; init; }

    /// <summary>Inclusive lower bound on <see cref="StandardDefinition.PublicationDate"/>. A standard with no recorded publication date never matches. <see langword="null"/> for no lower bound.</summary>
    public DateOnly? PublishedOnOrAfter { get; init; }

    /// <summary>Inclusive upper bound on <see cref="StandardDefinition.PublicationDate"/>. A standard with no recorded publication date never matches. <see langword="null"/> for no upper bound.</summary>
    public DateOnly? PublishedOnOrBefore { get; init; }

    /// <summary>Matches any standard whose recorded equivalences include a designation containing this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? EquivalentToDesignationContaining { get; init; }

    /// <summary>Matches any standard whose normative references include a designation containing this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? NormativelyReferencesDesignationContaining { get; init; }

    /// <summary>Matches any standard whose publisher-stated replacements include a designation containing this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? ReplacesDesignationContaining { get; init; }
}
