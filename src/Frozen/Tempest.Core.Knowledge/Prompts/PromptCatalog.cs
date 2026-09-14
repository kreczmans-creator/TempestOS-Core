using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Knowledge.Prompts;

/// <summary>A deterministic filter over the prompt library.</summary>
public sealed record PromptQuery
{
    /// <summary>Matches any prompt whose reference, name or task contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these purposes. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<PromptPurpose> Purposes { get; init; } = [];

    /// <summary>Matches prompts applying to this enquiry. <see langword="null"/> to leave every dimension open.</summary>
    public KnowledgeEnquiry? Enquiry { get; init; }

    /// <summary>Matches only prompts a person has reviewed and found sound. <see langword="null"/> to match any.</summary>
    public bool? IsAuthoritative { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The prompt library.</summary>
/// <remarks>
/// A library, not a runtime. Nothing here executes a prompt, binds a
/// model or reaches a provider (`ADR-0140`).
/// </remarks>
public interface IPromptCatalog : IReferenceDataCatalog<PromptRecord>
{
    /// <summary>Returns the prompt registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<PromptRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered prompt matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<PromptRecord>>> SearchAsync(PromptQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IPromptCatalog"/> implementation.</summary>
public sealed class PromptCatalog : ReferenceDataCatalog<PromptRecord>, IPromptCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every prompt's own backing document carries.</summary>
    public const string PromptDocumentKind = "KnowledgePrompt";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string PromptLibraryName = "KnowledgePrompts";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>promptId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "KnowledgePrompts.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each prompt reference to the <c>promptId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "KnowledgePrompts.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="PromptCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own prompts are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public PromptCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => PromptLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => PromptDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<PromptRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(PromptRecord.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<PromptRecord>>> SearchAsync(
        PromptQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(PromptRecord definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(PromptRecord definition) => $"Prompt reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<PromptRecord> record, PromptQuery query)
    {
        var prompt = record.Definition;

        if (query.TextContains is { } text
            && !prompt.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !prompt.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !(prompt.TaskDescription?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            return false;

        if (query.Purposes.Count > 0 && !query.Purposes.Contains(prompt.Purpose))
            return false;

        if (query.Enquiry is { } enquiry && !prompt.Applicability.AppliesTo(enquiry))
            return false;

        if (query.IsAuthoritative is { } authoritative && prompt.Provenance.IsAuthoritative != authoritative)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes F1's validation service reports.</summary>
public static class PromptValidationRules
{
    /// <summary>The prompt carries no instruction anybody could act on.</summary>
    public const string InstructionIsEmpty = "TEMPEST-KNP-001";

    /// <summary>The prompt does not say what it is for.</summary>
    public const string PurposeNotStated = "TEMPEST-KNP-002";

    /// <summary>The prompt does not say what it is meant to produce.</summary>
    public const string ExpectedOutputNotStated = "TEMPEST-KNP-003";

    /// <summary>The prompt does not say what a person must check before relying on the output.</summary>
    /// <remarks>
    /// An error. Every prompt in an engineering context produces something
    /// a person must check, and one that does not say what checking looks
    /// like is an invitation to skip it.
    /// </remarks>
    public const string HumanReviewGuidanceMissing = "TEMPEST-KNP-004";

    /// <summary>Two input or output slots share one reference.</summary>
    public const string DuplicateSlotReference = "TEMPEST-KNP-005";

    /// <summary>An input slot the prompt's instruction never mentions.</summary>
    public const string SlotIsUnused = "TEMPEST-KNP-006";

    /// <summary>A review or checking prompt states no safety constraint.</summary>
    public const string NoSafetyConstraint = "TEMPEST-KNP-007";

    /// <summary>The prompt records no way it is known to go wrong.</summary>
    public const string NoKnownFailureModes = "TEMPEST-KNP-008";

    /// <summary>The prompt asks for something it should not — a decision, an approval, a certification.</summary>
    public const string PromptSeeksAnAuthorityAct = "TEMPEST-KNP-009";
}

/// <summary>Governance of the prompt library itself.</summary>
public interface IPromptValidationService : IReferenceValidationService<PromptRecord>
{
}

/// <summary>The concrete <see cref="IPromptValidationService"/> implementation.</summary>
/// <remarks>
/// Two findings carry the weight. A prompt with no human-review guidance
/// is an error, because the guidance is the thing that keeps generated
/// output out of engineering decisions unchecked. And a prompt whose
/// instruction asks a machine to approve, certify or sign off is
/// reported, because asking is the first step to somebody acting on the
/// answer.
/// </remarks>
public sealed class PromptValidationService : ReferenceValidationService<PromptRecord>, IPromptValidationService
{
    /// <summary>Words that, in a prompt's instruction, ask for an act of authority.</summary>
    public static IReadOnlyList<string> AuthoritySeekingTerms { get; } =
        ["approve", "sign off", "sign-off", "certify", "authorise", "authorize", "guarantee", "warrant that"];

    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="PromptValidationService"/> class.</summary>
    /// <param name="catalog">The prompt library whose records this service validates.</param>
    /// <param name="timeProvider">The clock staleness checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public PromptValidationService(IPromptCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        PromptRecord definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Prompt '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (string.IsNullOrWhiteSpace(definition.Instruction))
            errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                PromptValidationRules.InstructionIsEmpty,
                $"{subject} carries no instruction."));

        if (definition.Purpose == PromptPurpose.Unspecified)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                PromptValidationRules.PurposeNotStated,
                $"{subject} does not say what it is for."));

        if (!definition.StatesExpectedOutput)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                PromptValidationRules.ExpectedOutputNotStated,
                $"{subject} does not say what it is meant to produce, so nobody can tell whether it worked."));

        if (!definition.StatesHumanReview)
            errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                PromptValidationRules.HumanReviewGuidanceMissing,
                $"{subject} does not say what a person must check before relying on the output. Every prompt in an "
                + "engineering context produces something somebody has to check."));

        KnowledgeGovernanceValidation.EvaluateDuplicateReferences(
            definition.Inputs.Select(i => i.Reference).Concat(definition.ExpectedOutputs.Select(o => o.Reference)),
            $"{subject} has two slots sharing the reference",
            errors);

        foreach (var slot in definition.Inputs.Where(i =>
                     !definition.Instruction.Contains(i.Reference, StringComparison.OrdinalIgnoreCase)))
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                PromptValidationRules.SlotIsUnused,
                $"{subject} declares input '{slot.Reference}' that its instruction never mentions."));

        if (definition.Purpose is PromptPurpose.Review or PromptPurpose.Checking && definition.SafetyConstraints.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                PromptValidationRules.NoSafetyConstraint,
                $"{subject} reviews or checks engineering work and states no safety constraint on what it may conclude."));

        if (definition.KnownFailureModes.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                PromptValidationRules.NoKnownFailureModes,
                $"{subject} records no way it is known to go wrong. Every prompt that has been used has some."));

        foreach (var term in AuthoritySeekingTerms.Where(t =>
                     definition.Instruction.Contains(t, StringComparison.OrdinalIgnoreCase)))
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                PromptValidationRules.PromptSeeksAnAuthorityAct,
                $"{subject} asks for something to \"{term}\". Approving, certifying and signing off are acts of human "
                + "authority; a prompt may ask for an assessment and must not ask for the act."));

        KnowledgeGovernanceValidation.Evaluate(
            definition.Provenance,
            definition.Applicability,
            subject,
            today,
            errors,
            warnings);

        return Task.CompletedTask;
    }
}
