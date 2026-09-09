using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringAssets;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Knowledge;
using Tempest.Core.Knowledge.Academy;
using Tempest.Core.Knowledge.Challenges;
using Tempest.Core.Knowledge.Lessons;
using Tempest.Core.Knowledge.Prompts;
using Tempest.Core.Knowledge.WorkedExamples;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.EngineeringIntelligence;
using Tempest.Core.Tests.ReferenceData;

namespace Tempest.Core.Tests.Knowledge;

/// <summary>
/// Shared construction for the `P06` test suite.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every value here is fictional.</b> No real lesson, prompt,
/// challenge, worked example or failure appears anywhere in this suite,
/// and no citation names a real work. The fixture organisation is
/// "TestFixture Engineering"; failures are attributed to nobody real.
/// </para>
/// <para>
/// Note the asymmetry with the other suites: fixture content here is
/// built with <see cref="KnowledgeProvenance.Fictional"/> wherever the
/// test is about behaviour, so it can never be mistaken for knowledge.
/// Tests that need <em>governable</em> content use
/// <see cref="Reviewed"/>, which is authored fixture content a fictional
/// reviewer has checked — still fiction, and still not registered
/// anywhere at run time.
/// </para>
/// </remarks>
internal static class KnowledgeFixtures
{
    /// <summary>A fixed date, so a record's own dating is asserted rather than tolerated.</summary>
    public static DateOnly Today { get; } = new(2026, 3, 1);

    /// <summary>A clock pinned to <see cref="Today"/>.</summary>
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    /// <summary>Reference-data provenance a named reviewer has verified — the only kind that can reach Released.</summary>
    public static ReferenceProvenance Verified() => new(
        SourceOrganisation: "TestFixture Engineering",
        SourceDocument: "Fixture knowledge record (not a real document)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Fixture",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data. Not engineering knowledge.")
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    /// <summary>Registers a record under a caller-chosen Id.</summary>
    public static Task<IReferenceRecord<TDefinition>> RegisterAsync<TDefinition>(
        ReferenceDataCatalog<TDefinition> catalog,
        string recordId,
        TDefinition definition)
        where TDefinition : class =>
        catalog.RegisterAsync(recordId, definition, Verified());

    /// <summary>Walks a record through the full lifecycle to Released.</summary>
    public static async Task<IReferenceRecord<TDefinition>> ReleaseAsync<TDefinition>(
        ReferenceDataCatalog<TDefinition> catalog,
        string recordId)
        where TDefinition : class
    {
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Released, "Released.");
    }

    /// <summary>Registers a record and walks it straight through to Released.</summary>
    public static async Task<IReferenceRecord<TDefinition>> RegisterReleasedAsync<TDefinition>(
        ReferenceDataCatalog<TDefinition> catalog,
        string recordId,
        TDefinition definition)
        where TDefinition : class
    {
        await RegisterAsync(catalog, recordId, definition);
        return await ReleaseAsync(catalog, recordId);
    }

    /// <summary>Knowledge provenance for authored fixture content a fictional reviewer has checked.</summary>
    public static KnowledgeProvenance Reviewed(
        KnowledgeOrigin origin = KnowledgeOrigin.Authored,
        string author = "engineer-1",
        string? reviewer = "reviewer-2") => new()
    {
        Origin = origin,
        ReviewState = reviewer is null ? KnowledgeReviewState.Unreviewed : KnowledgeReviewState.Reviewed,
        AuthoredByPrincipalId = author,
        AuthoredOn = Today.AddMonths(-2),
        ReviewedByPrincipalId = reviewer,
        ReviewedOn = reviewer is null ? null : Today.AddMonths(-1),
        Citations = KnowledgeOrigins.RequiresCitation(origin) ? [Citation()] : [],
    };

    /// <summary>A citation specific enough to be findable — and entirely invented.</summary>
    public static KnowledgeCitation Citation() => new(
        "Fictional fixture handbook, invented for testing.",
        Author: "A. Fictional",
        Title: "Notional Handbook of Fixture Engineering",
        Identifier: "FIX-ISBN-0000000000",
        Edition: "1st",
        Year: 2026,
        Locator: "p. 1");

    /// <summary>Applicability naming a discipline and a level.</summary>
    public static KnowledgeApplicability Applicability(
        EngineeringDiscipline discipline = EngineeringDiscipline.Mechanical,
        KnowledgeLevel level = KnowledgeLevel.Intermediate) => new()
    {
        Disciplines = [discipline],
        Topics = ["fixture-topic"],
        Level = level,
        Audiences = [KnowledgeAudience.Learner, KnowledgeAudience.PractisingEngineer],
    };

    // ---- F1 -----------------------------------------------------------

    public static PromptCatalog BuildPromptCatalog() => Build((d, p) => new PromptCatalog(d, p));

    public static PromptRecord Prompt(string reference = "PR-1", PromptPurpose purpose = PromptPurpose.Explanation) => new()
    {
        Reference = reference,
        Name = "Fictional explanation prompt",
        Instruction = "Explain the concept named in {topic} to a reader at {level}, in plain words.",
        Purpose = purpose,
        TaskDescription = "Produces a short explanation for a fixture learner.",
        Inputs =
        [
            new PromptSlot("{topic}", "The concept to explain.", IsRequired: true),
            new PromptSlot("{level}", "How much the reader already knows.", IsRequired: true),
        ],
        ExpectedOutputs = [new PromptSlot("explanation", "A short explanation.")],
        Constraints = [new PromptConstraint("Must not state a numerical engineering value without a source.", IsSafetyConstraint: true)],
        HumanReviewGuidance = "An engineer must check every value and claim before the explanation is used.",
        KnownFailureModes = ["Invents plausible numbers when the topic is unfamiliar."],
        Applicability = Applicability(),
        Provenance = Reviewed(),
    };

