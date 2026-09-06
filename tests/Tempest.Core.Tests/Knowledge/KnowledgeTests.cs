using System.Reflection;
using System.Text.Json;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringAssets;
using Tempest.Core.Knowledge;
using Tempest.Core.Knowledge.Academy;
using Tempest.Core.Knowledge.Challenges;
using Tempest.Core.Knowledge.Lessons;
using Tempest.Core.Knowledge.Prompts;
using Tempest.Core.Knowledge.WorkedExamples;
using Tempest.Core.ReferenceData;
using Xunit;

namespace Tempest.Core.Tests.Knowledge;

/// <summary>The shared core — where content came from, and what that permits.</summary>
public sealed class KnowledgeProvenanceTests
{
    [Fact]
    public void Fiction_can_never_become_authoritative_however_it_is_reviewed()
    {
        var reviewedFiction = KnowledgeProvenance.Fictional with
        {
            ReviewState = KnowledgeReviewState.Reviewed,
            ReviewedByPrincipalId = "reviewer-2",
        };

        Assert.True(reviewedFiction.IsReviewed);
        Assert.False(reviewedFiction.IsAuthoritative);
        Assert.False(KnowledgeOrigins.CanBecomeAuthoritative(KnowledgeOrigin.FictionalFixture));
    }

    [Fact]
    public void Content_that_does_not_say_where_it_came_from_can_never_be_authoritative()
    {
        var unstated = new KnowledgeProvenance
        {
            Origin = KnowledgeOrigin.Unspecified,
            ReviewState = KnowledgeReviewState.Reviewed,
            ReviewedByPrincipalId = "reviewer-2",
        };

        Assert.False(unstated.IsAuthoritative);
    }

    [Fact]
    public void Machine_generated_content_becomes_authoritative_only_once_a_person_reviews_it()
    {
        var generated = new KnowledgeProvenance
        {
            Origin = KnowledgeOrigin.MachineGenerated,
            AuthoredByPrincipalId = "engineer-1",
        };

        Assert.False(generated.IsAuthoritative);

        var reviewed = generated with
        {
            ReviewState = KnowledgeReviewState.Reviewed,
            ReviewedByPrincipalId = "reviewer-2",
        };

        // The review is what makes it knowledge, not the generation.
        Assert.True(reviewed.IsAuthoritative);
    }

    [Fact]
    public void External_content_citing_nothing_findable_is_not_authoritative()
    {
        var uncited = new KnowledgeProvenance
        {
            Origin = KnowledgeOrigin.PublishedReference,
            ReviewState = KnowledgeReviewState.Reviewed,
            ReviewedByPrincipalId = "reviewer-2",
        };

        Assert.False(uncited.IsCited);
        Assert.False(uncited.IsAuthoritative);

        var vague = uncited with { Citations = [new KnowledgeCitation("A handbook.", Title: "Some Handbook")] };

        // A title with no edition or year is not findable: editions differ.
        Assert.False(vague.IsAuthoritative);

        var specific = uncited with { Citations = [KnowledgeFixtures.Citation()] };

        Assert.True(specific.IsAuthoritative);
    }

    [Fact]
    public void Authored_content_needs_no_citation_to_be_authoritative()
    {
        var authored = KnowledgeFixtures.Reviewed();

        Assert.True(authored.IsAuthoritative);
        Assert.False(KnowledgeOrigins.RequiresCitation(KnowledgeOrigin.Authored));
    }

    [Fact]
    public void A_higher_level_reader_is_offered_lower_level_material()
    {
        var introductory = KnowledgeFixtures.Applicability(level: KnowledgeLevel.Introductory);
        var specialist = KnowledgeFixtures.Applicability(level: KnowledgeLevel.Specialist);
        var enquiry = new KnowledgeEnquiry { Level = KnowledgeLevel.Intermediate };

        Assert.True(introductory.AppliesTo(enquiry));
        Assert.False(specialist.AppliesTo(enquiry));
    }
}

/// <summary>`F1` / `WP06.1` — prompts as knowledge, never as a runtime.</summary>
public sealed class PromptTests
{
    [Fact]
    public async Task A_prompt_with_no_human_review_guidance_is_an_error()
    {
        var catalog = KnowledgeFixtures.BuildPromptCatalog();
        var service = new PromptValidationService(catalog, KnowledgeFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            KnowledgeFixtures.Prompt() with { HumanReviewGuidance = null },
            KnowledgeFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == PromptValidationRules.HumanReviewGuidanceMissing);
    }

