using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Knowledge.Lessons;

/// <summary>A deterministic filter over the lessons database.</summary>
public sealed record LessonQuery
{
    /// <summary>Matches any record whose reference, title, context or lesson contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these categories. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<FailureCategory> Categories { get; init; } = [];

    /// <summary>Matches any of these severities. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<FailureSeverity> Severities { get; init; } = [];

    /// <summary>Matches records applying to this enquiry. <see langword="null"/> to leave every dimension open.</summary>
    public KnowledgeEnquiry? Enquiry { get; init; }

    /// <summary>Matches only records at or below this classification. <see langword="null"/> to match any.</summary>
    public ConfidentialityClassification? MaximumClassification { get; init; }

    /// <summary>Matches only records with, or without, root causes nothing addresses. <see langword="null"/> to match any.</summary>
    public bool? HasUnaddressedRootCauses { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The failure and lessons database.</summary>
public interface ILessonCatalog : IReferenceDataCatalog<LessonRecord>
{
    /// <summary>Returns the record registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<LessonRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered record matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<LessonRecord>>> SearchAsync(LessonQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// The lessons somebody starting this kind of work should read,
    /// most serious first.
    /// </summary>
    /// <remarks>
    /// The query `F4` exists to answer. A lessons database nobody
    /// searches at the right moment has taught nothing, so the lookup is
    /// framed as "what should I know before I start?" rather than as a
    /// browse.
    /// </remarks>
    /// <param name="enquiry">What the reader is about to work on.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="enquiry"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<LessonRecord>>> FindApplicableLessonsAsync(
        KnowledgeEnquiry enquiry,
        CancellationToken cancellationToken = default);

    /// <summary>Every record with a root cause nothing addresses, most serious first. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IReferenceRecord<LessonRecord>>> FindWithUnaddressedRootCausesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ILessonCatalog"/> implementation.</summary>
public sealed class LessonCatalog : ReferenceDataCatalog<LessonRecord>, ILessonCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every lesson record's own backing document carries.</summary>
    public const string LessonDocumentKind = "KnowledgeLessonRecord";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string LessonLibraryName = "KnowledgeLessons";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>lessonId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "KnowledgeLessons.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each record reference to the <c>lessonId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "KnowledgeLessons.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="LessonCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public LessonCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => LessonLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => LessonDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<LessonRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(LessonRecord.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<LessonRecord>>> SearchAsync(
        LessonQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<LessonRecord>>> FindApplicableLessonsAsync(
        KnowledgeEnquiry enquiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(enquiry);

        var applicable = await FilterAsync(
            record => record.Definition.HasLesson
                      && record.Definition.Provenance.IsCurrent
                      && record.Definition.Applicability.AppliesTo(enquiry),
            cancellationToken).ConfigureAwait(false);

        return applicable
            .OrderByDescending(r => (int)r.Definition.Severity)
            .ThenBy(r => r.Definition.Reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<LessonRecord>>> FindWithUnaddressedRootCausesAsync(
        CancellationToken cancellationToken = default)
    {
        var open = await FilterAsync(
            record => record.Definition.UnaddressedRootCauses.Count > 0,
            cancellationToken).ConfigureAwait(false);

        return open
            .OrderByDescending(r => (int)r.Definition.Severity)
            .ThenBy(r => r.Definition.Reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(LessonRecord definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(LessonRecord definition) => $"Lesson reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<LessonRecord> record, LessonQuery query)
    {
        var lesson = record.Definition;

        if (query.TextContains is { } text
            && !lesson.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !lesson.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !lesson.Context.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !(lesson.Lesson?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            return false;

        if (query.Categories.Count > 0 && !query.Categories.Contains(lesson.Category))
            return false;

        if (query.Severities.Count > 0 && !query.Severities.Contains(lesson.Severity))
            return false;

        if (query.Enquiry is { } enquiry && !lesson.Applicability.AppliesTo(enquiry))
            return false;

        if (query.MaximumClassification is { } ceiling && lesson.Classification > ceiling)
            return false;

        if (query.HasUnaddressedRootCauses is { } unaddressed
            && (lesson.UnaddressedRootCauses.Count > 0) != unaddressed)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes F4's validation service reports.</summary>
public static class LessonValidationRules
{
    /// <summary>The record carries no transferable lesson.</summary>
    /// <remarks>
    /// The heaviest finding in `F4`. An incident with no lesson is an
    /// archive entry, and the whole purpose of the database is that the
    /// next project does not repeat this one.
    /// </remarks>
    public const string NoTransferableLesson = "TEMPEST-KNL-001";

    /// <summary>Nobody investigated, and the record states causes anyway.</summary>
    public const string CausesStatedWithoutInvestigation = "TEMPEST-KNL-002";

    /// <summary>The record identifies no root cause.</summary>
    public const string NoRootCauseIdentified = "TEMPEST-KNL-003";

    /// <summary>A root cause nothing addresses.</summary>
    public const string RootCauseIsUnaddressed = "TEMPEST-KNL-004";

    /// <summary>A cause is asserted with nothing anybody can check behind it.</summary>
    public const string CauseIsUnevidenced = "TEMPEST-KNL-005";

    /// <summary>A cause is recorded as established without evidence.</summary>
    public const string EstablishedCauseIsUnevidenced = "TEMPEST-KNL-006";

    /// <summary>A corrective action addresses no cause the record states.</summary>
    public const string ActionAddressesNoStatedCause = "TEMPEST-KNL-007";

    /// <summary>An action is marked effective with nothing showing it worked.</summary>
    public const string EffectivenessIsUnevidenced = "TEMPEST-KNL-008";

    /// <summary>An action was declined without a stated reason.</summary>
    public const string DeclineHasNoReason = "TEMPEST-KNL-009";

    /// <summary>An action names nobody responsible for it.</summary>
    public const string ActionHasNoOwner = "TEMPEST-KNL-010";

    /// <summary>Two causes or actions share one reference.</summary>
    public const string DuplicateLessonReference = "TEMPEST-KNL-011";

    /// <summary>The record says nowhere when its lesson applies.</summary>
    /// <remarks>
    /// A lesson filed under nothing is a lesson nobody finds.
    /// </remarks>
    public const string LessonHasNoApplicability = "TEMPEST-KNL-012";

    /// <summary>A serious failure is classified no more sensitively than internal.</summary>
    public const string SeriousFailureIsLooselyClassified = "TEMPEST-KNL-013";

    /// <summary>The lesson is marked shareable while the record names people, customers or suppliers.</summary>
    public const string ShareableLessonMayIdentifyParties = "TEMPEST-KNL-014";

    /// <summary>The record does not say how badly it went.</summary>
    public const string SeverityNotStated = "TEMPEST-KNL-015";
}

/// <summary>Governance of the lessons database itself.</summary>
public interface ILessonValidationService : IReferenceValidationService<LessonRecord>
{
}

/// <summary>The concrete <see cref="ILessonValidationService"/> implementation.</summary>
/// <remarks>
/// Two concerns run through the findings. Whether the organisation
/// actually learned anything — a lesson stated, root causes found and
/// addressed, effectiveness confirmed. And whether sharing the record
/// would disclose something it should not, because a lessons database is
/// exactly the sort of thing that travels further than intended.
/// </remarks>
public sealed class LessonValidationService : ReferenceValidationService<LessonRecord>, ILessonValidationService
{
    /// <summary>Words suggesting a record names a party it should not, when the lesson is marked shareable.</summary>
    public static IReadOnlyList<string> IdentifyingTerms { get; } =
        ["customer ", "client ", "supplier ", "subcontractor ", " ltd", " limited", " plc", " gmbh", " inc"];

    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="LessonValidationService"/> class.</summary>
    /// <param name="catalog">The lessons database whose records this service validates.</param>
    /// <param name="timeProvider">The clock staleness checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public LessonValidationService(ILessonCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        LessonRecord definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Lesson record '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        EvaluateLearning(definition, subject, errors, warnings);
        EvaluateCauses(definition, subject, errors, warnings);
        EvaluateActions(definition, subject, warnings);
        EvaluateDisclosure(definition, subject, warnings);

        KnowledgeGovernanceValidation.Evaluate(
            definition.Provenance,
            definition.Applicability,
            subject,
            today,
            errors,
            warnings);

        return Task.CompletedTask;
    }

    private static void EvaluateLearning(
        LessonRecord definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (!definition.HasLesson)
            errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                LessonValidationRules.NoTransferableLesson,
                $"{subject} records what happened and states no lesson. An incident with no lesson is an archive entry, "
                + "and the point of the database is that the next project does not repeat this one."));

        if (definition.Severity == FailureSeverity.Unspecified)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                LessonValidationRules.SeverityNotStated,
                $"{subject} does not say how badly it went."));

        if (definition.HasLesson
            && definition.AppliesWhen.Count == 0
            && definition.Applicability.Disciplines.Count == 0
            && definition.Applicability.Topics.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                LessonValidationRules.LessonHasNoApplicability,
                $"{subject} states a lesson and says nowhere when it applies. A lesson filed under nothing is a lesson "
                + "nobody finds."));
    }

    private static void EvaluateCauses(
        LessonRecord definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        KnowledgeGovernanceValidation.EvaluateDuplicateReferences(
            definition.Causes.Select(c => c.Reference),
            $"{subject} has two causes sharing the reference",
            errors);

        if (!definition.WasInvestigated && definition.Causes.Count > 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                LessonValidationRules.CausesStatedWithoutInvestigation,
                $"{subject} states causes and records no investigation, so the causes are somebody's account rather "
                + "than a finding."));

        if (definition.RootCauses.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                LessonValidationRules.NoRootCauseIdentified,
                $"{subject} identifies no root cause, so nothing here prevents a recurrence."));

        foreach (var cause in definition.UnaddressedRootCauses)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                LessonValidationRules.RootCauseIsUnaddressed,
                $"{subject} identifies root cause '{cause.Reference}' and nothing addresses it."));

        foreach (var cause in definition.Causes.Where(c => c.IsEstablished && !c.IsEvidenced))
            errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                LessonValidationRules.EstablishedCauseIsUnevidenced,
                $"{subject} records cause '{cause.Reference}' as established with nothing anybody can check behind it. "
                + "Established means the evidence ruled the alternatives out."));

        foreach (var cause in definition.Causes.Where(c => c.Confidence == CauseConfidence.Probable && !c.IsEvidenced))
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                LessonValidationRules.CauseIsUnevidenced,
                $"{subject} records cause '{cause.Reference}' as probable with no supporting evidence."));
    }

    private static void EvaluateActions(LessonRecord definition, string subject, List<IValidationDiagnostic> warnings)
    {
        var causeReferences = definition.Causes.Select(c => c.Reference).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var action in definition.CorrectiveActions)
        {
            if (action.AddressesCauseReferences.Count > 0
                && !action.AddressesCauseReferences.Any(causeReferences.Contains))
                warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                    LessonValidationRules.ActionAddressesNoStatedCause,
                    $"{subject} action '{action.Reference}' addresses no cause the record states."));

            if (action.State == CorrectiveActionState.VerifiedEffective && !action.IsVerifiedEffective)
                warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                    LessonValidationRules.EffectivenessIsUnevidenced,
                    $"{subject} action '{action.Reference}' is marked effective with nothing showing it worked. "
                    + "Implemented and effective are different things."));

            if (action.IsUnexplainedDecline)
                warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                    LessonValidationRules.DeclineHasNoReason,
                    $"{subject} action '{action.Reference}' was declined without a stated reason."));

            if (action.IsOutstanding && string.IsNullOrWhiteSpace(action.OwnerPrincipalId))
                warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                    LessonValidationRules.ActionHasNoOwner,
                    $"{subject} action '{action.Reference}' is outstanding and names nobody responsible for it."));
        }
    }

    private static void EvaluateDisclosure(LessonRecord definition, string subject, List<IValidationDiagnostic> warnings)
    {
        if (definition.Severity == FailureSeverity.Serious
            && definition.Classification <= ConfidentialityClassification.Internal)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                LessonValidationRules.SeriousFailureIsLooselyClassified,
                $"{subject} records a serious failure classified only {definition.Classification}. Worth confirming "
                + "that is intended."));

        if (!definition.LessonIsShareable || definition.Lesson is not { } lesson)
            return;

        foreach (var term in IdentifyingTerms.Where(t => lesson.Contains(t, StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                LessonValidationRules.ShareableLessonMayIdentifyParties,
                $"{subject} marks its lesson shareable and the lesson text mentions \"{term.Trim()}\". A shareable "
                + "lesson should carry the learning without the parties."));

            break;
        }
    }
}