    // ---- F2 -----------------------------------------------------------

    public static AcademyCatalog BuildAcademyCatalog() => Build((d, p) => new AcademyCatalog(d, p));

    public static AcademyNode Node(
        string reference = "LES-1",
        AcademyNodeKind kind = AcademyNodeKind.Lesson,
        string? parent = null) => new()
    {
        Reference = reference,
        Title = "Fictional lesson on a fixture topic",
        Kind = kind,
        ParentReference = parent,
        Summary = "Invented for testing. Teaches nothing real.",
        Outcomes = [new LearningOutcome("LO-1", "Can do the fixture thing.", ["ACT-1"])],
        Activities =
        [
            new AcademyActivity("ACT-1", "Fixture exercise", AcademyActivityKind.Problem, ["LO-1"], EstimatedMinutes: 30),
        ],
        Applicability = Applicability(),
        Provenance = Reviewed(),
    };

    // ---- F3 -----------------------------------------------------------

    public static ChallengeCatalog BuildChallengeCatalog() => Build((d, p) => new ChallengeCatalog(d, p));

    public static EngineeringChallenge Challenge(
        string reference = "CH-1",
        ChallengeKind kind = ChallengeKind.WhatIf,
        ChallengeDifficulty difficulty = ChallengeDifficulty.Moderate) => new()
    {
        Reference = reference,
        Title = "Fictional what-if",
        Scenario = "An invented fixture bracket carries an invented load.",
        Question = "What happens if the load doubles?",
        Kind = kind,
        Difficulty = difficulty,
        ReasoningAreas =
        [
            new ReasoningArea("RA-1", "Whether the failure mode changes.", IsEssential: true),
            new ReasoningArea("RA-2", "Whether deflection now governs."),
        ],
        Guidance = new ChallengeGuidance(
            "Notices that the governing mode may change rather than simply doubling the stress.",
            CommonMistakes: ["Doubles the stress and stops."]),
        Applicability = Applicability(),
        Provenance = Reviewed(),
    };

    // ---- F4 -----------------------------------------------------------

    public static LessonCatalog BuildLessonCatalog() => Build((d, p) => new LessonCatalog(d, p));

    public static LessonRecord Lesson(
        string reference = "LR-1",
        FailureSeverity severity = FailureSeverity.Minor,
        bool addressed = true) => new()
    {
        Reference = reference,
        Title = "Fictional fixture failure",
        Context = "An invented project, on an invented part. Nothing here happened.",
        ObservedProblem = "The invented part did not fit the invented housing.",
        Consequence = "Invented rework.",
        Category = FailureCategory.Design,
        Severity = severity,
        OccurredOn = Today.AddMonths(-6),
        InvestigatedByPrincipalId = "engineer-1",
        Causes =
        [
            new FailureCause(
                "C-1",
                "The invented tolerance stack was never checked.",
                IsRootCause: true,
                Confidence: CauseConfidence.Probable,
                Evidence: [new EngineeringEvidence(EngineeringEvidenceKind.InternalRecord, "Fixture note.", Reference: "FIX-N-1")]),
        ],
        CorrectiveActions = addressed
            ?
            [
                new CorrectiveAction(
                    "CA-1",
                    "Add a tolerance stack step to the fixture checklist.",
                    CorrectiveActionState.VerifiedEffective,
                    "engineer-1",
                    ["C-1"],
                    [new EngineeringEvidence(EngineeringEvidenceKind.InternalRecord, "Fixture checklist.", Reference: "FIX-C-1")]),
            ]
            : [],
        Lesson = "Check the tolerance stack before releasing a mating pair.",
        AppliesWhen = ["Any invented mating pair."],
        Classification = ConfidentialityClassification.Confidential,
        Applicability = Applicability(),
        Provenance = Reviewed(KnowledgeOrigin.OrganisationalExperience),
    };

    // ---- F5 -----------------------------------------------------------

    public static WorkedExampleCatalog BuildWorkedExampleCatalog() => Build((d, p) => new WorkedExampleCatalog(d, p));

    public static WorkedExample Example(string reference = "WE-1") => new()
    {
        Reference = reference,
        Title = "Fictional bracket stress, worked through",
        ProblemStatement = "An invented bracket carries an invented load. Find the bending stress.",
        Inputs =
        [
            new WorkedValue("F", "Applied load", "1200", "N"),
            new WorkedValue("Z", "Section modulus", "4.2e-6", "m^3"),
        ],
        Assumptions = ["The load is static.", "The section is uniform."],
        MethodSummary = "Simple bending.",
        Steps =
        [
            new WorkedStep("S1", WorkedStepKind.Setup, "Identify the governing section.", "It is where the moment is greatest."),
            new WorkedStep("S2", WorkedStepKind.Calculation, "Compute the bending stress.", "sigma = M / Z is the standard relation.", "M / Z", "48 MPa"),
            new WorkedStep("S3", WorkedStepKind.Interpretation, "Compare with the allowable.", "A stress means nothing without something to compare it to."),
        ],
        Result = new WorkedValue("sigma", "Bending stress", "48", "MPa"),
        Interpretation = "Comfortably below the invented allowable; deflection will govern before stress does.",
        Verification = "Checked against an invented hand estimate.",
        TeachingPoints = ["Compare against the allowable, not against zero."],
        CommonMistakes = ["Forgetting that Z, not I, belongs in this expression."],
        Applicability = Applicability(),
        Provenance = Reviewed(),
    };

    private static TCatalog Build<TCatalog>(Func<EngineeringDocumentStore, InMemoryPersistenceStore, TCatalog> create)
    {
        var persistence = new InMemoryPersistenceStore();

        return create(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }
}