    [Fact]
    public async Task A_prompt_asking_for_something_to_be_approved_is_reported()
    {
        var catalog = KnowledgeFixtures.BuildPromptCatalog();
        var service = new PromptValidationService(catalog, KnowledgeFixtures.Clock());

        var prompt = KnowledgeFixtures.Prompt() with
        {
            Instruction = "Review the design in {topic} at {level} and approve it if it is sound.",
        };

        var result = await service.ValidateDefinitionAsync(prompt, KnowledgeFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == PromptValidationRules.PromptSeeksAnAuthorityAct);
    }

    [Fact]
    public async Task A_declared_input_the_instruction_never_mentions_is_reported()
    {
        var catalog = KnowledgeFixtures.BuildPromptCatalog();
        var service = new PromptValidationService(catalog, KnowledgeFixtures.Clock());

        var prompt = KnowledgeFixtures.Prompt() with
        {
            Inputs = [.. KnowledgeFixtures.Prompt().Inputs, new PromptSlot("{unused}", "Never referenced.")],
        };

        var result = await service.ValidateDefinitionAsync(prompt, KnowledgeFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == PromptValidationRules.SlotIsUnused);
    }

    [Fact]
    public void Every_prompt_output_requires_human_review()
    {
        Assert.True(KnowledgeFixtures.Prompt().OutputRequiresHumanReview);
    }
}

/// <summary>`F2` / `WP06.2` — a hierarchy that holds.</summary>
public sealed class AcademyTests
{
    [Fact]
    public void A_node_may_contain_anything_strictly_narrower()
    {
        Assert.True(AcademyNodeKinds.CanContain(AcademyNodeKind.Module, AcademyNodeKind.Lesson));

        // Straight to a concept, skipping the lesson, is a reasonable
        // curriculum and the model permits it.
        Assert.True(AcademyNodeKinds.CanContain(AcademyNodeKind.Module, AcademyNodeKind.Concept));

        Assert.False(AcademyNodeKinds.CanContain(AcademyNodeKind.Lesson, AcademyNodeKind.Subject));
        Assert.False(AcademyNodeKinds.CanContain(AcademyNodeKind.Lesson, AcademyNodeKind.Lesson));
    }

    [Fact]
    public async Task A_lesson_placed_inside_a_lesson_is_an_error()
    {
        var catalog = KnowledgeFixtures.BuildAcademyCatalog();
        await KnowledgeFixtures.RegisterAsync(catalog, "les-parent", KnowledgeFixtures.Node("LES-PARENT"));

        var service = new AcademyValidationService(catalog, KnowledgeFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            KnowledgeFixtures.Node("LES-CHILD", parent: "LES-PARENT"),
            KnowledgeFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == AcademyValidationRules.InvalidHierarchyPlacement);
    }

    [Fact]
    public async Task A_node_that_is_its_own_parent_is_an_error()
    {
        var catalog = KnowledgeFixtures.BuildAcademyCatalog();
        var service = new AcademyValidationService(catalog, KnowledgeFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            KnowledgeFixtures.Node("LES-1", parent: "LES-1"),
            KnowledgeFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == AcademyValidationRules.HierarchyContainsACycle);
    }

    [Fact]
    public async Task Mutual_prerequisites_are_an_error()
    {
        var catalog = KnowledgeFixtures.BuildAcademyCatalog();

        await KnowledgeFixtures.RegisterAsync(catalog, "les-a", KnowledgeFixtures.Node("LES-A") with
        {
            PrerequisiteReferences = ["LES-B"],
        });

        var service = new AcademyValidationService(catalog, KnowledgeFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            KnowledgeFixtures.Node("LES-B") with { PrerequisiteReferences = ["LES-A"] },
            KnowledgeFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == AcademyValidationRules.PrerequisiteCycle);
    }

