using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets.DesignReviews;

/// <summary>A deterministic filter over the design-review library.</summary>
public sealed record DesignReviewQuery
{
    /// <summary>Matches any pack whose reference or subject contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these review kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<DesignReviewKind> Kinds { get; init; } = [];

    /// <summary>Matches any of these outcomes. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReviewOutcome> Outcomes { get; init; } = [];

    /// <summary>Matches only packs with, or without, actions still outstanding. <see langword="null"/> to match any.</summary>
    public bool? HasOutstandingActions { get; init; }

    /// <summary>Matches only packs covering this requirement. <see langword="null"/> to match any.</summary>
    public Guid? RequirementId { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The design-review library.</summary>
public interface IDesignReviewCatalog : IReferenceDataCatalog<DesignReviewPack>
{
    /// <summary>Returns the pack registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<DesignReviewPack>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered pack matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<DesignReviewPack>>> SearchAsync(DesignReviewQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every action nobody has closed, across every review, most
    /// overdue first.
    /// </summary>
    /// <remarks>
    /// The list a project actually needs. Actions live inside their own
    /// review packs, which is where they belong and the last place
    /// anybody looks for them.
    /// </remarks>
    /// <param name="asAt">The date overdue is judged against.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<IReadOnlyList<OutstandingReviewAction>> FindOutstandingActionsAsync(
        DateOnly asAt,
        CancellationToken cancellationToken = default);
}

/// <summary>One unclosed action, and the review it came from.</summary>
/// <param name="ReviewReference">The review pack holding it.</param>
/// <param name="ReviewSubject">What that review was about.</param>
/// <param name="Action">The action itself.</param>
/// <param name="DaysOverdue">How many days past its due date, or <see langword="null"/> where it has none or is not yet due.</param>
public sealed record OutstandingReviewAction(
    string ReviewReference,
    string ReviewSubject,
    ReviewAction Action,
    int? DaysOverdue)
{
    /// <summary>Whether the action is past its own due date.</summary>
    public bool IsOverdue => DaysOverdue is > 0;
}

/// <summary>The concrete <see cref="IDesignReviewCatalog"/> implementation.</summary>
public sealed class DesignReviewCatalog : ReferenceDataCatalog<DesignReviewPack>, IDesignReviewCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every pack's own backing document carries.</summary>
    public const string DesignReviewDocumentKind = "EngineeringDesignReviewPack";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string DesignReviewLibraryName = "EngineeringDesignReviews";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>reviewId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "EngineeringDesignReviews.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each pack reference to the <c>reviewId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "EngineeringDesignReviews.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="DesignReviewCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own packs are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public DesignReviewCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => DesignReviewLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => DesignReviewDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<DesignReviewPack>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(DesignReviewPack.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<DesignReviewPack>>> SearchAsync(
        DesignReviewQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutstandingReviewAction>> FindOutstandingActionsAsync(
        DateOnly asAt,
        CancellationToken cancellationToken = default)
    {
        var packs = await ListAsync(cancellationToken).ConfigureAwait(false);

        return packs
            .SelectMany(record => record.Definition.OutstandingActions.Select(action => new OutstandingReviewAction(
                record.Definition.Reference,
                record.Definition.Subject,
                action,
                action.DueBy is { } due ? asAt.DayNumber - due.DayNumber : null)))
            .OrderByDescending(a => a.DaysOverdue ?? int.MinValue)
            .ThenBy(a => a.ReviewReference, StringComparer.Ordinal)
            .ThenBy(a => a.Action.Reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(DesignReviewPack definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(DesignReviewPack definition) => $"Design review reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<DesignReviewPack> record, DesignReviewQuery query)
    {
        var pack = record.Definition;

        if (query.TextContains is { } text
            && !pack.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !pack.Subject.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Kinds.Count > 0 && !query.Kinds.Contains(pack.Kind))
            return false;

        if (query.Outcomes.Count > 0 && !query.Outcomes.Contains(pack.Outcome))
            return false;

        if (query.HasOutstandingActions is { } outstanding && (pack.OutstandingActions.Count > 0) != outstanding)
            return false;

        if (query.RequirementId is { } requirementId && !pack.RequirementIds.Contains(requirementId))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes E4's validation service reports.</summary>
public static class DesignReviewValidationRules
{
    /// <summary>The pack records no participants, so nobody reviewed anything.</summary>
    public const string ReviewHadNoParticipants = "TEMPEST-EAR-001";

    /// <summary>Nobody other than the people who did the work took part.</summary>
    public const string NoIndependentReviewer = "TEMPEST-EAR-002";

    /// <summary>Two observations, actions or decisions share one reference.</summary>
    public const string DuplicateReviewReference = "TEMPEST-EAR-003";

    /// <summary>An action or decision cites an observation the pack does not hold.</summary>
    public const string ObservationReferenceUnresolved = "TEMPEST-EAR-004";

    /// <summary>An action names nobody who agreed to do it.</summary>
    public const string ActionHasNoOwner = "TEMPEST-EAR-005";

    /// <summary>An action names no date by which it is due.</summary>
    public const string ActionHasNoDueDate = "TEMPEST-EAR-006";

    /// <summary>An action was waived without a stated reason.</summary>
    public const string WaiverHasNoReason = "TEMPEST-EAR-007";

    /// <summary>The review concluded the work may proceed while a blocking observation stands unanswered.</summary>
    public const string ProceedsOverBlockingObservation = "TEMPEST-EAR-008";

    /// <summary>An observation has no action and no decision against it.</summary>
    public const string ObservationIsUnanswered = "TEMPEST-EAR-009";

    /// <summary>The review reached a conclusion but says nothing about why.</summary>
    public const string OutcomeHasNoRationale = "TEMPEST-EAR-010";

    /// <summary>The pack records an approval but names nobody who gave it.</summary>
    public const string ApprovalNotAttributable = "TEMPEST-EAR-011";

    /// <summary>The pack was approved while actions remain outstanding.</summary>
    public const string ApprovedWithOutstandingActions = "TEMPEST-EAR-012";

    /// <summary>The review put nothing before itself — no documents, calculations or verification.</summary>
    public const string ReviewHadNoMaterial = "TEMPEST-EAR-013";

    /// <summary>The review says it was held but carries no date.</summary>
    public const string ReviewHasNoDate = "TEMPEST-EAR-014";

    /// <summary>The pack cites a calculation pack or verification artefact the libraries do not hold.</summary>
    public const string CitedArtefactMustResolve = "TEMPEST-EAR-015";
}

/// <summary>Governance of the design-review library itself.</summary>
public interface IDesignReviewValidationService : IReferenceValidationService<DesignReviewPack>
{
}

/// <summary>The concrete <see cref="IDesignReviewValidationService"/> implementation.</summary>
/// <remarks>
/// The findings are about whether the review can be relied on as a
/// record. A review with no independent participant, an action with no
/// owner, a critical observation nothing answers — each produces a pack
/// that looks like governance and provides none.
/// </remarks>
public sealed class DesignReviewValidationService
    : ReferenceValidationService<DesignReviewPack>, IDesignReviewValidationService
{
    private readonly CalculationPacks.ICalculationPackCatalog? _calculationPacks;
    private readonly Verification.IVerificationArtefactCatalog? _verificationArtefacts;

    /// <summary>Initialises a new instance of the <see cref="DesignReviewValidationService"/> class.</summary>
    /// <param name="catalog">The review library whose records this service validates.</param>
    /// <param name="calculationPacks">The `E2` library, for confirming cited packs exist. Optional.</param>
    /// <param name="verificationArtefacts">The `E3` library, for confirming cited artefacts exist. Optional.</param>
    public DesignReviewValidationService(
        IDesignReviewCatalog catalog,
        CalculationPacks.ICalculationPackCatalog? calculationPacks = null,
        Verification.IVerificationArtefactCatalog? verificationArtefacts = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _calculationPacks = calculationPacks;
        _verificationArtefacts = verificationArtefacts;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        DesignReviewPack definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Design review '{definition.Reference}'";

        EvaluateParticipation(definition, subject, errors, warnings);
        EvaluateReferences(definition, subject, errors, warnings);
        EvaluateActions(definition, subject, warnings);
        EvaluateOutcome(definition, subject, errors, warnings);

        AssetGovernanceValidation.Evaluate(definition.Governance, subject, errors, warnings);

        await EvaluateCitedArtefactsAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private static void EvaluateParticipation(
        DesignReviewPack definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (!definition.WasAttended)
            errors.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.ReviewHadNoParticipants,
                $"{subject} records nobody as having attended. A review nobody attended did not happen."));

        else if (!definition.HasIndependentReviewer)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.NoIndependentReviewer,
                $"{subject} was attended only by the people presenting the work. Often unavoidable in a small team; "
                + "recorded so the record never implies otherwise."));

        if (definition.HeldOn is null && definition.Outcome != ReviewOutcome.NotConcluded)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.ReviewHasNoDate,
                $"{subject} reached a conclusion but carries no date."));

        if (definition.DocumentIds.Count == 0
            && definition.CalculationPackReferences.Count == 0
            && definition.VerificationArtefactReferences.Count == 0
            && definition.RequirementIds.Count == 0)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.ReviewHadNoMaterial,
                $"{subject} records no requirements, documents, calculations or verification put before it."));
    }

    private static void EvaluateReferences(
        DesignReviewPack definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        AssetGovernanceValidation.EvaluateDuplicateReferences(
            definition.Observations.Select(o => o.Reference),
            $"{subject} has two observations sharing the reference",
            errors);

        AssetGovernanceValidation.EvaluateDuplicateReferences(
            definition.Actions.Select(a => a.Reference),
            $"{subject} has two actions sharing the reference",
            errors);

        AssetGovernanceValidation.EvaluateDuplicateReferences(
            definition.Decisions.Select(d => d.Reference),
            $"{subject} has two decisions sharing the reference",
            errors);

        var observationReferences = definition.Observations
            .Select(o => o.Reference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (owner, cited) in definition.Actions
                     .SelectMany(a => a.ObservationReferences.Select(r => (Owner: $"action '{a.Reference}'", Cited: r)))
                     .Concat(definition.Decisions
                         .SelectMany(d => d.ObservationReferences.Select(r => (Owner: $"decision '{d.Reference}'", Cited: r))))
                     .Where(pair => !observationReferences.Contains(pair.Cited)))
            errors.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.ObservationReferenceUnresolved,
                $"{subject} {owner} cites observation '{cited}', which the pack does not hold."));

        foreach (var observation in definition.UnansweredObservations.Where(o =>
                     o.Severity is ObservationSeverity.Major or ObservationSeverity.Critical))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.ObservationIsUnanswered,
                $"{subject} observation '{observation.Reference}' is {observation.Severity} and has no action or "
                + "decision against it."));
    }

    private static void EvaluateActions(DesignReviewPack definition, string subject, List<IValidationDiagnostic> warnings)
    {
        foreach (var action in definition.UnownedActions)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.ActionHasNoOwner,
                $"{subject} action '{action.Reference}' names nobody who agreed to do it. An unowned action is a wish."));

        foreach (var action in definition.Actions.Where(a => a.IsOutstanding && a.DueBy is null))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.ActionHasNoDueDate,
                $"{subject} action '{action.Reference}' is outstanding with no date by which it is due."));

        foreach (var action in definition.Actions.Where(a => a.IsUnexplainedWaiver))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.WaiverHasNoReason,
                $"{subject} action '{action.Reference}' was waived without a stated reason."));
    }

    private static void EvaluateOutcome(
        DesignReviewPack definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (definition.ProceedsOverBlockingObservations)
            errors.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.ProceedsOverBlockingObservation,
                $"{subject} concluded that the work may proceed while a critical observation stands with no action and "
                + "no decision against it. Accepting a critical finding is a decision somebody must be recorded as taking."));

        if (definition.Outcome != ReviewOutcome.NotConcluded && string.IsNullOrWhiteSpace(definition.OutcomeRationale))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.OutcomeHasNoRationale,
                $"{subject} concluded {definition.Outcome} and says nothing about why."));

        if (definition.Approval is not null && definition.OutstandingActions.Count > 0)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                DesignReviewValidationRules.ApprovedWithOutstandingActions,
                $"{subject} was approved with {definition.OutstandingActions.Count} action(s) still open. Legitimate "
                + "where the approver knew; worth confirming they did."));
    }

    private async Task EvaluateCitedArtefactsAsync(
        DesignReviewPack definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_calculationPacks is not null)
        {
            foreach (var reference in definition.CalculationPackReferences)
            {
                var pack = await _calculationPacks.FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false);

                if (pack is null)
                    warnings.Add(AssetGovernanceValidation.Diagnostic(
                        DesignReviewValidationRules.CitedArtefactMustResolve,
                        $"{subject} cites calculation pack '{reference}', which the library does not hold."));
            }
        }

        if (_verificationArtefacts is null)
            return;

        foreach (var reference in definition.VerificationArtefactReferences)
        {
            var artefact = await _verificationArtefacts.FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false);

            if (artefact is null)
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    DesignReviewValidationRules.CitedArtefactMustResolve,
                    $"{subject} cites verification artefact '{reference}', which the library does not hold."));
        }
    }
}
