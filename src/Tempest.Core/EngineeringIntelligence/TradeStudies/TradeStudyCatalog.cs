using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.TradeStudies;

/// <summary>A deterministic filter over the trade-study library.</summary>
public sealed record TradeStudyQuery
{
    /// <summary>Matches any study whose code contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CodeContains { get; init; }

    /// <summary>Matches any study whose name, problem or objective contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches studies comparing this subject kind, and studies not tied to one. <see langword="null"/> to match any.</summary>
    public string? SubjectKind { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The library of governed engineering trade-study definitions.</summary>
public interface ITradeStudyCatalog : IReferenceDataCatalog<TradeStudyDefinition>
{
    /// <summary>Returns the study registered under <paramref name="code"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<TradeStudyDefinition>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Every registered study matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<TradeStudyDefinition>>> SearchAsync(
        TradeStudyQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ITradeStudyCatalog"/> implementation.</summary>
/// <remarks>
/// A trade-study definition is authored, sourced, reviewed, released and
/// revisioned exactly as a material or a standard is, so it uses the
/// shared reference-data lifecycle rather than a second one of its own
/// (`ADR-0128`).
/// </remarks>
public sealed class TradeStudyCatalog : ReferenceDataCatalog<TradeStudyDefinition>, ITradeStudyCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every trade-study record's own backing document carries.</summary>
    public const string TradeStudyDocumentKind = "EngineeringTradeStudy";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>studyId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "EngineeringTradeStudies.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each study code to the <c>studyId</c> holding it.</summary>
    public const string CodeIndexCollection = "EngineeringTradeStudies.CodeIndex";

    /// <summary>Initialises a new instance of the <see cref="TradeStudyCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own study records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public TradeStudyCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "EngineeringTradeStudies";

    /// <inheritdoc />
    public override string DocumentKind => TradeStudyDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => CodeIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<TradeStudyDefinition>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(TradeStudyDefinition.CodeKeyFor(code), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<TradeStudyDefinition>>> SearchAsync(
        TradeStudyQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(TradeStudyDefinition definition) => definition.CodeKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(TradeStudyDefinition definition) => $"Trade-study code '{definition.Code}'";

    private static bool Matches(IReferenceRecord<TradeStudyDefinition> record, TradeStudyQuery query)
    {
        var study = record.Definition;

        if (query.CodeContains is not null && !study.Code.Contains(query.CodeContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.TextContains is { } text
            && !study.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !study.Problem.Contains(text, StringComparison.OrdinalIgnoreCase)
            && (study.Objective is null || !study.Objective.Contains(text, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.SubjectKind is { } kind
            && study.SubjectKind is not null
            && !string.Equals(study.SubjectKind, kind, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}
