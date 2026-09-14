using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>The concrete <see cref="IRuleCatalog"/> implementation.</summary>
/// <remarks>
/// Everything about storing, revising, governing and superseding a rule
/// comes from <see cref="ReferenceDataCatalog{TDefinition}"/> (`ADR-0126`,
/// applied to rules by `ADR-0128`). What this class adds is
/// rule-specific: the rule-code uniqueness key, the rule query, and the
/// released-and-applicable set an assessment runs.
/// </remarks>
public sealed class RuleCatalog : ReferenceDataCatalog<RuleDefinition>, IRuleCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every rule record's own backing document carries.</summary>
    public const string RuleDocumentKind = "EngineeringRule";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>ruleId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "EngineeringRules.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each rule code to the <c>ruleId</c> holding it.</summary>
    public const string CodeIndexCollection = "EngineeringRules.CodeIndex";

    /// <summary>Initialises a new instance of the <see cref="RuleCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own rule records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public RuleCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "EngineeringRules";

    /// <inheritdoc />
    public override string DocumentKind => RuleDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => CodeIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<RuleDefinition>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(RuleDefinition.CodeKeyFor(code), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<RuleDefinition>>> SearchAsync(RuleQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => RuleQueryEvaluator.Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<RuleDefinition>>> FindReleasedApplicableAsync(
        IAssessmentSubject subject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return FilterAsync(
            record => record.ValidationState == ReferenceValidationState.Released
                && record.Definition.Applicability.DecideFor(subject) != ApplicabilityDecision.DoesNotApply,
            cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(RuleDefinition definition) => definition.CodeKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(RuleDefinition definition) => $"Rule code '{definition.Code}'";
}