    [Fact]
    public async Task An_outcome_nothing_assesses_is_reported()
    {
        var catalog = KnowledgeFixtures.BuildAcademyCatalog();
        var service = new AcademyValidationService(catalog, KnowledgeFixtures.Clock());

        var node = KnowledgeFixtures.Node() with
        {
            Outcomes = [new LearningOutcome("LO-1", "Can do the fixture thing.")],
            Activities = [new AcademyActivity("ACT-1", "Reading", AcademyActivityKind.Reading)],
        };

        Assert.Single(node.UnassessedOutcomes);

        var result = await service.ValidateDefinitionAsync(node, KnowledgeFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == AcademyValidationRules.OutcomeIsUnassessed);
    }

    [Fact]
    public async Task A_path_through_the_hierarchy_comes_back_broadest_first()
    {
        var catalog = KnowledgeFixtures.BuildAcademyCatalog();

        await KnowledgeFixtures.RegisterAsync(catalog, "sub", KnowledgeFixtures.Node("SUB", AcademyNodeKind.Subject));
        await KnowledgeFixtures.RegisterAsync(catalog, "mod", KnowledgeFixtures.Node("MOD", AcademyNodeKind.Module, "SUB"));
        await KnowledgeFixtures.RegisterAsync(catalog, "les", KnowledgeFixtures.Node("LES", AcademyNodeKind.Lesson, "MOD"));

        var path = await catalog.FindPathToAsync("LES");

        Assert.Equal(["SUB", "MOD", "LES"], path.Select(n => n.Reference));
    }

    [Fact]
    public async Task A_cyclic_hierarchy_stops_the_walk_rather_than_hanging_it()
    {
        var catalog = KnowledgeFixtures.BuildAcademyCatalog();

        await KnowledgeFixtures.RegisterAsync(catalog, "a", KnowledgeFixtures.Node("A", AcademyNodeKind.Module, "B"));
        await KnowledgeFixtures.RegisterAsync(catalog, "b", KnowledgeFixtures.Node("B", AcademyNodeKind.Module, "A"));

        var path = await catalog.FindPathToAsync("A");

        Assert.Equal(2, path.Count);
    }
}

/// <summary>`F3` / `WP06.3` — challenges without a grader.</summary>
public sealed class ChallengeTests
{
    [Fact]
    public void A_design_challenge_is_open_ended_whatever_its_guidance_says()
    {
        var design = KnowledgeFixtures.Challenge(kind: ChallengeKind.DesignChallenge);

        Assert.True(design.IsOpenEnded);
    }

    [Fact]
    public async Task An_open_challenge_admitting_no_alternative_answer_is_reported()
    {
        var catalog = KnowledgeFixtures.BuildChallengeCatalog();
        var service = new ChallengeValidationService(catalog, timeProvider: KnowledgeFixtures.Clock());

        var challenge = KnowledgeFixtures.Challenge(kind: ChallengeKind.DesignChallenge) with
        {
            Guidance = new ChallengeGuidance("One right answer only."),
        };

        var result = await service.ValidateDefinitionAsync(challenge, KnowledgeFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == ChallengeValidationRules.OpenChallengeAdmitsNoAlternatives);
    }

    [Fact]
    public async Task An_estimation_challenge_declaring_nothing_withheld_is_reported()
    {
        var catalog = KnowledgeFixtures.BuildChallengeCatalog();
        var service = new ChallengeValidationService(catalog, timeProvider: KnowledgeFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            KnowledgeFixtures.Challenge(kind: ChallengeKind.Estimation),
            KnowledgeFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == ChallengeValidationRules.OmissionsNotDeclared);
    }

    [Fact]
    public void Guidance_must_be_able_to_describe_a_good_answer()
    {
        Assert.Throws<ArgumentException>(() => new ChallengeGuidance("   "));
    }

    [Fact]
    public async Task A_challenge_naming_an_academy_node_that_does_not_exist_is_reported()
    {
        var academy = KnowledgeFixtures.BuildAcademyCatalog();
        var catalog = KnowledgeFixtures.BuildChallengeCatalog();
        var service = new ChallengeValidationService(catalog, academy, KnowledgeFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            KnowledgeFixtures.Challenge() with { PrerequisiteNodeReferences = ["NOT-A-NODE"] },
            KnowledgeFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == ChallengeValidationRules.PrerequisiteMustResolve);
    }
}

