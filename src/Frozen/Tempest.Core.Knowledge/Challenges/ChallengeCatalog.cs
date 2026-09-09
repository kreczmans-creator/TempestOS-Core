using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Knowledge.Challenges;

/// <summary>A deterministic filter over the challenge library.</summary>
public sealed record ChallengeQuery
{
    /// <summary>Matches any challenge whose reference, title, scenario or question contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ChallengeKind> Kinds { get; init; } = [];

    /// <summary>Matches any of these difficulties. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ChallengeDifficulty> Difficulties { get; init; } = [];

    /// <summary>Matches challenges applying to this enquiry. <see langword="null"/> to leave every dimension open.</summary>
    public KnowledgeEnquiry? Enquiry { get; init; }

    /// <summary>Matches only challenges somebody could actually mark. <see langword="null"/> to match any.</summary>
    public bool? IsMarkable { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The engineering challenge library.</summary>
/// <remarks>
/// A library, not a tutor. Nothing here evaluates a response, scores one
/// or sequences challenges adaptively (`ADR-0141`).
/// </remarks>
public interface IChallengeCatalog : IReferenceDataCatalog<EngineeringChallenge>
{
    /// <summary>Returns the challenge registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<EngineeringChallenge>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered challenge matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<EngineeringChallenge>>> SearchAsync(ChallengeQuery query, CancellationToken cancellationToken = default);

    /// <summary>Every challenge listing <paramref name="nodeReference"/> as a prerequisite. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="nodeReference"/> is null, empty, or whitespace.</exception>
    Task<IReadOnlyList<IReferenceRecord<EngineeringChallenge>>> FindForAcademyNodeAsync(string nodeReference, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IChallengeCatalog"/> implementation.</summary>
public sealed class ChallengeCatalog : ReferenceDataCatalog<EngineeringChallenge>, IChallengeCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every challenge's own backing document carries.</summary>
    public const string ChallengeDocumentKind = "KnowledgeChallenge";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string ChallengeLibraryName = "KnowledgeChallenges";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>challengeId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "KnowledgeChallenges.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each challenge reference to the <c>challengeId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "KnowledgeChallenges.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="ChallengeCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own challenges are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public ChallengeCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => ChallengeLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => ChallengeDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<EngineeringChallenge>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(EngineeringChallenge.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<EngineeringChallenge>>> SearchAsync(
        ChallengeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<EngineeringChallenge>>> FindForAcademyNodeAsync(
        string nodeReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeReference);

        var node = nodeReference.Trim();

        return FilterAsync(
            record => record.Definition.PrerequisiteNodeReferences.Contains(node, StringComparer.OrdinalIgnoreCase),
            cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(EngineeringChallenge definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(EngineeringChallenge definition) => $"Challenge reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<EngineeringChallenge> record, ChallengeQuery query)
    {
        var challenge = record.Definition;

        if (query.TextContains is { } text
            && !challenge.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !challenge.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !challenge.Scenario.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !challenge.Question.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Kinds.Count > 0 && !query.Kinds.Contains(challenge.Kind))
            return false;

        if (query.Difficulties.Count > 0 && !query.Difficulties.Contains(challenge.Difficulty))
            return false;

        if (query.Enquiry is { } enquiry && !challenge.Applicability.AppliesTo(enquiry))
            return false;

        if (query.IsMarkable is { } markable && challenge.IsMarkable != markable)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes F3's validation service reports.</summary>
public static class ChallengeValidationRules
{
    /// <summary>The challenge says what a good response engages with nowhere.</summary>
    public const string NoReasoningAreas = "TEMPEST-KNC-001";

    /// <summary>Two reasoning areas share one reference.</summary>
    public const string DuplicateReasoningArea = "TEMPEST-KNC-002";

    /// <summary>Nobody has written guidance for marking the challenge.</summary>
    public const string NoMarkingGuidance = "TEMPEST-KNC-003";

    /// <summary>An open-ended challenge's guidance admits no alternative answers.</summary>
    /// <remarks>
    /// A design challenge with one accepted answer is a problem
    /// mislabelled, and marking it as though there were one right answer
    /// teaches the wrong lesson.
    /// </remarks>
    public const string OpenChallengeAdmitsNoAlternatives = "TEMPEST-KNC-004";

    /// <summary>The challenge does not say how demanding it is.</summary>
    public const string DifficultyNotStated = "TEMPEST-KNC-005";

    /// <summary>The challenge names a prerequisite the Academy does not hold.</summary>
    public const string PrerequisiteMustResolve = "TEMPEST-KNC-006";

    /// <summary>The challenge cites a worked example the library does not hold.</summary>
    public const string WorkedExampleMustResolve = "TEMPEST-KNC-007";

    /// <summary>The challenge withholds information without saying it did so deliberately.</summary>
    public const string OmissionsNotDeclared = "TEMPEST-KNC-008";

    /// <summary>No reasoning area is marked essential, so no response can miss the point.</summary>
    public const string NoEssentialReasoningArea = "TEMPEST-KNC-009";
}

/// <summary>Governance of the challenge library itself.</summary>
public interface IChallengeValidationService : IReferenceValidationService<EngineeringChallenge>
{
}

/// <summary>The concrete <see cref="IChallengeValidationService"/> implementation.</summary>
/// <remarks>
/// The findings are about whether a challenge can be set and marked
/// honestly. A challenge nobody can mark, or an open design problem whose
/// guidance admits only one answer, teaches something worse than nothing.
/// </remarks>
public sealed class ChallengeValidationService : ReferenceValidationService<EngineeringChallenge>, IChallengeValidationService
{
    private readonly Academy.IAcademyCatalog? _academy;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ChallengeValidationService"/> class.</summary>
    /// <param name="catalog">The challenge library whose records this service validates.</param>
    /// <param name="academy">The Academy, for confirming named prerequisites exist. Optional.</param>
    /// <param name="timeProvider">The clock staleness checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public ChallengeValidationService(
        IChallengeCatalog catalog,
        Academy.IAcademyCatalog? academy = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _academy = academy;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        EngineeringChallenge definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Challenge '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.ReasoningAreas.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                ChallengeValidationRules.NoReasoningAreas,
                $"{subject} says nowhere what a good response engages with, so nobody can tell a good answer from a "
                + "confident one."));

        KnowledgeGovernanceValidation.EvaluateDuplicateReferences(
            definition.ReasoningAreas.Select(a => a.Reference),
            $"{subject} has two reasoning areas sharing the reference",
            errors);

        if (definition.ReasoningAreas.Count > 0 && definition.EssentialReasoningAreas.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                ChallengeValidationRules.NoEssentialReasoningArea,
                $"{subject} marks no reasoning area essential, so no response can be said to have missed the point."));

        if (!definition.IsMarkable)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                ChallengeValidationRules.NoMarkingGuidance,
                $"{subject} carries no guidance for whoever marks it."));

        else if (definition.IsOpenEnded && !definition.Guidance!.AdmitsAlternatives)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                ChallengeValidationRules.OpenChallengeAdmitsNoAlternatives,
                $"{subject} is open-ended and its guidance names no acceptable alternative. An open design problem "
                + "with one accepted answer is a problem mislabelled."));

        if (definition.Difficulty == ChallengeDifficulty.Unspecified)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                ChallengeValidationRules.DifficultyNotStated,
                $"{subject} does not say how demanding it is, so it cannot be matched to a reader."));

        if (definition.Kind == ChallengeKind.Estimation && definition.DeliberateOmissions.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                ChallengeValidationRules.OmissionsNotDeclared,
                $"{subject} is an estimation challenge and declares nothing deliberately withheld. Whether information "
                + "is missing on purpose is exactly what the responder is being tested on."));

        await EvaluatePrerequisitesAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);

        KnowledgeGovernanceValidation.Evaluate(
            definition.Provenance,
            definition.Applicability,
            subject,
            today,
            errors,
            warnings);
    }

    private async Task EvaluatePrerequisitesAsync(
        EngineeringChallenge definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_academy is null)
            return;

        foreach (var reference in definition.PrerequisiteNodeReferences)
        {
            var node = await _academy.FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false);

            if (node is null)
                warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                    ChallengeValidationRules.PrerequisiteMustResolve,
                    $"{subject} requires Academy node '{reference}' first, which the Academy does not hold."));
        }
    }
}
