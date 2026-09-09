using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Development;

/// <summary>A deterministic filter over the opportunity pipeline.</summary>
public sealed record OpportunityQuery
{
    /// <summary>Matches any opportunity whose reference, title or organisation contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these stages. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<PipelineStage> Stages { get; init; } = [];

    /// <summary>Matches opportunities owned by this principal. <see langword="null"/> to match any.</summary>
    public string? OwnerPrincipalId { get; init; }

    /// <summary>Matches open opportunities, closed ones, or either. <see langword="null"/> to match any.</summary>
    public bool? IsOpen { get; init; }

    /// <summary>Matches opportunities expected to be decided on or before this date. <see langword="null"/> to match any.</summary>
    public DateOnly? DecisionExpectedBy { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The organisation's opportunity pipeline.</summary>
public interface IOpportunityCatalog : IReferenceDataCatalog<Opportunity>
{
    /// <summary>Returns the opportunity registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<Opportunity>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered opportunity matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<Opportunity>>> SearchAsync(OpportunityQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IOpportunityCatalog"/> implementation.</summary>
public sealed class OpportunityCatalog : ReferenceDataCatalog<Opportunity>, IOpportunityCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every opportunity record's own backing document carries.</summary>
    public const string OpportunityDocumentKind = "BusinessOpportunity";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>opportunityId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessOpportunities.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each opportunity reference to the <c>opportunityId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessOpportunities.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="OpportunityCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own opportunity records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public OpportunityCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessOpportunities";

    /// <inheritdoc />
    public override string DocumentKind => OpportunityDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<Opportunity>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(Opportunity.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<Opportunity>>> SearchAsync(
        OpportunityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(Opportunity definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(Opportunity definition) => $"Opportunity reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<Opportunity> record, OpportunityQuery query)
    {
        var opportunity = record.Definition;

        if (query.TextContains is { } text
            && !opportunity.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !opportunity.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !opportunity.OrganisationName.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Stages.Count > 0 && !query.Stages.Contains(opportunity.Stage))
            return false;

        if (query.OwnerPrincipalId is { } owner
            && !string.Equals(opportunity.Governance.Ownership.OwnerPrincipalId, owner, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.IsOpen is { } open && opportunity.IsOpen != open)
            return false;

        if (query.DecisionExpectedBy is { } by
            && (opportunity.ExpectedDecisionDate is not { } expected || expected > by))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes C6's validation service reports.</summary>
public static class PipelineValidationRules
{
    /// <summary>The opportunity claims revenue more real than its stage supports.</summary>
    public const string RevenueIsOverstated = "TEMPEST-BGD-001";

    /// <summary>The opportunity is Won and names no contract.</summary>
    public const string WonOpportunityNeedsContract = "TEMPEST-BGD-002";

    /// <summary>The opportunity is closed and does not say why.</summary>
    public const string ClosedOpportunityNeedsOutcome = "TEMPEST-BGD-003";

    /// <summary>The opportunity has passed qualification with no estimated value.</summary>
    public const string QualifiedOpportunityNeedsValue = "TEMPEST-BGD-004";

    /// <summary>A win probability is outside 0–1.</summary>
    public const string WinProbabilityOutOfRange = "TEMPEST-BGD-005";

    /// <summary>The opportunity is open with no next action planned.</summary>
    public const string OpenOpportunityNeedsNextAction = "TEMPEST-BGD-006";

    /// <summary>The next action is past its own date.</summary>
    public const string NextActionIsOverdue = "TEMPEST-BGD-007";

    /// <summary>Nothing has happened on an open opportunity for a long time.</summary>
    public const string OpportunityIsStale = "TEMPEST-BGD-008";

    /// <summary>An expected decision date has passed and the opportunity is still open.</summary>
    public const string DecisionDateHasPassed = "TEMPEST-BGD-009";

    /// <summary>The opportunity records no interaction at all beyond being identified.</summary>
    public const string NoInteractionsRecorded = "TEMPEST-BGD-010";

    /// <summary>Two opportunities share one reference.</summary>
    public const string DuplicateOpportunityReference = "TEMPEST-BGD-011";
}

/// <summary>Governance of the pipeline itself.</summary>
public interface IOpportunityValidationService : IReferenceValidationService<Opportunity>
{
}

/// <summary>The concrete <see cref="IOpportunityValidationService"/> implementation.</summary>
public sealed class OpportunityValidationService : ReferenceValidationService<Opportunity>, IOpportunityValidationService
{
    /// <summary>How long an open opportunity may go without an interaction before it is reported as stale.</summary>
    public const int StaleAfterDays = 60;

    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="OpportunityValidationService"/> class.</summary>
    /// <param name="catalog">The pipeline whose records this service validates.</param>
    /// <param name="timeProvider">The clock staleness checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public OpportunityValidationService(IOpportunityCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        Opportunity definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Opportunity '{definition.Reference}' ({definition.OrganisationName})";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings, expectEvidence: false);

        if (definition.WinProbability is { } probability && (probability < 0m || probability > 1m))
            errors.Add(Diagnostic(
                PipelineValidationRules.WinProbabilityOutOfRange,
                $"{subject} records a win probability of {probability}, which is not a proportion between 0 and 1."));

        if (definition.OverstatesRevenue)
            errors.Add(Diagnostic(
                PipelineValidationRules.RevenueIsOverstated,
                $"{subject} is at stage {definition.Stage} and records its value as {definition.ValueReality}. Revenue is "
                + "contracted only when an opportunity is Won and names a contract; anything else is a figure somebody promoted "
                + "without the paperwork."));

        if (definition.Stage == PipelineStage.Won && string.IsNullOrWhiteSpace(definition.ContractReference))
            warnings.Add(Diagnostic(
                PipelineValidationRules.WonOpportunityNeedsContract,
                $"{subject} is recorded as Won and names no contract, so nothing ties the win to an obligation the organisation "
                + "has actually taken on."));

        if (PipelineStages.IsClosed(definition.Stage) && string.IsNullOrWhiteSpace(definition.Outcome))
            warnings.Add(Diagnostic(
                PipelineValidationRules.ClosedOpportunityNeedsOutcome,
                $"{subject} is {definition.Stage} and does not say why. A pipeline that never records why work was lost cannot "
                + "be learned from."));

        if (definition.IsOpen && PipelineStages.Order(definition.Stage) >= PipelineStages.Order(PipelineStage.Qualified)
            && definition.EstimatedValue is null)
            warnings.Add(Diagnostic(
                PipelineValidationRules.QualifiedOpportunityNeedsValue,
                $"{subject} has passed qualification with no estimated value, so it cannot be weighed against anything else in "
                + "the pipeline."));

        if (definition.IsOpen && string.IsNullOrWhiteSpace(definition.NextAction))
            warnings.Add(Diagnostic(
                PipelineValidationRules.OpenOpportunityNeedsNextAction,
                $"{subject} is open with nothing planned, which is how an opportunity quietly becomes a lost one."));

        if (definition.NextActionIsOverdueAt(today))
            warnings.Add(Diagnostic(
                PipelineValidationRules.NextActionIsOverdue,
                $"{subject} has a next action due on {definition.NextActionDue:O}: {definition.NextAction}"));

        if (definition.IsStaleAt(today, StaleAfterDays))
            warnings.Add(Diagnostic(
                PipelineValidationRules.OpportunityIsStale,
                $"{subject} is open and nothing has been recorded against it since "
                + (definition.LastInteractionDate is { } last ? $"{last:O}." : "it was created.")));

        if (definition.IsOpen && definition.ExpectedDecisionDate is { } expected && expected < today)
            warnings.Add(Diagnostic(
                PipelineValidationRules.DecisionDateHasPassed,
                $"{subject} was expected to be decided by {expected:O} and is still open. Either the date was optimistic or the "
                + "opportunity has quietly gone away."));

        if (definition.Interactions.Count == 0 && definition.Stage != PipelineStage.Identified)
            warnings.Add(Diagnostic(
                PipelineValidationRules.NoInteractionsRecorded,
                $"{subject} is at stage {definition.Stage} with no interaction recorded, so how it got there is unrecorded."));

        return Task.CompletedTask;
    }
}