/// <summary>`F4` / `WP06.4` — did the organisation actually learn anything?</summary>
public sealed class LessonTests
{
    [Fact]
    public async Task An_incident_with_no_transferable_lesson_is_an_error()
    {
        var catalog = KnowledgeFixtures.BuildLessonCatalog();
        var service = new LessonValidationService(catalog, KnowledgeFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            KnowledgeFixtures.Lesson() with { Lesson = null },
            KnowledgeFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == LessonValidationRules.NoTransferableLesson);
    }

    [Fact]
    public async Task A_root_cause_nothing_addresses_is_reported()
    {
        var catalog = KnowledgeFixtures.BuildLessonCatalog();
        var service = new LessonValidationService(catalog, KnowledgeFixtures.Clock());

        var lesson = KnowledgeFixtures.Lesson(addressed: false);

        Assert.Single(lesson.UnaddressedRootCauses);

        var result = await service.ValidateDefinitionAsync(lesson, KnowledgeFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == LessonValidationRules.RootCauseIsUnaddressed);
    }

    [Fact]
    public async Task A_cause_recorded_as_established_with_no_evidence_is_an_error()
    {
        var catalog = KnowledgeFixtures.BuildLessonCatalog();
        var service = new LessonValidationService(catalog, KnowledgeFixtures.Clock());

        var lesson = KnowledgeFixtures.Lesson() with
        {
            Causes = [new FailureCause("C-1", "It was this.", IsRootCause: true, Confidence: CauseConfidence.Established)],
        };

        var result = await service.ValidateDefinitionAsync(lesson, KnowledgeFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == LessonValidationRules.EstablishedCauseIsUnevidenced);
    }

    [Fact]
    public void Implemented_and_effective_are_different_things()
    {
        var implemented = new CorrectiveAction("CA-1", "Did a thing.", CorrectiveActionState.VerifiedEffective, "engineer-1");

        Assert.False(implemented.IsVerifiedEffective);

        var evidenced = implemented with
        {
            EffectivenessEvidence = [new EngineeringEvidence(EngineeringEvidenceKind.InternalRecord, "Shows it worked.", Reference: "FIX-1")],
        };

        Assert.True(evidenced.IsVerifiedEffective);
    }

    [Fact]
    public async Task A_serious_failure_classified_only_internal_is_reported()
    {
        var catalog = KnowledgeFixtures.BuildLessonCatalog();
        var service = new LessonValidationService(catalog, KnowledgeFixtures.Clock());

        var lesson = KnowledgeFixtures.Lesson(severity: FailureSeverity.Serious) with
        {
            Classification = ConfidentialityClassification.Internal,
        };

        var result = await service.ValidateDefinitionAsync(lesson, KnowledgeFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == LessonValidationRules.SeriousFailureIsLooselyClassified);
    }

    [Fact]
    public async Task A_shareable_lesson_naming_a_party_is_reported()
    {
        var catalog = KnowledgeFixtures.BuildLessonCatalog();
        var service = new LessonValidationService(catalog, KnowledgeFixtures.Clock());

        var lesson = KnowledgeFixtures.Lesson() with
        {
            LessonIsShareable = true,
            Lesson = "The supplier shipped the wrong grade; always check the certificate.",
        };

        var result = await service.ValidateDefinitionAsync(lesson, KnowledgeFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == LessonValidationRules.ShareableLessonMayIdentifyParties);
    }

    [Fact]
    public async Task Applicable_lessons_come_back_most_serious_first()
    {
        var catalog = KnowledgeFixtures.BuildLessonCatalog();

        await KnowledgeFixtures.RegisterAsync(catalog, "lr-minor", KnowledgeFixtures.Lesson("LR-MINOR"));
        await KnowledgeFixtures.RegisterAsync(catalog, "lr-serious", KnowledgeFixtures.Lesson("LR-SERIOUS", FailureSeverity.Serious));

        var applicable = await catalog.FindApplicableLessonsAsync(
            new KnowledgeEnquiry { Discipline = EngineeringDiscipline.Mechanical });

        Assert.Equal("LR-SERIOUS", applicable[0].Definition.Reference);
    }

    [Fact]
    public void A_record_is_closed_out_only_when_every_root_cause_is_addressed_and_confirmed()
    {
        Assert.True(KnowledgeFixtures.Lesson().IsClosedOut);
        Assert.False(KnowledgeFixtures.Lesson(addressed: false).IsClosedOut);
    }
}

/// <summary>`F5` / `WP06.5` — examples that teach rather than demonstrate.</summary>
public sealed class WorkedExampleTests
{
    [Fact]
    public void An_example_is_instructive_only_when_every_step_explains_itself()
    {
        var example = KnowledgeFixtures.Example();

        Assert.True(example.IsComplete);
        Assert.True(example.IsInstructive);

        var silent = example with
        {
            Steps = [new WorkedStep("S1", WorkedStepKind.Calculation, "Compute it.", Expression: "M / Z", Result: "48 MPa")],
        };

        Assert.True(silent.IsComplete);
        Assert.False(silent.IsInstructive);
        Assert.Single(silent.UnexplainedSteps);
    }

    [Fact]
    public async Task A_quantity_with_no_unit_is_an_error()
    {
        var catalog = KnowledgeFixtures.BuildWorkedExampleCatalog();
        var service = new WorkedExampleValidationService(catalog, timeProvider: KnowledgeFixtures.Clock());

        var example = KnowledgeFixtures.Example() with
        {
            Inputs = [new WorkedValue("F", "Applied load", "1200")],
        };

        var result = await service.ValidateDefinitionAsync(example, KnowledgeFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == WorkedExampleValidationRules.QuantityHasNoUnit);
    }

    [Fact]
    public async Task A_genuinely_dimensionless_quantity_is_not_an_error()
    {
        var catalog = KnowledgeFixtures.BuildWorkedExampleCatalog();
        var service = new WorkedExampleValidationService(catalog, timeProvider: KnowledgeFixtures.Clock());

        var example = KnowledgeFixtures.Example() with
        {
            Inputs = [new WorkedValue("n", "Safety factor", "1.5", IsDimensionless: true)],
        };

        var result = await service.ValidateDefinitionAsync(example, KnowledgeFixtures.Verified());

        Assert.DoesNotContain(result.Errors, e => e.Code == WorkedExampleValidationRules.QuantityHasNoUnit);
    }

    [Fact]
    public async Task A_result_with_no_interpretation_is_reported()
    {
        var catalog = KnowledgeFixtures.BuildWorkedExampleCatalog();
        var service = new WorkedExampleValidationService(catalog, timeProvider: KnowledgeFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            KnowledgeFixtures.Example() with { Interpretation = null },
            KnowledgeFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == WorkedExampleValidationRules.ResultHasNoInterpretation);
    }

    [Fact]
    public async Task Teachable_examples_come_back_instructive_first()
    {
        var catalog = KnowledgeFixtures.BuildWorkedExampleCatalog();

        await KnowledgeFixtures.RegisterAsync(catalog, "we-bare", KnowledgeFixtures.Example("WE-BARE") with
        {
            Steps = [new WorkedStep("S1", WorkedStepKind.Calculation, "Compute it.", Result: "48 MPa")],
            TeachingPoints = [],
        });

        await KnowledgeFixtures.RegisterAsync(catalog, "we-good", KnowledgeFixtures.Example("WE-GOOD"));

        var teachable = await catalog.FindTeachableAsync(
            new KnowledgeEnquiry { Discipline = EngineeringDiscipline.Mechanical });

        Assert.Equal("WE-GOOD", teachable[0].Definition.Reference);
    }
}

/// <summary>The persistence cycle §35 requires, for every P06 library.</summary>
public sealed class KnowledgePersistenceTests
{
    [Fact]
    public async Task A_lesson_survives_the_full_create_revise_supersede_cycle()
    {
        var catalog = KnowledgeFixtures.BuildLessonCatalog();

        await KnowledgeFixtures.RegisterAsync(catalog, "lr-1", KnowledgeFixtures.Lesson());

        var loaded = (await catalog.FindByReferenceAsync("LR-1"))!.Definition;

        Assert.Equal(FailureCategory.Design, loaded.Category);
        Assert.Equal(ConfidentialityClassification.Confidential, loaded.Classification);
        Assert.Single(loaded.RootCauses);
        Assert.Equal(CauseConfidence.Probable, loaded.Causes[0].Confidence);
        Assert.True(loaded.CorrectiveActions[0].IsVerifiedEffective);
        Assert.Equal(KnowledgeOrigin.OrganisationalExperience, loaded.Provenance.Origin);
        Assert.True(loaded.Provenance.IsAuthoritative);

        var revised = await catalog.ReviseAsync(
            "lr-1",
            KnowledgeFixtures.Lesson() with { Title = "Revised fixture failure" },
            KnowledgeFixtures.Verified(),
            "Retitled.");

        Assert.Equal(2, revised.RevisionNumber);
        Assert.Equal("Fictional fixture failure", (await catalog.GetRevisionAsync("lr-1", 1)).Definition.Title);

        await KnowledgeFixtures.ReleaseAsync(catalog, "lr-1");
        await KnowledgeFixtures.RegisterAsync(catalog, "lr-2", KnowledgeFixtures.Lesson("LR-2"));
        await catalog.SupersedeAsync("lr-1", "lr-2", "Replaced.");

        Assert.Equal(ReferenceValidationState.Superseded, (await catalog.FindAsync("lr-1"))!.ValidationState);
    }

    [Fact]
    public async Task A_released_knowledge_record_cannot_be_revised_in_place()
    {
        var catalog = KnowledgeFixtures.BuildWorkedExampleCatalog();

        await KnowledgeFixtures.RegisterReleasedAsync(catalog, "we-1", KnowledgeFixtures.Example());

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(() =>
            catalog.ReviseAsync("we-1", KnowledgeFixtures.Example() with { Title = "Sneaky" }, KnowledgeFixtures.Verified(), "No."));
    }

    [Fact]
    public async Task An_academy_node_survives_persistence_with_its_outcomes_and_activities_intact()
    {
        var catalog = KnowledgeFixtures.BuildAcademyCatalog();

        await KnowledgeFixtures.RegisterAsync(catalog, "les-1", KnowledgeFixtures.Node());

        var loaded = (await catalog.FindByReferenceAsync("LES-1"))!.Definition;

        Assert.Single(loaded.Outcomes);
        Assert.Single(loaded.Activities);
        Assert.Equal(30, loaded.EstimatedMinutes);
        Assert.Empty(loaded.UnassessedOutcomes);
    }

    [Fact]
    public async Task A_worked_example_survives_persistence_with_its_units_and_pins_intact()
    {
        var catalog = KnowledgeFixtures.BuildWorkedExampleCatalog();
        var example = KnowledgeFixtures.Example() with
        {
            Steps =
            [
                new WorkedStep("S1", WorkedStepKind.Lookup, "Look up the yield strength.", "It governs the allowable.",
                    Result: "250 MPa", SourcePin: new ReferencePin("Materials", "mat-1", 2)),
            ],
        };

        await KnowledgeFixtures.RegisterAsync(catalog, "we-1", example);

        var loaded = (await catalog.FindByReferenceAsync("WE-1"))!.Definition;

        Assert.Equal("MPa", loaded.Result!.Unit);
        Assert.Equal(new ReferencePin("Materials", "mat-1", 2), Assert.Single(loaded.AllPins));
        Assert.True(loaded.Steps[0].IsTraceable);
    }

    [Theory]
    [MemberData(nameof(RoundTrippableKnowledge))]
    public void Every_P06_type_round_trips_through_JSON(object knowledge)
    {
        var json = JsonSerializer.Serialize(knowledge, knowledge.GetType());
        var restored = JsonSerializer.Deserialize(json, knowledge.GetType());

        Assert.NotNull(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored, knowledge.GetType()));
    }

    public static TheoryData<object> RoundTrippableKnowledge() =>
    [
        KnowledgeFixtures.Prompt(),
        KnowledgeFixtures.Node(),
        KnowledgeFixtures.Challenge(),
        KnowledgeFixtures.Lesson(),
        KnowledgeFixtures.Example(),
        KnowledgeFixtures.Reviewed(),
        KnowledgeFixtures.Citation(),
        KnowledgeFixtures.Applicability(),
    ];
}

/// <summary>
/// Structural guards over `P06`: assertions about what the knowledge
/// layer does <em>not</em> contain.
/// </summary>
public sealed class KnowledgeStructuralTests
{
    /// <summary>
    /// Verbs that, at the start of a method name, would mean `P06` runs
    /// a prompt or judges a response.
    /// </summary>
    /// <remarks>
    /// Matched as a leading verb rather than anywhere in the name, so
    /// <c>IsMachineGenerated</c> — a predicate about provenance — and
    /// <c>EvaluateDuplicateReferences</c> — ordinary validation — are not
    /// false positives. "Evaluate" is deliberately absent: validating a
    /// record is exactly what these services are for, and the grading
    /// concern is caught by <c>Grade</c> and <c>Score</c>. "Prompt" is
    /// absent too — it reads as a noun here (<c>PromptLibraryName</c>),
    /// and prompt execution is already caught by <c>Execute</c>,
    /// <c>Invoke</c>, <c>Chat</c> and <c>Ask</c>.
    /// </remarks>
    private static readonly string[] ForbiddenLeadingVerbs =
    [
        "Execute",
        "Invoke",
        "Run",
        "Complete",
        "Generate",
        "Infer",
        "Grade",
        "Score",
        "Mark",
        "Chat",
        "Ask",
    ];

    private static bool IsForbidden(string methodName) =>
        ForbiddenLeadingVerbs.Any(verb =>
            string.Equals(methodName, verb, StringComparison.Ordinal)
            || (methodName.StartsWith(verb, StringComparison.Ordinal)
                && methodName.Length > verb.Length
                && char.IsUpper(methodName[verb.Length])));

    public static TheoryData<Type> KnowledgeTypes
    {
        get
        {
            var data = new TheoryData<Type>();

            foreach (var type in typeof(PromptRecord).Assembly
                         .GetTypes()
                         .Where(t => t.Namespace?.StartsWith("Tempest.Core.Knowledge", StringComparison.Ordinal) == true))
                data.Add(type);

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(KnowledgeTypes))]
    public void No_P06_type_executes_a_prompt_or_grades_a_response(Type type)
    {
        // P06 is the knowledge layer. It holds prompts and challenges; it
        // runs neither.
        var members = type
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m is MethodInfo { IsSpecialName: false })
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain(members, IsForbidden);
    }

    [Fact]
    public void The_structural_guard_matches_a_leading_verb_and_not_an_incidental_one()
    {
        // The guard itself is worth a test: too blunt a match flags
        // IsMachineGenerated and EvaluateDuplicateReferences, and a guard
        // that cries wolf gets deleted.
        Assert.True(IsForbidden("ExecutePrompt"));
        Assert.True(IsForbidden("Grade"));
        Assert.True(IsForbidden("ScoreResponse"));

        Assert.False(IsForbidden("IsMachineGenerated"));
        Assert.False(IsForbidden("EvaluateDuplicateReferences"));
        Assert.False(IsForbidden("Completeness"));
        Assert.False(IsForbidden("PromptLibraryName"));
    }

    [Fact]
    public void P06_takes_no_model_or_provider_dependency()
    {
        string[] forbiddenAssemblies =
            ["OpenAI", "Anthropic", "Azure.AI", "Microsoft.ML", "SemanticKernel", "LangChain", "HuggingFace"];

        var referenced = typeof(PromptRecord).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        foreach (var forbidden in forbiddenAssemblies)
            Assert.DoesNotContain(referenced, name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_P06_document_kind_and_library_name_is_unique()
    {
        string[] libraries =
        [
            PromptCatalog.PromptLibraryName,
            AcademyCatalog.AcademyLibraryName,
            ChallengeCatalog.ChallengeLibraryName,
            LessonCatalog.LessonLibraryName,
            WorkedExampleCatalog.WorkedExampleLibraryName,
        ];

        string[] kinds =
        [
            PromptCatalog.PromptDocumentKind,
            AcademyCatalog.AcademyNodeDocumentKind,
            ChallengeCatalog.ChallengeDocumentKind,
            LessonCatalog.LessonDocumentKind,
            WorkedExampleCatalog.WorkedExampleDocumentKind,
        ];

        Assert.Equal(libraries.Length, libraries.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(kinds.Length, kinds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Fictional_content_registered_anywhere_is_an_error_in_every_library()
    {
        // The single guard that keeps test data out of the knowledge base,
        // asserted at the shared level so no library can opt out.
        Assert.False(KnowledgeProvenance.Fictional.IsAuthoritative);
        Assert.True(KnowledgeProvenance.Fictional.IsFictional);
    }
}
